using CrashReport.ViewModels;
using System.Text.Json;

namespace CrashReport.Services;

public class MonthlyMemoDocService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MonthlyMemoDocService> _logger;

    public MonthlyMemoDocService(IWebHostEnvironment env,
        ILogger<MonthlyMemoDocService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<byte[]> GenerateAsync(MonthlyMemoViewModel vm)
    {
        var tmpJson = Path.Combine(Path.GetTempPath(), $"memo_{Guid.NewGuid():N}.json");
        var tmpDocx = Path.Combine(Path.GetTempPath(), $"memo_{Guid.NewGuid():N}.docx");

        try
        {
            await File.WriteAllTextAsync(tmpJson,
                JsonSerializer.Serialize(vm, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    WriteIndented = false
                }));

            var scriptPath = FindScript();
            _logger.LogInformation("Running Node generator: {path}", scriptPath);

            var (exit, _, stderr) = await RunAsync(scriptPath, tmpJson, tmpDocx);

            if (exit != 0)
                throw new Exception($"Generator failed (exit {exit}): {stderr}");

            return await File.ReadAllBytesAsync(tmpDocx);
        }
        finally
        {
            if (File.Exists(tmpJson)) File.Delete(tmpJson);
            if (File.Exists(tmpDocx)) File.Delete(tmpDocx);
        }
    }

    private string FindScript()
    {
        foreach (var sub in new[] { "js", "scripts" })
        {
            var p = Path.Combine(_env.WebRootPath, sub, "generate_monthly.js");
            if (File.Exists(p)) return p;
        }
        throw new FileNotFoundException(
            "generate_monthly.js not found in wwwroot/js/ or wwwroot/scripts/. " +
            "Also ensure charts.js is in the same folder.");
    }

    private static async Task<(int, string, string)> RunAsync(
        string scriptPath, string jsonPath, string docxPath)
    {
        var scriptDir = Path.GetDirectoryName(scriptPath)!;

        using var proc = new System.Diagnostics.Process();
        proc.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{scriptPath}\" \"{jsonPath}\" \"{docxPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,

    
            WorkingDirectory = scriptDir
        };

  
        var nodePath = BuildNodePath(scriptDir);
        proc.StartInfo.Environment["NODE_PATH"] = nodePath;

        proc.Start();
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, stdout, stderr);
    }

    private static string BuildNodePath(string scriptDir)
    {
        var sep = Path.PathSeparator;
        var paths = new List<string>();

        
        paths.Add(Path.Combine(scriptDir, "node_modules"));


        var appData = Environment.GetEnvironmentVariable("APPDATA");
        if (!string.IsNullOrEmpty(appData))
            paths.Add(Path.Combine(appData, "npm", "node_modules"));


        var npmPrefix = GetNpmPrefix();
        if (!string.IsNullOrEmpty(npmPrefix))
        {
          
            paths.Add(Path.Combine(npmPrefix, "node_modules"));
            paths.Add(Path.Combine(npmPrefix, "lib", "node_modules"));
        }

        
        var pf = Environment.GetEnvironmentVariable("ProgramFiles");
        if (!string.IsNullOrEmpty(pf))
            paths.Add(Path.Combine(pf, "nodejs", "node_modules"));

        
        var existing = Environment.GetEnvironmentVariable("NODE_PATH");
        if (!string.IsNullOrEmpty(existing))
            paths.Add(existing);

        return string.Join(sep, paths.Where(p => !string.IsNullOrEmpty(p)));
    }

    private static string? GetNpmPrefix()
    {
        try
        {
            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "npm",
                Arguments = "prefix -g",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return proc.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}