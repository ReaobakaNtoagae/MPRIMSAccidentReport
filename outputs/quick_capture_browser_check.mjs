import { chromium } from "../.codex_tmp/node_modules/playwright/index.mjs";

const browser = await chromium.launch({ channel: "msedge", headless: true });
const page = await browser.newPage();
const errors = [];
page.on("console", message => {
  if (message.type() === "error") errors.push(`console: ${message.text()}`);
});
page.on("pageerror", error => errors.push(`page: ${error.message}`));

await page.goto("http://127.0.0.1:5189/Account/Login");
await page.locator('input[name="email"]').fill("admin@gmail.com");
await page.locator('input[name="password"]').fill("Admin@12345!");
await Promise.all([
  page.waitForURL(url => !url.pathname.includes("/Account/Login")),
  page.locator('button[type="submit"], input[type="submit"]').click(),
]);

const response = await page.goto("http://127.0.0.1:5189/CreateSummary/CreateSummary");
await page.waitForLoadState("networkidle");
const result = await page.evaluate(() => ({
  url: location.href,
  title: document.title,
  costCentreWidget: Boolean($("#qcCostCentre").dxSelectBox("instance")),
  costCentreItems: $("#qcCostCentre").dxSelectBox("instance")?.getDataSource()?.items()?.length ?? -1,
  noInjuryWidget: Boolean($("#qcNoInjury").dxNumberBox("instance")),
  addVehicleWidget: Boolean($("#qcAddVehicle").dxButton("instance")),
  fatalDetailsPositionedInsideGroup: Boolean(document.querySelector("#qcFatalSection")?.closest(".qc-injury-group")),
}));
process.stdout.write(JSON.stringify({ status: response?.status(), result, errors }, null, 2) + "\n");
await browser.close();
