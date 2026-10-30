using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// Fehlerprotokoll, Absturz-Schutz und Programmstart
public partial class PetForm
{
    static string lastErr = ""; static DateTime lastErrAt = DateTime.MinValue;

    internal static string ErrLogPath() { return Path.Combine(DataDir(), "fehler.log"); }

    // Schreibt einen Fehler ins Protokoll; derselbe Fehler höchstens alle 5 Sekunden, die Datei bleibt unter etwa 200 KB
    internal static void LogError(Exception ex) { LogError(ex == null ? "unbekannter Fehler" : ex.ToString()); }
    internal static void LogError(string text)
    {
        try
        {
            DateTime now = DateTime.Now;
            string key = text.Length > 200 ? text.Substring(0, 200) : text;
            if (key == lastErr && (now - lastErrAt).TotalSeconds < 5) return;
            lastErr = key; lastErrAt = now;
            Directory.CreateDirectory(DataDir());
            string p = ErrLogPath();
            if (File.Exists(p) && new FileInfo(p).Length > 200 * 1024)
            {
                string all = File.ReadAllText(p, Encoding.UTF8);
                File.WriteAllText(p, all.Substring(all.Length / 2), Encoding.UTF8);          // die ältere Hälfte weg
            }
            File.AppendAllText(p, "[" + now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + (text.Length > 4000 ? text.Substring(0, 4000) : text) + "\r\n\r\n", Encoding.UTF8);
        }
        catch { }
    }

    // Startet das Pet. Einzelne Fehler werden protokolliert, das Pet läuft weiter; nur ein harter Absturz beendet den Prozess.
    // Rückgabe: 0 = normal beendet (oder läuft schon), 1 = Absturz
    public static int RunGuarded()
    {
        bool created;
        using (Mutex m = new Mutex(true, "ClaudePetDesktop", out created))
        {
            if (!created) return 0;
            Application.EnableVisualStyles();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { LogError(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { LogError(e.ExceptionObject as Exception); };
            try { Application.Run(new PetForm(false)); }
            catch (Exception ex) { LogError(ex); return 1; }
            return 0;
        }
    }
}

// Wächter: startet das Pet neu, wenn es unerwartet beendet wird. "Beenden" im Menü beendet auch den Wächter.
static class Watchdog
{
    public static int Run(string exe)
    {
        bool created;
        using (Mutex m = new Mutex(true, "ClaudePetWatchdog", out created))
        {
            if (!created) return 0;
            List<DateTime> starts = new List<DateTime>();
            while (true)
            {
                DateTime now = DateTime.Now;
                starts.RemoveAll(delegate(DateTime d) { return (now - d).TotalMinutes > 10; });
                starts.Add(now);
                if (starts.Count > 5) { PetForm.LogError("Der Wächter gibt auf: mehr als 5 Neustarts in 10 Minuten."); return 1; }
                int code;
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo(exe, "--run");
                    psi.UseShellExecute = false;
                    using (Process p = Process.Start(psi)) { p.WaitForExit(); code = p.ExitCode; }
                }
                catch (Exception ex) { PetForm.LogError(ex); return 1; }
                if (code == 0) return 0;
                PetForm.LogError("Das Pet wurde unerwartet beendet (Code " + code + "). Neustart in 2 Sekunden.");
                Thread.Sleep(2000);
            }
        }
    }
}

// Einstieg der .exe: ohne Argument der Wächter, "--run" das Pet selbst, dazu Testaufrufe für Bilder
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        string self = System.Reflection.Assembly.GetExecutingAssembly().Location;
        if (args.Length >= 2 && args[0] == "--snapshot") { PetForm.Snapshot(args[1], args.Length > 2 ? args[2] : ""); return 0; }
        if (args.Length >= 2 && args[0] == "--menu") { PetForm.MenuSnapshot(args[1]); return 0; }
        PetForm.ScriptPath = self;
        if (args.Length >= 1 && args[0] == "--run") return PetForm.RunGuarded();
        return Watchdog.Run(self);
    }
}
