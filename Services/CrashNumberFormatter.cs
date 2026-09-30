namespace CrashReport.Services;

public static class CrashNumberFormatter
{
    
    public static string Format(string station, string number, string? previousStation = null)
    {
        var prefix = string.Join(' ', station.Trim().ToUpperInvariant().Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));
        var value = number.Trim().ToUpperInvariant();
        
        var oldPrefix = previousStation?.Trim().ToUpperInvariant() + "-";
        if (!string.IsNullOrWhiteSpace(previousStation) &&
            !string.Equals(previousStation.Trim(), prefix, StringComparison.OrdinalIgnoreCase) &&
            value.StartsWith(oldPrefix, StringComparison.Ordinal))
            value = value[oldPrefix.Length..];
        if (prefix.Length == 0 || value.Length == 0)
            throw new ArgumentException("Accident SAPS station and AR/CR number are required.");
        var result = value.StartsWith(prefix + "-", StringComparison.Ordinal)
            ? value : prefix + "-" + value;
        
        if (result.Length > 50) throw new ArgumentException("The combined station and AR/CR number must not exceed 50 characters.");
        return result;
    }
}
