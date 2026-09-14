# 🌼 MPRIMS Excel Import Logic — Cute but Serious Notes

> **Tiny memory hook:** the workbook does **not** jump straight into the registry.  
> It goes through **Reception → Staging → Review → Approval → Commit**.

These notes describe the import code that is currently active in this MVC solution. They are written as a learning guide, so each section explains both **what the code does** and **why it exists**.

---

## 1. The whole journey at a glance 🗺️

```text
Excel file
   │
   ▼
① Intake and fingerprint
   │  Validate file → copy safely → calculate SHA-256 → create ImportBatch
   ▼
② Detect and parse
   │  Find template/header → read detail rows → stop before summary tables
   ▼
③ Clean and validate in staging
   │  Keep original values → store cleaned values → create quality findings
   ▼
④ Human review
   │  Edit / accept / apply suggestion / dismiss / refer / approve / reject
   ▼
⑤ Mark batch ready
   │  A deliberate reviewer sign-off; it does not import yet
   ▼
⑥ Commit approved rows
      One transaction writes production summaries, vehicles, injuries and demographics
```

### The golden rule ✨

Only `ImportCommitService.CommitAsync()` crosses the boundary into production. Uploading, parsing, editing and approving all work with staged records.

```csharp
// ImportController.cs
// This is the only action that crosses the staging-to-production boundary.
var result = await _commitService.CommitAsync(
    batchId, CurrentUserId(), cancellationToken);
```

This separation is why a messy spreadsheet can be inspected without contaminating the official crash registry.

---

## 2. The cast of characters 🎭

| Component | Its job | Easy way to remember it |
|---|---|---|
| `ImportController` | Receives HTTP requests and redirects to the correct page | The receptionist |
| `ImportWorkbookIntakeService` | Validates, stores and fingerprints the file | Security at the door |
| `WorkbookTemplateDetector` | Finds the worksheet, header row and column positions | The map reader |
| `WorkbookCrashRowParser` | Converts Excel cells into staged crash rows | The translator |
| `WorkbookSectionLocator` | Separates detail rows from summary tables | The boundary guard |
| `WorkbookSummaryParser` | Reads totals/demographics and reconciles them | The accountant |
| `StagingCrashQualityValidator` | Creates precise quality findings | The inspector |
| `ImportReviewService` | Applies reviewer decisions and controls workflow state | The review desk |
| `ImportWorkflowRules` | Defines shared readiness/import rules | The rulebook |
| `ImportCommitService` | Writes approved data to production | The final gate |
| `usp_SyncImportGeography` | Adds reviewed missing SAPS/route lookup values | The geography assistant |

The controller talks to interfaces such as `IImportBatchProcessingService`; the concrete service performs the work. This keeps HTTP concerns out of the business logic.

---

## 3. Step one: intake the workbook safely 📥

The upload action first calls the intake service and then the processing service:

```csharp
var intake = await _intakeService.IntakeAsync(new ImportWorkbookIntakeCommand(
    content, file.FileName, file.Length, province, reportingMonth,
    reportingYear, userId, notes), cancellationToken);

await _processingService.ProcessAsync(
    intake.ImportBatchId, cancellationToken);
```

### Intake checks

- The stream is readable.
- The extension is `.xlsx`.
- The file is not empty or larger than the configured limit.
- Region, month, year and user are valid.
- The file is a real readable OpenXML/Excel package—not merely a renamed file.
- The display filename is sanitised.
- The real stored filename is a generated GUID.

### Fingerprinting prevents accidental repeat uploads 🐾

The service copies the upload while calculating a SHA-256 hash. It searches for an existing batch with the same:

```text
file hash + selected region + reporting month + reporting year
```

```csharp
var existing = await _batches.FindByFingerprintAsync(
    hash, region, command.ReportingMonth,
    command.ReportingYear, cancellationToken);

if (existing != null)
    return ToResult(existing, alreadyExists: true);
```

The same file therefore reopens its existing batch instead of generating duplicate staging rows.

### Safety detail 🔐

The stored path is resolved and checked against the application content directory before it is opened. Database path text is treated as untrusted input.

---

## 4. Step two: recognise the spreadsheet 🔍

`WorkbookTemplateDetector` examines the first 20 rows of every worksheet. It scores candidate header rows using expected headings such as:

```csharp
private static readonly string[] Required =
[
    "Station", "Date", "Day", "Time",
    "Route", "Location", "CrashType", "VehiclesInvolved"
];
```

It selects the highest-scoring layout and refuses to stage a workbook below `0.80` confidence. Two supported profiles currently exist:

- `EHLANZENI_CAS` — includes a CAS column.
- `STANDARD_DISTRICT` — the standard district layout.

This is column mapping, not “column 7 forever.” If the recognised header moves, the detected column map moves with it.

---

## 5. Step three: know where accident rows end 🛑

The detail table and summary tables live on the same worksheet. `WorkbookSectionLocator` searches for known headings rather than assuming a fixed last row:

```csharp
private static readonly string[] SummaryHeadings =
    ["TOTAL", "GRANDTOTAL", "VICTIMS", "VICTIMGENDER", "RACE", "AGE"];
```

The crash parser stops immediately before that boundary:

```csharp
var summaryStart = WorkbookSectionLocator.FindSummaryStart(sheet, profile);
var detailEnd = summaryStart.HasValue ? summaryStart.Value - 1 : last;

for (var rowNumber = profile.FirstDataRow;
     rowNumber <= detailEnd;
     rowNumber++)
{
    var row = ParseRow(...);
    if (row != null) rows.Add(row);
}
```

Without this guard, totals such as `TOTAL`, `AGE` or `VICTIM GENDER` could be mistaken for SAPS stations and become fake crash records. Tiny guard, enormous responsibility. 🫡

---

## 6. Step four: parse while keeping evidence 🧼

Each row stores three useful kinds of information:

1. **Raw row JSON** — the source row snapshot.
2. **Original fields** — what Excel displayed.
3. **Cleaned/typed fields** — values safe for validation and later production use.

```csharp
OriginalStation = Null(station),
Station = Clean(station, 50, stripCas: true),
OriginalDate = Null(date),
CrashDate = crashDate,
OriginalDay = Null(Read("Day")),
CalculatedDay = crashDate?.DayOfWeek.ToString()
```

This is why automatic cleaning remains auditable. The original value is not erased.

### Date cleaning 📅

The parser accepts actual Excel dates, day-only values and expected date strings. Accidental separator whitespace is removed:

```csharp
// "14/ 03" becomes "14/03" before parsing.
var value = MultiSpace().Replace(
    cell.GetFormattedString(), string.Empty);
```

If Excel supplies only day/month, the selected reporting year is used deliberately rather than letting .NET guess the current year.

### Time cleaning ⏰

The parser supports Excel `DateTime`, valid one-day `TimeSpan`, `H:mm`, `HH:mm`, `Hmm` and `HHmm`.

```csharp
if (span < TimeSpan.Zero || span >= TimeSpan.FromDays(1))
    return null;
```

Invalid durations such as `24:00` are returned as unparsed values. One bad cell becomes a review finding instead of crashing the whole batch.

### Day abbreviations 🌞

Known operational abbreviations are mapped to `DayOfWeek`:

```csharp
"SU" or "SUN" or "SUNDAY" => DayOfWeek.Sunday,
"M"  or "MO"  or "MON"    => DayOfWeek.Monday,
"TU" or "TUE" or "TUES"   => DayOfWeek.Tuesday,
"W"  or "WE"  or "WED"    => DayOfWeek.Wednesday,
"TH" or "THU" or "THUR"   => DayOfWeek.Thursday,
"F"  or "FR"  or "FRI"    => DayOfWeek.Friday,
"SA" or "SAT"               => DayOfWeek.Saturday
```

Unknown text still gets flagged. Supporting valid abbreviations does not mean accepting arbitrary spelling mistakes.

### Vehicle-string parsing 🚗

Workbook vehicle types are slash-separated, but `P/D` and `M/C` contain meaningful internal slashes. They are protected before splitting:

```csharp
var protectedValue = PedestrianAbbreviation()
    .Replace(value, "PEDESTRIAN");
protectedValue = MotorcycleAbbreviation()
    .Replace(protectedValue, "MOTORCYCLE");
```

So `M/C / SED` means **two** entries, not three.

---

## 7. Step five: validate without hiding uncertainty 🚦

The validator creates `ImportDataQualityIssue` records. It does not merely return one error string.

### Blocking errors 🛑

Missing station, crash date, crash time or location blocks that row because it cannot be identified reliably.

```csharp
Required(issues, row, "Station", row.OriginalStation, row.Station);
Required(issues, row, "CrashDate", row.OriginalDate,
    row.CrashDate?.ToString("yyyy-MM-dd"));
```

A date outside the selected reporting month/year is also blocking because it would distort the report period.

### Non-blocking warnings ⚠️

Missing route or crash type is important but may be genuinely absent in historical data. These fields create warnings rather than automatically discarding the crash.

Other warning examples:

- `DAY_DATE_MISMATCH`
- `VEHICLES_MISSING`
- `CASUALTY_TOTAL_IMPLAUSIBLE`
- `SUMMARY_TOTAL_MISMATCH`

### Informational corrections ℹ️

Deterministic cleanup such as station trimming/uppercasing is recorded as already corrected:

```csharp
IssueCode = "STATION_NORMALISED";
ResolutionStatus = ImportIssueResolutionStatuses.Corrected;
```

This is a useful distinction:

- **Cleaning** changes presentation safely.
- **Validation** identifies uncertainty.
- **Review** makes decisions where judgment is required.

---

## 8. Summary reconciliation: reported versus recalculated 🧮

The summary parser reads optional crash totals and demographic sections below the detail table. It supports:

- Crash count.
- Fatal, serious and slight totals by road-user role.
- Fatality age groups.
- Fatality gender by road-user role.
- Population group totals.

The detail rows are recalculated and compared with the workbook totals:

```csharp
if (reported == calculated) return;

issues.Add(new ImportDataQualityIssue
{
    IssueCode = "SUMMARY_TOTAL_MISMATCH",
    OriginalValue = reported.ToString(),
    SuggestedValue = calculated.ToString(),
    IsBlocking = false
});
```

The reviewer can deliberately choose the recalculated value. The decision is stored on the finding; production crash values continue to come from the detailed rows.

> **Memory hook:** summary tables are a reconciliation source, not a second set of crashes.

---

## 9. Duplicate logic: compare the crash, not merely the label 🕵️

An AR/CR number can be reused for genuinely different crashes in source workbooks. Therefore identifier equality alone is not treated as proof of duplication.

The duplicate comparison considers the complete identifying content, including station, CAS, date, time, route, location, crash type, vehicles and casualty breakdown.

### Genuine duplicate

If complete row content matches, only occurrences after the first are flagged. The first row remains the canonical copy.

### Same AR, different crash

If the time, route, location, crash type or other substantive values differ, both may be kept. At commit time, production identifiers receive deterministic suffixes:

```csharp
var crashNumber = previousCount == 0
    ? baseNumber
    : AddOccurrenceSuffix(baseNumber, previousCount);
```

Suffixes progress as `-A`, `-B`, … and later `-DUP26`, while the workbook AR remains unchanged in staging.

---

## 10. The human review choices 👩🏽‍💻

### Edit a row

The service parses every submitted date/time before changing the entity. It then runs the same validation and duplicate checks again.

```csharp
ReconcileValidationIssues(
    row,
    qualityValidator.Validate(row, row.ImportBatch),
    userId,
    CleanNotes(command.Notes));

await RefreshDuplicateIssuesAsync(row, userId, cancellationToken);
```

An edit is therefore not a shortcut around the rules.

### Resolve a finding

The available decisions are:

- **Apply suggestion** — use a safe proposed/recalculated value.
- **Accept** — acknowledge a non-blocking difference with notes.
- **Dismiss** — close a finding that does not apply.

A blocking error cannot simply be accepted “as is.” It must be corrected or the row rejected.

### Refer to a data owner 📮

Referral stores the person/unit, question, due date, referring user and timestamp. The finding becomes `PendingDataOwner` and appears in the central verification queue.

```csharp
issue.RequiresDataOwner = true;
issue.ResolutionStatus = ImportIssueResolutionStatuses.PendingDataOwner;
issue.ReferredTo = ...;
issue.ReferralQuestion = ...;
```

A response is evidence, not an automatic decision. Recording it returns the finding to `Open`, ready for the reviewer’s final choice.

### Approve or reject a row

- **Approve:** all open findings must first be decided.
- **Reject:** the row stays in staging and will never be committed; its open findings close for audit purposes.
- **Awaiting data owner:** the uncertain row stays staged, while safe approved rows can proceed.

---

## 11. “Mark batch ready” is not “Import” ✅ ≠ 📦

This distinction is essential.

### Mark batch ready

`ApproveBatchAsync()` checks that retained rows have been reviewed, workbook-level differences have decisions, and at least one newly approved row exists. It records reviewer sign-off:

```csharp
batch.Status = ImportBatchStatuses.ReadyForImport;
batch.ReviewedByUserId = userId;
batch.ReviewedAt = DateTime.UtcNow;
```

No production crash is inserted here.

### Import approved rows

`CommitAsync()` requires `ReadyForImport`, selects only explicit candidates, then opens the production transaction.

```csharp
public static bool IsImportCandidate(StagingCrashSummary row) =>
    row.ReviewStatus == ImportReviewStatuses.Approved &&
    row.ImportStatus == ImportRecordStatuses.NotImported &&
    !row.ProductionSummaryId.HasValue;
```

If somebody edits or resolves a finding after sign-off, `RefreshBatchStateAsync()` resets the batch to `RequiresReview`. The user must mark it ready again because the previously approved facts changed.

---

## 12. Production commit: the guarded doorway 🚪

Before opening the transaction, the commit service:

- Confirms the batch is ready and has reviewer sign-off.
- Selects approved, unimported rows only.
- Confirms required production fields.
- Reserves crash numbers already used in both registry representations.
- Resolves all vehicle descriptions to controlled lookup codes.

Inside one transaction it:

1. Marks the batch `Importing`.
2. Synchronises approved SAPS stations and routes.
3. Inserts `CrashSummary` records.
4. Inserts vehicle rows.
5. Inserts casualty rows.
6. Links each staging row to its production summary.
7. Imports demographics only when the batch has no deferred work.
8. Marks the batch `Completed` or `PartiallyCompleted`.

If an exception occurs, the transaction rolls back. A failed follow-up attempt does not erase rows committed by an earlier successful partial import.

### Idempotency guards 🛡️

- A completed batch returns its existing result when clicked again.
- `ProductionSummaryId` proves a staging row already crossed the boundary.
- `ImportStatus` distinguishes `NotImported`, `Imported` and `Failed`.

These checks are why repeated button clicks should not duplicate production records.

---

## 13. Vehicle lookup resolution 🚙🚌🏍️

Source text is normalised and compared with vehicle codes, full names and descriptions. Known aliases include:

```csharp
"PD"  or "PED"                         => ["PEDESTRIAN"],
"SED"                                  => ["SEDAN"],
"UNK" or "UNSPECIFIED" or "NOTKNOWN" => ["UNKNOWN"],
"ART" or "ARTICULATED"                => ["ARTIC"]
```

`M/C` is protected and becomes `MOTORCYCLE` before splitting. Longer phrases can match shorter configured descriptions, such as `LIGHT MOTOR VEHICLE (SEDAN)`.

If nothing matches, the controlled `UNKNOWN`/`OTHER` fallback is used. If no fallback exists, commit stops with a useful message instead of violating the vehicle-type foreign key.

---

## 14. Geography synchronisation 🌍

Immediately before inserting production rows, the commit calls:

```csharp
await context.Database.ExecuteSqlInterpolatedAsync(
    $"EXEC dbo.usp_SyncImportGeography @ImportBatchId={batch.ImportBatchId}",
    cancellationToken);
```

The procedure considers only approved, not-yet-imported rows. It:

- Backfills proper district foreign keys from legacy district text.
- Adds missing active SAPS stations only when a reliable district can be determined.
- Adds missing routes without duplicating existing codes.
- Uses locking checks to protect concurrent upserts.
- Refuses to guess when an unknown station has no reliable district match.

Regional demographic summaries may keep `district_id = NULL`; assigning a regional total to Ehlanzeni North or South without evidence would be misleading.

> **Important caution:** fuzzy station matching is operationally convenient but still sensitive. Lookup assignments should be periodically reviewed by a data owner.

---

## 15. The three state machines 🧠

### Batch status

```text
Uploaded → Processing → RequiresReview → ReadyForImport → Importing
                                                     ├─→ Completed
                                                     └─→ PartiallyCompleted

Any processing/commit failure → Failed
```

### Row review status

```text
Pending / AwaitingReviewer → Approved
                          → Rejected
                          → AwaitingDataOwner
```

### Finding resolution status

```text
Open → Corrected / Accepted / Dismissed
Open → PendingDataOwner → Open → final reviewer decision
```

Do not use these statuses interchangeably. A row can be approved while its batch is not yet signed off; a batch can be partially completed while referred rows remain in staging.

---

## 16. What each database table remembers 🗃️

### `import_batches`

One upload attempt: file fingerprint, reporting period, template, state, uploader/reviewer/importer and timestamps.

### `staging_crash_summaries`

One parsed accident row: original values, cleaned values, casualty counts, review state, import state and optional production link.

### `import_data_quality_issues`

One finding: code, severity, blocking flag, original/suggested values, decision notes and full data-owner consultation trail.

### `staging_import_demographics`

Optional workbook-level age, gender and population totals plus the original summary-section JSON.

### Production tables

`crash_summaries`, `crash_summary_vehicles`, `crash_summary_injuries` and `crash_demographics` receive data only during commit.

Both batches and staged rows use `row_version`. If two reviewers edit the same item, the second receives a reload message rather than silently overwriting the first reviewer.

---

## 17. What is automatic, and what needs a person? 🤖 + 🧑🏽

| Automatic and deterministic | Requires judgment |
|---|---|
| Trim/collapse whitespace | Decide whether a suspicious value is truthful |
| Uppercase controlled text | Accept or dismiss a warning |
| Remove spaces in dates | Correct a blocking value |
| Calculate weekday from date | Decide whether to retain a possible duplicate |
| Recognise supported day abbreviations | Reject an unusable row |
| Detect summary-table boundary | Ask a data owner for missing evidence |
| Recalculate totals | Choose reported versus recalculated summary authority |
| Match configured vehicle aliases | Confirm uncertain geography mappings |

This is the best short explanation for why the system does not “just auto-fix everything”: deterministic formatting is safe to automate; factual uncertainty needs accountable human evidence.

---

## 18. Legacy service note 👻

`Services/ExcelImportService.cs` still exists in the codebase, but it is **not registered in `Program.cs` and is not used by `ImportController`**. The active path is the staged pipeline:

```csharp
builder.Services.AddScoped<IImportWorkbookIntakeService, ImportWorkbookIntakeService>();
builder.Services.AddScoped<IImportBatchProcessingService, ImportBatchProcessingService>();
builder.Services.AddScoped<IImportReviewService, ImportReviewService>();
builder.Services.AddScoped<IImportCommitService, ImportCommitService>();
```

Until the legacy code is formally retired, avoid “helpfully” calling it from a new controller—it bypasses the review architecture described above.

---

## 19. A tiny debugging checklist 🩺

### Upload fails before review

Check extension, size, OpenXML validity, storage path, template confidence and `FailureReason` on the batch.

### A row cannot be approved

Look for findings whose resolution is still `Open`. Blocking issues require correction/rejection; warnings need an explicit decision.

### “Mark ready” appears to do nothing

Check whether every retained row is reviewed, batch-level summary findings are resolved, and at least one approved unimported row exists.

### “Import approved rows” imports nothing

Check `ReadyForImport`, reviewer sign-off, `ReviewStatus == Approved`, `ImportStatus == NotImported` and `ProductionSummaryId == null`.

### Commit rolls back

Read the inner error/failure reason. Frequent causes are unmatched vehicle lookup data, uncertain station geography or a production constraint.

### A referral has no immediate answer

That is expected. The row remains staged in `AwaitingDataOwner`; other safe rows can proceed. Record the response later, make the final decision, approve the row and sign off the batch again.

---

## 20. One-minute explanation for a senior 🎤

> “I replaced direct spreadsheet-to-database loading with a staged, auditable ingestion pipeline. It fingerprints and validates the workbook, detects its layout, preserves original and cleaned values, stops before summary sections, parses typed crash data, recalculates totals, and raises field-level quality findings. Reviewers can correct, accept, reject or refer uncertain values to data owners. Only explicitly approved rows cross into production through an idempotent transaction, with duplicate, lookup, geography and concurrency safeguards. This reduces repetitive manual cleaning without automating factual decisions that require accountability.”

---

## 21. Source map 📚

- `Controllers/ImportController.cs` — HTTP endpoints and navigation.
- `Services/Import/ImportWorkbookIntakeService.cs` — secure intake/fingerprinting.
- `Services/Import/ImportBatchProcessingService.cs` — staging coordinator.
- `Services/Import/WorkbookTemplateDetector.cs` — layout recognition.
- `Services/Import/WorkbookSectionLocator.cs` — detail/summary boundary.
- `Services/Import/WorkbookCrashRowParser.cs` — row parsing and cleaning.
- `Services/Import/WorkbookSummaryParser.cs` — summary/demographic reconciliation.
- `Services/Import/StagingCrashQualityValidator.cs` — quality rules.
- `Services/Import/ImportReviewService.cs` — decisions and state transitions.
- `Services/Import/ImportWorkflowRules.cs` — shared eligibility rules.
- `Services/Import/ImportCommitService.cs` — production transaction.
- `Models/Import/Models/` — persisted batch, row, finding and demographic state.
- `Migrations/20260903130000_ReviseImportGeographyForRegionalBatches.cs` — current geography procedure.

🌱 **Final memory hook:** *Preserve the source, clean what is certain, flag what is uncertain, and import only what a human approved.*
