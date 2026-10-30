using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Farben für das Menü, passend zum Windows-Design (hell oder dunkel)
class Pal
{
    public Color Bg, Border, Fg, Dim, Hover, HoverFg, Track;
    public static Pal Make()
    {
        bool light = false;
        try
        {
            object v = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
            light = v != null && Convert.ToInt32(v) == 1;
        }
        catch { }
        Pal p = new Pal();
        if (light)
        {
            p.Bg = Color.FromArgb(251, 249, 246); p.Border = Color.FromArgb(214, 208, 200); p.Fg = Color.FromArgb(31, 36, 48);
            p.Dim = Color.FromArgb(112, 118, 130); p.Track = Color.FromArgb(226, 221, 214);
        }
        else
        {
            p.Bg = Color.FromArgb(38, 36, 44); p.Border = Color.FromArgb(72, 70, 82); p.Fg = Color.FromArgb(238, 236, 232);
            p.Dim = Color.FromArgb(150, 154, 166); p.Track = Color.FromArgb(64, 62, 74);
        }
        p.Hover = Color.FromArgb(219, 119, 98); p.HoverFg = Color.FromArgb(42, 23, 16);
        return p;
    }
}

static class Gfx
{
    public static GraphicsPath Round(float x, float y, float w, float h, float r)
    {
        GraphicsPath p = new GraphicsPath();
        r = Math.Min(r, Math.Min(w, h) / 2f);
        if (r < 0.5f) { p.AddRectangle(new RectangleF(x, y, w, h)); return p; }
        p.AddArc(x, y, 2 * r, 2 * r, 180, 90); p.AddArc(x + w - 2 * r, y, 2 * r, 2 * r, 270, 90);
        p.AddArc(x + w - 2 * r, y + h - 2 * r, 2 * r, 2 * r, 0, 90); p.AddArc(x, y + h - 2 * r, 2 * r, 2 * r, 90, 90);
        p.CloseFigure(); return p;
    }
}

class PetRenderer : ToolStripProfessionalRenderer
{
    Pal P; float K;
    public PetRenderer(Pal p, float k) : base(new ProfessionalColorTable()) { P = p; K = k; RoundedEdges = false; }
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using (Brush b = new SolidBrush(P.Bg)) e.Graphics.FillRectangle(b, e.AffectedBounds);
    }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using (Pen p = new Pen(P.Border)) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
    protected override void OnRenderItemBackground(ToolStripItemRenderEventArgs e) { }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        ToolStripItem it = e.Item;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (it.Selected && it.Enabled)
            using (Brush b = new SolidBrush(P.Hover))
            using (GraphicsPath gp = Gfx.Round(5 * K, 1, it.Width - 10 * K, it.Height - 2, 7 * K))
                e.Graphics.FillPath(b, gp);
        // Einträge mit Farbe im Tag (Sitzungen) bekommen links einen Punkt
        if (it.Tag is Color)
            using (Brush d = new SolidBrush((Color)it.Tag))
                e.Graphics.FillEllipse(d, 12 * K, it.Height / 2f - 4 * K, 8 * K, 8 * K);
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        bool sel = e.Item.Selected && e.Item.Enabled;
        ToolStripMenuItem mi = e.Item as ToolStripMenuItem;
        bool isShortcut = mi != null && mi.ShortcutKeyDisplayString != null && mi.ShortcutKeyDisplayString.Length > 0
            && e.Text == mi.ShortcutKeyDisplayString && e.Text != mi.Text;
        e.TextColor = !e.Item.Enabled ? P.Dim : sel ? P.HoverFg : (isShortcut ? P.Dim : P.Fg);
        base.OnRenderItemText(e);
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        float y = e.Item.Height / 2f;
        using (Pen p = new Pen(P.Border)) e.Graphics.DrawLine(p, 12 * K, y, e.Item.Width - 12 * K, y);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = e.Item.Selected ? P.HoverFg : P.Dim;
        base.OnRenderArrow(e);
    }
}

// Kopfzeile des Menüs: Name, drei Balken, Claude-Status
class StatItem : ToolStripItem
{
    public Pal P; public float K;
    public string Title = "", ClaudeText = "", Extra = "";
    public float Food, Joy, Energy;
    public Color ClaudeDot = Color.Gray;
    public StatItem(Pal p, float k)
    {
        P = p; K = k; AutoSize = false;
        Size = new Size((int)(280 * k), (int)(118 * k));
    }
    public override bool CanSelect { get { return false; } }
    static Color BarColor(float v)
    {
        return v < 25 ? Color.FromArgb(214, 84, 72) : v < 55 ? Color.FromArgb(226, 170, 50) : Color.FromArgb(70, 176, 118);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        float x = 16 * K, w = Width - 32 * K;
        string[] names = new string[] { "Hunger", "Laune", "Energie" };
        float[] vals = new float[] { Food, Joy, Energy };
        using (Font fT = new Font("Segoe UI Semibold", 11.5f), fS = new Font("Segoe UI", 9f))
        using (Brush bf = new SolidBrush(P.Fg), bd = new SolidBrush(P.Dim), bt = new SolidBrush(P.Track))
        {
            g.DrawString(Title, fT, bf, x, 8 * K);
            if (Extra.Length > 0)
            {
                SizeF es = g.MeasureString(Extra, fS);
                g.DrawString(Extra, fS, bd, Width - 16 * K - es.Width, 11 * K);
            }
            for (int i = 0; i < 3; i++)
            {
                float y = (40 + i * 20) * K;
                g.DrawString(names[i], fS, bd, x, y - 3 * K);
                float bx = x + 64 * K, bw = w - 64 * K - 34 * K, bh = 8 * K, by = y + 1 * K;
                using (GraphicsPath tp = Gfx.Round(bx, by, bw, bh, bh / 2)) g.FillPath(bt, tp);
                float fw = Math.Max(bh, bw * Math.Max(0f, Math.Min(100f, vals[i])) / 100f);
                using (Brush bc = new SolidBrush(BarColor(vals[i])))
                using (GraphicsPath fp = Gfx.Round(bx, by, fw, bh, bh / 2)) g.FillPath(bc, fp);
                g.DrawString(((int)vals[i]).ToString(), fS, bd, bx + bw + 8 * K, y - 3 * K);
            }
            float y2 = Height - 24 * K;
            using (Brush bdot = new SolidBrush(ClaudeDot)) g.FillEllipse(bdot, x, y2 + 6 * K, 8 * K, 8 * K);
            g.DrawString(ClaudeText, fS, bd, x + 16 * K, y2 + 1 * K);
        }
    }
}
