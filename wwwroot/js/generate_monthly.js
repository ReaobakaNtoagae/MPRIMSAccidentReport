'use strict';
const {
    Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
    AlignmentType, BorderStyle, WidthType, ShadingType, VerticalAlign,
    ImageRun
} = require('docx');
const fs = require('fs');
const path = require('path');
const { createGroupedBarChart, ROUTE_PALETTE } = require(path.join(__dirname, 'charts.js'));

const vm = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
const out = process.argv[3];

// ── Page ─────────────────────────────────────────────────────
const PW = 9638; 


// ── Colours ───────────────────────────────────────────────────
const NAVY = "003366";
const THEAD = "1F3864";
const LGRAY = "F2F2F2";
const WHITE = "FFFFFF";
const BLACK = "000000";

// ── Metric colour scheme (used in tables AND charts) ──────────
const MC = {
    CRASH: "2E5FA3", 
    FATAL: "000000",  
    SERIOUS: "C00000", 
    SLIGHT: "00B050",   
    DEFAULT: "000000"
};


function metricColor(label) {
    const u = String(label).toUpperCase();
    if (u.includes('CRASH')) return MC.CRASH;
    if (u.includes('FATAL')) return MC.FATAL;
    if (u.includes('SERIOUS')) return MC.SERIOUS;
    if (u.includes('SLIGHT')) return MC.SLIGHT;
    return MC.DEFAULT;
}

// ── Borders ───────────────────────────────────────────────────
const tb = { style: BorderStyle.SINGLE, size: 4, color: "BBBBBB" };
const borders = { top: tb, bottom: tb, left: tb, right: tb };
const CM = { top: 80, bottom: 80, left: 120, right: 120 };


function run(text, opts = {}) {
    return new TextRun({
        text: String(text ?? ''), font: opts.font || "Arial",
        size: opts.size || 20, bold: opts.bold || false,
        color: opts.color || BLACK
    });
}
function para(children, opts = {}) {
    const c = Array.isArray(children) ? children : [children];
    return new Paragraph({
        alignment: opts.align || AlignmentType.LEFT,
        spacing: { before: opts.before ?? 0, after: opts.after ?? 120 },
        indent: opts.indent ? { left: opts.indent } : undefined,
        children: c
    });
}
function heading(text) {
    return new Paragraph({
        spacing: { before: 200, after: 80 },
        children: [run(text, { bold: true, size: 22, color: NAVY })]
    });
}
function blank(s = 120) {
    return new Paragraph({ spacing: { before: 0, after: s }, children: [run('')] });
}
function variation(prev, curr) {
    if (!prev || prev === 0) return curr === 0 ? "0" : "N/A";
    const pct = ((curr - prev) / prev * 100).toFixed(2);
    return (curr >= prev ? "+" : "") + pct;
}
function chartPara(buf, w = 520, h = 260) {
    return new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 80, after: 120 },
        children: [new ImageRun({ data: buf, transformation: { width: w, height: h }, type: "png" })]
    });
}


function hdrCell(text, width, span) {
    return new TableCell({
        width: { size: width, type: WidthType.DXA },
        columnSpan: span || 1, borders,
        shading: { fill: THEAD, type: ShadingType.CLEAR }, margins: CM,
        verticalAlign: VerticalAlign.CENTER,
        children: [para(run(text, { bold: true, size: 18, color: WHITE }),
            { align: AlignmentType.CENTER, after: 0 })]
    });
}
function dataCell(text, width, opts) {
    opts = opts || {};
    return new TableCell({
        width: { size: width, type: WidthType.DXA }, borders,
        shading: { fill: opts.fill || WHITE, type: ShadingType.CLEAR }, margins: CM,
        children: [para(run(String(text), { bold: opts.bold || false, size: 18, color: opts.color || BLACK }),
            { align: AlignmentType.CENTER, after: 0 })]
    });
}
function labelCell(text, width, color) {
    return new TableCell({
        width: { size: width, type: WidthType.DXA }, borders,
        shading: { fill: LGRAY, type: ShadingType.CLEAR }, margins: CM,
        children: [para(run(text, { bold: true, size: 18, color: color || metricColor(text) }), { after: 0 })]
    });
}

function compTable(rows) {
    const C = [Math.floor(PW * .35), Math.floor(PW * .20),
    Math.floor(PW * .20), PW - Math.floor(PW * .35) - Math.floor(PW * .40)];
    return new Table({
        width: { size: PW, type: WidthType.DXA }, columnWidths: C,
        rows: [
            new TableRow({
                children: [hdrCell('', C[0]), hdrCell(String(vm.PriorYear), C[1]),
                hdrCell(String(vm.CurrentYear), C[2]), hdrCell('VARIATION', C[3])]
            }),
            ...rows.map(r => {
                const mc = metricColor(r.label);
                return new TableRow({
                    children: [
                        labelCell(r.label, C[0], mc),
                        dataCell(r.prev ?? 0, C[1], { fill: "#F0F4FF", color: mc }),
                        dataCell(r.curr ?? 0, C[2], { bold: true, color: mc }),
                        dataCell(variation(r.prev, r.curr), C[3],
                            { bold: true, color: mc, fill: r.curr > r.prev ? "#FFE8E8" : "#E8FFE8" })
                    ]
                });
            })
        ]
    });
}

function distTable(rows) {
    const dists = vm.Districts || [];
    const lW = 1400;
    const dW = Math.floor((PW - lW) / (dists.length || 4));
    const cW = Math.floor(dW / 2);
    const allW = [lW, ...dists.flatMap(() => [cW, cW])];
    return new Table({
        width: { size: PW, type: WidthType.DXA }, columnWidths: allW,
        rows: [
            new TableRow({
                children: [hdrCell('', lW),
                ...dists.map(d => new TableCell({
                    width: { size: dW, type: WidthType.DXA }, columnSpan: 2, borders,
                    shading: { fill: THEAD, type: ShadingType.CLEAR }, margins: CM,
                    verticalAlign: VerticalAlign.CENTER,
                    children: [para(run(d.Name, { bold: true, size: 18, color: WHITE }),
                        { align: AlignmentType.CENTER, after: 0 })]
                }))]
            }),
            new TableRow({
                children: [hdrCell('', lW),
                ...dists.flatMap(() => [hdrCell(String(vm.PriorYear), cW), hdrCell(String(vm.CurrentYear), cW)])]
            }),
            ...rows.map(r => {
                const mc = metricColor(r.label);
                return new TableRow({
                    children: [
                        labelCell(r.label, lW, mc),
                        ...dists.flatMap(d => [
                            dataCell(r.getPrev(d) ?? 0, cW, { fill: "#F0F4FF", color: mc }),
                            dataCell(r.getCurr(d) ?? 0, cW, { bold: true, color: mc })
                        ])
                    ]
                });
            })
        ]
    });
}

function routeTable(routes) {
    const C = [Math.floor(PW * .35), Math.floor(PW * .16), Math.floor(PW * .16),
    Math.floor(PW * .16), PW - Math.floor(PW * .35) - Math.floor(PW * .16) * 3];
    return new Table({
        width: { size: PW, type: WidthType.DXA }, columnWidths: C,
        rows: [
            new TableRow({
                children: [
                    hdrCell('', C[0]), hdrCell('CRASHES ' + vm.PriorYear, C[1]),
                    hdrCell('CRASHES ' + vm.CurrentYear, C[2]),
                    hdrCell('FATALITIES ' + vm.PriorYear, C[3]),
                    hdrCell('FATALITIES ' + vm.CurrentYear, C[4])]
            }),
            ...routes.map(r => new TableRow({
                children: [
                    labelCell(r.Route, C[0], BLACK),
                    dataCell(r.CrashesPrev ?? 0, C[1], { color: MC.CRASH }),
                    dataCell(r.CrashesCurr ?? 0, C[2], { color: MC.CRASH, bold: true }),
                    dataCell(r.FatalPrev ?? 0, C[3], { color: MC.FATAL }),
                    dataCell(r.FatalCurr ?? 0, C[4], { color: MC.FATAL, bold: true })]
            }))
        ]
    });
}

function fiveYearTable() {
    const hist = vm.FiveYearHistory || [];
    const years = hist.map(h => h.Year);           
    const lW = Math.floor(PW * .18);
    const cW = Math.floor((PW - lW - Math.floor(PW * .15)) / (years.length || 1));
    const aW = PW - lW - cW * (years.length || 1);
    const avg = key => (hist.reduce((s, h) => s + (h[key] || 0), 0) / (hist.length || 1)).toFixed(1);
    return new Table({
        width: { size: PW, type: WidthType.DXA },
        columnWidths: [lW, ...years.map(() => cW), aW],
        rows: [
            new TableRow({ children: [hdrCell('', lW), ...years.map(y => hdrCell(String(y), cW)), hdrCell('AVG/MTH', aW)] }),
            ...['Crashes', 'Fatalities'].map(key => {
                const mc = key === 'Crashes' ? MC.CRASH : MC.FATAL;
                return new TableRow({
                    children: [
                        labelCell(key.toUpperCase(), lW, mc),
                        ...hist.map(h => dataCell(h[key] ?? 0, cW, { color: mc })),
                        dataCell(avg(key), aW, { bold: true, color: mc })
                    ]
                });
            })
        ]
    });
}


const p = vm.Provincial || {};
const c = p.Current || {};
const pr = p.Prior || {};
const cy = vm.CurrentYear;
const py = vm.PriorYear;
const hist = vm.FiveYearHistory || [];

function numWords(n) {
    const ones = ['', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine',
        'ten', 'eleven', 'twelve', 'thirteen', 'fourteen', 'fifteen', 'sixteen',
        'seventeen', 'eighteen', 'nineteen'];
    const tens = ['', '', 'twenty', 'thirty', 'forty', 'fifty', 'sixty', 'seventy', 'eighty', 'ninety'];
    if (n === 0) return 'zero';
    if (n < 20) return ones[n];
    if (n < 100) return tens[Math.floor(n / 10)] + (n % 10 ? ' ' + ones[n % 10] : '');
    if (n < 1000) return ones[Math.floor(n / 100)] + ' hundred' + (n % 100 ? ' and ' + numWords(n % 100) : '');
    return n.toString();
}
const W = n => numWords(n).toUpperCase();


function chg(prev, curr) {
    return curr >= prev ? 'increased' : 'decreased';
}

function pctChg(prev, curr) {
    if (!prev || prev === 0) return '';
    const v = Math.abs(((curr - prev) / prev) * 100).toFixed(1);
    return ' by ' + v + '%';
}


function changedList(items) {
    const up = items.filter(i => i.curr >= i.prev).map(i => i.label.toLowerCase());
    const down = items.filter(i => i.curr < i.prev).map(i => i.label.toLowerCase());
    const parts = [];
    if (up.length > 0) parts.push(joinList(up) + ' increased');
    if (down.length > 0) parts.push(joinList(down) + ' decreased');
    return parts.join(' while ');
}

function joinList(arr) {
    if (arr.length === 0) return '';
    if (arr.length === 1) return arr[0];
    return arr.slice(0, -1).join(', ') + ' and ' + arr[arr.length - 1];
}

function figNarrative(text) {
    return para([run(text, { size: 20 })], { after: 80 });
}


const CW = 580, CH = 300;


const fig1 = createGroupedBarChart({
    title: 'Provincial Crashes: ' + (vm.MonthYear || ''),
    labels: [String(py), String(cy)],
    datasets: [
        { label: 'CRASHES', data: [pr.Crashes || 0, c.Crashes || 0] },
        { label: 'FATALITIES', data: [pr.Fatalities || 0, c.Fatalities || 0] },
        { label: 'SERIOUS', data: [pr.Serious || 0, c.Serious || 0] },
        { label: 'SLIGHT', data: [pr.Slight || 0, c.Slight || 0] }
    ],
    legendPosition: 'right', width: CW, height: CH
});


const fig2a = createGroupedBarChart({
    title: 'Total Crashes: ' + (vm.MonthName || ''),
    labels: hist.map(h => String(h.Year)),
    datasets: [
        { label: 'CRASHES', data: hist.map(h => h.Crashes || 0) }
    ],
    legendPosition: 'right', width: CW, height: CH
});


const fig2b = createGroupedBarChart({
    title: 'Fatalities: ' + (vm.MonthName || ''),
    labels: hist.map(h => String(h.Year)),
    datasets: [
        { label: 'FATALITIES', data: hist.map(h => h.Fatalities || 0) }
    ],
    legendPosition: 'right', width: CW, height: CH
});


const fig3 = createGroupedBarChart({
    title: 'Fatal Victims by Category',
    labels: [String(py), String(cy)],
    datasets: [
        { label: 'DRIVERS', data: [pr.FatalDrivers || 0, c.FatalDrivers || 0] },
        { label: 'PASSENGERS', data: [pr.FatalPassengers || 0, c.FatalPassengers || 0] },
        { label: 'PEDESTRIANS', data: [pr.FatalPedestrians || 0, c.FatalPedestrians || 0] },
        { label: 'CYCLISTS', data: [pr.FatalCyclists || 0, c.FatalCyclists || 0] }
    ],
    legendPosition: 'right', width: CW, height: CH
});


const fig4 = createGroupedBarChart({
    title: 'Serious Injuries by Category',
    labels: [String(py), String(cy)],
    datasets: [
        { label: 'DRIVERS', data: [pr.SeriousDrivers || 0, c.SeriousDrivers || 0] },
        { label: 'PASSENGERS', data: [pr.SeriousPassengers || 0, c.SeriousPassengers || 0] },
        { label: 'PEDESTRIANS', data: [pr.SeriousPedestrians || 0, c.SeriousPedestrians || 0] },
        { label: 'CYCLISTS', data: [pr.SeriousCyclists || 0, c.SeriousCyclists || 0] }
    ],
    legendPosition: 'right', width: CW, height: CH
});


const fig5 = createGroupedBarChart({
    title: 'Slight Injuries by Category',
    labels: [String(py), String(cy)],
    datasets: [
        { label: 'DRIVERS', data: [pr.SlightDrivers || 0, c.SlightDrivers || 0] },
        { label: 'PASSENGERS', data: [pr.SlightPassengers || 0, c.SlightPassengers || 0] },
        { label: 'PEDESTRIANS', data: [pr.SlightPedestrians || 0, c.SlightPedestrians || 0] },
        { label: 'CYCLISTS', data: [pr.SlightCyclists || 0, c.SlightCyclists || 0] }
    ],
    legendPosition: 'right', width: CW, height: CH
});


const pRoutes = (vm.ProvincialRoutes || []).slice(0, 6);
const fig6 = pRoutes.length > 0 ? createGroupedBarChart({
    title: 'Provincial Problematic Routes',
    labels: ['CRASHES\n' + py, 'CRASHES\n' + cy, 'FAT\n' + py, 'FAT\n' + cy],
    datasets: pRoutes.map(r => ({
        label: r.Route,
        data: [r.CrashesPrev || 0, r.CrashesCurr || 0, r.FatalPrev || 0, r.FatalCurr || 0]
    })),
    legendPosition: 'right', palette: ROUTE_PALETTE, width: CW, height: CH
}) : null;


const DEFAULT_SLOTS = [
    { Slot: '06H00 - 14H00', CrashesPrev: 0, CrashesCurr: 0, FatalPrev: 0, FatalCurr: 0 },
    { Slot: '14H00 - 22H00', CrashesPrev: 0, CrashesCurr: 0, FatalPrev: 0, FatalCurr: 0 },
    { Slot: '22H00 - 06H00', CrashesPrev: 0, CrashesCurr: 0, FatalPrev: 0, FatalCurr: 0 }
];
const tSlots = (vm.TimeSlots && vm.TimeSlots.length > 0) ? vm.TimeSlots : DEFAULT_SLOTS;
const fig7 = tSlots.length > 0 ? createGroupedBarChart({
    title: 'Crashes by Time of Day',
    labels: tSlots.map(t => t.Slot),
    datasets: [
        { label: String(py), data: tSlots.map(t => t.CrashesPrev || 0) },
        { label: String(cy), data: tSlots.map(t => t.CrashesCurr || 0) }
    ],
    legendPosition: 'right', width: CW, height: CH
}) : null;


const dowProv = (vm.DaysOfWeek || {}).Provincial || [];
const fig8 = dowProv.length > 0 ? createGroupedBarChart({
    title: 'Crashes by Day of Week (Province)',
    labels: dowProv.map(d => d.Day.slice(0, 3)),
    datasets: [
        { label: String(py), data: dowProv.map(d => d.CrashesPrev || 0) },
        { label: String(cy), data: dowProv.map(d => d.CrashesCurr || 0) }
    ],
    legendPosition: 'right', width: CW, height: CH
}) : null;


const crashes = c.Crashes || 0, fatals = c.Fatalities || 0,
    serious = c.Serious || 0, slight = c.Slight || 0;
const days = vm.DaysInPeriod || vm.DaysInMonth || 30;

const children = [];

// ── Header ────────────────────────────────────────────────
children.push(
    para([run('Ref: ' + (vm.RefNumber || '16/9/4'), { size: 18 })], { align: AlignmentType.RIGHT, after: 0 }),
    para([run('Enq: ' + (vm.EnquiryName || 'M C Mdhluli'), { size: 18 })], { align: AlignmentType.RIGHT, after: 0 }),
    para([run('Tel: ' + (vm.EnquiryTel || '082 802 6966'), { size: 18 })], { align: AlignmentType.RIGHT, after: 160 }),
    para([run('MEMORANDUM', { bold: true, size: 28, color: NAVY })], { align: AlignmentType.CENTER, after: 200 })
);
[
    ['TO', vm.ToName || 'MR P NGOMANE (MPL)'],
    ['', vm.ToTitle || 'MEMBER OF THE EXECUTIVE COUNCIL'],
    ['FROM', vm.FromName || 'MR W MTHOMBOTHI'],
    ['', vm.FromTitle || 'HEAD OF DEPARTMENT'],
    ['DATE', vm.ReportDate || ''],
    ['SUBJECT', 'REPORT ON CRASHES, FATALITIES, SERIOUS AND SLIGHT INJURIES: ' +
        vm.PeriodFrom + ' TO ' + vm.PeriodTo +
        ' AS COMPARED WITH THE SAME PERIOD THE PREVIOUS YEAR']
].forEach(([lbl, val]) => {
    if (!lbl) { children.push(para([run(val, { bold: true, size: 20 })], { indent: 1440, after: 40 })); return; }
    children.push(para([run(lbl.padEnd(10), { bold: true, size: 20 }), run(':   ', { bold: true, size: 20 }), run(val, { bold: true, size: 20 })], { after: 40 }));
});
children.push(blank(160));


children.push(
    heading('PURPOSE'),
    para([run('To inform the Member of the Executive Council, of crashes and fatalities recorded in the province for the period ' + vm.PeriodFrom + ' to ' + vm.PeriodTo + ', as compared with the same period the previous year.', { size: 20 })], { after: 120 }),
    blank()
);


children.push(
    heading('DISCUSSION'),
    para([run('During ' + vm.MonthYear + ' ' + W(crashes) + ' (' + crashes + ') road crashes took place, resulting in ' + W(fatals) + ' (' + fatals + ') fatalities, ' + W(serious) + ' (' + serious + ') serious injuries and ' + W(slight) + ' (' + slight + ') slight injuries.', { size: 20 })], { after: 120 }),
    blank()
);


children.push(
    heading('PROVINCIAL CRASHES: MONTH TO MONTH COMPARISON:'),
    para([run(vm.PeriodFrom + ' \u2013 ' + vm.PeriodTo, { bold: true, size: 20 })], { after: 80 }),
    para([run('The following represent the number of crashes, fatalities and injuries recorded.', { size: 20 })], { after: 80 }),
    compTable([
        { label: 'CRASHES', prev: pr.Crashes, curr: c.Crashes },
        { label: 'FATALITIES', prev: pr.Fatalities, curr: c.Fatalities },
        { label: 'SERIOUS INJURIES', prev: pr.Serious, curr: c.Serious },
        { label: 'SLIGHT INJURIES', prev: pr.Slight, curr: c.Slight }
    ]),
    blank(80),
    para([run('FIGURE 1', { bold: true, size: 18 })], { align: AlignmentType.CENTER, after: 40 }),
    chartPara(fig1),
    figNarrative('Figure 1 represents ' + changedList([
        { label: 'Crashes', prev: pr.Crashes, curr: c.Crashes },
        { label: 'Fatalities', prev: pr.Fatalities, curr: c.Fatalities },
        { label: 'Serious injuries', prev: pr.Serious, curr: c.Serious },
        { label: 'Slight injuries', prev: pr.Slight, curr: c.Slight }
    ]) + ' as compared with the same period the previous year.'),
    blank()
);


children.push(para([run('AVERAGE PER DAY', { bold: true, size: 20 })], { after: 80 }));
{
    const C = [Math.floor(PW / 4), Math.floor(PW / 4), Math.floor(PW / 4), PW - Math.floor(PW / 4) * 3];
    children.push(
        new Table({
            width: { size: PW, type: WidthType.DXA }, columnWidths: C, rows: [
                new TableRow({
                    children: [
                        hdrCell('CRASHES', C[0]), hdrCell('FATALITIES', C[1]),
                        hdrCell('SERIOUS INJURIES', C[2]), hdrCell('SLIGHT INJURIES', C[3])]
                }),
                new TableRow({
                    children: [
                        dataCell((crashes / days).toFixed(1), C[0], { bold: true, color: MC.CRASH }),
                        dataCell((fatals / days).toFixed(1), C[1], { bold: true, color: MC.FATAL }),
                        dataCell((serious / days).toFixed(1), C[2], { bold: true, color: MC.SERIOUS }),
                        dataCell((slight / days).toFixed(1), C[3], { bold: true, color: MC.SLIGHT })
                    ]
                })
            ]
        }),
        blank()
    );
}

// ── 5-year history + Figure 2 ─────────────────────────────
if (hist.length > 0) {
    const avgC = (hist.reduce((s, h) => s + (h.Crashes || 0), 0) / hist.length).toFixed(1);
    const avgF = (hist.reduce((s, h) => s + (h.Fatalities || 0), 0) / hist.length).toFixed(1);
    children.push(
        para([run('FIGURE 2: CRASHES AND FATALITIES: ' + (vm.MonthName || ''), { bold: true, size: 20 })], { after: 80 }),
        fiveYearTable(),
        blank(80),
        para([run('FIGURE 2A: TOTAL CRASHES – ' + (vm.MonthName || ''), { bold: true, size: 18 })], { align: AlignmentType.CENTER, after: 40 }),
        chartPara(fig2a),
        blank(40),
        para([run('FIGURE 2B: FATALITIES – ' + (vm.MonthName || ''), { bold: true, size: 18 })], { align: AlignmentType.CENTER, after: 40 }),
        chartPara(fig2b),
        figNarrative(
            'Figure 2A and 2B reflect a 5-year trend for ' + (vm.MonthName || 'this period') + '. ' +
            'The average is ' + avgC + ' crashes and ' + avgF + ' fatalities per ' +
            (vm.MonthName || 'month') + ' over the past five years. ' +
            'Crashes have ' + chg(hist[0] && hist[0].Crashes || 0, hist[hist.length - 1] && hist[hist.length - 1].Crashes || 0) +
            ' and fatalities have ' + chg(hist[0] && hist[0].Fatalities || 0, hist[hist.length - 1] && hist[hist.length - 1].Fatalities || 0) +
            ' over this period.'
        ),
        blank()
    );
}


children.push(
    para([run('CATEGORIES OF VICTIMS', { bold: true, size: 20 })], { after: 80 }),
    compTable([
        { label: 'DRIVERS', prev: pr.FatalDrivers, curr: c.FatalDrivers },
        { label: 'PASSENGERS', prev: pr.FatalPassengers, curr: c.FatalPassengers },
        { label: 'PEDESTRIANS', prev: pr.FatalPedestrians, curr: c.FatalPedestrians },
        { label: 'CYCLISTS', prev: pr.FatalCyclists, curr: c.FatalCyclists }
    ]),
    blank(80),
    para([run('FIGURE 3: GRAPHIC REPRESENTATION OF THE ABOVE', { bold: true, size: 18 })], { align: AlignmentType.CENTER, after: 40 }),
    chartPara(fig3),
    figNarrative('Figure 3 represents ' + changedList([
        { label: 'Driver fatalities', prev: pr.FatalDrivers, curr: c.FatalDrivers },
        { label: 'Passenger fatalities', prev: pr.FatalPassengers, curr: c.FatalPassengers },
        { label: 'Pedestrian fatalities', prev: pr.FatalPedestrians, curr: c.FatalPedestrians },
        { label: 'Cyclist fatalities', prev: pr.FatalCyclists, curr: c.FatalCyclists }
    ]) + ' as compared with the same period the previous year.'),
    blank()
);


children.push(
    para([run('SERIOUS INJURIES', { bold: true, size: 20 })], { after: 80 }),
    compTable([
        { label: 'DRIVERS', prev: pr.SeriousDrivers, curr: c.SeriousDrivers },
        { label: 'PASSENGERS', prev: pr.SeriousPassengers, curr: c.SeriousPassengers },
        { label: 'PEDESTRIANS', prev: pr.SeriousPedestrians, curr: c.SeriousPedestrians },
        { label: 'CYCLISTS', prev: pr.SeriousCyclists, curr: c.SeriousCyclists }
    ]),
    blank(80),
    para([run('FIGURE 4: GRAPHIC REPRESENTATION OF THE ABOVE', { bold: true, size: 18 })], { align: AlignmentType.CENTER, after: 40 }),
    chartPara(fig4),
    figNarrative('Figure 4 shows serious injuries for ' + changedList([
        { label: 'drivers', prev: pr.SeriousDrivers, curr: c.SeriousDrivers },
        { label: 'passengers', prev: pr.SeriousPassengers, curr: c.SeriousPassengers },
        { label: 'pedestrians', prev: pr.SeriousPedestrians, curr: c.SeriousPedestrians },
        { label: 'cyclists', prev: pr.SeriousCyclists, curr: c.SeriousCyclists }
    ]) + ' as compared with the same period the previous year.'),
    blank()
);


children.push(
    para([run('SLIGHT INJURIES', { bold: true, size: 20 })], { after: 80 }),
    compTable([
        { label: 'DRIVERS', prev: pr.SlightDrivers, curr: c.SlightDrivers },
        { label: 'PASSENGERS', prev: pr.SlightPassengers, curr: c.SlightPassengers },
        { label: 'PEDESTRIANS', prev: pr.SlightPedestrians, curr: c.SlightPedestrians },
        { label: 'CYCLISTS', prev: pr.SlightCyclists, curr: c.SlightCyclists }
    ]),
    blank(80),
    para([run('FIGURE 5: GRAPHIC REPRESENTATION OF THE ABOVE', { bold: true, size: 18 })], { align: AlignmentType.CENTER, after: 40 }),
    chartPara(fig5),
    figNarrative('Figure 5 shows slight injuries for ' + changedList([
        { label: 'drivers', prev: pr.SlightDrivers, curr: c.SlightDrivers },
        { label: 'passengers', prev: pr.SlightPassengers, curr: c.SlightPassengers },
        { label: 'pedestrians', prev: pr.SlightPedestrians, curr: c.SlightPedestrians },
        { label: 'cyclists', prev: pr.SlightCyclists, curr: c.SlightCyclists }
    ]) + ' as compared with the same period the previous year.'),
    blank()
);


children.push(
    para([run('COMPARISON BY DISTRICT: ' + vm.PeriodFrom + ' \u2013 ' + vm.PeriodTo + ' AS COMPARED WITH THE SAME PERIOD THE PREVIOUS YEAR.', { bold: true, size: 20 })], { after: 80 }),
    distTable([
        { label: 'CRASHES', getPrev: d => d.Prior?.Crashes, getCurr: d => d.Current?.Crashes },
        { label: 'FATAL', getPrev: d => d.Prior?.Fatalities, getCurr: d => d.Current?.Fatalities },
        { label: 'SERIOUS', getPrev: d => d.Prior?.Serious, getCurr: d => d.Current?.Serious },
        { label: 'SLIGHT', getPrev: d => d.Prior?.Slight, getCurr: d => d.Current?.Slight }
    ]),
    blank()
);


children.push(
    para([run('CATEGORIES OF VICTIMS PER DISTRICT', { bold: true, size: 20 })], { after: 80 }),
    distTable([
        { label: 'DRIVER', getPrev: d => d.Prior?.FatalDrivers, getCurr: d => d.Current?.FatalDrivers },
        { label: 'PASSENGER', getPrev: d => d.Prior?.FatalPassengers, getCurr: d => d.Current?.FatalPassengers },
        { label: 'PEDESTRIANS', getPrev: d => d.Prior?.FatalPedestrians, getCurr: d => d.Current?.FatalPedestrians },
        { label: 'CYCLISTS', getPrev: d => d.Prior?.FatalCyclists, getCurr: d => d.Current?.FatalCyclists }
    ]),
    blank(80),
    para([run('SERIOUS INJURIES', { bold: true, size: 20 })], { after: 80 }),
    distTable([
        { label: 'DRIVER', getPrev: d => d.Prior?.SeriousDrivers, getCurr: d => d.Current?.SeriousDrivers },
        { label: 'PASSENGER', getPrev: d => d.Prior?.SeriousPassengers, getCurr: d => d.Current?.SeriousPassengers },
        { label: 'PEDESTRIANS', getPrev: d => d.Prior?.SeriousPedestrians, getCurr: d => d.Current?.SeriousPedestrians },
        { label: 'CYCLISTS', getPrev: d => d.Prior?.SeriousCyclists, getCurr: d => d.Current?.SeriousCyclists }
    ]),
    blank(80),
    para([run('SLIGHT INJURIES', { bold: true, size: 20 })], { after: 80 }),
    distTable([
        { label: 'DRIVER', getPrev: d => d.Prior?.SlightDrivers, getCurr: d => d.Current?.SlightDrivers },
        { label: 'PASSENGER', getPrev: d => d.Prior?.SlightPassengers, getCurr: d => d.Current?.SlightPassengers },
        { label: 'PEDESTRIANS', getPrev: d => d.Prior?.SlightPedestrians, getCurr: d => d.Current?.SlightPedestrians },
        { label: 'CYCLISTS', getPrev: d => d.Prior?.SlightCyclists, getCurr: d => d.Current?.SlightCyclists }
    ]),
    blank()
);


if (vm.ProvincialRoutes && vm.ProvincialRoutes.length > 0) {
    const rf = c.Fatalities || 1;
    const rft = pRoutes.reduce((s, r) => s + (r.FatalCurr || 0), 0);
    children.push(
        heading('PROVINCIAL PROBLEMATIC ROUTES'),
        para([run('The following priority routes were identified in the province. The problematic routes show a ' + ((rft / rf) * 100).toFixed(1) + '% contribution of fatalities during this period.', { size: 20 })], { after: 80 }),
        routeTable(vm.ProvincialRoutes),
        blank(80)
    );
    if (fig6) {
        children.push(
            para([run('FIGURE 6: GRAPHIC REPRESENTATION OF THE ABOVE', { bold: true, size: 18 })], { align: AlignmentType.CENTER, after: 40 }),
            chartPara(fig6),
            blank()
        );
    }
}


if (vm.Districts) {
    children.push(heading('REGIONAL PROBLEMATIC ROUTES'));
    (vm.Districts || []).forEach(d => {
        if (!d.Routes || !d.Routes.length) return;
        const df = d.Current?.Fatalities || 1;
        const dft = d.Routes.reduce((s, r) => s + (r.FatalCurr || 0), 0);
        children.push(
            para([run(d.Name + ' DISTRICT', { bold: true, size: 20 })], { after: 80 }),
            routeTable(d.Routes),
            para([run('The problematic routes for ' + d.Name + ' shows a ' + ((dft / df) * 100).toFixed(0) + '% contribution of fatalities during this period.', { size: 20 })], { after: 80 }),
            blank()
        );
    });
}


if (vm.CrashTypes && vm.CrashTypes.length > 0) {
    children.push(heading('PROVINCIAL CRASHES TYPES'));
    const C = [Math.floor(PW * .32), Math.floor(PW * .17), Math.floor(PW * .17),
    Math.floor(PW * .17), PW - Math.floor(PW * .32) - Math.floor(PW * .17) * 3];
    children.push(
        new Table({
            width: { size: PW, type: WidthType.DXA }, columnWidths: C, rows: [
                new TableRow({
                    children: [hdrCell('TYPES', C[0]), hdrCell('CRASHES ' + py, C[1]),
                    hdrCell('CRASHES ' + cy, C[2]), hdrCell('FATALITIES ' + py, C[3]), hdrCell('FATALITIES ' + cy, C[4])]
                }),
                ...(vm.CrashTypes || []).map(ct => new TableRow({
                    children: [
                        labelCell(ct.Type, C[0], BLACK),
                        dataCell(ct.CrashesPrev ?? 0, C[1], { color: MC.CRASH }),
                        dataCell(ct.CrashesCurr ?? 0, C[2], { color: MC.CRASH, bold: true }),
                        dataCell(ct.FatalPrev ?? 0, C[3], { color: MC.FATAL }),
                        dataCell(ct.FatalCurr ?? 0, C[4], { color: MC.FATAL, bold: true })]
                }))
            ]
        }),
        blank()
    );
}


if (vm.VehicleCategories && vm.VehicleCategories.length > 0) {
    children.push(
        heading('PROVINCIAL VEHICLE CATEGORIES'),
        para([run("A majority of vehicles involved in crashes are Sedans, LDV and Taxi's.", { size: 20 })], { after: 80 })
    );
    const C = [Math.floor(PW * .32), Math.floor(PW * .17), Math.floor(PW * .17),
    Math.floor(PW * .17), PW - Math.floor(PW * .32) - Math.floor(PW * .17) * 3];
    children.push(
        new Table({
            width: { size: PW, type: WidthType.DXA }, columnWidths: C, rows: [
                new TableRow({
                    children: [hdrCell('VEHICLE CATEGORIES', C[0]), hdrCell('CRASHES ' + py, C[1]),
                    hdrCell('CRASHES ' + cy, C[2]), hdrCell('FATALITIES ' + py, C[3]), hdrCell('FATALITIES ' + cy, C[4])]
                }),
                ...(vm.VehicleCategories || []).map(vc => new TableRow({
                    children: [
                        labelCell(vc.Category, C[0], BLACK),
                        dataCell(vc.CrashesPrev ?? 0, C[1], { color: MC.CRASH }),
                        dataCell(vc.CrashesCurr ?? 0, C[2], { color: MC.CRASH, bold: true }),
                        dataCell(vc.FatalPrev ?? 0, C[3], { color: MC.FATAL }),
                        dataCell(vc.FatalCurr ?? 0, C[4], { color: MC.FATAL, bold: true })]
                }))
            ]
        }),
        blank()
    );
}


{
   
    const tW = Math.floor(PW * .30);
    const cW2 = Math.floor((PW - tW) / 4);
    const c4 = [tW, cW2, cW2, cW2, PW - tW - cW2 * 3];

    const timeTable = new Table({
        width: { size: PW, type: WidthType.DXA }, columnWidths: c4,
        rows: [
            
            new TableRow({
                children: [
                    hdrCell('TIME', c4[0]),
                    new TableCell({
                        width: { size: c4[1] + c4[2], type: WidthType.DXA }, columnSpan: 2, borders,
                        shading: { fill: THEAD, type: ShadingType.CLEAR }, margins: CM,
                        verticalAlign: VerticalAlign.CENTER,
                        children: [para(run('CRASHES', { bold: true, size: 18, color: WHITE }), { align: AlignmentType.CENTER, after: 0 })]
                    }),
                    new TableCell({
                        width: { size: c4[3] + c4[4], type: WidthType.DXA }, columnSpan: 2, borders,
                        shading: { fill: THEAD, type: ShadingType.CLEAR }, margins: CM,
                        verticalAlign: VerticalAlign.CENTER,
                        children: [para(run('FATALITIES', { bold: true, size: 18, color: WHITE }), { align: AlignmentType.CENTER, after: 0 })]
                    })
                ]
            }),
            // Header row 2: (empty) | prior year | curr year | prior year | curr year
            new TableRow({
                children: [
                    hdrCell('', c4[0]),
                    hdrCell(String(py), c4[1]),
                    hdrCell(String(cy), c4[2]),
                    hdrCell(String(py), c4[3]),
                    hdrCell(String(cy), c4[4])
                ]
            }),
            // Data rows
            ...tSlots.map(t => new TableRow({
                children: [
                    labelCell(t.Slot, c4[0], BLACK),
                    dataCell(t.CrashesPrev || 0, c4[1], { fill: '#F0F4FF', color: MC.CRASH }),
                    dataCell(t.CrashesCurr || 0, c4[2], { bold: true, color: MC.CRASH }),
                    dataCell(t.FatalPrev || 0, c4[3], { fill: '#F0F4FF', color: MC.FATAL }),
                    dataCell(t.FatalCurr || 0, c4[4], { bold: true, color: MC.FATAL })
                ]
            }))
        ]
    });

    // Narrative: contribution of 14h00–06h00 fatalities
    const fatal1406 = tSlots.filter(t => t.Slot !== '06H00 - 14H00').reduce((s, t) => s + (t.FatalCurr || 0), 0);
    const fatalTotal = tSlots.reduce((s, t) => s + (t.FatalCurr || 0), 0);
    const contribPct = fatalTotal > 0 ? ((fatal1406 / fatalTotal) * 100).toFixed(0) : 0;
    const prevalentNarrative = para([run(
        'The prevalent times between 14h00 until 06h00 shows a ' + contribPct +
        ' % contribution of fatalities during this period.',
        { size: 20 })], { after: 80 });
    children.push(
        heading('PROVINCIAL PREVALENT TIMES'),
        para([run('Fatalities are mostly prevalent between 14h00 to 06h00. The table that follows indicates the times and number of crashes in the Province and all Districts respectively.', { size: 20 })], { after: 80 }),
        timeTable,
        blank(80),
        prevalentNarrative
    );
    if (fig7) {
        const peakSlot = tSlots.reduce((a, b) => (b.FatalCurr || 0) > (a.FatalCurr || 0) ? b : a, tSlots[0]);
        children.push(
            chartPara(fig7),
            figNarrative(
                'Figure 7 illustrates the prevalent times of crashes in the province. ' +
                (peakSlot ? 'The highest number of fatalities occurred between ' +
                    peakSlot.Slot + ' with ' + (peakSlot.FatalCurr || 0) +
                    ' fatal' + ((peakSlot.FatalCurr || 0) !== 1 ? 'ities' : 'ity') + ' recorded.' : '')
            ),
            blank()
        );
    }
}

// ── Days of week + Figure 8 ───────────────────────────────
if (dowProv.length > 0) {
    const allDays = ['MONDAYS', 'TUESDAYS', 'WEDNESDAYS', 'THURSDAYS', 'FRIDAYS', 'SATURDAYS', 'SUNDAYS'];
    children.push(
        heading('PROVINCIAL DAYS OF THE WEEK'),
        para([run('There is a relative spread of crashes throughout the Province, with an increase being observed from Monday, Friday to Sunday.', { size: 20 })], { after: 80 }),
        para([run('PROVINCIAL', { bold: true, size: 20 })], { after: 60 })
    );
    const C = [Math.floor(PW * .25), Math.floor(PW * .19), Math.floor(PW * .19),
    Math.floor(PW * .19), PW - Math.floor(PW * .25) - Math.floor(PW * .19) * 3];
    children.push(
        new Table({
            width: { size: PW, type: WidthType.DXA }, columnWidths: C, rows: [
                new TableRow({
                    children: [hdrCell('DAYS', C[0]), hdrCell('CRASHES ' + py, C[1]),
                    hdrCell('CRASHES ' + cy, C[2]), hdrCell('FATALITIES ' + py, C[3]), hdrCell('FATALITIES ' + cy, C[4])]
                }),
                ...allDays.map(day => {
                    const d = (dowProv || []).find(x => x.Day === day) || {};
                    return new TableRow({
                        children: [labelCell(day, C[0]),
                        dataCell(d.CrashesPrev ?? 0, C[1]), dataCell(d.CrashesCurr ?? 0, C[2]),
                        dataCell(d.FatalPrev ?? 0, C[3]), dataCell(d.FatalCurr ?? 0, C[4])]
                    });
                })
            ]
        }),
        blank(80)
    );
    if (fig8) {
        const peakDay = dowProv.reduce((a, b) => (b.CrashesCurr || 0) > (a.CrashesCurr || 0) ? b : a, dowProv[0]);
        const peakFatalDay = dowProv.reduce((a, b) => (b.FatalCurr || 0) > (a.FatalCurr || 0) ? b : a, dowProv[0]);
        children.push(
            chartPara(fig8),
            figNarrative(
                'Figure 8 represents the distribution of crashes per day of the week. ' +
                (peakDay ? peakDay.Day + ' recorded the highest number of crashes (' +
                    (peakDay.CrashesCurr || 0) + ')' : '') +
                (peakFatalDay && peakFatalDay.Day !== (peakDay && peakDay.Day)
                    ? ' while ' + peakFatalDay.Day + ' recorded the highest fatalities (' +
                    (peakFatalDay.FatalCurr || 0) + ')' : '') + '.'
            ),
            blank()
        );
    }

    (vm.Districts || []).forEach(dist => {
        const dd = (vm.DaysOfWeek || {})[dist.Key] || [];
        if (!dd.length) return;
        children.push(
            para([run(dist.Name + ' DISTRICT', { bold: true, size: 20 })], { after: 60 }),
            new Table({
                width: { size: PW, type: WidthType.DXA }, columnWidths: C, rows: [
                    new TableRow({
                        children: [hdrCell('DAYS', C[0]), hdrCell('CRASHES ' + py, C[1]),
                        hdrCell('CRASHES ' + cy, C[2]), hdrCell('FATALITIES ' + py, C[3]), hdrCell('FATALITIES ' + cy, C[4])]
                    }),
                    ...allDays.map(day => {
                        const d = dd.find(x => x.Day === day) || {};
                        return new TableRow({
                            children: [labelCell(day, C[0]),
                            dataCell(d.CrashesPrev ?? 0, C[1]), dataCell(d.CrashesCurr ?? 0, C[2]),
                            dataCell(d.FatalPrev ?? 0, C[3]), dataCell(d.FatalCurr ?? 0, C[4])]
                        });
                    })
                ]
            }),
            blank()
        );
    });
}

// ── Conclusion ────────────────────────────────────────────
const cV = variation(pr.Crashes, c.Crashes), fV = variation(pr.Fatalities, c.Fatalities),
    sV = variation(pr.Serious, c.Serious), slV = variation(pr.Slight, c.Slight);
children.push(heading('CONCLUSION'));
[
    'Number of crashes ' + (c.Crashes > pr.Crashes ? 'increased' : 'decreased') + ' by ' + Math.abs(parseFloat(cV)).toFixed(2) +
    ' percent, fatalities ' + (c.Fatalities > pr.Fatalities ? 'increased' : 'decreased') + ' by ' + Math.abs(parseFloat(fV)).toFixed(2) +
    ' percent, serious injuries ' + (c.Serious > pr.Serious ? 'increased' : 'decreased') + ' by ' + Math.abs(parseFloat(sV)).toFixed(2) +
    ' percent and slight injuries ' + (c.Slight > pr.Slight ? 'increased' : 'decreased') + ' by ' + Math.abs(parseFloat(slV)).toFixed(2) + ' percent.',
    'Law Enforcement to increase its deployment of traffic officers over weekends and into late shifts.',
    "Attention should be given to Sedans, LDV's and Taxi's, as they contributed highest number of crashes and injuries recorded."
].forEach(t => {
    children.push(para([run('\u2022  ', { bold: true }), run(t, { size: 20 })], { indent: 360, after: 80 }));
});
children.push(blank(120));

// ── Recommendations ───────────────────────────────────────
children.push(
    heading('RECOMMENDATIONS'),
    para([run('It is recommended that the MEC takes note of the contents of the report and give guidance where he deems necessary.', { size: 20 })], { after: 300 }),
    blank(300)
);

// ── Signature ─────────────────────────────────────────────
const sigLine = { style: BorderStyle.SINGLE, size: 6, color: NAVY };
children.push(
    new Paragraph({ border: { bottom: sigLine }, spacing: { after: 60 }, children: [run('')] }),
    para([run(vm.FromName || 'MR W MTHOMBOTHI', { bold: true, size: 20 })], { after: 0 }),
    para([run(vm.FromTitle || 'HEAD OF DEPARTMENT', { bold: true, size: 20 })], { after: 160 }),
    para([run('NOTED / NOT NOTED', { bold: true, size: 20 })], { after: 60 }),
    para([run('COMMENTS:', { bold: true, size: 20 })], { after: 200 }),
    new Paragraph({ border: { bottom: sigLine }, spacing: { after: 60 }, children: [run('')] }),
    para([run(vm.ToName || 'MR P NGOMANE (MPL)', { bold: true, size: 20 })], { after: 0 }),
    para([run(vm.ToTitle || 'MEMBER OF THE EXECUTIVE COUNCIL', { bold: true, size: 20 })], { after: 0 })
);


// ── Build ─────────────────────────────────────────────────
const doc = new Document({
    styles: { default: { document: { run: { font: "Arial", size: 20 } } } },
    sections: [{
        properties: {
            page: {
                size: { width: 11906, height: 16838 },
                margin: { top: 1134, right: 1134, bottom: 1134, left: 1134 }
            }
        }, children
    }]
});

Packer.toBuffer(doc).then(buf => {
    fs.writeFileSync(out, buf);
    console.log('OK');
}).catch(e => { console.error(e.message); process.exit(1); });