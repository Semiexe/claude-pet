using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

// Zeichnen: Pixel-Sprite (24 x 16 Zellen), Status-Zeichen, Sprechblase, Ausgabe aufs Fenster
public partial class PetForm
{
    Brush bNeed = new SolidBrush(Color.FromArgb(255, 200, 40));
    Brush bBad = new SolidBrush(Color.FromArgb(214, 84, 72));
    Brush bOk = new SolidBrush(Color.FromArgb(80, 200, 120));
    Brush bPet = new SolidBrush(Color.FromArgb(219, 119, 98));
    Brush bShade = new SolidBrush(Color.FromArgb(196, 102, 84));
    Brush bBlush = new SolidBrush(Color.FromArgb(238, 150, 130));
    Brush bCompact = new SolidBrush(Color.FromArgb(170, 150, 230));
    Brush bBelly =new SolidBrush(Color.FromArgb(240, 150, 128));
    Brush bCape = new SolidBrush(Color.FromArgb(176, 58, 70));
    Brush bCapeShade = new SolidBrush(Color.FromArgb(146, 44, 58));
    Brush bNote = new SolidBrush(Color.FromArgb(252, 226, 120));
    Brush bNoteDark = new SolidBrush(Color.FromArgb(226, 192, 70));
    Brush bNoteLine = new SolidBrush(Color.FromArgb(150, 110, 40));
    Brush bEye = new SolidBrush(Color.FromArgb(42, 23, 16));
    Brush bShadow = new SolidBrush(Color.FromArgb(50, 0, 0, 0));
    Brush bBub = new SolidBrush(Color.FromArgb(245, 252, 250, 246));
    Brush bTxt = new SolidBrush(Color.FromArgb(31, 36, 48));
    Brush bZ = new SolidBrush(Color.FromArgb(230, 94, 102, 117));
    Pen pRay = new Pen(Color.FromArgb(185, 73, 42), 10f);
    Pen pMouth = new Pen(Color.FromArgb(42, 23, 16), 3.5f);
    Pen pBub = new Pen(Color.FromArgb(200, 140, 132, 120), 1.5f);
    Font fBub = new Font("Segoe UI", 9.5f);
    Font fSym = new Font("Segoe UI Symbol", 13f);
    Font fZ = new Font("Segoe UI", 15f, FontStyle.Bold);

    static readonly int[] LegCols = new int[] { 6, 10, 14, 18 };
    Brush bRun = new SolidBrush(Color.FromArgb(110, 150, 230));
    Brush bRunDim = new SolidBrush(Color.FromArgb(120, 110, 150, 230));

    // Kleine Pixel-Symbole je Werkzeug-Art; null = keines (dann drei Punkte)
    internal static string[] ToolIcon(string tool)
    {
        switch (tool)
        {
            case "read": return new string[] { ".###.", "#.#.#", ".###." };                                 // Auge
            case "write": return new string[] { "...##", "..##.", ".##..", "##...", "#...." };              // Stift
            case "search": return new string[] { ".###.", "#...#", "#...#", ".###.", "....#" };             // Lupe
            case "exec": return new string[] { "#.....", ".#....", "..#...", ".#.###", "#....." };         // > und _
            case "agent": return new string[] { "..#..", ".###.", "#.#.#", "#...#" };                       // Verzweigung
            default: return null;
        }
    }

    GraphicsPath RoundRect(float x, float y, float w, float h, float r)
    {
        return Gfx.Round(x, y, w, h, r);
    }

    // eine Zeile Sprite-Pixel: Spalte c, Zeile r, Breite w
    void Px(Graphics g, Brush b, int ox, int oy, int pu, int c, int r, int w)
    {
        g.FillRectangle(b, ox + c * pu, oy + r * pu, w * pu, pu);
    }

    void Draw(Graphics g)
    {
        EnsureStageColors();
        bool flash = evoFrames > 0 && EvoWhite();          // bei der Entwicklung blitzt die Figur weiß auf
        if (flash) WhiteBrushes(true);
        try { DrawCore(g); }
        finally { if (flash) WhiteBrushes(false); }
    }

    void DrawCore(Graphics g)
    {
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        string mood = Mood();
        float cx = LW / 2f, ground = LH - 16f;
        bool dance = Dancing(), gaze = GazeActive() && !sleeping;
        int stage = Stage();
        float lift = (hop > 0 ? (float)Math.Sin(Math.PI * (1 - hop / 22.0)) * 38f : 0f) + (dragging && moved ? 10f : 0f) + (dance ? beatPulse * 9f : 0f);
        if (evoFrames > 60) lift += (float)Math.Sin(evoFrames * 0.9) * 2f;          // zittert vor der Verwandlung
        if (Jelly() && !sleeping) lift += 8f + (walking ? 6f : 0f) + (float)Math.Sin(t * 2.2) * 2f;     // die Qualle schwebt, beim Laufen höher
        float topL = ground - (Jelly() ? 120f : 96f) - lift;      // die Qualle ist höher als das normale Sprite

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.ScaleTransform(sc, sc);
        float sw = Math.Max(60f, 124f - lift * 0.8f);
        g.FillEllipse(bShadow, cx - sw / 2, ground - 5, sw, 10);
        g.ResetTransform();

        // Sprite ohne Glättung in ganzen Pixeln
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        int pu = Math.Max(1, (int)Math.Round(6 * sc));
        int ox = (int)(cx * sc) - (24 * pu) / 2 + (dance ? (danceStep * 2 - 1) * Math.Max(1, pu / 2) : 0);     // wiegt sich im Takt
        int oy = (int)(ground * sc) - 16 * pu - (int)(lift * sc);
        int phase = ((int)(t * 8)) % 2;
        bool seated = sleeping || act == "sit";
        int bodyUp = (walking && phase == 0) ? Math.Max(1, pu / 2) : 0;
        int squeeze = cCompact && !sleeping ? (((int)(t * 4)) % 2) * pu : 0;        // beim Aufräumen drückt es sich zusammen
        int by = oy + ((seated && !Jelly()) ? 2 * pu : 0) - bodyUp + squeeze;

        // Möbel hinter dem Pet: Bett zum Schlafen, Napf bei Hunger
        if (sleeping) DrawBed(g, ox, oy, pu);
        else if (food < 60) DrawBowl(g, ox, oy, pu);

        // Mit der Stufe verändert sich das Pet: ab 3 Ohren, ab 5 ein heller Bauch, ab 8 ein Umhang
        int lv = Level();
        if (lv >= 8 && !Jelly())
        {
            for (int r = 2; r <= 14; r++) Px(g, bCape, ox, by, pu, 2, r, 20);
            Px(g, stage >= 1 ? bGoldS : bCapeShade, ox, by, pu, 2, 14, 20);
            if (stage >= 1) { Px(g, bGoldD, ox, by, pu, 2, 2, 1); Px(g, bGoldD, ox, by, pu, 21, 2, 1); }
        }

        if (Fox()) DrawFoxTail(g, ox, by, pu, stage);

        // Kopf und Körper in einem Stück, oben gerade, unten eine dunklere Kante
        if (Jelly()) { }                                     // die Qualle hat eine eigene Form und wird weiter unten gezeichnet
        else if (Fox()) DrawFoxBody(g, ox, by, pu, stage);
        else
        {
            for (int r = 0; r <= 11; r++) Px(g, bPet, ox, by, pu, 4, r, 16);
            Px(g, bShade, ox, by, pu, 4, 11, 16);
            if (lv >= 5) for (int r = 7; r <= 10; r++) Px(g, bBelly, ox, by, pu, 9, r, 6);
            if (stage >= 1) DrawClaudeEmblem(g, ox, by, pu);
        }
        DrawCtxGauge(g, ox, oy, by, pu);

        // Arme links und rechts
        int la = 5, ra = 5;
        if (sleeping) { la = 7; ra = 7; }
        else if (jumping) { la = 2; ra = 2; }
        else if (cCompact) { la = 2; ra = 2; }
        else if (dance) { la = danceStep == 0 ? 1 : 5; ra = danceStep == 0 ? 5 : 1; }       // abwechselnd ein Arm hoch
        else if (act == "sit") { la = 6; ra = 6; }
        else if (act == "stretch") { la = 1; ra = 1; }
        else if (act == "wave") { ra = 2 + ((int)(t * 10)) % 2; }
        else if (act == "shoulders") { int sp = ((int)(t * 1.8f)) % 2; la = 3 + sp * 2; ra = 5 - sp * 2; }
        else if (act == "breathe") { int br = (int)((Math.Sin(t * 1.6) + 1) * 1.5); la = 5 - br; ra = 5 - br; }
        else if (DateTime.Now < noteUntil) { la = 5; ra = 3; }                   // hält den Notizzettel hoch
        else if (hop > 0) { la = 3; ra = 3; }
        else if (cState == "blocked") { la = 6; ra = 6; }
        else if (cState == "needs") { int kn = ((int)(t * 14)) % 2; la = 3 + kn; ra = 4 - kn; }    // klopft
        else if (cState == "running") { int ty = ((int)(t * 12)) % 2; la = 4 + ty; ra = 5 - ty; }
        else if (walking) { if (phase == 0) la = 4; else ra = 4; }
        if (Jelly()) DrawJellyBody(g, ox, by, pu, la, ra, stage);
        if (!Jelly()) for (int r = 0; r < 4; r++) { Px(g, bPet, ox, by, pu, 0, la + r, 4); Px(g, bPet, ox, by, pu, 20, ra + r, 4); }
        if (!Jelly()) { Px(g, bShade, ox, by, pu, 0, la + 3, 4); Px(g, bShade, ox, by, pu, 20, ra + 3, 4); }
        if (Fox()) DrawFoxPaws(g, ox, by, pu, la, ra);
        if (stage >= 2)           // dritte Gestalt: goldene, mondhelle oder sternhelle Funken
            DrawStageSparks(g, ox, by, pu, Fox() ? bFoxMoon : Jelly() ? bJelG : bGoldS, Fox() ? bFoxWhite : Jelly() ? bJelStar : bSpark);

        // Beine (die Qualle hat keine); beim Fuchs sind die Füße dunkel
        if (!Jelly()) for (int i = 0; i < 4; i++)
        {
            bool lifted = (walking && (i % 2 == phase)) || (kickFrames > 0 && i == (dir > 0 ? 3 : 0));    // beim Kicken hebt ein Bein ab
            int r1 = seated ? 13 : (lifted ? 14 : 15);
            for (int r = 12; r <= r1; r++) Px(g, Fox() && r >= 14 ? bFoxDark : bPet, ox, oy + (seated ? 2 * pu : 0), pu, LegCols[i], r, 2);
        }

        // Helfer, wenn Claude Subagenten startet (bei mehreren Sitzungen zeigen die Mini-Pets ihre eigenen)
        if (sessions.Count > 0 && (sessions.Count < 2 || !minisOn || minis.Count == 0)) DrawHelpers(g, sessions[0].agents, ox, oy, pu);

        if (lv >= 3 && !Fox() && !Jelly())
        {
            Px(g, bPet, ox, by, pu, 5, -2, 1); Px(g, bPet, ox, by, pu, 5, -1, 2);      // Ohren
            Px(g, bPet, ox, by, pu, 18, -2, 1); Px(g, bPet, ox, by, pu, 17, -1, 2);
        }
        DrawHat(g, ox, by - (Jelly() ? 3 * pu : 0), pu);

        // Notizzettel der Erinnerung, über der rechten Hand
        if (DateTime.Now < noteUntil && !sleeping)
        {
            Px(g, bNoteDark, ox, by, pu, 20, -1, 4);
            for (int r = 0; r < 3; r++) Px(g, bNote, ox, by, pu, 20, r, 4);
            Px(g, bNoteLine, ox, by, pu, 21, 0, 2); Px(g, bNoteLine, ox, by, pu, 21, 1, 3); Px(g, bNoteLine, ox, by, pu, 21, 2, 1);
        }

        // Wangen, wenn das Pet sich freut
        if (!sleeping && (hop > 0 || joy > 85) && act != "stretch")
        {
            Px(g, bBlush, ox, by, pu, 6, 5, 2); Px(g, bBlush, ox, by, pu, 16, 5, 2);
        }

        // Augen: folgen der Maus, sonst in Laufrichtung
        bool blink = (t % 5f) < 0.14f;
        int look = dir;
        if (!walking)
        {
            Point cp = System.Windows.Forms.Cursor.Position;
            int dx = cp.X - (px + W / 2), dy = cp.Y - (py + H / 2);
            if (Math.Abs(dx) < 600 * sc && Math.Abs(dy) < 450 * sc) look = Math.Abs(dx) <= 40 * sc ? 0 : (dx > 0 ? 1 : -1);
            else if (DateTime.Now < lookUntil) look = lookDir;
        }
        bool shut = sleeping || blink || yawn > 0 || cCompact || act == "stretch" || act == "breathe" || mood == "tired" || dance;
        int eby = by - (gaze ? pu : 0);                                    // in die Ferne schauen: Blick nach oben
        foreach (int e0 in new int[] { 5, 15 })
        {
            int e = e0 + (sleeping ? 0 : gaze ? 1 : (act == "look" ? (((int)t) % 2 == 0 ? -1 : 1) : look));
            if (!sleeping && cState == "needs" && !blink && yawn <= 0 && !dance && !gaze) { for (int r = 1; r <= 3; r++) Px(g, bEye, ox, by, pu, e + 1, r, 2); }
            else if (dance)
            {
                // vergnügt zugekniffen: nach oben gewölbte Bögen
                Px(g, bEye, ox, by, pu, e, 3, 1); Px(g, bEye, ox, by, pu, e + 1, 2, 2); Px(g, bEye, ox, by, pu, e + 3, 3, 1);
            }
            else if (shut) Px(g, bEye, ox, by, pu, e, 3, 4);
            else if (mood == "sad" || mood == "hungry" || cState == "blocked")
            {
                Px(g, bEye, ox, by, pu, e, 2, 1); Px(g, bEye, ox, by, pu, e + 3, 2, 1);
                Px(g, bEye, ox, by, pu, e + 1, 3, 2);
            }
            else
            {
                Px(g, bEye, ox, eby, pu, e + 1, 2, 2);
                Px(g, bEye, ox, eby, pu, e, 3, 1); Px(g, bEye, ox, eby, pu, e + 3, 3, 1);
            }
        }
        if (gaze) DrawGazeBar(g, ox, by, pu);

        if (!sleeping && CurrentHat() == "shades") DrawShades(g, ox, by, pu);

        // Mund
        if (yawn > 0) { int yh = yawn > 40 ? 2 : (yawn > 14 ? 3 : 1); for (int r = 0; r < yh; r++) Px(g, bEye, ox, by, pu, 11, (Jelly() ? 4 : 6) + r, 2); }
        if ((hop > 0 || dance) && !sleeping)
        {
            Px(g, bEye, ox, by, pu, 10, 5, 1); Px(g, bEye, ox, by, pu, 13, 5, 1); Px(g, bEye, ox, by, pu, 11, 6, 2);
        }
        else if (mood == "hungry" && !sleeping)
        {
            Px(g, bEye, ox, by, pu, 11, 6, 2); Px(g, bEye, ox, by, pu, 11, 7, 2);
        }

        // Status-Zeichen über dem Kopf
        string[] tico = cState == "running" ? ToolIcon(cTool) : null;
        if (cCompact)
        {
            // zwei Pfeile, die sich zusammenschieben: der Kontext wird zusammengefasst
            string[] press = new string[] { "#...#", ".#.#.", "..#..", ".....", "..#..", ".#.#.", "#...#" };
            for (int r = 0; r < press.Length; r++)
                for (int c = 0; c < press[r].Length; c++)
                    if (press[r][c] == '#') Px(g, bCompact, ox, by, pu, 18 + c, -10 + r, 1);
        }
        else if (tico != null)
        {
            // Symbol für das, was Claude gerade tut; blinkt leicht
            bool lit = ((int)(t * 2.5f)) % 2 == 0;
            Brush bti = lit ? bRun : bRunDim;
            for (int r = 0; r < tico.Length; r++)
                for (int c = 0; c < tico[r].Length; c++)
                    if (tico[r][c] == '#') Px(g, bti, ox, by, pu, 17 + c, -9 + r, 1);
        }
        else if (cState == "running")
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = ((int)(t * 3)) % 3 == i;
                using (Brush bd = new SolidBrush(Color.FromArgb(on ? 255 : 110, 110, 150, 230)))
                    for (int r = 0; r < 2; r++) Px(g, bd, ox, by, pu, 16 + i * 3, -4 + r, 2);
            }
        }
        else if (cState == "needs" || cState == "ready" || cState == "blocked")
        {
            string[] icon = cState == "needs"
                ? new string[] { "##", "##", "##", "##", "..", "##" }
                : cState == "blocked" ? new string[] { "#...#", ".#.#.", "..#..", ".#.#.", "#...#" } : new string[] { "......#", ".....##", "#...##.", "##.##..", ".###...", "..#...." };
            int bx0 = cState == "needs" ? 18 : 17;
            int by0 = -9 - (cState == "needs" ? (int)(Math.Abs(Math.Sin(t * 6)) * 2) : 0);
            Brush bi = cState == "needs" ? bNeed : cState == "blocked" ? bBad : bOk;
            for (int r = 0; r < icon.Length; r++)
                for (int c = 0; c < icon[r].Length; c++)
                    if (icon[r][c] == '#') Px(g, bi, ox, by, pu, bx0 + c, by0 + r, 1);
        }

        // Bei mehreren Sitzungen: je ein Feld pro Sitzung links über dem Kopf
        if (sessions.Count >= 2 && !minisOn)
        {
            int n = 0;
            foreach (Sess s in sessions)
            {
                if (focusMode && s.state == "running") continue;
                if (n >= 5) break;
                Brush bs = s.state == "needs" ? bNeed : s.state == "blocked" ? bBad : s.state == "ready" ? bOk : bRun;
                for (int r = 0; r < 2; r++) Px(g, bs, ox, by, pu, 4 + n * 3, -8 + r, 2);
                n++;
            }
        }

        // Rest mit Glättung und in logischen Koordinaten
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.Default;
        g.ScaleTransform(sc, sc);
        if (sleeping)
        {
            float zy = topL + 4 + (float)Math.Sin(t * 2) * 4;
            g.DrawString("z", fZ, bZ, cx + 56, zy);
            g.DrawString("z", fSym, bZ, cx + 74, zy - 14);
        }
        foreach (float[] p in parts)
        {
            int a = (int)(Math.Max(0, Math.Min(1, p[2])) * 255);
            string ch = p[3] == 0 ? "♥" : p[3] == 1 ? "★" : "•";
            Color c = p[3] == 0 ? Color.FromArgb(a, 226, 74, 92) : p[3] == 1 ? Color.FromArgb(a, 230, 170, 30) : Color.FromArgb(a, 150, 110, 70);
            using (Brush b = new SolidBrush(c)) g.DrawString(ch, fSym, b, p[0], p[1]);
        }

        DrawSeason(g);

        if (DateTime.Now < bubbleUntil && bubble.Length > 0)
        {
            SizeF sz = g.MeasureString(bubble, fBub, 190);
            float bw = sz.Width + 18, bh = sz.Height + 10;
            float bx = cx - bw / 2, byy = Math.Max(2f, topL - 14 - (cState != "none" ? 40 : 0) - bh);
            using (GraphicsPath gp = RoundRect(bx, byy, bw, bh, 10))
            {
                g.FillPath(bBub, gp); g.DrawPath(pBub, gp);
            }
            PointF[] tail = new PointF[] { new PointF(cx - 6, byy + bh - 1), new PointF(cx + 6, byy + bh - 1), new PointF(cx, byy + bh + 8) };
            g.FillPolygon(bBub, tail);
            g.DrawLine(pBub, tail[0], tail[2]); g.DrawLine(pBub, tail[1], tail[2]);
            g.FillRectangle(bBub, cx - 5, byy + bh - 2, 10, 2);
            // Schreibmaschine: der Text erscheint Buchstabe für Buchstabe (die Blase hat gleich die volle Größe)
            int shown = still ? bubble.Length : (int)Math.Min(bubble.Length, (DateTime.Now - bubbleStart).TotalSeconds * TypeSpeed);
            g.DrawString(bubble.Substring(0, Math.Max(0, shown)), fBub, bTxt, new RectangleF(bx + 9, byy + 5, sz.Width + 2, sz.Height));
        }
    }

    // Zeichnet ein Bild und legt es mit Transparenz aufs Fenster
    void Render()
    {
        lastRX = px; lastRY = py;
        using (Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); Draw(g); }
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr mem = CreateCompatibleDC(screen);
            IntPtr hb = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = SelectObject(mem, hb);
            SIZE sz = new SIZE(); sz.cx = W; sz.cy = H;
            POINT src = new POINT();
            POINT dst = new POINT(); dst.x = px; dst.y = py;
            BLEND bl = new BLEND(); bl.Op = 0; bl.Flags = 0; bl.Alpha = 255; bl.Format = 1;
            UpdateLayeredWindow(Handle, screen, ref dst, ref sz, mem, ref src, 0, ref bl, 2);
            SelectObject(mem, old); DeleteObject(hb); DeleteDC(mem); ReleaseDC(IntPtr.Zero, screen);
        }
    }
}
