const {
    Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
    AlignmentType, BorderStyle, WidthType, ShadingType, VerticalAlign
} = require('docx');
const fs = require('fs');

// ── Data passed in via JSON argument ─────────────────────────
const vm = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));

// ── Helpers ───────────────────────────────────────────────────
const BLACK = "000000";
const DARK_BLUE = "1F3864";
const LIGHT_BLUE = "BDD7EE";
const WHITE = "FFFFFF";

const border = (color = BLACK) => ({ style: BorderStyle.SINGLE, size: 4, color });
const borders = (color = BLACK) => ({
    top: border(color), bottom: border(color),
    left: border(color), right: border(color)
});

const cellMargins = { top: 60, bottom: 60, left: 108, right: 108 };

function txt(text, opts = {}) {
    return new TextRun({
        text: String(text ?? ''),
        font: "Arial",
        size: opts.size || 20,
        bold: opts.bold || false,
        color: opts.color || BLACK
    });
}

function para(children, opts = {}) {
    return new Paragraph({
        alignment: opts.align || AlignmentType.LEFT,
        spacing: { before: opts.before ?? 0, after: opts.after ?? 0 },
        children: Array.isArray(children) ? children : [children]
    });
}

function hdrCell(text, width, opts = {}) {
    return new TableCell({
        width: { size: width, type: WidthType.DXA },
        borders: borders(),
        shading: { fill: opts.fill || DARK_BLUE, type: ShadingType.CLEAR },
        margins: cellMargins,
        verticalAlign: VerticalAlign.CENTER,
        columnSpan: opts.span || 1,
        children: [para(txt(text, { bold: true, size: 18, color: WHITE }), { align: AlignmentType.CENTER })]
    });
}

function dataCell(text, width, opts = {}) {
    return new TableCell({
        width: { size: width, type: WidthType.DXA },
        borders: borders(),
        shading: { fill: opts.fill || WHITE, type: ShadingType.CLEAR },
        margins: cellMargins,
        verticalAlign: VerticalAlign.CENTER,
        children: [para(txt(text, { bold: opts.bold || false, size: 20 }), { align: AlignmentType.CENTER })]
    });
}

function sectionHeading(text) {
    return new Paragraph({
        spacing: { before: 160, after: 80 },
        children: [txt(text, { bold: true, size: 22 })]
    });
}

// ── Constants ─────────────────────────────────────────────────
const TW1 = 9560;
const C1 = [1686, 935, 747, 658, 694, 745, 794, 808, 800, 881, 812];

// ── Table 1: Weekly Statistics (Crashes, Fatalities, Serious, Slight) ──
function weekStatsTable(vm) {
    const districts = [
        { label: 'PROVINCE', cur: vm.CurrentProvince, pri: vm.PriorProvince },
        { label: 'EHLANZENI', cur: vm.CurrentEhlanzeni, pri: vm.PriorEhlanzeni },
        { label: 'BOHLABELO', cur: vm.CurrentBohlabelo, pri: vm.PriorBohlabelo },
        { label: 'GERT SIBANDE', cur: vm.CurrentGertSibande, pri: vm.PriorGertSibande },
        { label: 'NKANGALA', cur: vm.CurrentNkangala, pri: vm.PriorNkangala }
    ];

    const labelW = 1686;
    const distW = Math.floor((TW1 - labelW) / 5);
    const yearW = Math.floor(distW / 2);
    const priorYear = vm.DateFrom ? parseInt(vm.DateFrom.split('-')[0]) - 1 : new Date().getFullYear() - 1;
    const curYear = vm.DateFrom ? parseInt(vm.DateFrom.split('-')[0]) : new Date().getFullYear();

    const hdrRow = new TableRow({
        children: [
            new TableCell({
                width: { size: labelW, type: WidthType.DXA }, borders: borders(),
                shading: { fill: DARK_BLUE, type: ShadingType.CLEAR }, margins: cellMargins,
                children: [para(txt('', { size: 18 }))]
            }),
            ...districts.map(d =>
                new TableCell({
                    columnSpan: 2,
                    width: { size: distW, type: WidthType.DXA },
                    borders: borders(),
                    shading: { fill: DARK_BLUE, type: ShadingType.CLEAR },
                    margins: cellMargins,
                    verticalAlign: VerticalAlign.CENTER,
                    children: [para(txt(d.label, { bold: true, size: 18, color: WHITE }), { align: AlignmentType.CENTER })]
                })
            )
        ]
    });

    const yearRow = new TableRow({
        children: [
            new TableCell({
                width: { size: labelW, type: WidthType.DXA }, borders: borders(),
                shading: { fill: DARK_BLUE, type: ShadingType.CLEAR }, margins: cellMargins,
                children: [para(txt(''))]
            }),
            ...districts.flatMap(() => [
                hdrCell(priorYear, yearW),
                hdrCell(curYear, yearW)
            ])
        ]
    });

    const metrics = [
        { label: 'CRASHES', key: 'Crashes' },
        { label: 'FATALITIES', key: 'Fatalities' },
        { label: 'SERIOUS', key: 'Serious' },
        { label: 'SLIGHT', key: 'Slight' }
    ];

    const dataRows = metrics.map(m =>
        new TableRow({
            children: [
                new TableCell({
                    width: { size: labelW, type: WidthType.DXA }, borders: borders(),
                    shading: { fill: DARK_BLUE, type: ShadingType.CLEAR }, margins: cellMargins,
                    children: [para(txt(m.label, { bold: true, size: 18, color: WHITE }), { align: AlignmentType.CENTER })]
                }),
                ...districts.flatMap(d => [
                    dataCell(d.pri[m.key] ?? 0, yearW, { fill: LIGHT_BLUE }),
                    dataCell(d.cur[m.key] ?? 0, yearW, { bold: true })
                ])
            ]
        })
    );

    return new Table({
        width: { size: TW1, type: WidthType.DXA },
        columnWidths: [labelW, ...Array(10).fill(yearW)],
        rows: [hdrRow, yearRow, ...dataRows]
    });
}

// ── Table 2: Fatalities by Time Slot ─────────────────────────
function fatalTimeTable(vm) {
    const districts = [
        { label: 'PROVINCE', cur: vm.CurrentProvince, pri: vm.PriorProvince },
        { label: 'EHLANZENI', cur: vm.CurrentEhlanzeni, pri: vm.PriorEhlanzeni },
        { label: 'BOHLABELO', cur: vm.CurrentBohlabelo, pri: vm.PriorBohlabelo },
        { label: 'GERT SIBANDE', cur: vm.CurrentGertSibande, pri: vm.PriorGertSibande },
        { label: 'NKANGALA', cur: vm.CurrentNkangala, pri: vm.PriorNkangala }
    ];

    const TW = 9696;
    const labelW = 1954;
    const distW = Math.floor((TW - labelW) / 5);
    const yearW = Math.floor(distW / 2);
    const priorYear = vm.DateFrom ? parseInt(vm.DateFrom.split('-')[0]) - 1 : new Date().getFullYear() - 1;
    const curYear = vm.DateFrom ? parseInt(vm.DateFrom.split('-')[0]) : new Date().getFullYear();

    const hdrRow = new TableRow({
        children: [
            hdrCell('PREVALENT TIME', labelW),
            ...districts.flatMap(d => [
                hdrCell(`${d.label}\n${priorYear}`, yearW),
                hdrCell(`${d.label}\n${curYear}`, yearW)
            ])
        ]
    });

    const slots = [
        { label: '06H00 – 14H00', priKey: 'FatalTime1', curKey: 'FatalTime1' },
        { label: '14H00 – 22H00', priKey: 'FatalTime2', curKey: 'FatalTime2' },
        { label: '22H00 – 06H00', priKey: 'FatalTime3', curKey: 'FatalTime3' }
    ];

    const rows = slots.map(s =>
        new TableRow({
            children: [
                dataCell(s.label, labelW, { fill: LIGHT_BLUE }),
                ...districts.flatMap(d => [
                    dataCell(d.pri[s.priKey] ?? 0, yearW, { fill: LIGHT_BLUE }),
                    dataCell(d.cur[s.curKey] ?? 0, yearW, { bold: true })
                ])
            ]
        })
    );

    return new Table({
        width: { size: TW, type: WidthType.DXA },
        columnWidths: [labelW, ...Array(10).fill(yearW)],
        rows: [hdrRow, ...rows]
    });
}

// ── Summary text after the fatal time table ──────────────────
function buildFatalSummary(vm) {
    const p = vm.CurrentProvince || {};
    const lines = [];

    // Time distribution summary
    lines.push(new Paragraph({
        spacing: { before: 100, after: 60 },
        children: [txt(
            `FATALITIES OCCURRED BETWEEN 06:00 TO 14:00 (${p.FatalTime1 ?? 0}) ` +
            `14:00 TO 22:00 (${p.FatalTime2 ?? 0}) 22:00 TO 06:00 (${p.FatalTime3 ?? 0})`,
            { bold: false, size: 20 }
        )]
    }));

    // Pedestrian total
    const totalPeds = (vm.CurrentEhlanzeni?.FatalPedestrians ?? 0) +
        (vm.CurrentBohlabelo?.FatalPedestrians ?? 0) +
        (vm.CurrentGertSibande?.FatalPedestrians ?? 0) +
        (vm.CurrentNkangala?.FatalPedestrians ?? 0);

    if (totalPeds > 0) {
        lines.push(new Paragraph({
            spacing: { before: 60, after: 60 },
            children: [txt(`PROVINCE HAD ${totalPeds} FATAL PEDESTRIAN${totalPeds !== 1 ? 'S' : ''}`, { bold: true, size: 20 })]
        }));

        const pedDetails = [];
        if (vm.CurrentEhlanzeni?.FatalPedestrians > 0)
            pedDetails.push(`${vm.CurrentEhlanzeni.FatalPedestrians} (EHLANZENI)`);
        if (vm.CurrentBohlabelo?.FatalPedestrians > 0)
            pedDetails.push(`${vm.CurrentBohlabelo.FatalPedestrians} (BOHLABELO)`);
        if (vm.CurrentGertSibande?.FatalPedestrians > 0)
            pedDetails.push(`${vm.CurrentGertSibande.FatalPedestrians} (GERT SIBANDE)`);
        if (vm.CurrentNkangala?.FatalPedestrians > 0)
            pedDetails.push(`${vm.CurrentNkangala.FatalPedestrians} (NKANGALA)`);

        if (pedDetails.length > 0)
            lines.push(new Paragraph({
                spacing: { before: 0, after: 60 },
                children: [txt(pedDetails.join('    '), { bold: true, size: 20 })]
            }));
    }

    return lines;
}

// ── Problematic Routes section ──────────────────────────────
function buildRoutesSection(vm) {
    const paras = [
        new Paragraph({
            spacing: { before: 200, after: 60 },
            children: [txt('PROBLEMATIC ROUTES', { bold: true, size: 22 })]
        })
    ];

    if (!vm.ProblematicRoutes || vm.ProblematicRoutes.length === 0) {
        paras.push(para(txt('No problematic routes identified for this period.')));
        return paras;
    }

    const byDistrict = {};
    for (const r of vm.ProblematicRoutes) {
        if (!byDistrict[r.District]) byDistrict[r.District] = [];
        byDistrict[r.District].push(r);
    }

    for (const [district, routes] of Object.entries(byDistrict)) {
        const routeTexts = routes.map(r => {
            let t = `${r.Route} – ${r.Crashes} Crash${r.Crashes !== 1 ? 'es' : ''}`;
            if (r.Fatalities > 0) t += ` with ${r.Fatalities} Facilit${r.Fatalities !== 1 ? 'ies' : 'y'}`;
            if (r.Locations) t += ` (${r.Locations})`;
            return t;
        }).join('\t\t');

        paras.push(new Paragraph({
            spacing: { before: 60, after: 40 },
            children: [
                txt(`${district} DISTRICT – `, { bold: true, size: 20 }),
                txt(routeTexts, { size: 20 })
            ]
        }));
    }

    return paras;
}

// ── Sub‑period table (main stats) ────────────────────────────
function subPeriodTable(sp) {
    const TW = 9737;
    const labelW = 1784;
    const distW = Math.floor((TW - labelW) / 5);

    const districts = [
        { label: 'PROVINCE', d: sp.Province },
        { label: 'EHLANZENI', d: sp.Ehlanzeni },
        { label: 'BOHLABELO', d: sp.Bohlabelo },
        { label: 'GERT SIBANDE', d: sp.GertSibande },
        { label: 'NKANGALA', d: sp.Nkangala }
    ];

    const hdrRow = new TableRow({
        children: [
            hdrCell('', labelW),
            ...districts.map(d => hdrCell(d.label, distW))
        ]
    });

    const metrics = [
        { label: 'CRASHES', key: 'Crashes' },
        { label: 'FATALITIES', key: 'Fatalities' },
        { label: 'SERIOUS', key: 'Serious' },
        { label: 'SLIGHT', key: 'Slight' }
    ];

    const rows = metrics.map(m =>
        new TableRow({
            children: [
                new TableCell({
                    width: { size: labelW, type: WidthType.DXA }, borders: borders(),
                    shading: { fill: DARK_BLUE, type: ShadingType.CLEAR }, margins: cellMargins,
                    children: [para(txt(m.label, { bold: true, size: 18, color: WHITE }), { align: AlignmentType.CENTER })]
                }),
                ...districts.map(d =>
                    dataCell(d.d ? (d.d[m.key] ?? 0) : 0, distW, { bold: d.label === 'PROVINCE' })
                )
            ]
        })
    );

    return new Table({
        width: { size: TW, type: WidthType.DXA },
        columnWidths: [labelW, ...Array(5).fill(distW)],
        rows: [hdrRow, ...rows]
    });
}

// ── Sub‑period time table (fatalities by time slot) ────────
function subPeriodTimeTable(sp) {
    const TW = 9560;
    const labelW = 1918;
    const distW = Math.floor((TW - labelW) / 5);

    // Extract year from sp.From (format "YYYY-MM-DD")
    const year = sp?.From ? parseInt(sp.From.split('-')[0]) : '';

    const districts = [
        { label: 'PROVINCE', d: sp?.Province },
        { label: 'EHLANZENI', d: sp?.Ehlanzeni },
        { label: 'BOHLABELO', d: sp?.Bohlabelo },
        { label: 'GERT SIBANDE', d: sp?.GertSibande },
        { label: 'NKANGALA', d: sp?.Nkangala }
    ];

    const hdrRow = new TableRow({
        children: [
            hdrCell('PREVALENT TIME', labelW),
            ...districts.map(d => hdrCell(`${d.label} ${year}`, distW))
        ]
    });

    const slots = [
        { label: '06H00 – 14H00', key: 'FatalTime1' },
        { label: '14H00 – 22H00', key: 'FatalTime2' },
        { label: '22H00 – 06H00', key: 'FatalTime3' }
    ];

    const rows = slots.map(s =>
        new TableRow({
            children: [
                dataCell(s.label, labelW, { fill: LIGHT_BLUE }),
                ...districts.map(d =>
                    dataCell(d.d ? (d.d[s.key] ?? 0) : 0, distW)
                )
            ]
        })
    );

    return new Table({
        width: { size: TW, type: WidthType.DXA },
        columnWidths: [labelW, ...Array(5).fill(distW)],
        rows: [hdrRow, ...rows]
    });
}

// ── Victims Age Table ──────────────────────────────────────────
function victimsAgeTable(vm) {
    const TW = 9242;
    const colW = Math.floor(TW / 6);
    const v = vm.Victims || {};

    return new Table({
        width: { size: TW, type: WidthType.DXA },
        columnWidths: Array(6).fill(colW),
        rows: [
            new TableRow({
                children: [
                    hdrCell('VICTIMS PER AGE', colW, { span: 6 }),
                ]
            }),
            new TableRow({
                children: [
                    dataCell('TOTAL', colW, { bold: true }),
                    dataCell('', colW), dataCell('', colW), dataCell('', colW),
                    dataCell(v.TotalFatalities ?? 0, colW, { bold: true }),
                    dataCell('', colW)
                ]
            }),
            new TableRow({
                children: [
                    hdrCell('AGE', colW),
                    hdrCell('0-7', colW),
                    hdrCell('08-12', colW),
                    hdrCell('13-18', colW),
                    hdrCell('19-35', colW),
                    hdrCell('36+', colW)
                ]
            }),
            new TableRow({
                children: [
                    dataCell('', colW),
                    dataCell(v.Age0to7 ?? 0, colW),
                    dataCell(v.Age8to12 ?? 0, colW),
                    dataCell(v.Age13to18 ?? 0, colW),
                    dataCell(v.Age19to35 ?? 0, colW),
                    dataCell(v.Age36Plus ?? 0, colW)
                ]
            })
        ]
    });
}

// ── Victims Gender Table ──────────────────────────────────────
function victimsGenderTable(vm) {
    const TW = 9242;
    const c1 = 3080, c2 = 3081, c3 = 3081;
    const v = vm.Victims || {};

    function gRow(label, male, female) {
        return new TableRow({
            children: [
                dataCell(label, c1, { bold: true }),
                dataCell(male, c2),
                dataCell(female, c3)
            ]
        });
    }

    return new Table({
        width: { size: TW, type: WidthType.DXA },
        columnWidths: [c1, c2, c3],
        rows: [
            new TableRow({
                children: [
                    hdrCell('VICTIMS GENDER', c1),
                    hdrCell('M', c2),
                    hdrCell('F', c3)
                ]
            }),
            gRow('TOTAL', v.MaleTotal ?? 0, v.FemaleTotal ?? 0),
            gRow('DRIVER', v.MaleDriver ?? 0, v.FemaleDriver ?? 0),
            gRow('PASSENGER', v.MalePassenger ?? 0, v.FemalePassenger ?? 0),
            gRow('PEDESTRIANS', v.MalePedestrian ?? 0, v.FemalePedestrian ?? 0),
            gRow('CYCLIST', v.MaleCyclist ?? 0, v.FemaleCyclist ?? 0)
        ]
    });
}

// ── Helpers for formatting dates ─────────────────────────────
function fmtDate(d) {
    if (!d) return '';
    const months = ['JANUARY', 'FEBRUARY', 'MARCH', 'APRIL', 'MAY', 'JUNE',
        'JULY', 'AUGUST', 'SEPTEMBER', 'OCTOBER', 'NOVEMBER', 'DECEMBER'];
    const parts = d.split('-');
    return `${parseInt(parts[2])} ${months[parseInt(parts[1]) - 1]} ${parts[0]}`;
}

// ── Build the document ──────────────────────────────────────────
const title = `WEEKLY STATISTICS REPORT: ${fmtDate(vm.DateFrom)} TO ${fmtDate(vm.DateTo)}`;
const dayRange = `(${vm.DayRange || 'MONDAY TO SUNDAY'})`;

const children = [];

// 1. Title
children.push(
    new Paragraph({
        spacing: { before: 240, after: 60 },
        children: [txt(title, { bold: true, size: 24 })]
    }),
    new Paragraph({
        spacing: { before: 0, after: 120 },
        children: [txt(dayRange, { bold: true, size: 22 })]
    })
);

// 2. Main weekly statistics table
children.push(weekStatsTable(vm));

// 3. FATALITIES section
children.push(
    new Paragraph({ spacing: { before: 120, after: 60 }, children: [txt('FATALITIES', { bold: true, size: 22 })] })
);
children.push(fatalTimeTable(vm));
children.push(...buildFatalSummary(vm));
children.push(...buildRoutesSection(vm));

// 4. Sub‑period (e.g., Valentine's weekend)
if (vm.SubPeriod) {
    const sp = vm.SubPeriod;
    children.push(
        new Paragraph({
            spacing: { before: 200, after: 60 },
            children: [txt(`${fmtDate(sp.From)} – ${fmtDate(sp.To)}`, { bold: true, size: 22 })]
        }),
        subPeriodTable(sp),
        new Paragraph({ spacing: { before: 120, after: 60 }, children: [txt('FATALITIES', { bold: true, size: 22 })] }),
        subPeriodTimeTable(sp)
    );
}

// 5. Province victim demographics
children.push(
    new Paragraph({ spacing: { before: 120, after: 60 }, children: [txt('PROVINCE:', { bold: true, size: 22 })] }),
    victimsAgeTable(vm),
    new Paragraph({ spacing: { before: 60, after: 0 }, children: [txt('')] }),
    victimsGenderTable(vm)
);

// ── Create and save the document ─────────────────────────────
const doc = new Document({
    styles: {
        default: {
            document: { run: { font: "Arial", size: 20 } }
        }
    },
    sections: [{
        properties: {
            page: {
                size: { width: 11906, height: 16838 },
                margin: { top: 1440, right: 1440, bottom: 1440, left: 1440 }
            }
        },
        children
    }]
});

Packer.toBuffer(doc).then(buf => {
    fs.writeFileSync(process.argv[3], buf);
    console.log('OK');
}).catch(e => {
    console.error(e.message);
    process.exit(1);
});  