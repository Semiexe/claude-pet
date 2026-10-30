using System;
using System.Runtime.InteropServices;

// Musik-Tanz: Das Pet merkt an der Lautstärke des Lautsprechers, dass Musik läuft, und wippt im Takt.
// Gelesen wird nur der Pegel (ein Wert von 0 bis 1), nie der Ton selbst. Aufgenommen wird nichts.
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, [MarshalAs(UnmanagedType.IUnknown)] out object devices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
}

[ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioMeterInformation
{
    [PreserveSig] int GetPeakValue(out float peak);
}

// Pegelmesser am Standard-Lautsprecher
class AudioMeter
{
    IAudioMeterInformation meter;
    object holder;
    DateTime retryAt = DateTime.MinValue;

    // Aktueller Spitzenwert 0..1; -1, wenn es keinen Lautsprecher oder Fehler gibt
    public float Peak()
    {
        try
        {
            if (meter == null)
            {
                if (DateTime.Now < retryAt) return -1;
                retryAt = DateTime.Now.AddSeconds(30);
                IMMDeviceEnumerator en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                IMMDevice dev;
                if (en.GetDefaultAudioEndpoint(0, 1, out dev) != 0 || dev == null) return -1;
                Guid iid = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
                object o;
                if (dev.Activate(ref iid, 23, IntPtr.Zero, out o) != 0 || o == null) return -1;
                holder = o; meter = (IAudioMeterInformation)o;
                try { Marshal.ReleaseComObject(dev); Marshal.ReleaseComObject(en); } catch { }
            }
            float p;
            if (meter.GetPeakValue(out p) != 0) { Release(); return -1; }
            return p;
        }
        catch { Release(); return -1; }
    }

    public void Release()
    {
        try { if (holder != null) Marshal.ReleaseComObject(holder); } catch { }
        holder = null; meter = null;
    }
}

public partial class PetForm
{
    bool danceOn = true;
    AudioMeter audio;
    DateTime nextAudio = DateTime.MinValue, ownSoundUntil = DateTime.MinValue, nextBeat = DateTime.MinValue, lastAudio = DateTime.MinValue;
    float peakAvg;                        // gleitender Mittelwert des Pegels, zum Erkennen eines Schlags
    double musicSecs;                     // wie lange es schon laut ist
    bool danceRaw;
    internal int danceStep;               // wechselt mit jedem Schlag (0 / 1)
    internal float beatPulse;             // 1 bei einem Schlag, klingt ab
    internal string dbg = ""; internal int beats;                   // Zähler, für Tests

    // Tanzt das Pet gerade? Nicht beim Schlafen, bei einer Frage, beim Aufräumen oder wenn man es hält.
    bool Dancing()
    {
        return danceRaw && danceOn && !sleeping && !dragging && !jumping && !cCompact && cState != "needs" && cState != "blocked" && exStep < 0 && !GazeActive();
    }

    // Eigene Töne (Gong, Blips) nicht für Musik halten
    void OwnSound(double secs) { ownSoundUntil = DateTime.Now.AddSeconds(secs); }

    void UpdateDance(DateTime now)
    {
        if (!danceOn || still) { danceRaw = false; beatPulse = 0; return; }
        if (beatPulse > 0) beatPulse = Math.Max(0, beatPulse - 0.12f);
        if (now < nextAudio) return;
        nextAudio = now.AddMilliseconds(danceRaw ? 66 : 500);       // im Ruhezustand seltener nachsehen
        if (now < ownSoundUntil) return;
        if (audio == null) audio = new AudioMeter();
        float peak = audio.Peak();
        double dt = lastAudio == DateTime.MinValue ? 0.1 : Math.Min(1.0, (now - lastAudio).TotalSeconds);
        lastAudio = now;
        if (peak < 0) { musicSecs = 0; danceRaw = false; return; }
        peakAvg = peakAvg * 0.9f + peak * 0.1f;
        if (dbg.Length < 600 && danceRaw) dbg += ((int)(peak * 100)) + "/" + ((int)(peakAvg * 100)) + " ";
        bool loud = peak > 0.015f;
        if (loud) musicSecs = Math.Min(12, musicSecs + dt); else musicSecs = Math.Max(0, musicSecs - dt * 2.5);
        if (!danceRaw && musicSecs >= 2.5) { danceRaw = true; if (Dancing()) Say(Pick("Musik! Ich tanze mit!", "Ohh, ein guter Beat!", "Tanzen!"), 3); }
        else if (danceRaw && musicSecs <= 0.05) { danceRaw = false; beatPulse = 0; }
        if (danceRaw && peak > peakAvg * 1.25f + 0.01f && now >= nextBeat)
        {
            nextBeat = now.AddMilliseconds(230);
            beatPulse = 1f; danceStep = 1 - danceStep; beats++;
        }
        else if (danceRaw && now >= nextBeat.AddMilliseconds(700) && peak > 0.015f)
        {
            // gleichmäßige Musik ohne klare Schläge: trotzdem im ruhigen Takt wippen
            nextBeat = now.AddMilliseconds(500);
            beatPulse = 0.7f; danceStep = 1 - danceStep; beats++;
        }
    }

    // Test: Ton abspielen und prüfen, ob das Pet ihn bemerkt
    public static string TestDance()
    {
        PetForm f = new PetForm(true);
        AudioMeter m = new AudioMeter();
        float idle = m.Peak();
        if (idle < 0) return "kein Pegelmesser (Lautsprecher fehlt?) peak=" + idle;
        // Pulsierender Ton, 6 Sekunden
        int rate = 22050; short[] s = new short[rate * 6];
        for (int i = 0; i < s.Length; i++)
        {
            double tt = i / (double)rate, beat = Math.Exp(-((tt * 2.0) % 1.0) * 6);
            s[i] = (short)(Math.Sin(2 * Math.PI * 220 * tt) * (0.25 + beat * 0.75) * 12000);
        }
        System.Media.SoundPlayer sp = new System.Media.SoundPlayer(new System.IO.MemoryStream(Wav(s, rate)));
        sp.Play();
        float max = 0; string dancingAt = "nie", trace = "";
        DateTime t0 = DateTime.Now;
        while ((DateTime.Now - t0).TotalSeconds < 7)
        {
            System.Windows.Forms.Application.DoEvents();
            f.UpdateDance(DateTime.Now);
            float p = f.peakAvg; if (p > max) max = p;
            if (trace.Length < 400) trace += ((int)(p * 100)) + (f.danceRaw ? "d" : "") + (f.beatPulse > 0.9f ? "!" : "") + " ";
            if (dancingAt == "nie" && f.Dancing()) dancingAt = ((DateTime.Now - t0).TotalSeconds).ToString("0.0") + " s";
            System.Threading.Thread.Sleep(30);
        }
        bool stillDancing = f.Dancing();
        sp.Stop();
        t0 = DateTime.Now;
        while ((DateTime.Now - t0).TotalSeconds < 4) { f.UpdateDance(DateTime.Now); System.Threading.Thread.Sleep(30); }
        return "stille=" + idle.ToString("0.000") + " max=" + max.ToString("0.000") + " tanztAb=" + dancingAt + " schlaege=" + f.beats
            + " nachTon: tanzt=" + f.Dancing() + " (vorher noch " + stillDancing + ") avg=" + ((double)f.peakAvg).ToString("0.000") + " musicSecs=" + ((double)f.musicSecs).ToString("0.0") + " dbg: " + f.dbg;
    }
}
