namespace CrashReport.Options
{
    public sealed class ImportWorkbookOptions
    {
        public const string SectionName = "ImportWorkbook";
        public string StorageRoot { get; set; } = "App_Data/Imports";
        public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
        public long MaxExpandedSizeBytes { get; set; } = 100 * 1024 * 1024;
        public int MaxZipEntries { get; set; } = 2_000;
    }
}
