using System;
using System.Drawing;
using System.Runtime.InteropServices;

// Augenpause nach der 20-20-20-Regel: Nach 20 Minuten Arbeit am Rechner schaut man 20 Sekunden auf etwas in 6 Metern Entfernung.
// Das Pet erinnert daran, schaut selbst in die Ferne und zeigt einen Balken, der sich in 20 Sekunden füllt.
public partial class PetForm
{
    [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    bool eyesOn = true;
    double eyeActive;                          // Sekunden Arbeit seit der letzten Augenpause
    DateTime eyeTick = DateTime.MinValue, eyeUntil = DateTime.MinValue;
    const int EyeEvery = 20 * 60, EyeLen = 20;

    // Sekunden seit der letzten Eingabe mit Maus oder Tastatur (nur der Zeitpunkt, nie was getippt wurde)
    static double IdleSeconds()
    {
        try
        {
            LASTINPUTINFO li = new LASTINPUTINFO(); li.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref li)) return 0;
            return (uint)(Environment.TickCount - (int)li.dwTime) / 1000.0;
        }
        catch { return 0; }
    }

    bool GazeActive() { return DateTime.Now < eyeUntil; }

    void CheckEyes(DateTime now)
    {
        if (now < eyeTick) return;
        double dt = eyeTick == DateTime.MinValue ? 0 : (now - eyeTick).TotalSeconds + 1;      // gezählt wird im Sekundentakt
        eyeTick = now.AddSeconds(1);
        if (!eyesOn || autoHidden) { eyeUntil = DateTime.MinValue; return; }
        if (GazeActive())
        {
            if (sleeping || cState == "needs" || cState == "blocked" || dragging) eyeUntil = DateTime.MinValue;       // eine Frage geht vor
            return;
        }
        if (eyeUntil != DateTime.MinValue)
        {
            // gerade fertig geworden
            eyeUntil = DateTime.MinValue; eyeActive = 0; hop = 22; Burst("♥", 3);
            Say(Pick("Danke! Deine Augen sagen auch danke.", "Gut gemacht! Weiter geht's.", "Das war gut für die Augen."), 4);
            return;
        }
        double idle = IdleSeconds();
        if (idle < 60) eyeActive += dt; else if (idle > 300) eyeActive = 0;     // wer fünf Minuten weg war, hatte seine Pause
        if (eyeActive >= EyeEvery) StartGaze(now);
    }

    void StartGaze(DateTime now)
    {
        if (sleeping || still || cState == "needs" || cState == "blocked" || dragging || exStep >= 0 || cCompact) return;   // später nochmal
        eyeUntil = now.AddSeconds(EyeLen);
        walkFrames = 0; thrx = 0; yawn = 0; hop = 0;
        Say("20-20-20: Schau jetzt 20 Sekunden auf etwas,\ndas 6 Meter weit weg ist.\nIch schaue mit.", 6);
    }

    // Balken über dem Kopf, der sich in 20 Sekunden füllt
    void DrawGazeBar(Graphics g, int ox, int by, int pu)
    {
        double left = (eyeUntil - DateTime.Now).TotalSeconds;
        double done = Math.Max(0, Math.Min(1, 1 - left / EyeLen));
        int x0 = ox + 7 * pu, y0 = by - 5 * pu, w = 10 * pu;
        g.FillRectangle(bBub, x0 - 2, y0 - 2, w + 4, pu + 4);
        g.FillRectangle(bRunDim, x0, y0, w, pu);
        g.FillRectangle(bRun, x0, y0, (int)(w * done), pu);
    }
}
