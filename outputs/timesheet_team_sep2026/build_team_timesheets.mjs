import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const out = path.dirname(fileURLToPath(import.meta.url));
const headers = ['User','Task/General/Issues','Task/Issues ID','Daily Log','Time Period','Billing Type','Approval Status','Notes','Added By','Date','Hours(For Calculation)','Rate Per Hour','Log User Mailid','Role','Type','Project Group','phase','Task List/Module','From - To','Timer Notes','Reject Reason'];
const src = {
  early:'Project weekly report',
  middle:'RIMS weekly report',
  late:'RIMS updated weekly report',
};
// Reports give weekly summaries, not a reliable day-by-day ledger. Every
// time amount and most daily placements therefore remain explicitly draft.
const work = {
  Milisa: [
    [1,3,'Arrange Law Administration POS training','early'],
    [1,2,'Confirm training participants and POS access','early'],
    [2,3,'Prepare Law Administration POS walkthrough','early'],
    [2,2,'Review training setup and available POS functions','early'],
    [3,3,'Begin Law Administration POS training','early'],
    [3,2,'Identify POS defects blocking training','early'],
    [4,2,'Log POS defects with DevOps','early'],
    [4,2,'Document blocked training steps and follow-up needs','early'],
    [7,2,'Follow up on POS training defects','middle'],
    [7,2,'Prepare training restart after system corrections','middle'],
    [8,3,'Review Law Administration POS date-filter problem','middle'],
    [8,2,'Record prior-month viewing and printing issue','middle'],
    [9,3,'Review printed POS terms and conditions','middle'],
    [9,2,'Escalate incorrect deposit and delivery wording','middle'],
    [10,2,'Follow up with DevOps on training blockers','middle'],
    [10,2,'Check POS readiness for resumed training','middle'],
    [11,2,'Update Law Administration training plan','middle'],
    [11,2,'Provide POS issue status to the project team','middle'],
    [14,3,'Continue Law Administration POS training','late'],
    [14,2,'Guide users through practical POS workflow','late'],
    [15,3,'Demonstrate supporting-document uploads in POS','late'],
    [15,2,'Assist users with document-upload practice','late'],
    [16,3,'Test POS tasks requested by Darek','late'],
    [16,2,'Record POS task test observations','late'],
    [17,3,'Continue POS user guidance and training','late'],
    [17,2,'Answer user questions on POS workflow','late'],
    [18,2,'Review Law Administration training progress','late'],
    [18,2,'Prepare remaining POS training follow-ups','late'],
  ],
  Babalwa: [
    [1,2,'Engage Mr. Vuma on project matters','early'],
    [1,2,'Record matters deferred for a longer meeting','early'],
    [2,3,'Meet Alice on Debt Management issues','early'],
    [2,2,'Discuss proposed Debt Management solutions','early'],
    [3,2,'Discuss CTCC historical-data requirements','early'],
    [3,2,'Request CTCC data from RTMC for relevant financial years','early'],
    [4,2,'Discuss SIU referencing requirements with Darek','early'],
    [4,2,'Meet Mr. Bester on Traffic Administration Infringements','early'],
    [4,1,'Forward Infringements documents to Khutso for review','early'],
    [7,2,'Prepare monthly invoice and deliverable report','early'],
    [7,2,'Follow up on outstanding Development activities','early'],
    [8,5,'Visit Ehlanzeni cost centre for Quick Capture requirements','middle'],
    [8,1,'Record operational workflow observations','middle'],
    [9,5,'Visit two Gert Sibande cost centres','middle'],
    [9,1,'Record regional Quick Capture feedback','middle'],
    [10,2,'Meet CTCC team on Sensitive Transactions solution','middle'],
    [10,2,'Discuss proposed solution and testing readiness','middle'],
    [11,5,'Visit two Nkangala cost centres','middle'],
    [11,1,'Consolidate cost-centre requirements','middle'],
    [14,2,'Meet Mr. Vuma on project matters and next steps','late'],
    [14,2,'Meet Alice on outstanding project matters','late'],
    [15,3,'Continue CTCC Sensitive Transactions training','late'],
    [15,2,'Review CTCC concerns raised during training','late'],
    [16,2,'Escalate CTCC implementation concerns to Alice','late'],
    [16,2,'Coordinate planned CTCC solution demonstration','late'],
    [17,4,'Attend Accident Reports presentation in Ermelo','late'],
    [17,2,'Gather provincial and regional feedback','late'],
    [18,2,'Document CTCC questions and agreed next steps','late'],
    [18,2,'Consolidate Accident Reports presentation feedback','late'],
  ],
};

async function build(name) {
  const book = Workbook.create();
  const sheet = book.worksheets.add('MDCSSL ENHANCEMENTS -RI-10');
  sheet.showGridLines = false;
  const meta = [
    ['ORGANIZATION NAME : ','RIMS.COMPANY'],
    ['PREPARED FOR : ',name],
    ['PROJECT NAME : ','MDCSSL ENHANCEMENTS FINAL'],
    ['PROJECT ID : ','RI-10'],
    ['PREPARED ON : ','21 September 2026'],
    ['TIME PERIOD : ','01-09-2026 To 18-09-2026'],
    ['STATUS : ','Draft. Confirm dates, hours and Zoho user fields before import.'],
  ];
  sheet.getRange('A1:B7').values = meta;
  sheet.getRange('A1:A7').format.font = {name:'Arial',bold:true,size:10,color:'#17324D'};
  sheet.getRange('B1:B7').format.font = {name:'Arial',size:10,color:'#17324D'};
  sheet.getRange('A9:U9').values = [headers];
  sheet.getRange('A9:U9').format = {fill:'#123B63',font:{name:'Arial',size:10,bold:true,color:'#FFFFFF'},rowHeight:30,verticalAlignment:'center',horizontalAlignment:'center'};
  const rows = work[name].map(([day,hours,task,week])=>{
    const date = new Date(Date.UTC(2026,8,day));
    const duration = `${String(Math.floor(hours)).padStart(2,'0')}:${String(Math.round((hours%1)*60)).padStart(2,'0')}`;
    const note = `Estimated date and hours. Confirm with ${name}. Source: ${src[week]}.`;
    return [name,task,'-',duration,'-',null,'Draft',note,name,date,hours,null,null,null,'general','Ungrouped Projects','None','None','-','-','-'];
  });
  const end = 9+rows.length;
  sheet.getRange(`A10:U${end}`).values = rows;
  sheet.getRange(`A10:U${end}`).format.font = {name:'Arial',size:10,color:'#1E293B'};
  sheet.getRange(`A10:U${end}`).format.rowHeight = 25;
  sheet.getRange(`J10:J${end}`).setNumberFormat('mm-dd-yyyy');
  sheet.getRange(`K10:K${end}`).setNumberFormat('0.0');
  const widths={A:24,B:58,C:20,D:14,E:16,F:17,G:20,H:94,I:24,J:16,K:23,L:18,M:27,N:18,O:14,P:21,Q:14,R:22,S:16,T:19,U:18};
  for (const [col,width] of Object.entries(widths)) sheet.getRange(`${col}9:${col}${end}`).format.columnWidth=width;
  sheet.getRange('B1:B7').format.columnWidth=58;
  sheet.freezePanes.freezeRows(9);
  const total = rows.reduce((n,r)=>n+r[10],0);
  sheet.getRange(`C${end+1}:D${end+1}`).values=[['Total Log Hours',`${String(total).padStart(2,'0')}:00`]];
  sheet.getRange(`J${end+1}:K${end+1}`).values=[['Total Hours(For Calculation)',total]];
  sheet.getRange(`C${end+1}:K${end+1}`).format={fill:'#E8F3FA',font:{name:'Arial',size:10,bold:true,color:'#123B63'}};
  book.recalculate();
  const check=await book.inspect({kind:'table',range:'A9:K12',include:'values,formulas',tableMaxRows:4,tableMaxCols:11,maxChars:1800});
  console.log(name,check.ndjson);
  const errors=await book.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:20}});
  console.log(name,errors.ndjson);
  const preview=await book.render({sheetName:sheet.name,range:'A1:K15',scale:1,format:'png'});
  await fs.writeFile(path.join(out,`${name}_preview.png`),new Uint8Array(await preview.arrayBuffer()));
  const file=path.join(out,`${name}_September_2026_Zoho_Timesheet_Draft.xlsx`);
  const xlsx=await SpreadsheetFile.exportXlsx(book);
  await xlsx.save(file);
  console.log(JSON.stringify({name,file,rows:rows.length,totalHours:total}));
}

for (const name of ['Milisa','Babalwa']) await build(name);
