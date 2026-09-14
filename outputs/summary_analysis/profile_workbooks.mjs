import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

// These are the three real workflow stages supplied by the user: the original
// workbook, the colleague's converted workbook and the completed upload template.
const files = [
  String.raw`C:\Users\ReaobakaNtoagae\OneDrive - atnetgroup.com\EHL MARCH 2026.xlsx`,
  String.raw`C:\Users\ReaobakaNtoagae\Downloads\Upload EHLANZENI 2026-03.xlsx`,
  String.raw`C:\Users\ReaobakaNtoagae\Downloads\Upload EHLANZENI 2026-03 (filled).xlsx`,
];

const profiles = [];
for (const path of files) {
  const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(path));
  const sheets = [];

  for (const sheet of workbook.worksheets.items) {
    const used = sheet.getUsedRange();
    const values = used?.values ?? [];
    const nonEmptyRows = [];

    // Preserve coordinates and visible text for the bottom of each sheet. This is
    // enough to identify section headings without dumping every accident record.
    for (let rowIndex = 0; rowIndex < values.length; rowIndex++) {
      const row = values[rowIndex] ?? [];
      const cells = row.map((value, colIndex) => ({
        col: colIndex + 1,
        value: value instanceof Date ? value.toISOString() : value,
      })).filter(cell => cell.value !== null && cell.value !== undefined && String(cell.value).trim() !== "");
      if (cells.length) nonEmptyRows.push({ row: rowIndex + 1, cells });
    }

    const headingRows = nonEmptyRows.filter(item =>
      item.cells.some(cell => /total|summary|demograph|age|race|gender|driver|passenger|pedestrian|cyclist|reported accident/i.test(String(cell.value))));

    sheets.push({
      name: sheet.name,
      rowCount: values.length,
      columnCount: Math.max(0, ...values.map(row => row?.length ?? 0)),
      headings: headingRows,
      tail: nonEmptyRows.slice(-80),
    });
  }

  profiles.push({ path, sheets });
}

await fs.writeFile("workbook_summary_profiles.json", JSON.stringify(profiles, null, 2));
console.log(JSON.stringify(profiles.map(profile => ({
  path: profile.path,
  sheets: profile.sheets.map(sheet => ({
    name: sheet.name,
    rows: sheet.rowCount,
    cols: sheet.columnCount,
    headingRows: sheet.headings.map(row => row.row),
  })),
})), null, 2));
