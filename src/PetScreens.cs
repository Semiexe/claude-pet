using System;
using System.Drawing;
using System.Windows.Forms;

// Mehrere Bildschirme: Das Pet läuft über die Kante auf den Nachbarbildschirm und folgt Claude auf einen anderen Monitor.
public partial class PetForm
{
    Rectangle[] fakeAreas;                          // nur für Tests: eigene Bildschirme vorgeben
    Rectangle[] areaCache;
    DateTime areaAt = DateTime.MinValue;

    // Arbeitsbereiche aller Bildschirme (ohne Taskleiste); alle zwei Sekunden neu gelesen, falls ein Monitor dazukommt
    Rectangle[] Areas()
    {
        if (fakeAreas != null) return fakeAreas;
        DateTime now = DateTime.Now;
        if (areaCache == null || (now - areaAt).TotalSeconds > 2)
        {
            try
            {
                Screen[] all = Screen.AllScreens;
                Rectangle[] r = new Rectangle[all.Length];
                for (int i = 0; i < all.Length; i++) r[i] = all[i].WorkingArea;
                areaCache = r.Length > 0 ? r : new Rectangle[] { Screen.PrimaryScreen.WorkingArea };
            }
            catch { areaCache = new Rectangle[] { new Rectangle(0, 0, 1920, 1040) }; }
            areaAt = now;
        }
        return areaCache;
    }

    // Der Bildschirm, auf dem der Punkt liegt (sonst der nächste)
    Rectangle AreaAt(Point p)
    {
        Rectangle[] a = Areas();
        Rectangle best = a[0];
        double bd = double.MaxValue;
        foreach (Rectangle r in a)
        {
            if (r.Contains(p)) return r;
            double dx = p.X - (r.Left + r.Width / 2.0), dy = p.Y - (r.Top + r.Height / 2.0), d = dx * dx + dy * dy;
            if (d < bd) { bd = d; best = r; }
        }
        return best;
    }

    // Grenzt auf dieser Seite (-1 links, 1 rechts) ein weiterer Bildschirm an?
    bool HasNeighbor(Rectangle wa, int side)
    {
        foreach (Rectangle r in Areas())
        {
            if (r == wa || r.Top >= wa.Bottom || r.Bottom <= wa.Top) continue;
            if (side < 0 && Math.Abs(r.Right - wa.Left) <= 4) return true;
            if (side > 0 && Math.Abs(r.Left - wa.Right) <= 4) return true;
        }
        return false;
    }

    // Grenzen für die linke Fensterkante des Pets auf diesem Bildschirm: zum Nachbarn hin offen
    int MinPx(Rectangle wa) { return HasNeighbor(wa, -1) ? wa.Left - W : wa.Left; }
    int MaxPx(Rectangle wa) { return HasNeighbor(wa, 1) ? wa.Right : wa.Right - W; }

    // Liegt das Ziel (ein Fenster von Claude) auf einem anderen Bildschirm, läuft das Pet erst hinüber, statt quer zu springen.
    // false, wenn der Weg nicht über die Seitenkante führt (dann springt es).
    bool CrossTo(Rectangle target, Rectangle wa, DateTime now)
    {
        int d = target.Left >= wa.Right - 4 ? 1 : target.Right <= wa.Left + 4 ? -1 : 0;
        if (d == 0 || !HasNeighbor(wa, d)) return false;
        int cxp = px + W / 2;
        float spd = 3.4f;
        float dist = d > 0 ? wa.Right - cxp : cxp - wa.Left;
        dir = d; speed = spd; jumpOff = false;
        walkFrames = Math.Max(30, Math.Min(900, (int)((dist + W * 0.7f) / (spd * sc)) + 1));
        nextHome = now.AddSeconds(2);
        Say(Pick("Ich komme rüber zu Claude!", "Ich laufe zum anderen Bildschirm."), 3);
        return true;
    }

    // Test: Bildschirme vorgeben und schauen, ob das Pet hinüberläuft
    public static string TestScreens()
    {
        PetForm f = new PetForm(true);
        f.fakeAreas = new Rectangle[] { new Rectangle(0, 0, 1000, 700), new Rectangle(1000, -40, 1200, 740) };
        f.sleeping = false; f.food = 80; f.joy = 80; f.energy = 90; f.nextPlat = DateTime.MaxValue; f.danceOn = false; f.eyesOn = false;
        int w = f.W;
        f.px = 800; f.py = 700 - f.FeetOff() - 2;
        for (int i = 0; i < 5; i++) f.Step();
        string a = "start px=" + f.px + " py=" + f.py;
        f.dir = 1; f.speed = 3f; f.walkFrames = 400;
        int maxPx = f.px;
        for (int i = 0; i < 400; i++) { f.Step(); if (f.px > maxPx) maxPx = f.px; }
        string b = " nachLauf px=" + f.px + " max=" + maxPx + " bereich=" + f.AreaAt(new Point(f.px + w / 2, f.py + f.H / 2));
        // einseitig: am äußeren Rand dreht es um
        f.px = 2100; f.dir = 1; f.speed = 3f; f.walkFrames = 300;
        int maxOuter = f.px;
        for (int i = 0; i < 300; i++) { f.Step(); if (f.px > maxOuter) maxOuter = f.px; }
        string c = " aussenrand max=" + maxOuter + " (Grenze " + (2200 - w + 50 * f.sc) + ")";
        // Claude-Fenster auf dem anderen Bildschirm: erst laufen
        f.px = 300; f.py = 700 - f.FeetOff() - 2; f.walkFrames = 0; f.actFrames = 0; f.act = "none"; f.nextHome = DateTime.MinValue;
        Plat p = new Plat(); p.h = new IntPtr(777); p.l = 1300; p.r = 2000; p.top = 400;
        f.plats.Add(p); f.focusHwnd = p.h; f.cState = "running";
        f.nextPlat = DateTime.Now.AddHours(1);
        bool cr = f.CrossTo(f.AreaAt(new Point(1600, 400)), f.AreaAt(new Point(f.px + w / 2, f.py + f.H / 2)), DateTime.Now);
        string d = " crossTo=" + cr + " dir=" + f.dir + " frames=" + f.walkFrames;
        return a + b + c + d;
    }
}
