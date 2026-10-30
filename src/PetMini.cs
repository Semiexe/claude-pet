using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Legt ein Bild mit Transparenz auf ein Fenster
static class Layered
{
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte Op, Flags, Alpha, Format; }
    [DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLEND pblend, int dwFlags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

    public static void Update(IntPtr hwnd, Bitmap bmp, int x, int y)
    {
        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr mem = CreateCompatibleDC(screen);
        IntPtr hb = bmp.GetHbitmap(Color.FromArgb(0));
        IntPtr old = SelectObject(mem, hb);
        SIZE sz = new SIZE(); sz.cx = bmp.Width; sz.cy = bmp.Height;
        POINT src = new POINT();
        POINT dst = new POINT(); dst.x = x; dst.y = y;
        BLEND bl = new BLEND(); bl.Op = 0; bl.Flags = 0; bl.Alpha = 255; bl.Format = 1;
        UpdateLayeredWindow(hwnd, screen, ref dst, ref sz, mem, ref src, 0, ref bl, 2);
        SelectObject(mem, old); DeleteObject(hb); DeleteDC(mem); ReleaseDC(IntPtr.Zero, screen);
    }
}

// Ein kleines Pet für eine Claude-Sitzung. Es läuft neben dem großen Pet her und zeigt den Zustand dieser Sitzung.
// Ein Klick holt das Fenster der Sitzung nach vorn.
class MiniPet : Form
{
    const int LW = 140, LH = 104, GROUND = 100;     // logische Fenstergröße, Standlinie
    static readonly string[] NeedIcon = new string[] { "##", "##", "##", "##", "..", "##" };
    static readonly string[] ReadyIcon = new string[] { "......#", ".....##", "#...##.", "##.##..", ".###...", "..#...." };
    static readonly string[] BlockIcon = new string[] { "#...#", ".#.#.", "..#..", ".#.#.", "#...#" };
    static readonly Color[] Body = new Color[] { Color.FromArgb(219, 119, 98), Color.FromArgb(226, 150, 82), Color.FromArgb(214, 110, 130), Color.FromArgb(206, 160, 108), Color.FromArgb(176, 150, 214) };
    static readonly Color[] Shade = new Color[] { Color.FromArgb(196, 102, 84), Color.FromArgb(200, 128, 66), Color.FromArgb(190, 92, 112), Color.FromArgb(182, 138, 92), Color.FromArgb(150, 126, 190) };
    static readonly int[] LegCols = new int[] { 6, 10, 14, 18 };

    PetForm owner;
    public Sess S = new Sess();
    float sc = 1f, t;
    int W, H, x, y, dir = 1, tint;
    bool moving;
    int lastX = int.MinValue, lastY, frameN;
    public string Hat = "";                 // Hut des Gast-Pets
    public float Speed = 2.6f;
    public bool NoLabel;                    // ohne Namensschild (für das Minispiel)
    public int CenterX { get { return x + W / 2; } }
    public void Tick(float dt) { t += dt; }   // Zeit weiterstellen, wenn nicht Update() läuft
    Font fLab = new Font("Segoe UI", 8.5f);
    StringFormat sf = new StringFormat();

    public MiniPet(PetForm o, int startCx, float scale, int tintIndex)
    {
        owner = o; tint = tintIndex; sc = scale;
        W = (int)(LW * sc); H = (int)(LH * sc);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(W, H);
        x = startCx - W / 2;
        sf.Alignment = StringAlignment.Center; sf.Trimming = StringTrimming.EllipsisCharacter; sf.FormatFlags = StringFormatFlags.NoWrap;
        MouseUp += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) owner.FocusSession(S.hwnd); };
        MouseDown += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Right) owner.ShowMenuAt(Cursor.Position); };
    }
    // ohne Fokus anzeigen, damit das Tippen im Terminal nicht unterbrochen wird
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x80 | 0x08000000; return cp; }
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }

    // einmal pro Takt: zum Ziel laufen und neu zeichnen
    public void Update(Sess s, int targetCx, int feet, float scale, bool visible)
    {
        S = s;
        if (!visible) { if (Visible) Hide(); return; }
        if (Math.Abs(scale - sc) > 0.001f) { sc = scale; W = (int)(LW * sc); H = (int)(LH * sc); Size = new Size(W, H); }
        float dx = targetCx - (x + W / 2f);
        moving = Math.Abs(dx) > 6 * sc;
        if (moving)
        {
            dir = dx > 0 ? 1 : -1;
            x += (int)Math.Round(dir * Math.Min(Math.Abs(dx), Speed * sc));
        }
        y = feet - (int)(GROUND * sc);
        t += 0.033f;
        // Steht das Mini-Pet still, reicht jedes zweite Bild
        if (x == lastX && y == lastY && ((frameN++) & 1) == 1) return;
        lastX = x; lastY = y;
        if (!Visible) Show();
        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); DrawInto(g); }
            Layered.Update(Handle, bmp, x, y);
        }
    }

    static void Px(Graphics g, Brush b, int ox, int oy, int pu, int c, int r, int w)
    {
        g.FillRectangle(b, ox + c * pu, oy + r * pu, w * pu, pu);
    }

    // Bild für Testzwecke
    public Bitmap Snap()
    {
        Bitmap b = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
        using (Graphics g = Graphics.FromImage(b)) { g.Clear(Color.Transparent); DrawInto(g); }
        return b;
    }

    public void DrawInto(Graphics g)
    {
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(sc, sc);
        using (Brush bsh = new SolidBrush(Color.FromArgb(50, 0, 0, 0))) g.FillEllipse(bsh, LW / 2f - 38, GROUND - 4, 76, 8);
        g.ResetTransform();

        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        int pu = Math.Max(1, (int)Math.Round(3 * sc));
        int ox = W / 2 - 12 * pu, oy = (int)(GROUND * sc) - 16 * pu;
        string st = S.state;
        int phase = ((int)(t * 8)) % 2, ty = ((int)(t * 12)) % 2, kn = ((int)(t * 14)) % 2;
        int by = oy - ((moving && phase == 0) ? Math.Max(1, pu / 2) : 0);
        bool blink = (t % 5f) < 0.14f;

        int la = 5, ra = 5;
        if (moving) { if (phase == 0) la = 4; else ra = 4; }
        else if (st == "running") { la = 4 + ty; ra = 5 - ty; }
        else if (st == "needs") { la = 3 + kn; ra = 4 - kn; }
        else if (st == "ready") { la = 3; ra = 3; }
        else if (st == "blocked") { la = 6; ra = 6; }
        else if (st == "guest") { la = 5; ra = 2 + ((int)(t * 8)) % 2; }

        using (Brush bp = new SolidBrush(Body[tint % 5]), bs = new SolidBrush(Shade[tint % 5]), be = new SolidBrush(Color.FromArgb(42, 23, 16)))
        {
            for (int r = 0; r <= 11; r++) Px(g, bp, ox, by, pu, 4, r, 16);
            Px(g, bs, ox, by, pu, 4, 11, 16);
            for (int r = 0; r < 4; r++) { Px(g, bp, ox, by, pu, 0, la + r, 4); Px(g, bp, ox, by, pu, 20, ra + r, 4); }
            Px(g, bs, ox, by, pu, 0, la + 3, 4); Px(g, bs, ox, by, pu, 20, ra + 3, 4);
            for (int i = 0; i < 4; i++)
            {
                bool lifted = moving && (i % 2 == phase);
                for (int r = 12; r <= (lifted ? 14 : 15); r++) Px(g, bp, ox, oy, pu, LegCols[i], r, 2);
            }
            // Helfer (Subagenten) dieser Sitzung als winzige Pets
            int hsz = 2, nh = Math.Min(3, S.agents);
            for (int hi = 0; hi < nh; hi++)
            {
                int hx = hi == 0 ? ox + 24 * pu + 2 : hi == 1 ? ox - 7 * hsz - 2 : ox + 24 * pu + 2 + 8 * hsz;
                int hh = (((int)(t * 5 + hi * 2)) % 4 == 0) ? hsz : 0;
                PetForm.DrawHelper(g, bp, be, hx, oy + 16 * pu - 6 * hsz - hh, hsz);
            }
            foreach (int e0 in new int[] { 5, 15 })
            {
                int e = e0 + (moving ? dir : 0);
                if (st == "needs" && !blink) { for (int r = 1; r <= 3; r++) Px(g, be, ox, by, pu, e + 1, r, 2); }
                else if (blink) Px(g, be, ox, by, pu, e, 3, 4);
                else if (st == "blocked") { Px(g, be, ox, by, pu, e, 2, 1); Px(g, be, ox, by, pu, e + 3, 2, 1); Px(g, be, ox, by, pu, e + 1, 3, 2); }
                else { Px(g, be, ox, by, pu, e + 1, 2, 2); Px(g, be, ox, by, pu, e, 3, 1); Px(g, be, ox, by, pu, e + 3, 3, 1); }
            }
        }

        if (Hat.Length > 0)
        {
            string[] art = PetForm.HatArt(Hat);
            if (art != null)
            {
                Color[] cs = PetForm.HatColors(Hat);
                int col0 = 12 - art[0].Length / 2;
                using (Brush h0 = new SolidBrush(cs[0]), h1 = new SolidBrush(cs[1]))
                    for (int r = 0; r < art.Length; r++)
                        for (int c = 0; c < art[r].Length; c++)
                        {
                            if (art[r][c] == '#') Px(g, h0, ox, by, pu, col0 + c, r - art.Length, 1);
                            else if (art[r][c] == 'o') Px(g, h1, ox, by, pu, col0 + c, r - art.Length, 1);
                        }
            }
        }

        // Zustandszeichen über dem Kopf
        string[] icon = null; Color ic = Color.White; int ib = -9, icol = 17;
        if (st == "needs") { icon = NeedIcon; ic = Color.FromArgb(255, 200, 40); icol = 18; ib = -9 - (int)(Math.Abs(Math.Sin(t * 6)) * 2); }
        else if (st == "ready") { icon = ReadyIcon; ic = Color.FromArgb(80, 200, 120); }
        else if (st == "blocked") { icon = BlockIcon; ic = Color.FromArgb(214, 84, 72); }
        else if (st == "running")
        {
            icon = PetForm.ToolIcon(S.tool);
            ic = Color.FromArgb(((int)(t * 2.5f)) % 2 == 0 ? 255 : 130, 110, 150, 230);
            if (icon == null)
                using (Brush bd = new SolidBrush(Color.FromArgb(255, 110, 150, 230)))
                    for (int i = 0; i < 3; i++)
                        if (((int)(t * 3)) % 3 == i) for (int r = 0; r < 2; r++) Px(g, bd, ox, by, pu, 16 + i * 3, -4 + r, 2);
        }
        if (icon != null)
            using (Brush bi = new SolidBrush(ic))
                for (int r = 0; r < icon.Length; r++)
                    for (int c = 0; c < icon[r].Length; c++)
                        if (icon[r][c] == '#') Px(g, bi, ox, by, pu, icol + c, ib + r, 1);

        if (NoLabel) return;
        // Name der Sitzung
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Default;
        g.ScaleTransform(sc, sc);
        string text = S.Label;
        SizeF z = g.MeasureString(text, fLab);
        float pw = Math.Min(LW - 6, z.Width + 14), ph = z.Height + 2, px0 = (LW - pw) / 2f;
        using (GraphicsPath gp = Gfx.Round(px0, 2, pw, ph, 8))
        using (Brush bb = new SolidBrush(Color.FromArgb(235, 252, 250, 246)))
        using (Pen pb = new Pen(Color.FromArgb(200, 140, 132, 120), 1.2f))
        using (Brush bt = new SolidBrush(Color.FromArgb(31, 36, 48)))
        {
            g.FillPath(bb, gp); g.DrawPath(pb, gp);
            g.DrawString(text, fLab, bt, new RectangleF(px0 + 4, 3, pw - 8, ph), sf);
        }
    }
}

// Verwaltung der Mini-Pets im großen Pet
public partial class PetForm
{
    Dictionary<string, MiniPet> minis = new Dictionary<string, MiniPet>();

    // Jedes Projekt behält seine Farbe, auch über Sitzungen und Neustarts hinweg: die ersten vier Projekte bekommen vier verschiedene.
    Dictionary<string, int> projTint = new Dictionary<string, int>();
    int TintFor(string project)
    {
        string k = (project == null ? "" : project).Trim().ToLowerInvariant().Replace('|', '/').Replace(':', '-');
        if (k.Length == 0) k = "?";
        int i;
        if (!projTint.TryGetValue(k, out i)) { i = projTint.Count % 4; projTint[k] = i; SaveState(); }
        return i;
    }

    internal bool FocusSession(long hwnd)
    {
        if (FocusWindow(new IntPtr(hwnd))) return true;
        Say("Ich finde das Fenster nicht.");
        return false;
    }
    internal void ShowMenuAt(Point p) { menu.Show(p); }

    void DisposeMinis()
    {
        foreach (MiniPet m in minis.Values) { try { m.Close(); } catch { } }
        minis.Clear();
    }

    // Je Sitzung ein Mini-Pet, sobald es mindestens zwei Sitzungen gibt
    void UpdateMinis(DateTime now, int feet, int cxp, Rectangle wa)
    {
        if (!minisOn || sessions.Count < 2) { if (minis.Count > 0) DisposeMinis(); return; }
        List<Sess> list = new List<Sess>();
        for (int i = 0; i < sessions.Count && i < 4; i++) list.Add(sessions[i]);
        list.Sort(delegate(Sess a, Sess b) { return string.CompareOrdinal(a.id, b.id); });   // feste Plätze

        List<string> gone = new List<string>();
        foreach (string id in minis.Keys)
        {
            bool found = false;
            foreach (Sess s in list) if (s.id == id) found = true;
            if (!found) gone.Add(id);
        }
        foreach (string id in gone) { try { minis[id].Close(); } catch { } minis.Remove(id); }

        float lo = wa.Left + 40 * sc, hi = wa.Right - 40 * sc;
        Plat sp = supHwnd != IntPtr.Zero ? FindPlat(supHwnd) : null;
        if (sp != null) { lo = sp.l + 30 * sc; hi = sp.r - 30 * sc; }
        int[] slots = new int[] { 1, -1, 2, -2 };
        for (int i = 0; i < list.Count; i++)
        {
            Sess s = list[i];
            MiniPet m;
            if (!minis.TryGetValue(s.id, out m))
            {
                m = new MiniPet(this, cxp, sc, TintFor(s.project));
                minis[s.id] = m;
            }
            float off = (120 + (Math.Abs(slots[i]) - 1) * 80) * sc;
            float tx = Math.Max(lo, Math.Min(hi, cxp + Math.Sign(slots[i]) * off));
            m.Update(s, (int)tx, feet, sc, Visible);
        }
    }
}
