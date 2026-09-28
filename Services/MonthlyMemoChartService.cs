using System.IO.Compression;

namespace CrashReport.Services;

/// <summary>
/// Faithful C# port of wwwroot/js/charts.js (createGroupedBarChart / createPieChart), which
/// MonthlyMemoDocService used to invoke through a Node.js subprocess (generate_monthly.js
/// required charts.js and rendered PNGs via the `pngjs` npm package). Removing the Node.js
/// dependency (see old-app-remediation-plan.md item 8) meant this pixel-level renderer needed
/// a C# equivalent — ported algorithm-for-algorithm (same bitmap font, same layout math, same
/// palettes) rather than swapped for a different charting approach, so the rendered charts
/// look the same as they always have. The only thing that changed is: no external process, no
/// npm packages, no NODE_PATH probing — this writes raw PNG bytes itself (IHDR/IDAT/IEND),
/// exactly the small, self-contained job pngjs was doing for the Node script.
/// </summary>
public class ChartDataset
{
    public string Label { get; set; } = string.Empty;
    public double[] Data { get; set; } = Array.Empty<double>();
}

public class GroupedBarChartConfig
{
    public string? Title { get; set; }
    public string[] Labels { get; set; } = Array.Empty<string>();
    public List<ChartDataset> Datasets { get; set; } = new();
    public string LegendPosition { get; set; } = "right";
    public (byte R, byte G, byte B)[]? Palette { get; set; }
    public int Width { get; set; } = 700;
    public int Height { get; set; } = 380;
}

public class PieChartConfig
{
    public string? Title { get; set; }
    public double[] Data { get; set; } = Array.Empty<double>();
    public string[] Labels { get; set; } = Array.Empty<string>();
    public string LegendPosition { get; set; } = "right";
    public (byte R, byte G, byte B)[]? Colors { get; set; }
    public int Width { get; set; } = 600;
    public int Height { get; set; } = 400;
}

public static class MonthlyMemoChartService
{
    // ── Palettes (charts.js PALETTE / ROUTE_PALETTE) ────────────────
    public static readonly (byte R, byte G, byte B)[] Palette =
    {
        (68, 114, 196),
        (89, 89, 89),
        (255, 0, 0),
        (0, 176, 80),
        (255, 192, 0),
        (112, 173, 71),
    };

    public static readonly (byte R, byte G, byte B)[] RoutePalette =
    {
        (138, 159, 197),
        (128, 0, 0),
        (173, 216, 230),
        (255, 255, 153),
        (144, 238, 144),
        (255, 165, 0),
        (186, 143, 186),
        (255, 160, 122),
    };

    // ── Bitmap font (charts.js `G`) — 7 rows x 5 bits per glyph ─────
    private static readonly Dictionary<char, int[]> Glyphs = new()
    {
        [' '] = new[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        ['A'] = new[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
        ['B'] = new[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E },
        ['C'] = new[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E },
        ['D'] = new[] { 0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E },
        ['E'] = new[] { 0x1F, 0x10, 0x10, 0x1C, 0x10, 0x10, 0x1F },
        ['F'] = new[] { 0x1F, 0x10, 0x10, 0x1C, 0x10, 0x10, 0x10 },
        ['G'] = new[] { 0x0E, 0x11, 0x10, 0x13, 0x11, 0x11, 0x0F },
        ['H'] = new[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
        ['I'] = new[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x1F },
        ['J'] = new[] { 0x0F, 0x01, 0x01, 0x01, 0x01, 0x11, 0x0E },
        ['K'] = new[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 },
        ['L'] = new[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F },
        ['M'] = new[] { 0x11, 0x1B, 0x15, 0x11, 0x11, 0x11, 0x11 },
        ['N'] = new[] { 0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11 },
        ['O'] = new[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
        ['P'] = new[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 },
        ['Q'] = new[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x0E, 0x01 },
        ['R'] = new[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 },
        ['S'] = new[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E },
        ['T'] = new[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
        ['U'] = new[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
        ['V'] = new[] { 0x11, 0x11, 0x11, 0x11, 0x0A, 0x0A, 0x04 },
        ['W'] = new[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11 },
        ['X'] = new[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 },
        ['Y'] = new[] { 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04 },
        ['Z'] = new[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F },
        ['0'] = new[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E },
        ['1'] = new[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E },
        ['2'] = new[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F },
        ['3'] = new[] { 0x1F, 0x01, 0x02, 0x06, 0x01, 0x11, 0x0E },
        ['4'] = new[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 },
        ['5'] = new[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E },
        ['6'] = new[] { 0x0E, 0x10, 0x10, 0x1E, 0x11, 0x11, 0x0E },
        ['7'] = new[] { 0x1F, 0x01, 0x02, 0x02, 0x04, 0x04, 0x04 },
        ['8'] = new[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E },
        ['9'] = new[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x11, 0x0E },
        ['-'] = new[] { 0x00, 0x00, 0x00, 0x1F, 0x00, 0x00, 0x00 },
        ['+'] = new[] { 0x00, 0x04, 0x04, 0x1F, 0x04, 0x04, 0x00 },
        ['.'] = new[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x04 },
        [','] = new[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x04, 0x08 },
        ['\''] = new[] { 0x04, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00 },
        ['%'] = new[] { 0x11, 0x01, 0x02, 0x04, 0x08, 0x10, 0x11 },
        ['/'] = new[] { 0x01, 0x01, 0x02, 0x04, 0x08, 0x10, 0x10 },
        [':'] = new[] { 0x00, 0x04, 0x00, 0x00, 0x00, 0x04, 0x00 },
        ['&'] = new[] { 0x0C, 0x12, 0x12, 0x0C, 0x15, 0x12, 0x0D },
    };

    private const int GlyphW = 5;
    private const int GlyphH = 7;
    private const int GlyphGap = 1;

    // ── Canvas: flat RGB buffer, matches pngjs's colorType:2 (RGB, no alpha) ─
    private sealed class Canvas
    {
        public readonly int Width;
        public readonly int Height;
        public readonly byte[] Data; // 3 bytes per pixel (R,G,B)

        public Canvas(int width, int height)
        {
            Width = width;
            Height = height;
            Data = new byte[width * height * 3];
        }

        public void SetPixel(int x, int y, byte r, byte g, byte b)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            var i = (y * Width + x) * 3;
            Data[i] = r; Data[i + 1] = g; Data[i + 2] = b;
        }
    }

    private static int TextWidth(string str, int scale) =>
        str.ToUpperInvariant().Length * (GlyphW + GlyphGap) * scale;

    private static void DrawText(Canvas c, int x, int y, string str, byte r, byte g, byte b, int scale = 1)
    {
        var cx = x;
        foreach (var ch in str.ToUpperInvariant())
        {
            var rows = Glyphs.TryGetValue(ch, out var g2) ? g2 : Glyphs[' '];
            for (var row = 0; row < GlyphH; row++)
            {
                var bits = rows[row];
                for (var col = 0; col < GlyphW; col++)
                {
                    if ((bits & (1 << (GlyphW - 1 - col))) == 0) continue;
                    for (var sy = 0; sy < scale; sy++)
                        for (var sx = 0; sx < scale; sx++)
                            c.SetPixel(cx + col * scale + sx, y + row * scale + sy, r, g, b);
                }
            }
            cx += (GlyphW + GlyphGap) * scale;
        }
    }

    private static void FillRect(Canvas c, int x, int y, int w, int h, byte r, byte g, byte b)
    {
        for (var py = Math.Max(0, y); py < Math.Min(c.Height, y + h); py++)
            for (var px = Math.Max(0, x); px < Math.Min(c.Width, x + w); px++)
                c.SetPixel(px, py, r, g, b);
    }

    private static void HLine(Canvas c, int x, int y, int w, byte r, byte g, byte b)
    {
        for (var px = x; px < x + w; px++)
            c.SetPixel(px, y, r, g, b);
    }

    private static void VLine(Canvas c, int x, int y, int h, byte r, byte g, byte b)
    {
        for (var py = y; py < y + h; py++)
            c.SetPixel(x, py, r, g, b);
    }

    // ── Grouped bar chart (charts.js createGroupedBarChart) ─────────
    public static byte[] CreateGroupedBarChart(GroupedBarChartConfig cfg)
    {
        var w = cfg.Width;
        var h = cfg.Height;
        var pos = cfg.LegendPosition;

        const int swatchW = 14, swatchH = 10, swatchGap = 4, itemH = 16;
        var nDs = cfg.Datasets.Count;
        var maxLabelLen = cfg.Datasets.Count > 0 ? cfg.Datasets.Max(d => d.Label.Length) : 0;
        var legendItemW = swatchW + swatchGap + maxLabelLen * (GlyphW + GlyphGap) + 8;
        var legendBoxW = legendItemW + 12;
        var legendBoxH = nDs * itemH + 12;

        const int mt = 36, mb = 36, ml = 48;
        var mr = pos == "right" ? legendBoxW + 14 : 12;

        var cw = w - ml - mr;
        var ch = h - mt - mb;

        var canvas = new Canvas(w, h);
        FillRect(canvas, 0, 0, w, h, 255, 255, 255);

        if (!string.IsNullOrEmpty(cfg.Title))
        {
            const int scale = 2;
            var tw = TextWidth(cfg.Title, scale);
            var tx = ml + Math.Max(0, (cw - tw) / 2);
            DrawText(canvas, tx, 4, cfg.Title, 31, 56, 100, scale);
        }

        var allVals = cfg.Datasets.SelectMany(d => d.Data).ToArray();
        var maxVal = allVals.Length > 0 ? Math.Max(allVals.Max(), 1) : 1;
        const int nTicks = 5;
        var rawStep = maxVal / nTicks;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(rawStep <= 0 ? 1 : rawStep)));
        var step = Math.Ceiling(rawStep / mag) * mag;
        var tickMax = step * nTicks;
        if (tickMax <= 0) tickMax = 1;

        for (var t = 0; t <= nTicks; t++)
        {
            var val = (long)Math.Round(tickMax * t / nTicks);
            var py = mt + ch - (int)Math.Round((double)ch * t / nTicks);
            if (t == 0) HLine(canvas, ml, py, cw, 80, 80, 80);
            else HLine(canvas, ml, py, cw, 210, 210, 210);
            var lbl = val.ToString();
            var lw = TextWidth(lbl, 1);
            DrawText(canvas, ml - lw - 4, py - 3, lbl, 80, 80, 80);
        }

        VLine(canvas, ml, mt, ch, 80, 80, 80);
        HLine(canvas, ml, mt + ch, cw, 80, 80, 80);

        var nGroups = cfg.Labels.Length;
        if (nGroups > 0)
        {
            var groupW = cw / nGroups;
            var outerPad = Math.Max(6, (int)(groupW * 0.12));
            const int innerGap = 2;
            var totalBar = groupW - outerPad * 2;
            var barW = Math.Max(6, (totalBar - innerGap * (nDs - 1)) / Math.Max(nDs, 1));

            for (var gi = 0; gi < cfg.Labels.Length; gi++)
            {
                var gx = ml + gi * groupW + outerPad;

                var lines = cfg.Labels[gi].Split('\n');
                var gcx = gx + totalBar / 2;
                for (var li = 0; li < lines.Length; li++)
                {
                    var lw = TextWidth(lines[li], 1);
                    DrawText(canvas, gcx - lw / 2, mt + ch + 6 + li * (GlyphH + 2), lines[li], 50, 50, 50);
                }

                for (var di = 0; di < cfg.Datasets.Count; di++)
                {
                    var ds = cfg.Datasets[di];
                    var val = gi < ds.Data.Length ? ds.Data[gi] : 0;
                    var barH = val > 0 ? Math.Max(1, (int)Math.Round(ch * val / tickMax)) : 0;
                    var bx = gx + di * (barW + innerGap);
                    var by = mt + ch - barH;

                    var pal = cfg.Palette ?? Palette;
                    var (r, g, b) = pal[di % pal.Length];
                    FillRect(canvas, bx, by, barW, barH, r, g, b);

                    if (val > 0)
                    {
                        var vl = ((long)val).ToString();
                        var vw = TextWidth(vl, 1);
                        var vx = bx + (barW - vw) / 2;
                        var vy = by - (GlyphH + 2);
                        if (vy > mt) DrawText(canvas, vx, vy, vl, 40, 40, 40);
                    }
                }
            }
        }

        if (pos == "right")
        {
            var lx = w - legendBoxW + 4;
            var ly = mt + (ch - legendBoxH) / 2;
            FillRect(canvas, lx - 2, ly - 4, legendBoxW - 2, legendBoxH + 4, 248, 248, 248);
            HLine(canvas, lx - 2, ly - 4, legendBoxW - 2, 180, 180, 180);
            HLine(canvas, lx - 2, ly - 4 + legendBoxH + 3, legendBoxW - 2, 180, 180, 180);
            VLine(canvas, lx - 2, ly - 4, legendBoxH + 4, 180, 180, 180);
            VLine(canvas, lx - 2 + legendBoxW - 3, ly - 4, legendBoxH + 4, 180, 180, 180);

            for (var di = 0; di < cfg.Datasets.Count; di++)
            {
                var iy = ly + 4 + di * itemH;
                var pal2 = cfg.Palette ?? Palette;
                var (r, g, b) = pal2[di % pal2.Length];
                FillRect(canvas, lx + 4, iy + 1, swatchW, swatchH, r, g, b);
                DrawText(canvas, lx + 4 + swatchW + swatchGap, iy, cfg.Datasets[di].Label, 40, 40, 40);
            }
        }
        else
        {
            const int perRow = 3;
            var itemW2 = nDs > 0 ? (w - ml) / Math.Min(nDs, perRow) : 0;
            var ly = h - 18;
            for (var di = 0; di < cfg.Datasets.Count; di++)
            {
                var lx = ml + di % perRow * itemW2;
                var iy = ly + di / perRow * itemH;
                var pal3 = cfg.Palette ?? Palette;
                var (r, g, b) = pal3[di % pal3.Length];
                FillRect(canvas, lx, iy + 1, swatchW, swatchH, r, g, b);
                DrawText(canvas, lx + swatchW + swatchGap, iy, cfg.Datasets[di].Label, 40, 40, 40);
            }
        }

        return EncodePng(canvas);
    }

    // ── Pie chart (charts.js createPieChart) ─────────────────────────
    public static byte[] CreatePieChart(PieChartConfig cfg)
    {
        var w = cfg.Width;
        var h = cfg.Height;
        var pos = cfg.LegendPosition;
        var originalColors = cfg.Colors ?? Palette;

        var validEntries = new List<(double Value, string Label, (byte R, byte G, byte B) Color)>();
        for (var i = 0; i < cfg.Data.Length; i++)
        {
            var val = cfg.Data[i];
            if (val > 0)
            {
                var label = i < cfg.Labels.Length ? cfg.Labels[i] : $"Item {i + 1}";
                validEntries.Add((val, label, originalColors[i % originalColors.Length]));
            }
        }

        if (validEntries.Count == 0)
        {
            var empty = new Canvas(w, h);
            FillRect(empty, 0, 0, w, h, 255, 255, 255);
            DrawText(empty, 10, 10, "No data", 150, 150, 150, 2);
            return EncodePng(empty);
        }

        var data = validEntries.Select(e => e.Value).ToArray();
        var labels = validEntries.Select(e => e.Label).ToArray();
        var colors = validEntries.Select(e => e.Color).ToArray();
        var total = data.Sum();

        var nItems = data.Length;
        const int swatchW = 14, swatchH = 10, swatchGap = 4, itemH = 16;

        var legendTexts = data.Select((val, i) => $"{labels[i]} ({(val / total * 100):F1}%)").ToArray();
        var maxLegendWidth = legendTexts.Max(t => TextWidth(t, 1));
        var legendItemW = swatchW + swatchGap + maxLegendWidth + 8;
        var legendBoxW = legendItemW + 24;
        var legendBoxH = nItems * itemH + 12;

        const int mt = 36, mb = 36, ml = 48;
        var mr = pos == "right" ? legendBoxW + 40 : 12;
        var cw = w - ml - mr;
        var ch = h - mt - mb;

        var canvas = new Canvas(w, h);
        FillRect(canvas, 0, 0, w, h, 255, 255, 255);

        if (!string.IsNullOrEmpty(cfg.Title))
        {
            const int scale = 2;
            var tw = TextWidth(cfg.Title, scale);
            var tx = ml + Math.Max(0, (cw - tw) / 2);
            DrawText(canvas, tx, 4, cfg.Title, 31, 56, 100, scale);
        }

        if (total == 0)
        {
            DrawText(canvas, ml + 10, mt + 20, "Total is zero", 150, 150, 150, 2);
            return EncodePng(canvas);
        }

        var centerX = ml + cw / 2;
        var centerY = mt + ch / 2;
        var radius = Math.Min(cw, ch) / 2.0 - 10;

        var startAngle = -Math.PI / 2;
        startAngle = (startAngle + 2 * Math.PI) % (2 * Math.PI);

        for (var i = 0; i < data.Length; i++)
        {
            var value = data[i];
            var sliceAngle = value / total * 2 * Math.PI;
            var endAngle = startAngle + sliceAngle;
            var (r, g, b) = colors[i];

            var minX = Math.Max(0, (int)Math.Floor(centerX - radius));
            var maxX = Math.Min(w - 1, (int)Math.Ceiling(centerX + radius));
            var minY = Math.Max(0, (int)Math.Floor(centerY - radius));
            var maxY = Math.Min(h - 1, (int)Math.Ceiling(centerY + radius));

            var start = (startAngle + 2 * Math.PI) % (2 * Math.PI);
            var end = (endAngle + 2 * Math.PI) % (2 * Math.PI);

            for (var py = minY; py <= maxY; py++)
            {
                for (var px = minX; px <= maxX; px++)
                {
                    var dx = px - centerX;
                    var dy = py - centerY;
                    if (dx * dx + dy * dy > radius * radius) continue;

                    var angle = Math.Atan2(dy, dx);
                    if (angle < 0) angle += 2 * Math.PI;

                    bool inside = start <= end ? angle >= start && angle < end : angle >= start || angle < end;
                    if (inside) canvas.SetPixel(px, py, r, g, b);
                }
            }

            startAngle = endAngle;
        }

        for (double angle = 0; angle < 2 * Math.PI; angle += 0.01)
        {
            var px = (int)Math.Round(centerX + radius * Math.Cos(angle));
            var py = (int)Math.Round(centerY + radius * Math.Sin(angle));
            canvas.SetPixel(px, py, 180, 180, 180);
        }

        if (pos == "right")
        {
            const int padding = 10;
            var lx = w - legendBoxW - padding;
            var ly = mt + (ch - legendBoxH) / 2;

            FillRect(canvas, lx - 2, ly - 4, legendBoxW - 2, legendBoxH + 4, 248, 248, 248);
            HLine(canvas, lx - 2, ly - 4, legendBoxW - 2, 180, 180, 180);
            HLine(canvas, lx - 2, ly - 4 + legendBoxH + 3, legendBoxW - 2, 180, 180, 180);
            VLine(canvas, lx - 2, ly - 4, legendBoxH + 4, 180, 180, 180);
            VLine(canvas, lx - 2 + legendBoxW - 3, ly - 4, legendBoxH + 4, 180, 180, 180);

            for (var di = 0; di < data.Length; di++)
            {
                var iy = ly + 4 + di * itemH;
                var (r, g, b) = colors[di % colors.Length];
                FillRect(canvas, lx + 4, iy + 1, swatchW, swatchH, r, g, b);
                var label = $"{labels[di]} ({(data[di] / total * 100):F1}%)";
                DrawText(canvas, lx + 4 + swatchW + swatchGap, iy, label, 40, 40, 40);
            }
        }
        else
        {
            const int perRow = 3;
            var itemW2 = (w - ml) / Math.Min(nItems, perRow);
            var ly = h - 18;
            for (var di = 0; di < data.Length; di++)
            {
                var lx = ml + di % perRow * itemW2;
                var iy = ly + di / perRow * itemH;
                var (r, g, b) = colors[di % colors.Length];
                FillRect(canvas, lx, iy + 1, swatchW, swatchH, r, g, b);
                var label = $"{labels[di]} ({(data[di] / total * 100):F1}%)";
                DrawText(canvas, lx + swatchW + swatchGap, iy, label, 40, 40, 40);
            }
        }

        return EncodePng(canvas);
    }

    // ── Minimal PNG encoder (IHDR/IDAT/IEND) — replaces pngjs ────────
    private static byte[] EncodePng(Canvas c)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }); // PNG signature

        // IHDR
        var ihdr = new byte[13];
        WriteUInt32BE(ihdr, 0, (uint)c.Width);
        WriteUInt32BE(ihdr, 4, (uint)c.Height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 2;  // color type 2 = RGB (truecolor, no alpha) — matches pngjs colorType:2
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(output, "IHDR", ihdr);

        // Raw scanlines: one filter-type byte (0 = None) + RGB bytes per row
        var raw = new byte[(c.Width * 3 + 1) * c.Height];
        var stride = c.Width * 3;
        for (var y = 0; y < c.Height; y++)
        {
            var rowStart = y * (stride + 1);
            raw[rowStart] = 0; // filter type: None
            Buffer.BlockCopy(c.Data, y * stride, raw, rowStart + 1, stride);
        }

        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(raw, 0, raw.Length);
            compressed = ms.ToArray();
        }
        WriteChunk(output, "IDAT", compressed);

        WriteChunk(output, "IEND", Array.Empty<byte>());

        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        var lenBytes = new byte[4];
        WriteUInt32BE(lenBytes, 0, (uint)data.Length);
        output.Write(lenBytes);
        output.Write(typeBytes);
        output.Write(data);

        var crc = Crc32(typeBytes, data);
        var crcBytes = new byte[4];
        WriteUInt32BE(crcBytes, 0, crc);
        output.Write(crcBytes);
    }

    private static void WriteUInt32BE(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in type) c = Crc32Table[(c ^ b) & 0xFF] ^ (c >> 8);
        foreach (var b in data) c = Crc32Table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
