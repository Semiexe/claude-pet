using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

// ein fallender Token
class Token { public float x, y, vy; public int kind; }   // kind 0 = gold (1 Punkt), 1 = violett (3 Punkte)

// Minispiel: 30 Sekunden lang fallende Tokens mit der Maus fangen, um das Pet zu füttern
class CatchGame : Form
{
    PetForm owner; Pal pal; float k;
    List<Token> tokens = new List<Token>();
    List<float[]> pops = new List<float[]>();          // x, y, Lebensdauer, Punkte
    Random rnd = new Random();
    System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
    DateTime last;
    float timeLeft = 30f, elapsed, spawnAcc;
    int score, best, catchAnim;
    bool over, rewarded;
    MiniPet pm;
    Font fBig, fMid, fSmall, fTok;
    Rectangle btnAgain, btnClose;

    public CatchGame(PetForm o, Pal p, float scale, int bestScore)
    {
        owner = o; pal = p; k = scale; best = bestScore;
        Text = "Tokens fangen";
        FormBorderStyle = FormBorderStyle.FixedToolWindow; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; TopMost = true; ShowInTaskbar = false;
        ClientSize = new Size((int)(420 * k), (int)(560 * k));
        DoubleBuffered = true; BackColor = p.Bg;
        fBig = new Font("Segoe UI Semibold", 22f); fMid = new Font("Segoe UI", 12f); fSmall = new Font("Segoe UI", 9.5f);
        fTok = new Font("Segoe UI", 10f, FontStyle.Bold);
        pm = new MiniPet(o, 0, 1.6f * k, 0);
        pm.NoLabel = true; pm.S.state = "guest";
        tm.Interval = 16;
        tm.Tick += delegate { Step(); };
        last = DateTime.Now; tm.Start();
        FormClosed += delegate { tm.Stop(); tm.Dispose(); try { pm.Dispose(); } catch { } };
    }

    float GroundY() { return ClientSize.Height - 70 * k; }

    void Spawn()
    {
        Token t = new Token();
        t.x = (30 + rnd.Next(Math.Max(10, (int)(ClientSize.Width / k) - 60))) * k;
        t.y = -20 * k;
        t.vy = (150 + rnd.Next(150) + elapsed * 4) * k;
        t.kind = rnd.Next(100) < 8 ? 1 : 0;
        tokens.Add(t);
    }

    void Restart()
    {
        score = 0; timeLeft = 30f; elapsed = 0; spawnAcc = 0; over = false; rewarded = false;
        tokens.Clear(); pops.Clear();
    }

    void Step()
    {
        DateTime now = DateTime.Now;
        float dt = Math.Min(0.05f, (float)(now - last).TotalSeconds); last = now;
        pm.Tick(dt);
        if (catchAnim > 0) catchAnim--;
        if (!over)
        {
            elapsed += dt; timeLeft -= dt;
            spawnAcc += dt * (1.3f + elapsed / 12f);
            while (spawnAcc >= 1f) { spawnAcc -= 1f; Spawn(); }
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                Token t = tokens[i];
                t.y += t.vy * dt;
                if (t.y > GroundY() + 20 * k) tokens.RemoveAt(i);          // verpasst
            }
            if (timeLeft <= 0)
            {
                over = true; tokens.Clear();
                if (!rewarded) { rewarded = true; best = owner.GameReward(score); }
            }
        }
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            float[] p = pops[i]; p[1] -= 46 * k * dt; p[2] -= dt * 1.4f;
            if (p[2] <= 0) pops.RemoveAt(i);
        }
        pm.S.state = catchAnim > 0 ? "ready" : "guest";
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (over)
        {
            if (btnAgain.Contains(e.Location)) Restart();
            else if (btnClose.Contains(e.Location)) Close();
            return;
        }
        Token hit = null; float bd = 36 * k;
        foreach (Token t in tokens)
        {
            float d = (float)Math.Sqrt((t.x - e.X) * (t.x - e.X) + (t.y - e.Y) * (t.y - e.Y));
            if (d < bd) { bd = d; hit = t; }
        }
        if (hit == null) return;
        int v = hit.kind == 1 ? 3 : 1;
        score += v; tokens.Remove(hit); catchAnim = 8;
        pops.Add(new float[] { hit.x, hit.y, 1f, v });
    }

    void DrawToken(Graphics g, Token t)
    {
        float r = 17 * k;
        Color c1 = t.kind == 1 ? Color.FromArgb(176, 120, 238) : Color.FromArgb(242, 198, 62);
        Color c2 = t.kind == 1 ? Color.FromArgb(120, 74, 190) : Color.FromArgb(200, 150, 30);
        using (Brush b = new SolidBrush(c1)) g.FillEllipse(b, t.x - r, t.y - r, 2 * r, 2 * r);
        using (Pen pn = new Pen(c2, 2.4f * k)) g.DrawEllipse(pn, t.x - r + 3 * k, t.y - r + 3 * k, 2 * r - 6 * k, 2 * r - 6 * k);
        using (Brush bt = new SolidBrush(c2))
        {
            SizeF z = g.MeasureString("T", fTok);
            g.DrawString("T", fTok, bt, t.x - z.Width / 2f + 1, t.y - z.Height / 2f);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        float W = ClientSize.Width, H = ClientSize.Height;
        using (Brush bf = new SolidBrush(pal.Fg), bd = new SolidBrush(pal.Dim), bt = new SolidBrush(pal.Track), bh = new SolidBrush(pal.Hover))
        using (Pen pb = new Pen(pal.Border))
        {
            g.DrawString("Punkte: " + score, fMid, bf, 16 * k, 12 * k);
            string rec = "Rekord " + Math.Max(best, score);
            SizeF rs = g.MeasureString(rec, fSmall);
            g.DrawString(rec, fSmall, bd, W - 16 * k - rs.Width, 16 * k);
            using (GraphicsPath tp = Gfx.Round(16 * k, 46 * k, W - 32 * k, 8 * k, 4 * k)) g.FillPath(bt, tp);
            float fw = Math.Max(8 * k, (W - 32 * k) * Math.Max(0f, timeLeft) / 30f);
            using (GraphicsPath fp = Gfx.Round(16 * k, 46 * k, fw, 8 * k, 4 * k)) g.FillPath(bh, fp);
            if (!over && elapsed < 4f) g.DrawString("Klicke auf die Tokens, bevor sie den Boden erreichen!", fSmall, bd, 16 * k, 62 * k);

            g.DrawLine(pb, 0, GroundY(), W, GroundY());
            foreach (Token t in tokens) DrawToken(g, t);

            // das Pet unten
            using (Bitmap pbmp = pm.Snap())
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(pbmp, (W - pbmp.Width) / 2f, H - pbmp.Height, pbmp.Width, pbmp.Height);
                g.PixelOffsetMode = PixelOffsetMode.Default;
            }

            foreach (float[] p in pops)
            {
                int a = (int)(Math.Max(0f, Math.Min(1f, p[2])) * 255);
                using (Brush bp = new SolidBrush(Color.FromArgb(a, pal.Hover))) g.DrawString("+" + (int)p[3], fTok, bp, p[0] - 8 * k, p[1] - 30 * k);
            }

            if (over)
            {
                using (Brush ov = new SolidBrush(Color.FromArgb(215, pal.Bg))) g.FillRectangle(ov, 0, 0, W, H);
                string t1 = "Fertig!";
                SizeF s1 = g.MeasureString(t1, fBig);
                g.DrawString(t1, fBig, bf, (W - s1.Width) / 2f, 130 * k);
                string t2 = "Gefangen: " + score + (score >= best && score > 0 ? "   Neuer Rekord!" : "   Rekord: " + best);
                SizeF s2 = g.MeasureString(t2, fMid);
                g.DrawString(t2, fMid, bd, (W - s2.Width) / 2f, 180 * k);
                btnAgain = new Rectangle((int)(W / 2 - 150 * k), (int)(240 * k), (int)(140 * k), (int)(40 * k));
                btnClose = new Rectangle((int)(W / 2 + 10 * k), (int)(240 * k), (int)(140 * k), (int)(40 * k));
                using (GraphicsPath p1 = Gfx.Round(btnAgain.X, btnAgain.Y, btnAgain.Width, btnAgain.Height, 10 * k)) g.FillPath(bh, p1);
                using (GraphicsPath p2 = Gfx.Round(btnClose.X, btnClose.Y, btnClose.Width, btnClose.Height, 10 * k)) { g.FillPath(bt, p2); g.DrawPath(pb, p2); }
                using (Brush bk = new SolidBrush(pal.HoverFg))
                {
                    SizeF a1 = g.MeasureString("Nochmal", fMid);
                    g.DrawString("Nochmal", fMid, bk, btnAgain.X + (btnAgain.Width - a1.Width) / 2f, btnAgain.Y + (btnAgain.Height - a1.Height) / 2f);
                }
                SizeF a2 = g.MeasureString("Schließen", fMid);
                g.DrawString("Schließen", fMid, bf, btnClose.X + (btnClose.Width - a2.Width) / 2f, btnClose.Y + (btnClose.Height - a2.Height) / 2f);
            }
        }
    }
}

// Anbindung ans Pet
public partial class PetForm
{
    CatchGame gameForm;

    void StartGame()
    {
        if (gameForm != null && !gameForm.IsDisposed) { gameForm.Activate(); return; }
        gameForm = new CatchGame(this, pal, baseSc, gameBest);
        gameForm.FormClosed += delegate { gameForm = null; };
        gameForm.Show();
        Say("Fang mir Tokens! Ich habe Hunger.", 4);
    }

    // Das Spiel ist vorbei: das Pet bekommt zu fressen und freut sich. Gibt den Rekord zurück.
    internal int GameReward(int score)
    {
        food = C(food + Math.Min(40, score * 2));
        joy = C(joy + 5 + Math.Min(30, score));
        if (score > gameBest) gameBest = score;
        if (score >= 20) Unlock("catcher");
        hop = 22; Burst("★", 3);
        Say(score >= 20 ? "Wow, " + score + " Tokens! Ich bin papp satt." : score > 0 ? "Danke für " + score + " Tokens!" : "Diesmal nichts? Beim nächsten Mal!", 6);
        SaveState();
        return gameBest;
    }
}
