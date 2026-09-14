using CrashReport.Data;
using CrashReport.Models;
using CrashReport.Models.Import.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace CrashReport.Services.Import;

public sealed class ImportCommitService(
    AppDbContext context,
    ILogger<ImportCommitService> logger) : IImportCommitService
{
    public async Task<ImportCommitResult> CommitAsync(int importBatchId, string userId,
        CancellationToken cancellationToken = default)
    {
        var batch = await context.ImportBatches
            .Include(item => item.CrashRows)
                .ThenInclude(row => row.Issues)
            .Include(item => item.Demographics)
            .Include(item => item.Issues)
            .SingleOrDefaultAsync(item => item.ImportBatchId == importBatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Import batch {importBatchId} was not found.");

        // Completed is an idempotent success state. A repeated click must not insert the
        // same crashes again, so we return the recorded outcome instead.
        if (batch.Status == ImportBatchStatuses.Completed)
            return await ExistingResultAsync(batch, cancellationToken);

        if (batch.Status != ImportBatchStatuses.ReadyForImport)
            throw new InvalidOperationException("The batch must be fully reviewed and approved before it can be imported.");
        if (!batch.ReviewedAt.HasValue || string.IsNullOrWhiteSpace(batch.ReviewedByUserId))
            throw new InvalidOperationException("Mark the batch ready again so the reviewer sign-off is recorded before importing.");

        var approvedRows = batch.CrashRows
            // Only explicitly approved, not-yet-imported rows cross the production
            // boundary. Referred rows stay in staging until the data owner responds.
            .Where(ImportWorkflowRules.IsImportCandidate)
            .OrderBy(row => row.WorksheetName)
            .ThenBy(row => row.SourceRowNumber)
            .ToArray();

        if (approvedRows.Length == 0)
            throw new InvalidOperationException("The batch does not contain any approved rows to import.");
        if (approvedRows.Any(row => row.Issues.Any(issue =>
                issue.ResolutionStatus == ImportIssueResolutionStatuses.Open)))
            throw new InvalidOperationException("The batch contains unresolved quality issues.");
        if (approvedRows.Any(row => !row.CrashDate.HasValue || string.IsNullOrWhiteSpace(row.Station)))
            throw new InvalidOperationException("An approved row is missing a production-required station or crash date.");

        // Reserve numbers from both registry representations, including earlier batches.
        var occupiedNumbers = await context.CrashSummaries.AsNoTracking()
            .Select(row => row.CrNo).ToListAsync(cancellationToken);
        occupiedNumbers.AddRange(await context.Crashes.AsNoTracking()
            .Where(row => row.CrNo != null).Select(row => row.CrNo!).ToListAsync(cancellationToken));
        var prepared = BuildPreparedRows(batch.CrashRows, approvedRows, occupiedNumbers);

        // Resolve every source vehicle against the real lookup before beginning the
        // transaction. Generated text such as LIGHTMOTORVEHICLE is not necessarily a
        // valid foreign-key code and previously caused the complete commit to roll back.
        var vehicleTypes = await context.LookupVehicleTypes.AsNoTracking()
            .OrderByDescending(type => type.IsActive)
            .ToArrayAsync(cancellationToken);
        var resolvedVehicles = prepared.ToDictionary(
            item => item.Staging.StagingSummaryId,
            item => ResolveVehicles(item.Staging, vehicleTypes));

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            batch.Status = ImportBatchStatuses.Importing;
            await context.SaveChangesAsync(cancellationToken);

            // Extend the controlled station and route lookups from reviewed rows before
            // demographics resolve their required district. The procedure inserts only
            // missing values and refuses to guess when the batch district is ambiguous.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"EXEC dbo.usp_SyncImportGeography @ImportBatchId={batch.ImportBatchId}",
                cancellationToken);

            var vehicleRows = 0;
            var injuryRows = 0;
            foreach (var item in prepared)
            {
                // ProductionSummaryId is another idempotency guard. If it is populated,
                // this staging row has already crossed the production boundary.
                if (item.Staging.ProductionSummaryId.HasValue) continue;

                var summary = MapSummary(item.Staging, item.CrNumber, batch.OriginalFileName);
                context.CrashSummaries.Add(summary);
                await context.SaveChangesAsync(cancellationToken);

                var vehicles = BuildVehicles(
                    resolvedVehicles[item.Staging.StagingSummaryId], summary.SummaryId);
                context.CrashSummaryVehicles.AddRange(vehicles);
                vehicleRows += vehicles.Count;

                var injuries = BuildInjuries(item.Staging, summary.SummaryId);
                context.CrashSummaryInjuries.AddRange(injuries);
                injuryRows += injuries.Count;

                item.Staging.ProductionSummaryId = summary.SummaryId;
                item.Staging.ImportStatus = ImportRecordStatuses.Imported;
                item.Staging.ImportedAt = DateTime.UtcNow;
            }

            var hasDeferredRows = batch.CrashRows.Any(row =>
                row.ImportStatus == ImportRecordStatuses.NotImported &&
                row.ReviewStatus == ImportReviewStatuses.AwaitingDataOwner);
            var hasDeferredBatchIssues = batch.Issues.Any(issue =>
                issue.ResolutionStatus == ImportIssueResolutionStatuses.PendingDataOwner);

            // Demographics describe the whole workbook rather than one crash. Importing
            // them during a partial commit would publish totals while supporting rows or
            // summary questions are still unresolved.
            var demographicRows = hasDeferredRows || hasDeferredBatchIssues
                ? 0
                : await CommitDemographicsAsync(batch, cancellationToken);

            // A partial result is successful: all eligible rows were committed atomically,
            // while uncertain data remains isolated in staging for later verification.
            batch.Status = hasDeferredRows || hasDeferredBatchIssues
                ? ImportBatchStatuses.PartiallyCompleted
                : ImportBatchStatuses.Completed;
            batch.ImportedByUserId = userId;
            batch.ImportedAt = DateTime.UtcNow;
            batch.FailureReason = null;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "User {UserId} committed import batch {BatchId}: {Rows} crashes, {Vehicles} vehicles, {Injuries} injuries.",
                userId, batch.ImportBatchId, prepared.Length, vehicleRows, injuryRows);

            return new ImportCommitResult(batch.ImportBatchId, prepared.Length,
                batch.CrashRows.Count(row => row.ReviewStatus == ImportReviewStatuses.Rejected),
                vehicleRows, injuryRows, demographicRows);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();

            // The transaction guarantees that partial summaries do not remain. Record the
            // failure on the batch so support staff can see why the commit stopped.
            var failed = await context.ImportBatches
                .Include(item => item.CrashRows)
                .SingleAsync(item => item.ImportBatchId == importBatchId, cancellationToken);
            // A failed follow-up attempt must not hide the rows successfully imported by
            // an earlier transaction. Keep the batch partial when production data exists.
            failed.Status = failed.CrashRows.Any(row => row.ProductionSummaryId.HasValue)
                ? ImportBatchStatuses.PartiallyCompleted
                : ImportBatchStatuses.Failed;
            failed.FailureReason = Limit(ex.Message, 1000);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogError(ex, "Commit failed for import batch {BatchId}; transaction rolled back.", importBatchId);
            throw;
        }
    }

    private static PreparedRow[] BuildPreparedRows(
        IEnumerable<StagingCrashSummary> allBatchRows,
        IEnumerable<StagingCrashSummary> importCandidates,
        IEnumerable<string> occupiedNumbers)
    {
        var candidateIds = importCandidates.Select(row => row.StagingSummaryId).ToHashSet();
        var orderedRows = allBatchRows
            .Where(row => row.ReviewStatus != ImportReviewStatuses.Rejected)
            .OrderBy(row => row.WorksheetName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.SourceRowNumber)
            .ToArray();
        var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var occupied = occupiedNumbers.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prepared = new List<PreparedRow>(orderedRows.Length);

        foreach (var row in orderedRows)
        {
            if (!candidateIds.Contains(row.StagingSummaryId)) continue;
            var baseNumber = ImportProductionIdentity.BuildCrashNumber(row);
            occurrences.TryGetValue(baseNumber, out var previousCount);
            occurrences[baseNumber] = previousCount + 1;

            // Reused AR numbers for distinct crashes receive deterministic suffixes.
            // Preserve the workbook AR number in staging; only the production identifier
            // receives a suffix. A reviewer must already have approved keeping this row.
            var crashNumber = previousCount == 0
                ? baseNumber
                : AddOccurrenceSuffix(baseNumber, previousCount);
            var suffixIndex = previousCount;
            while (!occupied.Add(crashNumber))
                crashNumber = AddOccurrenceSuffix(baseNumber, ++suffixIndex);
            // Imported siblings still reserve their occurrence position during a later
            // partial-batch commit, but only current candidates are returned for insert.
            if (candidateIds.Contains(row.StagingSummaryId))
                prepared.Add(new PreparedRow(row, crashNumber));
        }
        return prepared.ToArray();
    }

    private static string AddOccurrenceSuffix(string baseNumber, int occurrence)
    {
        var suffix = occurrence <= 26
            ? $"-{(char)('A' + occurrence - 1)}"
            : $"-DUP{occurrence - 1}";
        var available = Math.Max(0, 50 - suffix.Length);
        return $"{baseNumber[..Math.Min(baseNumber.Length, available)]}{suffix}";
    }

    private static CrashSummary MapSummary(StagingCrashSummary row, string crNumber, string sourceFile) => new()
    {
        CrNo = Limit(crNumber, 50),
        Station = row.Station!,
        CasNo = row.CasNumber,
        CrashDate = row.CrashDate!.Value,
        CrashTime = row.CrashTime,
        Route = row.Route,
        Location = row.Location,
        CrashType = row.CrashType,
        VehiclesString = row.VehiclesString,
        VehicleCount = row.VehicleCount ?? 0,
        FatalDrivers = row.FatalDrivers ?? 0,
        FatalPassengers = row.FatalPassengers ?? 0,
        FatalPedestrians = row.FatalPedestrians ?? 0,
        FatalCyclists = row.FatalCyclists ?? 0,
        FatalMale = row.FatalMale ?? 0,
        FatalFemale = row.FatalFemale ?? 0,
        SeriousDrivers = row.SeriousDrivers ?? 0,
        SeriousPassengers = row.SeriousPassengers ?? 0,
        SeriousPedestrians = row.SeriousPedestrians ?? 0,
        SeriousCyclists = row.SeriousCyclists ?? 0,
        SlightDrivers = row.SlightDrivers ?? 0,
        SlightPassengers = row.SlightPassengers ?? 0,
        SlightPedestrians = row.SlightPedestrians ?? 0,
        SlightCyclists = row.SlightCyclists ?? 0,
        SourceFile = Limit(sourceFile, 255),
        ImportedAt = DateTime.UtcNow
    };

    private static IReadOnlyList<ResolvedVehicle> ResolveVehicles(
        StagingCrashSummary row,
        IReadOnlyCollection<LookupVehicleType> lookupTypes)
    {
        // P/D and M/C are domain abbreviations, not separators between vehicles.
        // Protect both before splitting the workbook's slash-separated vehicle list.
        var vehicleText = Regex.Replace(row.VehiclesString ?? string.Empty,
            @"\bP\s*/\s*D\b", "PEDESTRIAN", RegexOptions.IgnoreCase);
        vehicleText = Regex.Replace(vehicleText,
            @"\bM\s*/\s*C\b", "MOTORCYCLE", RegexOptions.IgnoreCase);
        var names = vehicleText
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Take(byte.MaxValue)
            .ToList();

        // VehicleCount may be present even when Excel omitted descriptions. Add UNKNOWN
        // placeholders rather than pretending to know a vehicle type.
        // For an existing staged batch VehicleCount may have been calculated by the old
        // parser, which incorrectly counted P/D as P + D. The protected parsed list is
        // authoritative whenever descriptions are available.
        var expected = names.Count > 0 ? (byte)names.Count : row.VehicleCount ?? 0;
        while (names.Count < expected) names.Add("UNKNOWN");

        var fallback = lookupTypes.FirstOrDefault(type =>
            VehicleKey(type.VehicleTypeCode) is "UNKNOWN" or "OTHER" or "UNSPECIFIED" ||
            VehicleKey(type.FullName) is "UNKNOWN" or "OTHER" or "UNSPECIFIED" ||
            VehicleKey(type.Description) is "UNKNOWN" or "OTHER" or "UNSPECIFIED");
        var resolved = new List<ResolvedVehicle>(names.Count);

        foreach (var name in names)
        {
            var sourceKey = VehicleKey(name);
            var acceptedSourceKeys = VehicleAliases(sourceKey).Append(sourceKey).ToHashSet();
            var match = lookupTypes.FirstOrDefault(type =>
                acceptedSourceKeys.Contains(VehicleKey(type.VehicleTypeCode)) ||
                acceptedSourceKeys.Contains(VehicleKey(type.FullName)) ||
                acceptedSourceKeys.Contains(VehicleKey(type.Description)));

            // Longer descriptive values often contain the shorter lookup wording, for
            // example "LIGHT MOTOR VEHICLE (SEDAN)" versus "LIGHT MOTOR VEHICLE".
            match ??= lookupTypes
                .Where(type => acceptedSourceKeys.Any(key => key.Length >= 3))
                .Select(type => new
                {
                    Type = type,
                    Keys = new[] { VehicleKey(type.VehicleTypeCode),
                        VehicleKey(type.FullName), VehicleKey(type.Description) }
                })
                .Where(candidate => candidate.Keys.Any(lookupKey => lookupKey.Length >= 3 &&
                    acceptedSourceKeys.Any(sourceAlias => sourceAlias.Length >= 3 &&
                        (sourceAlias.Contains(lookupKey, StringComparison.Ordinal) ||
                         lookupKey.Contains(sourceAlias, StringComparison.Ordinal)))))
                .OrderBy(candidate => candidate.Keys
                    .Where(key => key.Length >= 3)
                    .Min(lookupKey => acceptedSourceKeys
                        .Where(sourceAlias => sourceAlias.Length >= 3)
                        .Min(sourceAlias => Math.Abs(lookupKey.Length - sourceAlias.Length))))
                .Select(candidate => candidate.Type)
                .FirstOrDefault();

            match ??= fallback;
            if (match is null)
                throw new InvalidOperationException(
                    $"Vehicle type '{name}' on {row.WorksheetName} row {row.SourceRowNumber} " +
                    "does not match a configured vehicle type, and no Other/Unknown fallback exists. " +
                    "Add or correct that vehicle type in Lookup data, then mark the batch ready and import again.");

            resolved.Add(new ResolvedVehicle(match.VehicleTypeCode,
                match.FullName ?? match.Description ?? name));
        }

        return resolved;
    }

    private static List<CrashSummaryVehicle> BuildVehicles(
        IReadOnlyList<ResolvedVehicle> vehicles,
        int summaryId) =>
        vehicles.Select((vehicle, index) => new CrashSummaryVehicle
        {
            SummaryId = summaryId,
            VehicleNumber = checked((byte)(index + 1)),
            VehicleTypeCode = Limit(vehicle.Code, 20),
            VehicleTypeName = Limit(vehicle.Name, 60),
            CreatedAt = DateTime.UtcNow
        }).ToList();

    private static string VehicleKey(string? value) => new(
        (value ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static IEnumerable<string> VehicleAliases(string sourceKey) => sourceKey switch
    {
        // These are abbreviations used in the monthly station workbooks. Aliases are
        // matched to lookup codes/names; they are never inserted as invented FK values.
        "PD" or "PED" => ["PEDESTRIAN"],
        "SED" => ["SEDAN"],
        // Keep missing or abbreviated source descriptions attached to the controlled
        // UNKNOWN lookup instead of inventing a vehicle-type foreign-key value.
        "UNK" or "UNSPECIFIED" or "NOTKNOWN" => ["UNKNOWN"],
        // The database code is ARTIC, while workbooks commonly use ART, ARTIC,
        // or the full word ARTICULATED for an articulated truck.
        "ART" or "ARTICULATED" or "ARTICULATEDTRUCK" => ["ARTIC"],
        _ => []
    };

    private static List<CrashSummaryInjury> BuildInjuries(StagingCrashSummary row, int summaryId)
    {
        var injuries = new List<CrashSummaryInjury>();
        AddInjuries(injuries, summaryId, "Fatal", "Driver", row.FatalDrivers);
        AddInjuries(injuries, summaryId, "Fatal", "Passenger", row.FatalPassengers);
        AddInjuries(injuries, summaryId, "Fatal", "Pedestrian", row.FatalPedestrians);
        AddInjuries(injuries, summaryId, "Fatal", "Cyclist", row.FatalCyclists);
        AddInjuries(injuries, summaryId, "Serious", "Driver", row.SeriousDrivers);
        AddInjuries(injuries, summaryId, "Serious", "Passenger", row.SeriousPassengers);
        AddInjuries(injuries, summaryId, "Serious", "Pedestrian", row.SeriousPedestrians);
        AddInjuries(injuries, summaryId, "Serious", "Cyclist", row.SeriousCyclists);
        AddInjuries(injuries, summaryId, "Slight", "Driver", row.SlightDrivers);
        AddInjuries(injuries, summaryId, "Slight", "Passenger", row.SlightPassengers);
        AddInjuries(injuries, summaryId, "Slight", "Pedestrian", row.SlightPedestrians);
        AddInjuries(injuries, summaryId, "Slight", "Cyclist", row.SlightCyclists);
        return injuries;
    }

    private static void AddInjuries(List<CrashSummaryInjury> output, int summaryId,
        string severity, string role, byte? count)
    {
        // We know the aggregate role/severity but not which specific vehicle the person
        // occupied. VehicleId stays null rather than manufacturing an unsupported link.
        for (var index = 0; index < (count ?? 0); index++)
            output.Add(new CrashSummaryInjury
            {
                SummaryId = summaryId,
                VehicleId = null,
                Severity = severity,
                Role = role
            });
    }

    private async Task<int> CommitDemographicsAsync(ImportBatch batch, CancellationToken cancellationToken)
    {
        // Load the small lookup tables once. A demographic summary is district-level,
        // while the upload form currently supplies only the province code (for example MP).
        var stations = await context.SapsStations.AsNoTracking()
            .Where(item => item.IsActive && item.DistrictId.HasValue)
            .ToArrayAsync(cancellationToken);
        var districts = await context.LookupDistricts.AsNoTracking()
            .Where(item => item.IsActive)
            .ToArrayAsync(cancellationToken);

        var committed = 0;
        foreach (var staging in batch.Demographics.Where(item => !item.ProductionDemographicsId.HasValue))
        {
            var districtId = ResolveDemographicsDistrictId(staging, batch, stations, districts);
            var production = new CrashDemographicRecord
            {
                PeriodFrom = staging.PeriodFrom, PeriodTo = staging.PeriodTo,
                ProvinceCode = staging.ProvinceCode, DistrictId = districtId,
                Age0to7 = staging.Age0to7 ?? 0, Age8to12 = staging.Age8to12 ?? 0,
                Age13to18 = staging.Age13to18 ?? 0, Age19to35 = staging.Age19to35 ?? 0,
                Age36Plus = staging.Age36Plus ?? 0,
                DriverMale = staging.DriverMale ?? 0, DriverFemale = staging.DriverFemale ?? 0,
                PassengerMale = staging.PassengerMale ?? 0, PassengerFemale = staging.PassengerFemale ?? 0,
                PedestrianMale = staging.PedestrianMale ?? 0, PedestrianFemale = staging.PedestrianFemale ?? 0,
                CyclistMale = staging.CyclistMale ?? 0, CyclistFemale = staging.CyclistFemale ?? 0,
                RaceBlack = staging.RaceBlack ?? 0, RaceColoured = staging.RaceColoured ?? 0,
                RaceWhite = staging.RaceWhite ?? 0, RaceIndian = staging.RaceIndian ?? 0,
                RaceOther = staging.RaceOther ?? 0, CreatedAt = DateTime.UtcNow
            };
            context.CrashDemographics.Add(production);
            await context.SaveChangesAsync(cancellationToken);
            staging.ProductionDemographicsId = production.DemoId;
            committed++;
        }
        return committed;
    }

    private static int? ResolveDemographicsDistrictId(
        StagingImportDemographics demographics,
        ImportBatch batch,
        IReadOnlyCollection<SapsStation> stations,
        IReadOnlyCollection<LookupDistrict> districts)
    {
        // Use only detail rows from the worksheet that supplied this summary. This avoids
        // assigning one sheet's demographic totals to a district found on another sheet.
        var sourceStations = batch.CrashRows
            .Where(row => string.Equals(row.WorksheetName, demographics.WorksheetName,
                StringComparison.OrdinalIgnoreCase))
            .Select(row => DistrictKey(row.Station))
            .Where(key => key.Length > 0)
            .ToHashSet();

        var candidateIds = stations
            .Where(station => sourceStations.Contains(DistrictKey(station.StationName)))
            .Select(station => station.DistrictId!.Value)
            .Distinct()
            .ToArray();

        if (candidateIds.Length == 1) return candidateIds[0];

        // A workbook can legitimately cover more than one operational district.
        // Its summary totals must remain regional; assigning the whole total to either
        // district would double-count or misrepresent the source report.
        if (candidateIds.Length > 1) return null;

        // Some workbooks put the district in the worksheet name. Use that only to
        // disambiguate real lookup districts; never create or guess an ID.
        var contextKeys = new[]
        {
            DistrictKey(demographics.WorksheetName),
            DistrictKey(demographics.Region),
            DistrictKey(batch.SelectedRegion)
        };
        var namedMatches = districts.Where(district =>
        {
            var districtKey = DistrictKey(district.DistrictName);
            return districtKey.Length >= 3 && contextKeys.Any(contextKey =>
                contextKey.Length >= 3 &&
                (contextKey.Contains(districtKey, StringComparison.Ordinal) ||
                 districtKey.Contains(contextKey, StringComparison.Ordinal)));
        }).ToArray();

        if (candidateIds.Length > 1)
            namedMatches = namedMatches
                .Where(district => candidateIds.Contains(district.DistrictId)).ToArray();
        if (namedMatches.Length == 1) return namedMatches[0].DistrictId;

        var reason = candidateIds.Length > 1
            ? $"stations on the worksheet resolve to {candidateIds.Length} districts"
            : "none of its stations has a configured district";
        throw new InvalidOperationException(
            $"Cannot import demographics from worksheet '{demographics.WorksheetName}' because {reason}. " +
            "Correct the station-to-district assignments in Lookup data, then import the batch again.");
    }

    // Ignore punctuation and spacing when comparing workbook and lookup names.
    private static string DistrictKey(string? value) => new(
        (value ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string Limit(string value, int max) => value[..Math.Min(value.Length, max)];

    private async Task<ImportCommitResult> ExistingResultAsync(
        ImportBatch batch, CancellationToken cancellationToken)
    {
        var summaryIds = batch.CrashRows.Where(row => row.ProductionSummaryId.HasValue)
            .Select(row => row.ProductionSummaryId!.Value).ToArray();
        var vehicles = await context.CrashSummaryVehicles.CountAsync(
            row => summaryIds.Contains(row.SummaryId), cancellationToken);
        var injuries = await context.CrashSummaryInjuries.CountAsync(
            row => summaryIds.Contains(row.SummaryId), cancellationToken);
        return new ImportCommitResult(batch.ImportBatchId, summaryIds.Length,
            batch.CrashRows.Count(row => row.ReviewStatus == ImportReviewStatuses.Rejected),
            vehicles, injuries,
            batch.Demographics.Count(item => item.ProductionDemographicsId.HasValue));
    }

    private sealed record PreparedRow(StagingCrashSummary Staging, string CrNumber);
    private sealed record ResolvedVehicle(string Code, string Name);
}
