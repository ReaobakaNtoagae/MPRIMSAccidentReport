import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const source = "C:/Users/ReaobakaNtoagae/Downloads/sample_timesheet_data_daily_log (1).xlsx";
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(source));

const summary = await workbook.inspect({
  kind: "workbook,sheet,table,region",
  maxChars: 12000,
  tableMaxRows: 20,
  tableMaxCols: 20,
  tableMaxCellChars: 160,
});
console.log(summary.ndjson);

for (const sheet of workbook.worksheets.items) {
  const used = sheet.getUsedRange();
  console.log(`USED ${sheet.name}: ${used?.address ?? "none"}`);
  if (used) {
    const region = await workbook.inspect({
      kind: "region",
      sheetId: sheet.name,
      range: used.address,
      maxChars: 16000,
      tableMaxRows: 50,
      tableMaxCols: 25,
      tableMaxCellChars: 200,
    });
    console.log(region.ndjson);
    const preview = await workbook.render({ sheetName: sheet.name, autoCrop: "all", scale: 1, format: "png" });
    await fs.writeFile(`preview-${sheet.name.replaceAll(/[^a-z0-9]/gi, "_")}.png`, new Uint8Array(await preview.arrayBuffer()));
  }
}
