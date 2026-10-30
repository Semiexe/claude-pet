using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// Verhalten: Werte, Aktionen, Maus und die Simulation (laufen, sitzen, strecken, winken, springen ...)
public partial class PetForm
{
    string bubble = "";
    DateTime bubbleUntil = DateTime.MinValue, nextChat = DateTime.Now.AddSeconds(25), nextSave = DateTime.Now.AddSeconds(30);
    DateTime lookUntil = DateTime.MinValue, hoverSince = DateTime.MinValue, nextWave = DateTime.MinValue;
    bool dragging, moved, walking;
    int lookDir, hop, walkFrames, idleFrames = 60, yawn, actFrames;
    float speed = 2f, walkAcc;
    string act = "none";                    // none | sit | stretch | wave
    Point dragOff, downAt;
    List<float[]> parts = new List<float[]>();
    DateTime bubbleStart = DateTime.MinValue, nextInfo = DateTime.MinValue;
    float thrx;                                              // Schwung zur Seite nach dem Werfen
    List<float[]> trail = new List<float[]>();               // letzte Mauspositionen beim Ziehen: Zeit (ms), x, y

    void Decay(float m)
    {
        if (sleeping)
        {
            energy = C(energy + m * 1.2f); food = C(food - m * 0.15f); joy = C(joy - m * 0.05f);
            if (energy >= 100 && !nightSleep) { sleeping = false; Say("Ausgeschlafen! Was machen wir?"); }
        }
        else
        {
            food = C(food - m * 0.35f);
            joy = C(joy - m * 0.25f - (food < 20 ? m * 0.3f : 0f));
            energy = C(energy - m * 0.18f);
            if (energy <= 0) sleeping = true;
        }
    }
    string Mood()
    {
        if (sleeping) return "sleep";
        if (food < 25) return "hungry";
        if (energy < 20) return "tired";
        if (joy < 30) return "sad";
        return "happy";
    }
    // Sprechblase: Der Text erscheint wie auf einer Schreibmaschine, dazu gibt es auf Wunsch Blips.
    // SayQ ist die stille Variante für Statusmeldungen, die oft kommen.
    void Say(string s) { Say(s, 4.5, true); }
    void Say(string s, double secs) { Say(s, secs, true); }
    void SayQ(string s, double secs) { Say(s, secs, false); }
    void Say(string s, double secs, bool voice)
    {
        bubble = s; bubbleStart = DateTime.Now;
        bubbleUntil = bubbleStart.AddSeconds(secs + (still ? 0 : s.Length / TypeSpeed));   // die Zeit zum Schreiben kommt dazu
        if (voice) Speak(s);
    }
    string Pick(params string[] a) { return a[rnd.Next(a.Length)]; }
    void Burst(string glyph, int n)
    {
        for (int i = 0; i < n; i++) parts.Add(new float[] { LW / 2f + rnd.Next(-30, 30), LH - 100 + rnd.Next(-20, 10), 1f, glyph == "♥" ? 0 : glyph == "★" ? 1 : 2 });
    }

    // ---- Aktionen ----
    void Feed()
    {
        if (sleeping) { Say("Pssst, ich schlafe."); return; }
        if (food > 92) { Say("Ich bin satt. Wirklich."); return; }
        food = C(food + 28); joy = C(joy + 4); hop = 22; act = "none"; actFrames = 0; Burst("•", 4);
        Say(Pick("Nom nom, lecker Tokens!", "Danke, das war nötig.", "Mehr davon! ...nein, genug."));
    }
    void Play()
    {
        if (sleeping) { Say("Pssst, ich schlafe."); return; }
        if (energy < 15) { Say("Ich bin zu müde zum Spielen."); return; }
        joy = C(joy + 25); energy = C(energy - 14); food = C(food - 6); hop = 22; act = "none"; actFrames = 0; Burst("★", 4);
        Say(Pick("Wieder! Wieder!", "Das war schön.", "Rätsel: Was hat Tokens und keinen Geldbeutel?"));
    }
    void ToggleSleep()
    {
        sleeping = !sleeping; walkFrames = 0; act = "none"; actFrames = 0;
        // Weckt man das Pet nachts, schläft es für den Rest der Nacht nicht von selbst wieder ein
        if (!sleeping && nightSleep || (!sleeping && DateTime.Now.Hour < 6)) nightOverride = DateTime.Now.ToString("yyyyMMdd");
        nightSleep = false;
        Say(sleeping ? "Gute Nacht..." : "Ich bin wach!");
    }
    void PetIt()
    {
        if (sleeping) { Say("Mmh... zzz..."); return; }
        joy = C(joy + 6); hop = 22; act = "none"; actFrames = 0; Burst("♥", 3);
        Say(Pick("Hihi!", "Das kitzelt!", "Mehr kraulen bitte."));
    }

    // ---- Maus ----
    void OnDown(object s, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            dragging = true; moved = false; downAt = Cursor.Position; thrx = 0; trail.Clear();
            dragOff = new Point(Cursor.Position.X - px, Cursor.Position.Y - py);
        }
        else if (e.Button == MouseButtons.Right) menu.Show(Cursor.Position);
    }
    void OnMove(object s, MouseEventArgs e)
    {
        if (!dragging) return;
        Point c = Cursor.Position;
        if (Math.Abs(c.X - downAt.X) + Math.Abs(c.Y - downAt.Y) > 4) moved = true;
        if (moved)
        {
            px = c.X - dragOff.X; py = c.Y - dragOff.Y; vy = 0; walkFrames = 0; act = "none"; actFrames = 0; jumping = false; supHwnd = IntPtr.Zero;
            // Mausweg der letzten Zehntelsekunden merken, um beim Loslassen den Schwung zu berechnen
            float ms = (float)(DateTime.Now - DateTime.Today).TotalMilliseconds;
            trail.Add(new float[] { ms, px, py });
            while (trail.Count > 0 && ms - trail[0][0] > 120) trail.RemoveAt(0);
        }
    }

    // Aus dem Mausweg die Fluggeschwindigkeit (Pixel pro Bild) bestimmen. Gibt false zurück, wenn kaum Schwung da ist.
    bool ThrowFromTrail()
    {
        if (trail.Count < 2) return false;
        float[] a = trail[0], b = trail[trail.Count - 1];
        float dt = b[0] - a[0];
        if (dt < 25 || DateTime.Now.TimeOfDay.TotalMilliseconds - b[0] > 100) return false;      // stand zuletzt still
        float vx = (b[1] - a[1]) / dt * 33f, vyf = (b[2] - a[2]) / dt * 33f, lim = 40 * sc;
        if (Math.Abs(vx) + Math.Abs(vyf) < 6 * sc) return false;
        thrx = Math.Max(-lim, Math.Min(lim, vx));
        vy = (int)Math.Max(-lim, Math.Min(lim, vyf));
        return true;
    }
    void OnUp(object s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || !dragging) return;
        dragging = false;
        if (!moved) { if (!((cState == "needs" || cState == "blocked" || cState == "ready") && GoToClaude())) PetIt(); }
        else if (ThrowFromTrail()) { if (!sleeping) Say(Pick("Wheeeee!", "Ich fliege!", "Huiii!"), 2); }
        else if (!sleeping) Say("Wheee!", 2);
    }

    // ---- Simulation, 30 Mal pro Sekunde ----
    void Step()
    {
        t += 0.033f;
        Decay(0.033f / 60f);
        DateTime now = DateTime.Now;
        Poll(now);
        if (now >= nextSave) { SaveState(); nextSave = now.AddSeconds(30); }
        if (now >= nextChat)
        {
            nextChat = now.AddSeconds(rnd.Next(35, 80));
            if (!dragging && now > bubbleUntil)
            {
                string m = Mood();
                if (m == "hungry") Say(Pick("Mein Kontextfenster ist leer. Futter?", "Ich brauche Tokens!", "Mein Magen knurrt wie ein Lüfter."));
                else if (m == "sad") Say(Pick("Mir ist langweilig. Spielst du mit mir?", "Fragst du mich mal was?"));
                else if (m == "tired") Say(Pick("Ich gähne in Tokens...", "Ein kleines Nickerchen wäre gut."));
                else if (m == "happy")
                {
                    string own = CustomPhrase();       // eigene Sätze mischen sich ein
                    Say(own != null && rnd.Next(100) < 50 ? own : Pick("Mir geht es gut.", "Soll ich dir etwas erklären?", "Ich denke nach. Über Kekse.", "Kleiner Spaziergang?"));
                }
            }
        }

        CheckFullscreen(now);
        CheckDaytime(now);
        CheckBattery(now);
        CheckReminders(now);
        UpdateSeason(now);
        RefreshPlatforms(now);
        Rectangle wa = AreaAt(new Point(px + W / 2, py + H / 2));
        int fo = FeetOff();
        bool attention = cState == "needs" || cState == "blocked";    // dann bleibt das Pet, wo es ist
        walking = false;
        if (sleeping || attention || dragging) { act = "none"; actFrames = 0; }
        if (attention || sleeping || still) exStep = -1;          // eine Frage oder Schlaf beendet die Übung
        CheckEvolution(now);
        CheckEyes(now);
        UpdateDance(now);
        bool busy = Dancing() || GazeActive() || evoFrames > 0;     // tanzt, schaut in die Ferne oder entwickelt sich: bleibt stehen
        if (busy) { walkFrames = 0; if (act != "none" && act != "sit") { act = "none"; actFrames = 0; } }
        if (evoFrames > 0) { evoFrames--; sleeping = false; if (evoFrames == 59) EvolutionDone(); }

        bool airborne = StepJump();
        int cxp = px + W / 2;
        bool standing = false;
        if (!airborne && !dragging)
        {
            // Steht das Pet auf einem Fenster, fährt es mit, wenn das Fenster verschoben wird
            if (supHwnd != IntPtr.Zero)
            {
                Plat sp = FindPlat(supHwnd);
                if (sp != null && cxp >= sp.l && cxp <= sp.r && Math.Abs(sp.top - (py + fo)) < 500 * sc) py = sp.top - fo;
                else supHwnd = IntPtr.Zero;
            }
            IntPtr supH;
            int supportY = SupportY(cxp, py + fo, wa, out supH);
            if (py + fo < supportY)
            {
                // in der Luft: fallen (oder fliegen), bis eine Fensterkante oder der Boden erreicht ist
                vy += (int)(2 * sc);
                if (Math.Abs(thrx) > 0.1f)
                {
                    // Schwung nach dem Werfen: seitwärts weiter, am Bildschirmrand abprallen
                    px += (int)Math.Round(thrx);
                    int lo = MinPx(wa), hi = MaxPx(wa);
                    if (px < lo) { px = lo; thrx = -thrx * 0.6f; }
                    else if (px > hi) { px = hi; thrx = -thrx * 0.6f; }
                }
                int impact = vy;
                py = Math.Min(py + vy, supportY - fo); walkFrames = 0; act = "none"; actFrames = 0;
                if (py + fo >= supportY)
                {
                    if (impact > 9 * sc) { vy = -(int)(impact * 0.42f); py = supportY - fo - 1; thrx *= 0.8f; }    // federt noch einmal nach
                    else { vy = 0; hop = 10; supHwnd = supH; thrx *= 0.5f; }
                }
            }
            else
            {
                py = supportY - fo; vy = 0; supHwnd = supH; standing = true;
                if (Math.Abs(thrx) > 0.4f)
                {
                    // nach der Landung noch ein Stück rutschen
                    px = Math.Max(MinPx(wa), Math.Min(MaxPx(wa), px + (int)Math.Round(thrx)));
                    thrx *= 0.85f;
                }
                else thrx = 0;
            }
        }

        // Zeigt die Maus auf das Pet, winkt es nach einer Weile
        Point cp = Cursor.Position;
        bool hover = !dragging && !sleeping && cp.X > px + W * 0.25f && cp.X < px + W * 0.75f && cp.Y > py + H * 0.4f && cp.Y < py + H;
        if (!hover) hoverSince = DateTime.MinValue;
        else if (hoverSince == DateTime.MinValue) hoverSince = now;
        else if ((now - hoverSince).TotalSeconds > 0.8 && cState != "none" && sessions.Count > 0 && now >= nextInfo)
        {
            // Maus über dem Pet, während Claude etwas tut: Laufzeit und letzte Schritte zeigen
            nextInfo = now.AddSeconds(8);
            SayQ(InfoText(), 6);
        }
        else if ((now - hoverSince).TotalSeconds > 1.6 && now >= nextWave && act == "none" && hop <= 0 && standing && !attention && !still && cState == "none")
        {
            act = "wave"; actFrames = 70; walkFrames = 0; nextWave = now.AddSeconds(25);
            Say(Pick("Hallo!", "Na, du?", "Hi!"), 2.5);
        }

        // Zuhause: die Kante des Fensters, in dem die wichtigste Sitzung läuft (wenn sie als Standfläche taugt)
        Plat home = (focusHwnd != IntPtr.Zero && (cState == "running" || cState == "needs" || cState == "blocked")) ? FindPlat(focusHwnd) : null;
        bool onHome = home != null && supHwnd == home.h;
        if (home != null && !onHome && standing && hop <= 0 && !still && !dragging && !sleeping && now >= nextHome)
        {
            nextHome = now.AddSeconds(10);
            Rectangle ha = AreaAt(new Point((home.l + home.r) / 2, home.top));
            if (ha == wa || walkFrames > 0 || !CrossTo(ha, wa, now)) StartJump(home, py + fo, px + W / 2);     // anderer Bildschirm: erst hinüberlaufen
        }

        if (standing && !jumping && !sleeping && hop <= 0 && !still && !attention && !busy && Math.Abs(thrx) < 0.4f)
        {
            if (exStep >= 0 && actFrames == 0 && now >= exAt) AdvanceExercise(now);
            if (ball != null && actFrames == 0 && walkFrames == 0) ChaseBall(wa, px + W / 2);      // dem Ball hinterherlaufen
            // bei der Arbeit sitzt das Pet gemütlich auf dem Fenster
            if (onHome && cState == "running" && act == "none" && actFrames == 0) { act = "sit"; actFrames = 400; walkFrames = 0; }
            if (actFrames > 0) { if (--actFrames == 0) act = "none"; }
            else if (walkFrames > 0)
            {
                walking = true;
                walkAcc += dir * speed * sc; int mv = (int)walkAcc; px += mv; walkAcc -= mv;
                walkFrames--;
                int cxn = px + W / 2;
                Plat sp2 = supHwnd != IntPtr.Zero ? FindPlat(supHwnd) : null;
                if (sp2 != null)
                {
                    // auf einem Fenster: an den Kanten umdrehen, manchmal bewusst hinunterspringen
                    if (!jumpOff) { if (cxn < sp2.l + 30 * sc) dir = 1; else if (cxn > sp2.r - 30 * sc) dir = -1; }
                }
                else
                {
                    if (px < wa.Left - 50 * sc && !HasNeighbor(wa, -1)) { dir = 1; }
                    if (px > wa.Right - W + 50 * sc && !HasNeighbor(wa, 1)) { dir = -1; }
                }
            }
            else if (--idleFrames <= 0) ChooseIdle(now, cp, py + fo);
        }
        bool sleepyNow = energy < 30 || now.Hour >= 22 || now.Hour < 6;
        if (!sleeping && sleepyNow && !walking && yawn <= 0 && rnd.Next(sleepyNow && energy >= 30 ? 1500 : 600) == 0) { yawn = 60; Say("Gääähn...", 3); }
        if (yawn > 0) yawn--;
        if (!sleeping && !dragging && !walking && !jumping && hop <= 0 && act == "none" && joy > 60 && rnd.Next(1000) == 0) hop = 22;
        if (hop > 0) hop--;
        UpdateMinis(now, py + fo, px + W / 2, wa);
        UpdateGuest(now, py + fo, px + W / 2, wa);
        UpdateBall(now, wa, standing, px + W / 2);
        for (int i = parts.Count - 1; i >= 0; i--)
        {
            float[] p = parts[i]; p[1] -= 1.2f; p[2] -= 0.02f;
            if (p[2] <= 0) parts.RemoveAt(i);
        }
    }

    // Wählt, was das Pet als Nächstes tut, wenn es nichts zu tun hat
    void ChooseIdle(DateTime now, Point cp, int feet)
    {
        idleFrames = rnd.Next(60, 200);
        int cxp = px + W / 2;
        int dx = cp.X - cxp, dy = cp.Y - (py + H / 2);
        bool cursorNear = Math.Abs(dx) > 160 * sc && Math.Abs(dx) < 800 * sc && Math.Abs(dy) < 500 * sc;
        int r = rnd.Next(100);
        if (cursorNear && r < 30)
        {
            // der Maus ein Stück entgegenlaufen
            dir = dx > 0 ? 1 : -1; speed = 2.6f; jumpOff = rnd.Next(100) < 25;
            walkFrames = Math.Max(20, (int)Math.Min(260, (Math.Abs(dx) - 120 * sc) / (speed * sc)));
        }
        else if (r < 42 && TryJump(feet, cxp)) { }
        else if (r < 62) { dir = rnd.Next(2) * 2 - 1; speed = 1.6f + rnd.Next(3) * 0.5f; walkFrames = rnd.Next(60, 220); jumpOff = rnd.Next(100) < 25; }
        else if (r < 76)
        {
            act = "sit"; actFrames = rnd.Next(150, 360);
            if (rnd.Next(100) < 25) Say(Pick("Ich ruhe mich kurz aus.", "Schöne Aussicht hier.", "Ich schaue den Pixeln beim Wandern zu."), 3.5);
        }
        else if (r < 85) { act = "stretch"; actFrames = 55; if (rnd.Next(100) < 30) Say("Streck...", 2); }
        else if (r < 94) { lookDir = rnd.Next(3) - 1; lookUntil = now.AddSeconds(1.5); }
    }
}
