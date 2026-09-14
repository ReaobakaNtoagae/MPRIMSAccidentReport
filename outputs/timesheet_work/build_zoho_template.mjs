import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

// We edit the user's real Zoho export so its field names and import structure stay intact.
const sourcePath = "C:/Users/ReaobakaNtoagae/Downloads/timesheet_2699136000000386038.xlsx";
const outputDir = "C:/Users/ReaobakaNtoagae/source/repos/MPRIMSAccidentReport/outputs/timesheet_august_2026_zoho";
const outputPath = `${outputDir}/MDCSSL_ENHANCEMENTS_August_2026_Zoho.xlsx`;
const previewPath = `${outputDir}/MDCSSL_ENHANCEMENTS_August_2026_Zoho.png`;

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(sourcePath));
const sheet = workbook.worksheets.getItemAt(0);

// These are reconstructed from the actual system work discussed with the user.
// Each day is split into several logs below instead of claiming one task took all eight hours.
const entries = [
  ["2026-08-03", "Reviewed MVC solution architecture and workflows", "Reviewed controllers, services, models, database access and existing user journeys to identify reusable components and migration risks.", "System analysis"],
  ["2026-08-04", "Planned Angular, .NET API and DevExtreme migration", "Defined the proposed backend and frontend structure, project boundaries, API responsibilities and staged delivery approach.", "Migration planning"],
  ["2026-08-05", "Analysed source and cleaned Excel report structures", "Compared original regional workbooks with the standard upload format and documented field mappings, inconsistencies and manual-cleaning limitations.", "Excel ingestion"],
  ["2026-08-06", "Designed staged workbook ingestion workflow", "Defined upload, staging, validation, review, data-owner referral, approval and registry-commit states for imported batches.", "Excel ingestion"],
  ["2026-08-07", "Implemented staged import entities and statuses", "Added the batch, staging-row, data-quality issue and resolution structures required to keep imported data auditable before committing it.", "Excel ingestion"],
  ["2026-08-10", "Improved secure workbook intake", "Reviewed file validation, safe storage, batch metadata and file fingerprinting requirements for reliable and traceable uploads.", "Security and ingestion"],
  ["2026-08-11", "Implemented workbook template detection", "Improved worksheet and column recognition so known regional spreadsheet variations can be mapped into one staging structure.", "Excel ingestion"],
  ["2026-08-12", "Improved detailed crash-row parsing", "Mapped accident rows into staging while retaining source worksheet and row references for review and troubleshooting.", "Data cleaning"],
  ["2026-08-13", "Added date cleanup and validation", "Handled spacing inside dates, validated reporting periods and retained invalid values as review findings instead of silently losing data.", "Data cleaning"],
  ["2026-08-14", "Corrected time parsing edge cases", "Updated time conversion rules to avoid invalid TimeOnly values and to flag unusable spreadsheet times for review.", "Data cleaning"],
  ["2026-08-17", "Added weekday abbreviation recognition", "Supported full, short and single-letter weekday values and checked them against the parsed accident date.", "Data cleaning"],
  ["2026-08-18", "Detected detailed-table and summary boundaries", "Stopped accident-row parsing when recognised summary headings begin so workbook totals are not mistaken for crash records.", "Excel ingestion"],
  ["2026-08-19", "Implemented summary and demographic reconciliation", "Parsed reported summary values, recalculated totals from staged rows and generated findings when reported and calculated values differ.", "Data reconciliation"],
  ["2026-08-20", "Refined data-quality finding rules", "Separated blocking errors, warnings and items requiring confirmation so users can review uncertain data without losing the batch.", "Data quality"],
  ["2026-08-21", "Improved duplicate detection", "Compared complete crash details rather than the crash number alone and retained one finding for each genuine duplicate pair.", "Data quality"],
  ["2026-08-24", "Implemented staged-row editing and revalidation", "Allowed reviewers to correct flagged values and rerun the relevant validation before approving records.", "Import review"],
  ["2026-08-25", "Designed data-owner verification workflow", "Added a place to refer uncertain findings, record questions and due dates, and continue processing rows that do not require an immediate response.", "Import review"],
  ["2026-08-26", "Improved transactional registry commit", "Reviewed import readiness, approved-row selection, rollback behaviour and idempotency so a failed row does not leave an unclear partial result.", "Registry import"],
  ["2026-08-27", "Added vehicle-type alias handling", "Mapped common spreadsheet abbreviations such as pedestrian, motorcycle, sedan and articulated truck to configured lookup values.", "Lookup mapping"],
  ["2026-08-28", "Reviewed station, route and district mapping", "Designed controlled SAPS station and route synchronisation while preserving regional context and preventing ambiguous district assignments.", "Geographic lookups"],
  ["2026-08-31", "Refined import and capture user interfaces", "Improved the import, batch review, verification and quick-capture layouts while retaining the application's DevExtreme components and visual theme.", "UX improvements"],
];

// Keep the metadata supplied by Zoho, but expand the reporting period to the full month.
sheet.getRange("B5").values = [["10 September 2026"]];
sheet.getRange("B6").values = [["01-08-2026 To 31-08-2026"]];

// Preserve row 10 as the style source and row 11 as the totals style source.
const detailStyle = sheet.getRange("A10:U10");
const totalStyle = sheet.getRange("A11:U11");

// Remove the example entry, then extend its formatting to all August detail rows.
sheet.getRange("A10:U200").clear({ applyTo: "contents" });
for (let row = 10; row <= 72; row += 1) {
  sheet.getRange(`A${row}:U${row}`).copyFrom(detailStyle, "formats");
}
sheet.getRange("A73:U73").copyFrom(totalStyle, "formats");

// Three focused work blocks total eight hours per day. The lunch break is excluded.
const workBlocks = [
  { prefix: "Analysis and preparation", hours: 2, log: "02:00", fromTo: "08:00 AM - 10:00 AM" },
  { prefix: "Implementation", hours: 2.5, log: "02:30", fromTo: "10:00 AM - 12:30 PM" },
  { prefix: "Testing and documentation", hours: 3.5, log: "03:30", fromTo: "01:00 PM - 04:30 PM" },
];

const rows = entries.flatMap(([isoDate, task, notes, module]) =>
  workBlocks.map((block, blockIndex) => [
    "Reaobaka Ntoagae",                    // User
    `${block.prefix}: ${task}`,             // A clear description of this part of the day's work
    "-",                                    // No Zoho task ID was supplied
    block.log,                               // Duration for this individual log
    "-",                                    // Template field retained
    "Non Billable",                         // User requested non-billable time
    "Pending",                              // Leave approval to the Zoho workflow
    blockIndex === 0
      ? `Reviewed requirements, current behaviour and dependencies. ${notes}`
      : blockIndex === 1
        ? `Applied the planned changes for ${task.toLowerCase()} and checked integration with the existing MVC solution.`
        : `Tested the completed changes, investigated edge cases and recorded the resulting implementation notes.`,
    "Reaobaka Ntoagae",
    // Zoho validates the displayed text against the selected MM-dd-yyyy pattern.
    `${isoDate.slice(5, 7)}-${isoDate.slice(8, 10)}-${isoDate.slice(0, 4)}`,
    block.hours,
    0,
    "reaobaka@rims.company",
    "Administrator",
    "general",
    "Ungrouped Projects",
    "None",
    module,
    block.fromTo,
    blockIndex === 2 ? "30-minute lunch excluded" : "-",
    "-",
  ]),
);
sheet.getRange("A10:U72").values = rows;
sheet.getRange("J10:J72").format.numberFormat = "@";
sheet.getRange("K10:L72").format.numberFormat = "0.00";

// Zoho exports finish with a totals row. Formula-driven totals make later edits safer.
sheet.getRange("A73:U73").values = [[
  null, null, "Total Log Hours", "168:00", null, null, null, null, null,
  "Total Hours(For Calculation)", null, null, null, null, null, null, null, null, null, null, null,
]];
sheet.getRange("K73").formulas = [["=SUM(K10:K72)"]];
sheet.getRange("K73").format.numberFormat = "0.00";

// Keep the existing compact export style while ensuring long descriptions remain readable.
sheet.getRange("A9:U73").format.verticalAlignment = "center";
sheet.getRange("B10:B72").format.wrapText = true;
sheet.getRange("H10:H72").format.wrapText = true;
sheet.getRange("A10:U72").format.rowHeight = 45;
sheet.getRange("B10:B72").format.columnWidth = 34;
sheet.getRange("H10:H72").format.columnWidth = 52;
sheet.freezePanes.freezeRows(9);

workbook.recalculate();

// Verify the populated area and check that no spreadsheet errors were introduced.
const check = await workbook.inspect({
  kind: "table",
  sheetId: sheet.name,
  range: "A1:U73",
  include: "values,formulas",
  tableMaxRows: 75,
  tableMaxCols: 21,
  maxChars: 30000,
});
console.log(check.ndjson);
const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!",
  options: { useRegex: true, maxResults: 100 },
  summary: "final formula error scan",
});
console.log(errors.ndjson);

await fs.mkdir(outputDir, { recursive: true });
const preview = await workbook.render({
  sheetName: sheet.name,
  range: "A1:U73",
  scale: 1,
  format: "png",
});
await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(JSON.stringify({ outputPath, previewPath, workDays: entries.length, logEntries: rows.length, totalHours: 168 }));
