import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const source = "C:/Users/ReaobakaNtoagae/Downloads/sample_timesheet_data_daily_log (1).xlsx";
const outputDir = "C:/Users/ReaobakaNtoagae/source/repos/MPRIMSAccidentReport/outputs/timesheet_august_2026";
const outputPath = `${outputDir}/MDCSSL_August_2026_Timesheet_Zoho.xlsx`;

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(source));
const sheet = workbook.worksheets.getItemAt(0);

// These entries are reconstructed only from real project activities discussed and
// implemented in this solution. Unknown Zoho IDs/system timestamps stay blank.
const entries = [
  [3, "Reviewed MVC solution architecture and existing crash-report workflows", "System analysis", "Planning and analysis"],
  [4, "Documented Angular, .NET Core API and DevExtreme migration architecture", "Technical planning", "Planning and analysis"],
  [5, "Analysed original and cleaned Excel workbook structures", "Workbook analysis", "Data import enhancement"],
  [6, "Designed staged Excel ingestion and import-batch workflow", "Import workflow", "Data import enhancement"],
  [7, "Created staging entities, quality findings and workflow statuses", "Data model", "Data import enhancement"],
  [10, "Implemented secure workbook intake, storage and SHA-256 fingerprinting", "Workbook intake", "Data import enhancement"],
  [11, "Implemented workbook-template detection and dynamic column mapping", "Template detection", "Data import enhancement"],
  [12, "Implemented detailed crash-row parsing with original-value preservation", "Row parsing", "Data import enhancement"],
  [13, "Added date whitespace cleanup and controlled date parsing", "Data cleaning", "Data import enhancement"],
  [14, "Corrected Excel time parsing and invalid-duration handling", "Data cleaning", "Data import enhancement"],
  [17, "Added weekday abbreviation recognition and date reconciliation", "Data validation", "Data import enhancement"],
  [18, "Implemented accident-table and summary-table boundary detection", "Workbook sections", "Data import enhancement"],
  [19, "Parsed and reconciled casualty and demographic summary totals", "Summary reconciliation", "Data import enhancement"],
  [20, "Implemented blocking and non-blocking data-quality findings", "Quality validation", "Data import enhancement"],
  [21, "Revised duplicate detection to compare complete crash rows", "Duplicate handling", "Data import enhancement"],
  [24, "Implemented staged-row editing, revalidation and review decisions", "Review workflow", "Data import enhancement"],
  [25, "Implemented data-owner referrals and verification response tracking", "Verification workflow", "Data import enhancement"],
  [26, "Implemented transactional commit, rollback and idempotency controls", "Production import", "Data import enhancement"],
  [27, "Added vehicle-type aliases and controlled lookup resolution", "Lookup integration", "Data import enhancement"],
  [28, "Implemented SAPS station, route and regional district synchronisation", "Geography integration", "Data import enhancement"],
  [31, "Redesigned import review, verification and Quick Capture interfaces", "User interface", "UX and functional enhancement"],
];

const headers = [
  "Date", "Task/General/Issue", "Task/Issue ID", "Daily Log", "User",
  "Billing Type", "Notes", "Added By", "Hours(For Calculation)", "Owner Mailid",
  "Role", "Type", "Project Group", "milestone", "Task List/Module", "Timer Notes",
  "Number Field", "Created Time", "Modified Time"
];

// Keep the source template's seven metadata rows and exact 19-column import schema.
sheet.getRange("A1:B7").values = [
  ["ORGANIZATION NAME :", "Mpumalanga Department of Community Safety, Security and Liaison"],
  ["LOGIN NAME :", "Reaobaka Ntoagae"],
  ["PROJECT NAME :", "MDCSSL ENHANCEMENTS FINAL"],
  ["PROJECT ID :", null],
  ["REPORT TAKEN ON :", "10 September 2026"],
  ["REPORT FOR :", "08-01-2026 To 08-31-2026"],
  ["For Whom :", "Reaobaka Ntoagae"],
];
sheet.getRange("A9:S9").values = [headers];

// Remove sample people/tasks and any residual rows before writing the August log.
sheet.getRange("A10:S200").clear({ applyTo: "contents" });

const rows = entries.map(([day, task, module, milestone]) => {
  const date = new Date(2026, 7, day); // JavaScript month 7 = August.
  const dateText = `${String(day).padStart(2, "0")}-08-2026`;
  return [
    date,
    task,
    null,
    8,
    "Reaobaka Ntoagae",
    "Non Billable",
    `Timesheet log details:\nStart Time - ${dateText} 08:00 AM End time - ${dateText} 04:30 PM\nLunch - 00:30\nTime spent - 08:00`,
    "Reaobaka Ntoagae",
    8,
    "reaobaka@rims.company",
    "Administrator",
    "task",
    "Ungrouped Projects",
    milestone,
    module,
    "-",
    "-",
    null,
    null,
  ];
});

sheet.getRange(`A10:S${9 + rows.length}`).values = rows;

// Match the practical, plain export style while keeping long task/notes fields readable.
const dataEnd = 9 + rows.length;
sheet.getRange(`A9:S${dataEnd}`).format.font = { name: "Arial", size: 10, color: "#222222" };
sheet.getRange("A9:S9").format = {
  fill: "#E7E6E6",
  font: { name: "Arial", size: 10, bold: true, color: "#222222" },
  verticalAlignment: "center",
  horizontalAlignment: "center",
  wrapText: true,
  borders: { preset: "all", style: "thin", color: "#BFBFBF" },
};
sheet.getRange(`A10:S${dataEnd}`).format.borders = { preset: "all", style: "thin", color: "#E0E0E0" };
sheet.getRange(`A10:A${dataEnd}`).format.numberFormat = "dd-mmm-yyyy";
sheet.getRange(`D10:D${dataEnd}`).format.numberFormat = "0.0";
sheet.getRange(`I10:I${dataEnd}`).format.numberFormat = "0.0";
sheet.getRange(`G10:G${dataEnd}`).format.wrapText = true;
sheet.getRange(`A10:S${dataEnd}`).format.verticalAlignment = "top";

// Widths are deliberately set rather than globally autofitted so the import columns
// remain predictable and the long notes do not create an extremely wide worksheet.
sheet.getRange(`A1:A${dataEnd}`).format.columnWidth = 14;
sheet.getRange(`B1:B${dataEnd}`).format.columnWidth = 48;
sheet.getRange(`C1:C${dataEnd}`).format.columnWidth = 15;
sheet.getRange(`D1:D${dataEnd}`).format.columnWidth = 12;
sheet.getRange(`E1:F${dataEnd}`).format.columnWidth = 20;
sheet.getRange(`G1:G${dataEnd}`).format.columnWidth = 44;
sheet.getRange(`H1:H${dataEnd}`).format.columnWidth = 20;
sheet.getRange(`I1:I${dataEnd}`).format.columnWidth = 20;
sheet.getRange(`J1:J${dataEnd}`).format.columnWidth = 26;
sheet.getRange(`K1:L${dataEnd}`).format.columnWidth = 15;
sheet.getRange(`M1:M${dataEnd}`).format.columnWidth = 20;
sheet.getRange(`N1:O${dataEnd}`).format.columnWidth = 28;
sheet.getRange(`P1:S${dataEnd}`).format.columnWidth = 18;
sheet.getRange(`A10:S${dataEnd}`).format.rowHeight = 58;
sheet.getRange("A9:S9").format.rowHeight = 34;
sheet.freezePanes.freezeRows(9);

workbook.recalculate();

const inspection = await workbook.inspect({
  kind: "region",
  sheetId: sheet.name,
  range: `A1:S${dataEnd}`,
  maxChars: 14000,
  tableMaxRows: 30,
  tableMaxCols: 19,
});
console.log(inspection.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!",
  options: { useRegex: true, maxResults: 100 },
  summary: "final formula error scan",
});
console.log(errors.ndjson);

await fs.mkdir(outputDir, { recursive: true });
const preview = await workbook.render({ sheetName: sheet.name, range: `A1:S${dataEnd}`, scale: 1, format: "png" });
await fs.writeFile(`${outputDir}/MDCSSL_August_2026_Timesheet_preview.png`, new Uint8Array(await preview.arrayBuffer()));
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(`OUTPUT ${outputPath}`);
