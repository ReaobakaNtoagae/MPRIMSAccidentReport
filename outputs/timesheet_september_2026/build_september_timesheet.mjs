import fs from 'node:fs/promises';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const outputDir = new URL('./', import.meta.url).pathname.replace(/^\/(?:([A-Za-z]:))/, '$1');
const filePath = `${outputDir}MDCSSL_September_2026_Timesheet_Draft.xlsx`;
const workbook = Workbook.create();
const sheet = workbook.worksheets.add('MDCSSL ENHANCEMENTS -RI-10');
sheet.showGridLines = false;

// This follows the 21-column Zoho export supplied by the user. Activity and time
// splits beyond the named events are drafts, not a claim of verified attendance.
const entries = [
  ['09-01', [
    [2.0, 'Review quick-capture fields and workflow', 'Draft: compared capture layout with stakeholder questions; verify activity and hours.'],
    [3.0, 'Test staged import review and approval flow', 'Draft: checked row editing, verification and batch readiness; verify activity and hours.'],
    [3.0, 'Prepare stakeholder walkthrough scenarios', 'Draft: prepared Quick Capture and Import examples for the 4 September meeting; verify activity and hours.']]],
  ['09-02', [
    [3.0, 'Test import rules using sample workbooks', 'Draft: reviewed duplicate, date and summary-reconciliation cases; verify activity and hours.'],
    [2.5, 'Review quick-capture validation', 'Draft: checked AR/CAS format, required fields and injury counts; verify activity and hours.'],
    [2.5, 'Document questions for stakeholder confirmation', 'Draft: recorded ambiguous import rules and data-owner decisions; verify activity and hours.']]],
  ['09-03', [
    [3.0, 'Investigate import reconciliation and duplicate handling', 'Draft: reviewed workbook totals and distinct crash rows; verify activity and hours.'],
    [2.5, 'Check vehicle-type and geographic lookups', 'Draft: examined lookup gaps affecting approved-row import; verify activity and hours.'],
    [2.5, 'Prepare Quick Capture and Import demonstration', 'Draft: rehearsed the workflows and collected questions; verify activity and hours.']]],
  ['09-04', [
    [2.0, 'Prepare stakeholder demonstration', 'Prepared Quick Capture and Import walkthrough; duration estimated.'],
    [3.0, 'Stakeholder meeting: Quick Capture and Import', 'First stakeholder meeting. Walked through both functions and sought confirmation of import rules; duration estimated.'],
    [3.0, 'Record stakeholder decisions and open questions', 'Draft: captured requested changes and unresolved import-rule questions; verify activity and hours.']]],
  ['09-07', [
    [3.0, 'Refine Quick Capture layout and field order', 'Draft: aligned capture fields with the consolidated spreadsheet; verify activity and hours.'],
    [2.5, 'Review injury and vehicle detail requirements', 'Draft: checked road-user details, casualty totals and vehicle fields; verify activity and hours.'],
    [2.5, 'Prepare cost-centre site-visit questions', 'Draft: prepared questions about source forms, handoffs and current-system entry; verify activity and hours.']]],
  ['09-08', [
    [1.0, 'Prepare for Ehlanzeni cost-centre visit', 'Draft: reviewed data-flow questions and visit objectives; verify activity and hours.'],
    [6.0, 'Ehlanzeni cost-centre process visit', 'Visited one cost centre to trace data collection through capture in the current system; time includes travel and is estimated.'],
    [1.0, 'Record Ehlanzeni visit findings', 'Draft: captured observed handoffs and questions to follow up; verify activity and hours.']]],
  ['09-09', [
    [1.0, 'Prepare for Gert Sibande cost-centre visits', 'Draft: tailored process questions for two sites; verify activity and hours.'],
    [6.0, 'Gert Sibande cost-centre process visits', 'Visited two cost centres to understand collection, checking and entry into the current system; time includes travel and is estimated.'],
    [1.0, 'Record Gert Sibande visit findings', 'Draft: compared site workflows and captured follow-up questions; verify activity and hours.']]],
  ['09-10', [
    [3.0, 'Compare Ehlanzeni and Gert Sibande data flows', 'Draft: consolidated visit observations into common and differing steps; verify activity and hours.'],
    [2.5, 'Translate field findings into capture requirements', 'Draft: reviewed cost centre, injury, vehicle and validation needs; verify activity and hours.'],
    [2.5, 'Prepare Nkangala cost-centre questions', 'Draft: identified gaps to verify during the next site visits; verify activity and hours.']]],
  ['09-11', [
    [1.0, 'Prepare for Nkangala cost-centre visits', 'Draft: reviewed open data-collection questions; verify activity and hours.'],
    [6.0, 'Nkangala cost-centre process visits', 'Visited two cost centres to trace collection and current-system capture; time includes travel and is estimated.'],
    [1.0, 'Record Nkangala visit findings', 'Draft: captured workflow differences and unresolved questions; verify activity and hours.']]],
  ['09-14', [
    [3.0, 'Consolidate three-region visit findings', 'Draft: compared processes across Ehlanzeni, Gert Sibande and Nkangala; verify activity and hours.'],
    [3.0, 'Review stakeholder feedback against system', 'Draft: checked the requested enhancements against existing Quick Capture and Import behaviour; verify activity and hours.'],
    [2.0, 'Update presentation issues and questions', 'Draft: listed improvements and points requiring manager input; verify activity and hours.']]],
  ['09-15', [
    [3.0, 'Prepare regional-manager system walkthrough', 'Draft: organized demonstration sequence and representative scenarios; verify activity and hours.'],
    [2.5, 'Test Quick Capture and Import demo flows', 'Draft: checked sample scenarios and likely questions; verify activity and hours.'],
    [2.5, 'Prepare feedback and decision log', 'Draft: separated confirmed enhancements from rules requiring stakeholder answers; verify activity and hours.']]],
  ['09-16', [
    [2.0, 'Prepare walkthrough with provincial Safety Engineering staff', 'Draft: assembled demonstration material ahead of the preparation meeting; verify activity and hours.'],
    [4.0, 'Provincial Safety Engineering presentation rehearsal', 'Met provincial staff to rehearse the assistant-manager presentation, agree key messages and review enhancements and unanswered questions; duration estimated.'],
    [2.0, 'Revise walkthrough after rehearsal', 'Draft: adjusted talking points and presentation sequence; verify activity and hours.']]],
  ['09-17', [
    [2.0, 'Prepare regional-manager presentation', 'Draft: completed final walkthrough checks and examples; verify activity and hours.'],
    [4.0, 'Present system to regional assistant managers', 'Presented a system walkthrough to regional assistant managers and the provincial manager; discussed questions from both sides. Duration estimated.'],
    [2.0, 'Capture presentation questions and feedback', 'Draft: recorded questions, requested improvements and follow-up owners; verify activity and hours.']]],
  ['09-18', [
    [3.0, 'Triage presentation feedback', 'Draft: grouped feedback into confirmed changes, open decisions and future enhancements; verify activity and hours.'],
    [3.0, 'Review Quick Capture and Import improvements', 'Draft: assessed changes arising from the walkthrough and cost-centre visits; verify activity and hours.'],
    [2.0, 'Prepare follow-up actions for stakeholders', 'Draft: listed clarification requests and next implementation steps; verify activity and hours.']]],
];

const metadata = [
  ['ORGANIZATION NAME : ', 'RIMS.COMPANY'],
  ['EXPORTED BY : ', 'Reaobaka Ntoagae'],
  ['PROJECT NAME : ', 'MDCSSL ENHANCEMENTS FINAL'],
  ['PROJECT ID : ', 'RI-10'],
  ['PREPARED ON : ', '21 September 2026'],
  ['TIME PERIOD : ', '01-09-2026 To 18-09-2026'],
  ['For Whom : ', 'Current User'],
];
sheet.getRange('A1:B7').values = metadata;
sheet.getRange('A1:A7').format.font = { name: 'Arial', bold: true, size: 10, color: '#17324D' };
sheet.getRange('B1:B7').format.font = { name: 'Arial', size: 10, color: '#17324D' };
sheet.getRange('B1:B7').format.columnWidth = 31;
sheet.getRange('A1:A7').format.columnWidth = 24;

const headers = ['User','Task/General/Issues','Task/Issues ID','Daily Log','Time Period','Billing Type','Approval Status','Notes','Added By','Date','Hours(For Calculation)','Rate Per Hour','Log User Mailid','Role','Type','Project Group','phase','Task List/Module','From - To','Timer Notes','Reject Reason'];
sheet.getRange('A9:U9').values = [headers];
sheet.getRange('A9:U9').format = { fill:'#123B63', font:{name:'Arial',size:10,bold:true,color:'#FFFFFF'}, rowHeight:30, verticalAlignment:'center', horizontalAlignment:'center' };

const rows = [];
for (const [mmdd, dayEntries] of entries) {
  const [month, day] = mmdd.split('-').map(Number);
  const date = new Date(Date.UTC(2026, month-1, day));
  for (const [hours, task, note] of dayEntries) {
    const hh = String(Math.floor(hours)).padStart(2,'0');
    const min = String(Math.round((hours%1)*60)).padStart(2,'0');
    rows.push(['Reaobaka Ntoagae',task,'-',`${hh}:${min}`,'-','Non Billable','Draft',note,'Reaobaka Ntoagae',date,hours,'0.0','reaobaka@rims.company','Administrator','general','Ungrouped Projects','None','None','-','-','-']);
  }
}
const last = 9 + rows.length;
sheet.getRange(`A10:U${last}`).values = rows;
sheet.getRange(`A10:U${last}`).format.font = { name:'Arial', size:10, color:'#1E293B' };
sheet.getRange(`A10:U${last}`).format.rowHeight = 25;
sheet.getRange(`J10:J${last}`).setNumberFormat('mm-dd-yyyy');
sheet.getRange(`K10:K${last}`).setNumberFormat('0.0');
sheet.getRange(`B10:B${last}`).format.columnWidth = 48;
sheet.getRange(`H10:H${last}`).format.columnWidth = 95;
sheet.getRange(`J10:J${last}`).format.columnWidth = 16;
sheet.getRange(`D10:D${last}`).format.columnWidth = 14;
sheet.getRange(`K10:K${last}`).format.columnWidth = 22;
sheet.getRange(`M10:M${last}`).format.columnWidth = 27;
sheet.getRange(`A10:A${last}`).format.columnWidth = 24;
for (const [col,width] of Object.entries({C:20,E:16,F:17,G:20,I:24,L:18,N:18,O:14,P:21,Q:14,R:22,S:16,T:19,U:18})) {
  sheet.getRange(`${col}9:${col}${last}`).format.columnWidth = width;
}
sheet.freezePanes.freezeRows(9);

const totalHours = rows.reduce((sum,row)=>sum+row[10],0);
const totalRow = last+1;
sheet.getRange(`C${totalRow}:D${totalRow}`).values = [['Total Log Hours',`${String(totalHours).padStart(2,'0')}:00`]];
sheet.getRange(`J${totalRow}:K${totalRow}`).values = [['Total Hours(For Calculation)',totalHours]];
sheet.getRange(`C${totalRow}:K${totalRow}`).format = { fill:'#E8F3FA', font:{name:'Arial',size:10,bold:true,color:'#123B63'} };
sheet.getRange(`K${totalRow}`).setNumberFormat('0.0');

workbook.recalculate();
const check = await workbook.inspect({kind:'table',range:'A9:K13',include:'values,formulas',tableMaxRows:5,tableMaxCols:11,maxChars:3500});
console.log(check.ndjson);
const errors = await workbook.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:20},summary:'formula error scan'});
console.log(errors.ndjson);
const preview = await workbook.render({sheetName:sheet.name,range:'A1:K15',scale:1,format:'png'});
await fs.writeFile(`${outputDir}preview.png`,new Uint8Array(await preview.arrayBuffer()));
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(filePath);
console.log(JSON.stringify({filePath,entries:rows.length,days:entries.length,totalHours}));
