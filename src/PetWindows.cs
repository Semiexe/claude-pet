using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Eine freie Fensterkante, auf der das Pet stehen kann
class Plat
{
    public IntPtr h;
    public int l, r, top;
}

// Fenster als Standfläche: Das Pet landet auf Fenstern, springt hinauf und fährt mit, wenn du sie verschiebst.
// Es werden nur zwei- bis dreimal pro Sekunde die sichtbaren Fenster abgefragt.
public partial class PetForm
{
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);

    List<Plat> plats = new List<Plat>();
    DateTime nextPlat = DateTime.MinValue;
    IntPtr supHwnd, jTarget;                    // Fenster, auf dem das Pet steht / auf das es springt
    bool jumping, jumpOff;
    int jx0, jy0, jx1, jy1, jf, jt;

    int FeetOff() { return (int)((LH - 16) * sc); }

    // Sichtbare Oberkanten aller normalen Fenster, ohne die Teile, die andere Fenster verdecken
    void RefreshPlatforms(DateTime now)
    {
        if (now < nextPlat) return;
        nextPlat = now.AddMilliseconds(supHwnd != IntPtr.Zero ? 100 : 400);   // beim Mitfahren öfter prüfen
        List<Plat> res = new List<Plat>();
        List<RECT> above = new List<RECT>();
        IntPtr self = IsHandleCreated ? Handle : IntPtr.Zero;
        int minW = (int)(160 * sc), minTop = (int)(140 * sc), minBelow = (int)(60 * sc);
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            try
            {
                if (h == self || !IsWindowVisible(h) || IsIconic(h)) return true;
                int ex = GetWindowLong(h, -20);
                if ((ex & 0x80) != 0 || (ex & 0x08000000) != 0) return true;          // Werkzeug- und Nicht-aktivierbare Fenster
                int cloaked;
                if (DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;   // versteckt (anderer Desktop)
                if (GetWindowTextLength(h) == 0) return true;
                StringBuilder sb = new StringBuilder(64); GetClassName(h, sb, 64);
                string cn = sb.ToString();
                if (cn == "Progman" || cn == "WorkerW" || cn == "Shell_TrayWnd" || cn == "Shell_SecondaryTrayWnd") return true;
                RECT r;
                if (DwmGetWindowAttribute(h, 9, out r, 16) != 0) GetWindowRect(h, out r);
                if (r.Right - r.Left < 40 || r.Bottom - r.Top < 40) return true;

                // Teile der Oberkante, die kein höheres Fenster verdeckt
                List<int[]> iv = new List<int[]>(); iv.Add(new int[] { r.Left, r.Right });
                foreach (RECT a in above)
                {
                    if (a.Top > r.Top || a.Bottom < r.Top) continue;
                    List<int[]> next = new List<int[]>();
                    foreach (int[] s in iv)
                    {
                        if (a.Right <= s[0] || a.Left >= s[1]) { next.Add(s); continue; }
                        if (a.Left > s[0]) next.Add(new int[] { s[0], a.Left });
                        if (a.Right < s[1]) next.Add(new int[] { a.Right, s[1] });
                    }
                    iv = next;
                }
                above.Add(r);

                Rectangle wa = AreaAt(new Point((r.Left + r.Right) / 2, r.Top + 10));
                if (r.Top < wa.Top + minTop || r.Top > wa.Bottom - minBelow) return true;   // zu weit oben (Pet wäre abgeschnitten) oder unten
                int bl = 0, br = 0;
                foreach (int[] s in iv) if (s[1] - s[0] > br - bl) { bl = s[0]; br = s[1]; }
                if (br - bl < minW) return true;
                Plat p = new Plat(); p.h = h; p.l = bl; p.r = br; p.top = r.Top;
                res.Add(p);
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        plats = res;
    }

    Plat FindPlat(IntPtr h)
    {
        foreach (Plat p in plats) if (p.h == h) return p;
        return null;
    }

    // Höhe der Fläche, auf der das Pet bei x = cx landet (Fensterkante oder Boden)
    int SupportY(int cx, int feet, Rectangle wa, out IntPtr hw)
    {
        int best = wa.Bottom - (int)(2 * sc);
        hw = IntPtr.Zero;
        int tol = (int)(8 * sc);
        foreach (Plat p in plats)
            if (cx >= p.l && cx <= p.r && p.top >= feet - tol && p.top < best) { best = p.top; hw = p.h; }
        return best;
    }

    // Springt auf ein höheres Fenster in der Nähe, falls es eines gibt
    bool TryJump(int feet, int cxp)
    {
        if (still || plats.Count == 0) return false;
        List<Plat> c = new List<Plat>();
        foreach (Plat p in plats)
        {
            if (p.top >= feet - (int)(50 * sc) || feet - p.top > (int)(700 * sc)) continue;
            int d = cxp < p.l ? p.l - cxp : cxp > p.r ? cxp - p.r : 0;
            if (d < 420 * sc) c.Add(p);
        }
        if (c.Count == 0) return false;
        StartJump(c[rnd.Next(c.Count)], feet, cxp);
        return true;
    }

    // Startet einen Sprung auf eine Fensterkante
    void StartJump(Plat tp, int feet, int cxp)
    {
        int margin = Math.Min((int)(90 * sc), (tp.r - tp.l) / 3);
        int tx = Math.Max(tp.l + margin, Math.Min(tp.r - margin, cxp));
        jx0 = px; jy0 = py; jx1 = tx - W / 2; jy1 = tp.top - FeetOff();
        jt = Math.Max(24, Math.Min(60, 24 + (int)(Math.Abs(tx - cxp) / (12 * sc)) + (feet - tp.top) / (int)(20 * sc)));
        jf = 0; jumping = true; jTarget = tp.h;
        dir = tx > cxp ? 1 : -1; walkFrames = 0; act = "none"; actFrames = 0;
        Say(Pick("Hopp!", "Und hoch!", "Da oben ist die Aussicht besser."), 2);
    }

    // ein Schritt des Sprungs; true, solange er läuft
    bool StepJump()
    {
        if (!jumping) return false;
        jf++;
        float u = Math.Min(1f, jf / (float)jt);
        float arch = (float)Math.Max(60 * sc, Math.Abs(jy0 - jy1) * 0.25 + 40 * sc);
        px = (int)(jx0 + (jx1 - jx0) * u);
        py = (int)(jy0 + (jy1 - jy0) * u - Math.Sin(Math.PI * u) * arch);
        if (jf >= jt) { jumping = false; px = jx1; py = jy1; supHwnd = jTarget; vy = 0; hop = 8; }
        return true;
    }
}
