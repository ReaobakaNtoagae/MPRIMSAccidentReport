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

            // Set working directory to the script folder so require('docx')
            // and require('pngjs') resolve from local node_modules if present
            WorkingDirectory = scriptDir
        };

        // Ensure the npm global modules directory is on NODE_PATH so packages
        // installed with "npm install -g docx pngjs" are found even when
        // the ASP.NET process doesn't inherit the user's shell environment
        var nodePath = BuildNodePath(scriptDir);
        proc.StartInfo.Environment["NODE_PATH"] = nodePath;

        proc.Start();
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode, stdout, stderr);
    }

    /// <summary>
    /// Builds a NODE_PATH that includes (in priority order):
    ///   1. node_modules next to the script (wwwroot/js/node_modules)
    ///   2. The user-level npm global modules  (%APPDATA%\npm\node_modules)
    ///   3. Common system-wide npm locations   (%ProgramFiles%\nodejs\node_modules)
    ///   4. Any existing NODE_PATH from the environment
    /// </summary>
    private static string BuildNodePath(string scriptDir)
    {
        var sep = Path.PathSeparator;   // ';' on Windows, ':' on Linux
        var paths = new List<string>();

        // 1. Local node_modules beside the script
        paths.Add(Path.Combine(scriptDir, "node_modules"));

        // 2. npm global prefix → node_modules
        //    %APPDATA%\npm\node_modules  (Windows user install)
        var appData = Environment.GetEnvironmentVariable("APPDATA");
        if (!string.IsNullOrEmpty(appData))
            paths.Add(Path.Combine(appData, "npm", "node_modules"));

        // 3. Detect npm prefix dynamically (works on Windows and Linux)
        var npmPrefix = GetNpmPrefix();
        if (!string.IsNullOrEmpty(npmPrefix))
        {
            // Windows: <prefix>\node_modules
            // Linux:   <prefix>/lib/node_modules
            paths.Add(Path.Combine(npmPrefix, "node_modules"));
            paths.Add(Path.Combine(npmPrefix, "lib", "node_modules"));
        }

        // 4. %ProgramFiles%\nodejs\node_modules  (Windows system-wide install)
        var pf = Environment.GetEnvironmentVariable("ProgramFiles");
        if (!string.IsNullOrEmpty(pf))
            paths.Add(Path.Combine(pf, "nodejs", "node_modules"));

        // 5. Inherit any existing NODE_PATH
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