namespace CrashReport.Models.Import.Models
{
    public static class ImportBatchStatuses
    {
        public const string Uploaded = "Uploaded";
        public const string Processing = "Processing";
        public const string RequiresReview = "RequiresReview";
        public const string ReadyForImport = "ReadyForImport";
        public const string Importing = "Importing";
        // Approved rows were imported, but one or more findings are still waiting
        // for a data owner. Those deferred rows remain safely in staging.
        public const string PartiallyCompleted = "PartiallyCompleted";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";

    }

    public static class ImportValidationStatuses
    {
        public const string Pending = "Pending";
        public const string Valid = "Valid";
        public const string Warning = "Warning";
        public const string Error = "Error";
    }

    public static class ImportDuplicateStatuses
    {
        public const string NotChecked = "NotChecked";
        public const string None = "None";
        public const string Possible = "Possible";
        public const string Exact = "Exact";
        public const string IdentifierCollision = "IdentifierCollision";
        public const string ResolvedKeep = "ResolvedKeep";
        public const string ResolvedSkip = "ResolvedSkip";
    }

    public static class ImportReviewStatuses
    {
        public const string Pending = "Pending";
        public const string AwaitingReviewer = "AwaitingReviewer";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        // The reviewer has paused this row until the responsible data owner responds.
        public const string AwaitingDataOwner = "AwaitingDataOwner";
    }

    public static class ImportRecordStatuses
    {
        public const string NotImported = "NotImported";
        public const string Imported = "Imported";
        public const string Failed = "Failed";
    }

    public static class ImportIssueSeverities
    {
        public const string Information = "Information";
        public const string Warning = "Warning";
        public const string Error = "Error";
    }

    public static class ImportIssueResolutionStatuses
    {
        public const string Open = "Open";
        public const string Corrected = "Corrected";
        public const string Accepted = "Accepted";
        public const string Dismissed = "Dismissed";
        public const string AwaitingReviewer = "AwaitingReviewer";
        public const string PendingDataOwner = "PendingDataOwner";
    }


}
