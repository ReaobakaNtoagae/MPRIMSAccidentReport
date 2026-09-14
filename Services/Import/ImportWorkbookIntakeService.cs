using ClosedXML.Excel;
using CrashReport.Models.Import;
using CrashReport.Models.Import.Models;
using CrashReport.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Security.Cryptography;

namespace CrashReport.Services.Import;

public sealed class ImportWorkbookIntakeService : IImportWorkbookIntakeService
{
    private readonly IImportBatchRepository _batches;
    private readonly IWebHostEnvironment _environment;
    private readonly ImportWorkbookOptions _options;
    private readonly ILogger<ImportWorkbookIntakeService> _logger;

    public ImportWorkbookIntakeService(
        IImportBatchRepository batches,
        IWebHostEnvironment environment,
        IOptions<ImportWorkbookOptions> options,
        ILogger<ImportWorkbookIntakeService> logger)
    {
        _batches = batches;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ImportWorkbookIntakeResult> IntakeAsync(
        ImportWorkbookIntakeCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateCommand(command);

        var storageRoot = ResolveStorageRoot();
        Directory.CreateDirectory(storageRoot);

        var safeStoredName = $"{Guid.NewGuid():N}.xlsx";
        // ClosedXML uses the file extension to select the OpenXML document type.
        var temporaryPath = Path.Combine(storageRoot, $".{Guid.NewGuid():N}.uploading.xlsx");
        var finalPath = Path.Combine(storageRoot, safeStoredName);

        try
        {
            var hash = await CopyAndHashAsync(command.Content, temporaryPath, cancellationToken);
            ValidateXlsxPackage(temporaryPath);

            var region = command.SelectedRegion.Trim().ToUpperInvariant();
            var existing = await _batches.FindByFingerprintAsync(
                hash, region, command.ReportingMonth, command.ReportingYear, cancellationToken);

            if (existing != null)
            {
                return ToResult(existing, alreadyExists: true);
            }

            File.Move(temporaryPath, finalPath);

            var batch = new ImportBatch
            {
                OriginalFileName = SanitizeDisplayFileName(command.OriginalFileName),
                StoredFileReference = Path.GetRelativePath(_environment.ContentRootPath, finalPath)
                    .Replace('\\', '/'),
                FileSha256 = hash,
                SelectedRegion = region,
                ReportingMonth = checked((byte)command.ReportingMonth),
                ReportingYear = checked((short)command.ReportingYear),
                Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
                UploadedByUserId = command.UploadedByUserId,
                UploadedAt = DateTime.UtcNow,
                Status = ImportBatchStatuses.Uploaded
            };

            try
            {
                await _batches.AddAsync(batch, cancellationToken);
            }
            catch
            {
                TryDelete(finalPath);
                throw;
            }

            return ToResult(batch, alreadyExists: false);
        }
        catch (ImportWorkbookIntakeException)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            throw new ImportWorkbookIntakeException("The selected file is not a valid, readable .xlsx workbook.", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Workbook intake failed for region {Region}, period {Year}-{Month}.",
                command.SelectedRegion, command.ReportingYear, command.ReportingMonth);
            throw new ImportWorkbookIntakeException("The workbook could not be accepted. No accident records were imported.", ex);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private void ValidateCommand(ImportWorkbookIntakeCommand command)
    {
        if (command.Content == null || !command.Content.CanRead)
            throw new ImportWorkbookIntakeException("Please select a readable Excel workbook.");
        if (!string.Equals(Path.GetExtension(command.OriginalFileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new ImportWorkbookIntakeException("Only .xlsx workbooks are supported.");
        if (command.DeclaredLength <= 0)
            throw new ImportWorkbookIntakeException("The selected workbook is empty.");
        if (command.DeclaredLength > _options.MaxFileSizeBytes)
            throw new ImportWorkbookIntakeException($"The workbook exceeds the {_options.MaxFileSizeBytes / 1024 / 1024} MB limit.");
        if (string.IsNullOrWhiteSpace(command.SelectedRegion) || command.SelectedRegion.Length > 50)
            throw new ImportWorkbookIntakeException("Select a valid reporting region.");
        if (command.ReportingMonth is < 1 or > 12)
            throw new ImportWorkbookIntakeException("Select a valid reporting month.");
        if (command.ReportingYear is < 2000 or > 2100)
            throw new ImportWorkbookIntakeException("Select a valid reporting year.");
        if (string.IsNullOrWhiteSpace(command.UploadedByUserId))
            throw new ImportWorkbookIntakeException("The uploading user could not be identified.");
        if (command.Notes?.Length > 1000)
            throw new ImportWorkbookIntakeException("Import notes cannot exceed 1,000 characters.");
    }

    private string ResolveStorageRoot()
    {
        var configured = _options.StorageRoot.Trim();
        var combined = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(_environment.ContentRootPath, configured);
        return Path.GetFullPath(combined);
    }

    private async Task<string> CopyAndHashAsync(Stream source, string destination, CancellationToken cancellationToken)
    {
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > _options.MaxFileSizeBytes)
                throw new ImportWorkbookIntakeException($"The workbook exceeds the {_options.MaxFileSizeBytes / 1024 / 1024} MB limit.");
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        await output.FlushAsync(cancellationToken);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private void ValidateXlsxPackage(string path)
    {
        using (var archive = ZipFile.OpenRead(path))
        {
            if (archive.Entries.Count > _options.MaxZipEntries)
                throw new ImportWorkbookIntakeException("The workbook contains too many internal files.");
            long expanded = 0;
            foreach (var entry in archive.Entries)
            {
                expanded = checked(expanded + entry.Length);
                if (expanded > _options.MaxExpandedSizeBytes)
                    throw new ImportWorkbookIntakeException("The expanded workbook exceeds the permitted safety limit.");
            }
            if (archive.GetEntry("[Content_Types].xml") == null || archive.GetEntry("xl/workbook.xml") == null)
                throw new ImportWorkbookIntakeException("The selected file is not a valid .xlsx workbook.");
        }

        try
        {
            using var workbook = new XLWorkbook(path);
            if (!workbook.Worksheets.Any())
                throw new ImportWorkbookIntakeException("The workbook does not contain any worksheets.");
        }
        catch (ImportWorkbookIntakeException) { throw; }
        catch (Exception ex)
        {
            throw new ImportWorkbookIntakeException("The selected workbook is corrupt or unreadable.", ex);
        }
    }

    private static string SanitizeDisplayFileName(string original) =>
        Path.GetFileName(original.Trim()).Length is > 0 and <= 255
            ? Path.GetFileName(original.Trim())
            : "uploaded-workbook.xlsx";

    private static ImportWorkbookIntakeResult ToResult(ImportBatch batch, bool alreadyExists) =>
        new(batch.ImportBatchId, alreadyExists, batch.OriginalFileName, batch.FileSha256,
            batch.SelectedRegion, batch.ReportingMonth, batch.ReportingYear);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup; caller error is more important */ }
    }
}
