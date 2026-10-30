using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

// Ein Ball zum Spielen: eigenes kleines Fenster mit einfacher Physik. Das Pet läuft ihm nach und kickt ihn.
// Man kann den Ball auch mit der Maus ziehen und werfen; Rechtsklick räumt ihn weg.
class BallWin : Form
{
    PetForm owner;
    public float x, y, vx, vy;            // Position (obere linke Ecke) und Geschwindigkeit in Pixeln pro Bild
    public int D;                         // Durchmesser
    public bool Grabbed;
    float sc, rot;
    int sz;
    Point grabOff;

    public BallWin(PetForm o, float scale, int startX, int startY)
    {
        owner = o; sc = scale; D = Math.Max(14, (int)(22 * sc)); sz = D + 4;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; Size = new Size(sz, sz);
        x = startX; y = startY;
        MouseDown += delegate(object s, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { Grabbed = true; vx = 0; vy = 0; grabOff = new Point(Cursor.Position.X - (int)x, Cursor.Position.Y - (int)y); }
            else if (e.Button == MouseButtons.Right) owner.ToggleBall();
        };
        MouseUp += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) Grabbed = false; };
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x80 | 0x08000000; return cp; }
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }

    // einmal pro Takt: bewegen (Schwerkraft, Abprallen, Rollen) und zeichnen
    public void Step(Rectangle wa, bool visible)
    {
        if (!visible) { if (Visible) Hide(); return; }
        if (!Visible) Show();
        if (Grabbed)
        {
            Point c = Cursor.Position;
            float nx = c.X - grabOff.X, ny = c.Y - grabOff.Y;
            vx = vx * 0.5f + (nx - x) * 0.5f; vy = vy * 0.5f + (ny - y) * 0.5f;     // geglättete Wurfgeschwindigkeit
            x = nx; y = ny;
        }
        else
        {
            vy += 1.5f * sc;
            x += vx; y += vy;
            float floor = wa.Bottom - D - 2 * sc, right = wa.Right - D;
            if (x < wa.Left) { x = wa.Left; vx = -vx * 0.8f; }
            if (x > right) { x = right; vx = -vx * 0.8f; }
            if (y > floor)
            {
                y = floor;
                vy = Math.Abs(vy) > 2.4f * sc ? -vy * 0.62f : 0;
                vx *= 0.985f;
                if (Math.Abs(vx) < 0.15f) vx = 0;
            }
            if (y < wa.Top - 3 * D) { y = wa.Top - 3 * D; vy = 0; }
        }
        rot += vx * 0.1f;
        using (Bitmap bmp = new Bitmap(sz, sz, PixelFormat.Format32bppPArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); Draw(g); }
            Layered.Update(Handle, bmp, (int)x - 2, (int)y - 2);
        }
    }

    static readonly string[] Art = new string[] { "..###..", ".#####.", "#######", "#######", "#######", ".#####.", "..###.." };

    // Pixelball: weiß mit roten Streifen, die beim Rollen wandern
    public void Draw(Graphics g)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        int u = Math.Max(2, D / 7), off = (sz - 7 * u) / 2, shift = (int)Math.Floor(rot);
        using (Brush w = new SolidBrush(Color.FromArgb(246, 246, 248)), r = new SolidBrush(Color.FromArgb(214, 84, 72)), s = new SolidBrush(Color.FromArgb(200, 200, 212)))
            for (int ro = 0; ro < 7; ro++)
                for (int co = 0; co < 7; co++)
                {
                    if (Art[ro][co] != '#') continue;
                    bool stripe = (((co + ro + shift) % 4) + 4) % 4 == 0;
                    Brush b = stripe ? r : (ro == 6 || (ro == 5 && (co == 1 || co == 5)) ? s : w);
                    g.FillRectangle(b, off + co * u, off + ro * u, u, u);
                }
    }
}

public partial class PetForm
{
    BallWin ball;
    DateTime nextKick = DateTime.MinValue;
    int kickFrames;

    internal void ToggleBall()
    {
        if (ball != null) { DisposeBall(); Say("Ball weggeräumt.", 3); return; }
        Rectangle wa = AreaAt(new Point(px + W / 2, py + H / 2));
        int bx = px + W / 2 + (int)((rnd.Next(2) * 2 - 1) * (120 + rnd.Next(100)) * sc);
        bx = Math.Max(wa.Left + 10, Math.Min(wa.Right - 40, bx));
        ball = new BallWin(this, sc, bx, wa.Bottom - (int)(260 * sc));
        hop = 22;
        Say("Ball! Ball! Ball!", 4);
    }
    void DisposeBall()
    {
        if (ball == null) return;
        try { ball.Close(); } catch { }
        ball = null;
    }

    // Läuft das Pet hinter dem Ball her?
    void ChaseBall(Rectangle wa, int cxp)
    {
        if (ball == null || ball.Grabbed || supHwnd != IntPtr.Zero) return;
        float dxs = ball.x + ball.D / 2f - cxp;
        if (Math.Abs(dxs) < 70 * sc) return;
        dir = dxs > 0 ? 1 : -1; speed = 2.9f; jumpOff = false;
        walkFrames = Math.Max(4, (int)Math.Min(90, (Math.Abs(dxs) - 50 * sc) / (speed * sc)));
    }

    // einmal pro Takt: Ball bewegen und, wenn er vor den Füßen liegt, kicken
    void UpdateBall(DateTime now, Rectangle wa, bool standing, int cxp)
    {
        if (ball == null) return;
        ball.Step(wa, Visible && !autoHidden);
        if (kickFrames > 0) kickFrames--;
        if (!standing || supHwnd != IntPtr.Zero || sleeping || cState == "needs" || cState == "blocked" || dragging || jumping || ball.Grabbed || still) return;
        float dxs = ball.x + ball.D / 2f - cxp;
        bool low = ball.y + ball.D >= wa.Bottom - 16 * sc;
        if (Math.Abs(dxs) < 60 * sc && low && now >= nextKick)
        {
            nextKick = now.AddSeconds(1.1);
            dir = dxs >= 0 ? 1 : -1; kickFrames = 10; walkFrames = 0;
            ball.vx = dir * (9 + rnd.Next(7)) * sc; ball.vy = -(7 + rnd.Next(5)) * sc;
            if (rnd.Next(100) < 25) Say(Pick("Tor!", "Hopp!", "Fang!"), 1.5);
        }
    }
}
