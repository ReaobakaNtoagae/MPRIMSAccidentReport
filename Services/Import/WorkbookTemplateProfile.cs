namespace CrashReport.Services.Import
{
    public sealed record WorkbookTemplateProfile(string TemplateCode, string WorksheetName, int HeaderRow, int FirstDataRow, decimal Confidence, IReadOnlyDictionary<string, int> Columns);
    public static class ImportTemplateCodes
    {
        public const string EhlanzeniCas = "EHLANZENI_CAS";
        public const string StandardDistrict = "STANDARD_DISTRICT";
    }
}
