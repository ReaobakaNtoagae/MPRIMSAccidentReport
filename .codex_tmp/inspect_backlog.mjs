import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const source = process.argv[2];
const previewDir = process.argv[3];
const wb = await SpreadsheetFile.importXlsx(await FileBlob.load(source));
const sheets = await wb.inspect({ kind: "sheet", include: "id,name", maxChars: 8000 });
const overview = await wb.inspect({ kind: "workbook,sheet,table,region,definedName", maxChars: 30000, tableMaxRows: 25, tableMaxCols: 20, tableMaxCellChars: 180 });
console.log(sheets.ndjson);
console.log(overview.ndjson);
const active = wb.worksheets.getItem("Active Backlog");
console.log("ACTIVE_ROWS=" + JSON.stringify(active.getRange("A45:J60").values));
console.log("ACTIVE_FORMULAS=" + JSON.stringify(active.getRange("A45:J60").formulas));
console.log("ACTIVE_STYLE=" + (await wb.inspect({kind:"computedStyle",sheetId:"Active Backlog",range:"A53:J60",maxChars:12000})).ndjson);
console.log("TABLES=" + JSON.stringify(active.tables.items.map(t=>({name:t.name,style:t.style,showHeaders:t.showHeaders,showFilterButton:t.showFilterButton}))));
await fs.mkdir(previewDir, { recursive: true });
for (const line of sheets.ndjson.split("\n")) {
  if (!line.trim()) continue;
  const s = JSON.parse(line);
  const blob = await wb.render({ sheetName: s.name, autoCrop: "all", scale: 1, format: "png" });
  await fs.writeFile(`${previewDir}/${s.index}-${s.name.replace(/[^a-z0-9]+/gi,"_")}.png`, new Uint8Array(await blob.arrayBuffer()));
}
