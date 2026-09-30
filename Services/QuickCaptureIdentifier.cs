using System.Globalization;
using System.Text.RegularExpressions;

namespace CrashReport.Services;


public static partial class QuickCaptureIdentifier
{
    [GeneratedRegex(@"^\d+$")]
    private static partial Regex SequenceOnlyPattern();

    [GeneratedRegex(@"^(?<sequence>\d+)/(?<month>\d{1,2})/(?<year>\d{4})$")]
    private static partial Regex CasPattern();

    public static string FormatAr(string? input, DateOnly crashDate)
    {
        var sequence = (input ?? string.Empty).Trim();
        if (!SequenceOnlyPattern().IsMatch(sequence))
            throw new ArgumentException("Enter only the sequential AR number, for example 42.");

        
        if (!long.TryParse(sequence, NumberStyles.None, CultureInfo.InvariantCulture, out var sequenceNumber))
            throw new ArgumentException("The AR sequence is too large.");
        sequence = sequenceNumber.ToString(CultureInfo.InvariantCulture);
        return $"{sequence}-{crashDate:MM-yyyy}";
    }

    public static string? FormatCas(string? input, DateOnly crashDate)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var match = CasPattern().Match(input.Trim());
        if (!match.Success)
            throw new ArgumentException("CAS must use sequential number/month/year, for example 123/09/2026.");

        if (!long.TryParse(match.Groups["sequence"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
            throw new ArgumentException("The CAS sequence is too large.");
        var month = int.Parse(match.Groups["month"].Value, CultureInfo.InvariantCulture);
        var year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
        if (month != crashDate.Month || year != crashDate.Year)
            throw new ArgumentException($"CAS month and year must match the crash date ({crashDate:MM/yyyy}).");

        return $"{sequence}/{month:00}/{year:0000}";
    }

    public static string ExtractArSequence(string storedAr)
    {

        var match = Regex.Match(storedAr ?? string.Empty, @"(?<sequence>\d+)-\d{2}-\d{4}$");
        if (match.Success) return match.Groups["sequence"].Value;

        var finalPart = (storedAr ?? string.Empty).Split('-', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return finalPart is not null && SequenceOnlyPattern().IsMatch(finalPart) ? finalPart : storedAr ?? string.Empty;
    }
}
