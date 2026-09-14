import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const sourcePath = "C:/Users/ReaobakaNtoagae/source/repos/MPRIMSAccidentReport/outputs/timesheet_august_2026_zoho/MDCSSL_ENHANCEMENTS_August_2026_Zoho.xlsx";
const outputDir = "C:/Users/ReaobakaNtoagae/source/repos/MPRIMSAccidentReport/outputs/timesheet_work";

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(sourcePath));
const overview = await workbook.inspect({
  kind: "workbook,sheet,table",
  maxChars: 12000,
  tableMaxRows: 25,
  tableMaxCols: 25,
  tableMaxCellChars: 120,
});
console.log(overview.ndjson);

await fs.mkdir(outputDir, { recursive: true });
for (const sheet of workbook.worksheets.items) {
  const preview = await workbook.render({
    sheetName: sheet.name,
    autoCrop: "all",
    scale: 1,
    format: "png",
  });
  const safeName = sheet.name.replace(/[^a-z0-9_-]/gi, "_");
  await fs.writeFile(`${outputDir}/new_template_${safeName}.png`, new Uint8Array(await preview.arrayBuffer()));
}
