using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Kleine Helfer: Pausen-Erinnerung, Töne, Blip-Stimme, Akku-Warnung, Ausblenden bei Vollbild
public partial class PetForm
{
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();

    // Einstellungen (im Menü unter "Einstellungen", werden gespeichert)
    bool minisOn = true, breakOn = true, autoHideOn = true, soundOn = true, voiceOn = true, batteryOn = true;

    DateTime workSince = DateTime.MinValue, lastActive = DateTime.MinValue, lastBreakMsg = DateTime.MinValue;
    DateTime nextFs = DateTime.MinValue, nextHome = DateTime.MinValue, nextBat = DateTime.MinValue, nextVoice = DateTime.MinValue;
    bool autoHidden, userHidden;
    int batWarned;                                   // zuletzt gewarnte Stufe (20, 10, 5 Prozent)
    SoundPlayer chime, chimeBig, voicePlayer;

    // ---- Weniger Rechenzeit im Leerlauf ----
    Point lastCur; int lastRX = int.MinValue, lastRY, idleTick;

    // Läuft gerade etwas, das flüssig aussehen muss? Sonst reicht ein Bild alle drei Takte.
    bool NeedFast()
    {
        DateTime now = DateTime.Now;
        Point c = Cursor.Position;
        bool curMoved = c != lastCur; lastCur = c;
        // Die Augen folgen der Maus, aber nur in der Nähe des Pets
        bool eyes = curMoved && Math.Abs(c.X - (px + W / 2)) < 650 * sc && Math.Abs(c.Y - (py + H / 2)) < 500 * sc;
        return dragging || jumping || walking || hop > 0 || act != "none" || yawn > 0 || parts.Count > 0 || seasonParts.Count > 0
            || kickFrames > 0 || cState != "none" || cCompact || now < bubbleUntil || now < noteUntil || ball != null
            || minis.Count > 0 || guest != null || exStep >= 0 || Jelly() || Math.Abs(thrx) > 0.1f
            || Dancing() || GazeActive() || evoFrames > 0 || beatPulse > 0
            || px != lastRX || py != lastRY || eyes;
    }

    // Ist das Fenster dieser Sitzung gerade im Vordergrund?
    static bool ForegroundIs(long hwnd)
    {
        try { return hwnd != 0 && GetForegroundWindow() == new IntPtr(hwnd); } catch { return false; }
    }

    // Schalter "Antwort-Vorschau": Das Hook-Skript schreibt Claudes erste Antwortzeile nur, solange diese Datei existiert
    static string PreviewFlag() { return Path.Combine(DataDir(), "preview.on"); }
    static bool PreviewOn() { return File.Exists(PreviewFlag()); }
    static void SetPreview(bool on)
    {
        try
        {
            if (on) { Directory.CreateDirectory(DataDir()); File.WriteAllText(PreviewFlag(), "1"); }
            else if (File.Exists(PreviewFlag())) File.Delete(PreviewFlag());
        }
        catch { }
    }

    // Wie lange arbeitet Claude schon am Stück? Nach etwa 90 Minuten kommt ein Vorschlag für eine Pause.
    void TrackWork(DateTime now)
    {
        bool active = cRun + cNeed + cBlock > 0;
        if (!active) return;
        if (now - lastActive > TimeSpan.FromMinutes(10) || workSince == DateTime.MinValue) workSince = now;
        lastActive = now;
        if (!breakOn || autoHidden) return;
        if (now - workSince >= TimeSpan.FromMinutes(90) && now - lastBreakMsg >= TimeSpan.FromMinutes(60))
        {
            lastBreakMsg = now;
            int mins = (int)(now - workSince).TotalMinutes;
            string dauer = mins >= 120 ? (mins / 60) + " Std. " + (mins % 60) + " Min." : mins + " Minuten";
            hop = 22;
            StartExercise("Ihr arbeitet seit " + dauer + " zusammen.\nZeit für eine kleine Pause!\nMach die Übung einfach mit.", 7);
        }
    }

    // Blendet das Pet aus, solange ein Vollbildprogramm, Spiel oder eine Präsentation läuft
    void CheckFullscreen(DateTime now)
    {
        if (now < nextFs) return;
        nextFs = now.AddSeconds(2);
        if (!autoHideOn)
        {
            if (autoHidden) { autoHidden = false; if (!userHidden) Show(); }
            return;
        }
        int st;
        try { if (SHQueryUserNotificationState(out st) != 0) return; } catch { return; }
        bool full = st == 2 || st == 3 || st == 4;       // Vollbild-App, Direct3D-Vollbild, Präsentation
        if (full && Visible && !autoHidden) { autoHidden = true; Hide(); }
        else if (!full && autoHidden) { autoHidden = false; if (!userHidden) Show(); }
    }

    // ---- Akku: Das Pet "hat Hunger", wenn der Akku leer wird ----
    void CheckBattery(DateTime now)
    {
        if (now < nextBat) return;
        nextBat = now.AddSeconds(30);
        try
        {
            PowerStatus ps = SystemInformation.PowerStatus;
            bool has = (ps.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0 && ps.BatteryChargeStatus != BatteryChargeStatus.Unknown;
            CheckBatteryValues(ps.BatteryLifePercent, ps.PowerLineStatus == PowerLineStatus.Online, has);
        }
        catch { }
    }
    // pct: 0..1. Warnt bei 20, 10 und 5 Prozent, einmal je Stufe, und freut sich, wenn wieder Strom da ist.
    internal void CheckBatteryValues(float pct, bool online, bool has)
    {
        if (!batteryOn || !has || pct < 0f || pct > 1f) return;
        int p = (int)Math.Round(pct * 100);
        if (online)
        {
            if (batWarned > 0) { batWarned = 0; Say("Strom! Danke, jetzt ist mein Hunger weg.", 5); Burst("♥", 3); hop = 22; }
            return;
        }
        int level = p <= 5 ? 5 : p <= 10 ? 10 : p <= 20 ? 20 : 0;
        if (level > 0 && (batWarned == 0 || level < batWarned))
        {
            batWarned = level; sleeping = false; hop = 22;
            Say("Ich habe Hunger: Dein Akku ist bei " + p + " %.\nHast du ein Ladegerät?", 9);
            if (level <= 10) Chime(false);
        }
    }

    // ---- Töne: selbst erzeugt, damit sie nicht vom Windows-Klangschema abhängen ----
    static byte[] Wav(short[] samples, int rate)
    {
        MemoryStream ms = new MemoryStream();
        BinaryWriter w = new BinaryWriter(ms);
        int n = samples.Length;
        w.Write(new char[] { 'R', 'I', 'F', 'F' }); w.Write(36 + n * 2); w.Write(new char[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write(new char[] { 'd', 'a', 't', 'a' }); w.Write(n * 2);
        for (int i = 0; i < n; i++) w.Write(samples[i]);
        w.Flush();
        return ms.ToArray();
    }

    // Folge weicher Töne; die große Variante hat drei steigende Töne
    static byte[] MakeChime(bool big)
    {
        double[] notes = big ? new double[] { 523.25, 659.25, 783.99 } : new double[] { 659.25, 880.0 };
        int rate = 22050, per = (int)(rate * 0.14);
        short[] s = new short[per * notes.Length];
        for (int k = 0; k < notes.Length; k++)
            for (int i = 0; i < per; i++)
            {
                double tt = i / (double)rate;
                double env = Math.Exp(-tt * 13) * Math.Min(1.0, i / 90.0);
                s[k * per + i] = (short)(Math.Sin(2 * Math.PI * notes[k] * tt) * env * 8000);
            }
        return Wav(s, rate);
    }
    void Chime(bool big)
    {
        if (!soundOn || autoHidden) return;
        OwnSound(big ? 0.8 : 0.6);
        try
        {
            if (big) { if (chimeBig == null) chimeBig = new SoundPlayer(new MemoryStream(MakeChime(true))); chimeBig.Play(); }
            else { if (chime == null) chime = new SoundPlayer(new MemoryStream(MakeChime(false))); chime.Play(); }
        }
        catch { }
    }

    // ---- Blip-Stimme: kurze Rechteck-Töne im Takt der Schreibmaschine, Tonhöhe je Buchstabe ----
    const double TypeSpeed = 45.0;      // Zeichen pro Sekunde, auch für den Schreibmaschinen-Effekt

    static byte[] MakeVoice(string text)
    {
        int rate = 16000;
        int n = Math.Min(text.Length, 80);
        int total = (int)(rate * (n / TypeSpeed + 0.1));
        short[] s = new short[total];
        for (int i = 0; i < n; i += 3)                    // nicht jeder Buchstabe, sonst wird es hektisch
        {
            char c = text[i];
            if (!char.IsLetter(c)) continue;
            double f = 420 + (char.ToLowerInvariant(c) % 13) * 38;
            int start = (int)(i / TypeSpeed * rate), len = (int)(rate * 0.032);
            for (int k = 0; k < len && start + k < total; k++)
            {
                double tt = k / (double)rate;
                double env = Math.Min(1.0, k / 40.0) * Math.Exp(-tt * 55);
                double sq = Math.Sin(2 * Math.PI * f * tt) >= 0 ? 1 : -1;
                s[start + k] = (short)(sq * env * 2200);
            }
        }
        return Wav(s, rate);
    }
    void Speak(string text)
    {
        DateTime now = DateTime.Now;
        if (!voiceOn || autoHidden || still || now < nextVoice || text.Length == 0) return;
        nextVoice = now.AddSeconds(3);
        OwnSound(Math.Min(text.Length, 80) / TypeSpeed + 0.4);
        try
        {
            if (voicePlayer != null) { voicePlayer.Stop(); voicePlayer.Dispose(); }
            voicePlayer = new SoundPlayer(new MemoryStream(MakeVoice(text)));
            voicePlayer.Play();
        }
        catch { }
    }
}
