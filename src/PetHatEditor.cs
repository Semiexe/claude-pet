using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

// Fenster zum Zeichnen eines eigenen Huts: 12 x 8 Felder, zwei Farben, Vorschau auf dem Pet
class HatEditor : Form
{
    const int GW = 12, GH = 8;
    char[,] grid = new char[GH, GW];          // '.' leer, '#' Hauptfarbe, 'o' Akzentfarbe
    Color cMain, cAcc;
    char tool = '#';
    Pal P; float K; int cell;
    Panel canvas, preview;
    RadioButton rbMain, rbAcc, rbErase;
    public string[] ResultArt;
    public Color ResultMain, ResultAcc;

    class Canvas : Panel { public Canvas() { DoubleBuffered = true; } }

    public HatEditor(Pal p, float k, string[] art, Color main, Color acc)
    {
        P = p; K = k; cell = (int)(30 * k); cMain = main; cAcc = acc;
        for (int r = 0; r < GH; r++) for (int c = 0; c < GW; c++) grid[r, c] = '.';
        if (art != null)
        {
            int off = GH - art.Length;                     // von unten ausrichten
            for (int r = 0; r < art.Length && r + off >= 0; r++)
                for (int c = 0; c < art[r].Length && c < GW; c++) grid[r + off, c] = art[r][c];
        }
        Text = "Eigenen Hut zeichnen";
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size((int)(780 * k), (int)(430 * k));
        BackColor = p.Bg; ForeColor = p.Fg; Font = new Font("Segoe UI", 10f);

        Label hint = new Label();
        hint.Text = "Linke Maustaste malt mit dem gewählten Stift, die rechte mit der Akzentfarbe, die mittlere radiert.";
        hint.SetBounds((int)(20 * k), (int)(10 * k), (int)(740 * k), (int)(24 * k)); hint.ForeColor = p.Dim;

        canvas = new Canvas();
        canvas.SetBounds((int)(20 * k), (int)(44 * k), GW * cell, GH * cell);
        canvas.Paint += PaintCanvas; canvas.MouseDown += PaintAt; canvas.MouseMove += PaintAt;

        preview = new Canvas();
        preview.SetBounds((int)(20 * k) + GW * cell + (int)(30 * k), (int)(44 * k), (int)(310 * k), GH * cell);
        preview.Paint += PaintPreview;

        int by = (int)(44 * k) + GH * cell + (int)(18 * k);
        rbMain = Tool("Hauptfarbe", 20, by, '#'); rbAcc = Tool("Akzentfarbe", 150, by, 'o'); rbErase = Tool("Radierer", 280, by, '.');
        rbMain.Checked = true;
        Button bm = Btn("Hauptfarbe ändern...", 20, by + 44, 190); bm.Click += delegate { Pick(ref cMain); };
        Button ba = Btn("Akzentfarbe ändern...", 220, by + 44, 190); ba.Click += delegate { Pick(ref cAcc); };
        Button bc = Btn("Leeren", 420, by + 44, 100);
        bc.Click += delegate { for (int r = 0; r < GH; r++) for (int c = 0; c < GW; c++) grid[r, c] = '.'; Redraw(); };
        Button ok = Btn("Speichern", 560, by + 44, 100), cancel = Btn("Abbrechen", 668, by + 44, 92);
        ok.BackColor = p.Hover; ok.ForeColor = p.HoverFg; ok.FlatAppearance.BorderColor = p.Hover;
        ok.Click += delegate { if (Collect()) DialogResult = DialogResult.OK; else MessageBox.Show(this, "Zeichne zuerst etwas.", "Claude Pet"); };
        cancel.DialogResult = DialogResult.Cancel;
        CancelButton = cancel;
        Controls.AddRange(new Control[] { hint, canvas, preview, rbMain, rbAcc, rbErase, bm, ba, bc, ok, cancel });
    }

    RadioButton Tool(string text, int x, int y, char t)
    {
        RadioButton rb = new RadioButton();
        rb.Text = text; rb.Appearance = Appearance.Button; rb.FlatStyle = FlatStyle.Flat; rb.TextAlign = ContentAlignment.MiddleCenter;
        rb.SetBounds((int)(x * K), y, (int)(120 * K), (int)(34 * K));
        rb.FlatAppearance.BorderColor = P.Border; rb.FlatAppearance.CheckedBackColor = P.Track; rb.BackColor = P.Bg; rb.ForeColor = P.Fg;
        rb.CheckedChanged += delegate { if (rb.Checked) tool = t; };
        return rb;
    }
    Button Btn(string text, int x, int y, int w)
    {
        Button b = new Button();
        b.Text = text; b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = P.Border; b.BackColor = P.Bg; b.ForeColor = P.Fg;
        b.SetBounds((int)(x * K), (int)(y - 0), (int)(w * K), (int)(34 * K));
        return b;
    }
    void Pick(ref Color c)
    {
        using (ColorDialog d = new ColorDialog())
        {
            d.Color = c; d.FullOpen = true;
            if (d.ShowDialog(this) == DialogResult.OK) c = d.Color;
        }
        Redraw();
    }
    void Redraw() { canvas.Invalidate(); preview.Invalidate(); }

    void PaintAt(object s, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.None) return;
        int c = e.X / cell, r = e.Y / cell;
        if (c < 0 || c >= GW || r < 0 || r >= GH) return;
        char v = e.Button == MouseButtons.Left ? tool : e.Button == MouseButtons.Right ? 'o' : '.';
        if (grid[r, c] != v) { grid[r, c] = v; Redraw(); }
    }

    void PaintCanvas(object s, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        using (Brush bg = new SolidBrush(P.Track), bm = new SolidBrush(cMain), ba = new SolidBrush(cAcc)) using (Pen gp = new Pen(P.Border))
        {
            g.FillRectangle(bg, 0, 0, canvas.Width, canvas.Height);
            for (int r = 0; r < GH; r++)
                for (int c = 0; c < GW; c++)
                {
                    if (grid[r, c] == '#') g.FillRectangle(bm, c * cell, r * cell, cell, cell);
                    else if (grid[r, c] == 'o') g.FillRectangle(ba, c * cell, r * cell, cell, cell);
                }
            for (int c = 0; c <= GW; c++) g.DrawLine(gp, c * cell, 0, c * cell, GH * cell);
            for (int r = 0; r <= GH; r++) g.DrawLine(gp, 0, r * cell, GW * cell, r * cell);
        }
    }

    // Vorschau: ein Stück Pet-Kopf mit dem Hut darauf, in derselben Anordnung wie im Spiel
    void PaintPreview(object s, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None; g.PixelOffsetMode = PixelOffsetMode.Half;
        int pu = Math.Max(3, (int)(6 * K));
        int ox = (preview.Width - 24 * pu) / 2, y0 = GH * pu + 6;
        using (Brush bg = new SolidBrush(P.Track), bp = new SolidBrush(Color.FromArgb(219, 119, 98)), be = new SolidBrush(Color.FromArgb(42, 23, 16)), bm = new SolidBrush(cMain), ba = new SolidBrush(cAcc))
        {
            g.FillRectangle(bg, 0, 0, preview.Width, preview.Height);
            g.FillRectangle(bp, ox + 4 * pu, y0, 16 * pu, 8 * pu);
            g.FillRectangle(be, ox + 6 * pu, y0 + 2 * pu, pu, 2 * pu); g.FillRectangle(be, ox + 17 * pu, y0 + 2 * pu, pu, 2 * pu);
            int col0 = 12 - GW / 2;
            for (int r = 0; r < GH; r++)
                for (int c = 0; c < GW; c++)
                {
                    if (grid[r, c] == '.') continue;
                    g.FillRectangle(grid[r, c] == '#' ? bm : ba, ox + (col0 + c) * pu, y0 + (r - GH) * pu, pu, pu);
                }
        }
    }

    // Ergebnis einsammeln; leere Zeilen oben fallen weg, damit der Hut direkt auf dem Kopf sitzt
    bool Collect()
    {
        int first = -1;
        for (int r = 0; r < GH && first < 0; r++) for (int c = 0; c < GW; c++) if (grid[r, c] != '.') { first = r; break; }
        if (first < 0) return false;
        List<string> rows = new List<string>();
        for (int r = first; r < GH; r++)
        {
            StringBuilder sb = new StringBuilder();
            for (int c = 0; c < GW; c++) sb.Append(grid[r, c]);
            rows.Add(sb.ToString());
        }
        ResultArt = rows.ToArray(); ResultMain = cMain; ResultAcc = cAcc;
        return true;
    }
}

public partial class PetForm
{
    static string[] customArt;                                              // null = noch kein eigener Hut
    static Color customMain = Color.FromArgb(116, 92, 230), customAcc = Color.FromArgb(250, 210, 70);

    static string CustomHatPath() { return Path.Combine(DataDir(), "EigenerHut.txt"); }

    // Datei: main=RRGGBB, acc=RRGGBB, danach die Zeilen des Huts
    void LoadCustomHat()
    {
        try
        {
            customArt = null;
            if (!File.Exists(CustomHatPath())) return;
            List<string> rows = new List<string>();
            foreach (string line in File.ReadAllLines(CustomHatPath(), Encoding.UTF8))
            {
                if (line.StartsWith("main=")) customMain = ParseHex(line.Substring(5), customMain);
                else if (line.StartsWith("acc=")) customAcc = ParseHex(line.Substring(4), customAcc);
                else if (line.Length > 0 && line.Length <= 12 && rows.Count < 8) rows.Add(line.PadRight(12, '.').Replace(' ', '.'));
            }
            if (rows.Count > 0) customArt = rows.ToArray();
            hatBrushes.Remove("custom");
        }
        catch { }
    }
    static Color ParseHex(string h, Color fallback)
    {
        int v;
        return int.TryParse(h.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v) ? Color.FromArgb((v >> 16) & 255, (v >> 8) & 255, v & 255) : fallback;
    }
    void SaveCustomHat()
    {
        try
        {
            Directory.CreateDirectory(DataDir());
            List<string> lines = new List<string>();
            lines.Add("main=" + (customMain.ToArgb() & 0xFFFFFF).ToString("X6"));
            lines.Add("acc=" + (customAcc.ToArgb() & 0xFFFFFF).ToString("X6"));
            if (customArt != null) lines.AddRange(customArt);
            File.WriteAllLines(CustomHatPath(), lines.ToArray(), Encoding.UTF8);
        }
        catch { }
    }

    void EditCustomHat()
    {
        using (HatEditor ed = new HatEditor(pal, baseSc, customArt, customMain, customAcc))
        {
            if (ed.ShowDialog() != DialogResult.OK || ed.ResultArt == null) return;
            customArt = ed.ResultArt; customMain = ed.ResultMain; customAcc = ed.ResultAcc;
            hatBrushes.Remove("custom");
            SaveCustomHat();
            hatMode = "custom"; SaveState();
            hop = 22; Burst("★", 3);
            Say("Was für ein schöner Hut!", 4);
        }
    }
}
