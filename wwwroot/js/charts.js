/**
 * charts.js — Pure-JS grouped bar chart generator (pngjs only).
 * Corrected 5×7 bitmap font with proper bit patterns.
 */
'use strict';
const { PNG } = require('pngjs');

// ── Colours matching the original document ─────────────────
const PALETTE = [
    [68, 114, 196],   // blue       CRASHES
    [89, 89, 89],   // dark gray  FATALITIES
    [255, 0, 0],   // red        SERIOUS
    [0, 176, 80],   // green      SLIGHT
    [255, 192, 0],   // yellow
    [112, 173, 71],   // light green
];

// ── Route chart colours (more muted, distinguishable) ─────
const ROUTE_PALETTE = [
    [138, 159, 197],   // periwinkle blue
    [128, 0, 0],   // dark maroon
    [173, 216, 230],   // light cyan
    [255, 255, 153],   // light yellow
    [144, 238, 144],   // light green
    [255, 165, 0],   // orange
    [186, 143, 186],   // plum
    [255, 160, 122],   // light salmon
];

// ── 5×7 bitmap font — 5-bit rows, bit4 = leftmost pixel ────
// Each glyph: 7 rows. bit pattern: 0b10000=col0, 0b01000=col1, etc.
const G = {
    ' ': [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00],
    'A': [0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
    'B': [0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E],
    'C': [0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E],
    'D': [0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E],
    'E': [0x1F, 0x10, 0x10, 0x1C, 0x10, 0x10, 0x1F],
    'F': [0x1F, 0x10, 0x10, 0x1C, 0x10, 0x10, 0x10],
    'G': [0x0E, 0x11, 0x10, 0x13, 0x11, 0x11, 0x0F],
    'H': [0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11],
    'I': [0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x1F],
    'J': [0x0F, 0x01, 0x01, 0x01, 0x01, 0x11, 0x0E],
    'K': [0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11],
    'L': [0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F],
    'M': [0x11, 0x1B, 0x15, 0x11, 0x11, 0x11, 0x11],
    'N': [0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11],
    'O': [0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
    'P': [0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10],
    'Q': [0x0E, 0x11, 0x11, 0x11, 0x11, 0x0E, 0x01],
    'R': [0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11],
    'S': [0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E],
    'T': [0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04],
    'U': [0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E],
    'V': [0x11, 0x11, 0x11, 0x11, 0x0A, 0x0A, 0x04],
    'W': [0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11],
    'X': [0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11],
    'Y': [0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04],
    'Z': [0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F],
    '0': [0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E],
    '1': [0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E],
    '2': [0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F],
    '3': [0x1F, 0x01, 0x02, 0x06, 0x01, 0x11, 0x0E],
    '4': [0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02],
    '5': [0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E],
    '6': [0x0E, 0x10, 0x10, 0x1E, 0x11, 0x11, 0x0E],
    '7': [0x1F, 0x01, 0x02, 0x02, 0x04, 0x04, 0x04],
    '8': [0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E],
    '9': [0x0E, 0x11, 0x11, 0x0F, 0x01, 0x11, 0x0E],
    '-': [0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00],
    '+': [0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00],
    '.': [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04],
    ',': [0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x08],
    "'": [0x04, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00],
    '%': [0x11, 0x01, 0x02, 0x04, 0x08, 0x10, 0x11],
    '/': [0x01, 0x01, 0x02, 0x04, 0x08, 0x10, 0x10],
    ':': [0x00, 0x04, 0x00, 0x00, 0x00, 0x04, 0x00],
    '&': [0x0C, 0x12, 0x12, 0x0C, 0x15, 0x12, 0x0D],
};

const GLYPH_W = 5;   // pixels wide
const GLYPH_H = 7;   // pixels tall
const GLYPH_GAP = 1; // gap between chars

function textWidth(str, scale) {
    return String(str).toUpperCase().length * (GLYPH_W + GLYPH_GAP) * scale;
}

function drawText(png, x, y, str, r, g, b, scale) {
    scale = scale || 1;
    let cx = x;
    for (const ch of String(str).toUpperCase()) {
        const rows = G[ch] || G[' '];
        for (let row = 0; row < GLYPH_H; row++) {
            const bits = rows[row] || 0;
            for (let col = 0; col < GLYPH_W; col++) {
                if (bits & (1 << (GLYPH_W - 1 - col))) {
                    for (let sy = 0; sy < scale; sy++)
                        for (let sx = 0; sx < scale; sx++) {
                            const px = cx + col * scale + sx;
                            const py = y + row * scale + sy;
                            if (px < 0 || py < 0 || px >= png.width || py >= png.height) continue;
                            const i = (py * png.width + px) * 4;
                            png.data[i] = r; png.data[i + 1] = g; png.data[i + 2] = b; png.data[i + 3] = 255;
                        }
                }
            }
        }
        cx += (GLYPH_W + GLYPH_GAP) * scale;
    }
}

function fillRect(png, x, y, w, h, r, g, b) {
    for (let py = Math.max(0, y); py < Math.min(png.height, y + h); py++)
        for (let px = Math.max(0, x); px < Math.min(png.width, x + w); px++) {
            const i = (py * png.width + px) * 4;
            png.data[i] = r; png.data[i + 1] = g; png.data[i + 2] = b; png.data[i + 3] = 255;
        }
}

function hLine(png, x, y, w, r, g, b) {
    for (let px = x; px < x + w; px++) {
        if (px < 0 || px >= png.width || y < 0 || y >= png.height) continue;
        const i = (y * png.width + px) * 4;
        png.data[i] = r; png.data[i + 1] = g; png.data[i + 2] = b; png.data[i + 3] = 255;
    }
}
function vLine(png, x, y, h, r, g, b) {
    for (let py = y; py < y + h; py++) {
        if (py < 0 || py >= png.height || x < 0 || x >= png.width) continue;
        const i = (py * png.width + x) * 4;
        png.data[i] = r; png.data[i + 1] = g; png.data[i + 2] = b; png.data[i + 3] = 255;
    }
}

/**
 * createGroupedBarChart
 *
 * cfg:
 *   labels         : string[]           X-axis groups (e.g. ["2018","2019"])
 *   datasets       : {label, data}[]    One per bar colour (e.g. CRASHES, FATALITIES…)
 *   title          : string
 *   legendPosition : "right" | "bottom" (default "right")
 *   width, height
 */
function createGroupedBarChart(cfg) {
    const W = cfg.width || 700;
    const H = cfg.height || 380;
    const pos = cfg.legendPosition || 'right';

    // ── Legend dimensions ──────────────────────────────────
    const swatchW = 14, swatchH = 10, swatchGap = 4, itemH = 16;
    const nDS = cfg.datasets.length;
    const maxLabelLen = Math.max(...cfg.datasets.map(ds => ds.label.length));
    const legendItemW = swatchW + swatchGap + maxLabelLen * (GLYPH_W + GLYPH_GAP) + 8;
    const legendBoxW = legendItemW + 12;
    const legendBoxH = nDS * itemH + 12;

    // ── Margins ────────────────────────────────────────────
    const MT = 36;   // top (title)
    const MB = 36;   // bottom (x labels)
    const ML = 48;   // left (y labels)
    const MR = pos === 'right' ? legendBoxW + 14 : 12;

    const CW = W - ML - MR;
    const CH = H - MT - MB;

    // ── Canvas (white) ─────────────────────────────────────
    const png = new PNG({ width: W, height: H, colorType: 2 });
    fillRect(png, 0, 0, W, H, 255, 255, 255);

    // ── Title ──────────────────────────────────────────────
    if (cfg.title) {
        const scale = 2;
        const tw = textWidth(cfg.title, scale);
        const tx = ML + Math.max(0, Math.floor((CW - tw) / 2));
        drawText(png, tx, 4, cfg.title, 31, 56, 100, scale);
    }

    // ── Y axis scale ───────────────────────────────────────
    const allVals = cfg.datasets.flatMap(ds => ds.data.map(Number));
    const maxVal = Math.max(...allVals, 1);
    const nTicks = 5;
    // Round tick max up to a nice number
    const rawStep = maxVal / nTicks;
    const mag = Math.pow(10, Math.floor(Math.log10(rawStep)));
    const step = Math.ceil(rawStep / mag) * mag;
    const tickMax = step * nTicks;

    // ── Grid + Y tick labels ───────────────────────────────
    for (let t = 0; t <= nTicks; t++) {
        const val = Math.round(tickMax * t / nTicks);
        const py = MT + CH - Math.round(CH * t / nTicks);
        // grid line
        if (t === 0) hLine(png, ML, py, CW, 80, 80, 80);
        else hLine(png, ML, py, CW, 210, 210, 210);
        // y label
        const lbl = String(val);
        const lw = textWidth(lbl, 1);
        drawText(png, ML - lw - 4, py - 3, lbl, 80, 80, 80, 1);
    }

    // ── Axes ───────────────────────────────────────────────
    vLine(png, ML, MT, CH, 80, 80, 80);
    hLine(png, ML, MT + CH, CW, 80, 80, 80);

    // ── Bars ───────────────────────────────────────────────
    const nGroups = cfg.labels.length;
    const groupW = Math.floor(CW / nGroups);
    const outerPad = Math.max(6, Math.floor(groupW * 0.12));
    const innerGap = 2;
    const totalBar = groupW - outerPad * 2;
    const barW = Math.max(6, Math.floor((totalBar - innerGap * (nDS - 1)) / nDS));

    cfg.labels.forEach((label, gi) => {
        const gx = ML + gi * groupW + outerPad;

        // X label — supports \n for two-line labels
        const lines = String(label).split('\n');
        const gcx = gx + Math.floor(totalBar / 2);
        lines.forEach((line, li) => {
            const lw = textWidth(line, 1);
            drawText(png, gcx - Math.floor(lw / 2), MT + CH + 6 + li * (GLYPH_H + 2), line, 50, 50, 50, 1);
        });

        cfg.datasets.forEach((ds, di) => {
            const val = Number(ds.data[gi]) || 0;
            const barH = val > 0 ? Math.max(1, Math.round(CH * val / tickMax)) : 0;
            const bx = gx + di * (barW + innerGap);
            const by = MT + CH - barH;

            const pal = cfg.palette || PALETTE;
            const [r, g, b] = pal[di % pal.length];
            fillRect(png, bx, by, barW, barH, r, g, b);

            // Value on top
            if (val > 0) {
                const vl = String(val);
                const vw = textWidth(vl, 1);
                const vx = bx + Math.floor((barW - vw) / 2);
                const vy = by - (GLYPH_H + 2);
                if (vy > MT) drawText(png, vx, vy, vl, 40, 40, 40, 1);
            }
        });
    });

    // ── Legend ─────────────────────────────────────────────
    if (pos === 'right') {
        const lx = W - legendBoxW + 4;
        const ly = MT + Math.floor((CH - legendBoxH) / 2);
        // Box
        fillRect(png, lx - 2, ly - 4, legendBoxW - 2, legendBoxH + 4, 248, 248, 248);
        hLine(png, lx - 2, ly - 4, legendBoxW - 2, 180, 180, 180);
        hLine(png, lx - 2, ly - 4 + legendBoxH + 3, legendBoxW - 2, 180, 180, 180);
        vLine(png, lx - 2, ly - 4, legendBoxH + 4, 180, 180, 180);
        vLine(png, lx - 2 + legendBoxW - 3, ly - 4, legendBoxH + 4, 180, 180, 180);

        cfg.datasets.forEach((ds, di) => {
            const iy = ly + 4 + di * itemH;
            const pal2 = cfg.palette || PALETTE;
            const [r, g, b] = pal2[di % pal2.length];
            fillRect(png, lx + 4, iy + 1, swatchW, swatchH, r, g, b);
            drawText(png, lx + 4 + swatchW + swatchGap, iy, ds.label, 40, 40, 40, 1);
        });
    } else {
        // Bottom legend
        const perRow = 3;
        const itemW2 = Math.floor((W - ML) / Math.min(nDS, perRow));
        const ly = H - 18;
        cfg.datasets.forEach((ds, di) => {
            const lx = ML + (di % perRow) * itemW2;
            const iy = ly + Math.floor(di / perRow) * itemH;
            const pal3 = cfg.palette || PALETTE;
            const [r, g, b] = pal3[di % pal3.length];
            fillRect(png, lx, iy + 1, swatchW, swatchH, r, g, b);
            drawText(png, lx + swatchW + swatchGap, iy, ds.label, 40, 40, 40, 1);
        });
    }

    return PNG.sync.write(png);
}

module.exports = { createGroupedBarChart, ROUTE_PALETTE };