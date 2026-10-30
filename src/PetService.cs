using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Verbindung prüfen (Diagnose) und Installieren
public partial class PetForm
{
    static readonly string[] HookEvents = new string[] {
        "UserPromptSubmit", "PermissionRequest", "PreToolUse", "Notification", "PostToolUse", "Stop", "StopFailure",
        "SubagentStart", "SubagentStop", "PreCompact", "PostCompact", "SessionEnd" };

    static string ProgramDir() { try { return Path.GetDirectoryName(ScriptPath); } catch { return ""; } }
    static string SettingsPath() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json"); }
    static string Mark(char kind, string text) { return (kind == 'o' ? "✓  " : kind == 'x' ? "✗  " : "–  ") + text + "\r\n"; }

    // ---- Diagnose ----
    // Prüft, ob Claude Code das Pet erreichen kann. fixable = Hooks fehlen oder zeigen ins Leere und das Setup-Skript liegt bereit.
    internal string RunDiagnosis(string settingsPath, string progDir, out bool fixable)
    {
        StringBuilder sb = new StringBuilder();
        int problems = 0; fixable = false;
        bool haveSetup = File.Exists(Path.Combine(progDir, "ClaudeStatus-Setup.ps1"));

        // 1. Hook-Skript
        string hook = Path.Combine(progDir, "ClaudePetHook.ps1");
        if (File.Exists(hook)) sb.Append(Mark('o', "Hook-Skript gefunden"));
        else { sb.Append(Mark('x', "Hook-Skript fehlt: " + hook)); problems++; }

        // 2. Einträge in den Claude-Einstellungen
        string cmdPath = "";
        List<string> missing = new List<string>();
        bool parsed = false;
        if (!File.Exists(settingsPath)) { sb.Append(Mark('x', "Die Claude-Einstellungen gibt es noch nicht")); problems++; fixable = haveSetup; }
        else
        {
            try
            {
                Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(settingsPath)) as Dictionary<string, object>;
                Dictionary<string, object> hooks = root != null && root.ContainsKey("hooks") ? root["hooks"] as Dictionary<string, object> : null;
                foreach (string ev in HookEvents)
                {
                    bool found = false;
                    IEnumerable groups = hooks != null && hooks.ContainsKey(ev) ? hooks[ev] as IEnumerable : null;
                    if (groups != null)
                        foreach (object go in groups)
                        {
                            Dictionary<string, object> gd = go as Dictionary<string, object>;
                            IEnumerable hs = gd != null && gd.ContainsKey("hooks") ? gd["hooks"] as IEnumerable : null;
                            if (hs == null) continue;
                            foreach (object ho in hs)
                            {
                                Dictionary<string, object> hd = ho as Dictionary<string, object>;
                                string cmd = hd != null && hd.ContainsKey("command") ? hd["command"] as string : null;
                                if (cmd == null || !cmd.Contains("ClaudePetHook")) continue;
                                found = true;
                                Match m = Regex.Match(cmd, "-File \"([^\"]+)\"");
                                if (m.Success) cmdPath = m.Groups[1].Value;
                            }
                        }
                    if (!found) missing.Add(ev);
                }
                parsed = true;
            }
            catch { sb.Append(Mark('x', "Die Claude-Einstellungen lassen sich nicht lesen (kein gültiges JSON?)")); problems++; }
        }
        if (parsed)
        {
            if (missing.Count == 0) sb.Append(Mark('o', "Hooks eingetragen: alle " + HookEvents.Length));
            else
            {
                sb.Append(Mark('x', "Hooks eingetragen: " + (HookEvents.Length - missing.Count) + " von " + HookEvents.Length + "; es fehlen: " + string.Join(", ", missing.ToArray())));
                problems++; fixable = haveSetup;
            }
            if (cmdPath.Length > 0 && !File.Exists(cmdPath))
            {
                sb.Append(Mark('x', "Die Hooks zeigen auf eine Datei, die es nicht gibt:\r\n      " + cmdPath));
                problems++; fixable = haveSetup;
            }
            else if (cmdPath.Length > 0 && File.Exists(hook) && !string.Equals(Path.GetFullPath(cmdPath), Path.GetFullPath(hook), StringComparison.OrdinalIgnoreCase))
                sb.Append(Mark('-', "Die Hooks benutzen eine andere Kopie:\r\n      " + cmdPath));
        }

        // 3. kam zuletzt eine Meldung an?
        try
        {
            DateTime newest = DateTime.MinValue;
            if (Directory.Exists(stateDir))
                foreach (string fp in Directory.GetFiles(stateDir, "*.txt")) { DateTime w = File.GetLastWriteTime(fp); if (w > newest) newest = w; }
            if (newest == DateTime.MinValue) sb.Append(Mark('-', "Gerade keine aktive Claude-Sitzung. Starte eine neue, um den Status zu testen."));
            else
            {
                double s = (DateTime.Now - newest).TotalSeconds;
                sb.Append(Mark('o', "Letzte Meldung vor " + (s < 90 ? (int)s + " Sekunden" : (int)(s / 60) + " Minuten")));
            }
        }
        catch { }

        // 4. PowerShell (die Hooks rufen es auf)
        string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(ps)) sb.Append(Mark('o', "PowerShell gefunden"));
        else { sb.Append(Mark('x', "PowerShell wurde nicht gefunden, die Hooks können nicht laufen")); problems++; }

        // 5. Wächter
        if (ScriptPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            if (Process.GetProcessesByName("ClaudePet").Length >= 2) sb.Append(Mark('o', "Wächter läuft (startet das Pet nach einem Absturz neu)"));
            else sb.Append(Mark('-', "Der Wächter läuft nicht. Starte das Pet über die .exe oder die .bat-Datei."));
        }
        else sb.Append(Mark('-', "Das Pet läuft als Skript, also ohne Wächter."));

        // 6. Fehlerprotokoll
        try
        {
            if (File.Exists(ErrLogPath()))
            {
                int n = 0; foreach (string l in File.ReadAllLines(ErrLogPath())) if (l.StartsWith("[")) n++;
                sb.Append(Mark('-', "Fehlerprotokoll: " + n + " Einträge, zuletzt am " + File.GetLastWriteTime(ErrLogPath()).ToString("d. MMMM HH:mm", De)));
            }
            else sb.Append(Mark('o', "Keine Fehler protokolliert"));
        }
        catch { }

        // 7. Datenordner
        try
        {
            Directory.CreateDirectory(DataDir());
            string probe = Path.Combine(DataDir(), "probe.tmp");
            File.WriteAllText(probe, "x"); File.Delete(probe);
            sb.Append(Mark('o', "Datenordner beschreibbar: " + DataDir()));
        }
        catch { sb.Append(Mark('x', "Der Datenordner ist nicht beschreibbar: " + DataDir())); problems++; }

        sb.Append("\r\n" + (problems == 0 ? "Alles in Ordnung." : problems + (problems == 1 ? " Problem gefunden." : " Probleme gefunden.")));
        return sb.ToString();
    }

    static void RunSetup(string setupScript)
    {
        ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + setupScript + "\"");
        psi.CreateNoWindow = true; psi.UseShellExecute = false;
        using (Process p = Process.Start(psi)) p.WaitForExit(30000);
    }

    void ShowDiagnosis()
    {
        bool fix;
        string setup = Path.Combine(ProgramDir(), "ClaudeStatus-Setup.ps1");
        string rep = RunDiagnosis(SettingsPath(), ProgramDir(), out fix);
        using (Form o = DialogOwner())
        {
            if (!fix) { MessageBox.Show(o, rep, "Verbindung prüfen", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            DialogResult r = MessageBox.Show(o, rep + "\r\n\r\nSoll ich die Hooks jetzt in deine Claude-Einstellungen eintragen?\r\n(Vorher wird eine Sicherung angelegt.)",
                "Verbindung prüfen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            try { RunSetup(setup); Say("Hooks eingetragen. Sie gelten in neuen Claude-Sitzungen.", 6); }
            catch { Say("Das Eintragen hat nicht geklappt.", 5); }
        }
    }

    // ---- Installieren ----
    static string InstallDir()
    {
        string o = Environment.GetEnvironmentVariable("CLAUDEPET_INSTALL");
        return !string.IsNullOrEmpty(o) ? o : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudePet");
    }

    // Kopiert Programm, Skripte und Quellcode. Der Spielstand liegt woanders und bleibt unberührt.
    internal static int InstallFiles(string srcDir, string dstDir)
    {
        Directory.CreateDirectory(dstDir);
        int n = 0;
        string[] files = new string[] { "ClaudePet.exe", "ClaudePet.ico", "ClaudePet.ps1", "ClaudePetHook.ps1", "ClaudeStatus-Setup.ps1",
            "Claude-Pet-bauen.ps1", "Claude Pet starten.bat", "Claude Pet beenden.bat", "LIESMICH.txt" };
        foreach (string f in files)
        {
            string p = Path.Combine(srcDir, f);
            if (File.Exists(p)) { File.Copy(p, Path.Combine(dstDir, f), true); n++; }
        }
        string sd = Path.Combine(srcDir, "src");
        if (Directory.Exists(sd))
        {
            string dd = Path.Combine(dstDir, "src");
            Directory.CreateDirectory(dd);
            foreach (string cs in Directory.GetFiles(sd, "*.cs")) { File.Copy(cs, Path.Combine(dd, Path.GetFileName(cs)), true); n++; }
        }
        return n;
    }

    // Verknüpfung über das Windows-Skript-Objekt (ohne zusätzliche Bibliotheken)
    internal static bool MakeShortcut(string lnkPath, string target, string workDir)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnkPath));
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object sh = Activator.CreateInstance(t);
            object l = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnkPath });
            Type lt = l.GetType();
            lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, l, new object[] { target });
            lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, l, new object[] { workDir });
            lt.InvokeMember("Description", BindingFlags.SetProperty, null, l, new object[] { "Claude Pet" });
            lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, l, null);
            return File.Exists(lnkPath);
        }
        catch { return false; }
    }

    // Autostart-Datei für eine .exe anlegen (startet sie ohne Fenster)
    static void WriteAutostartFile(string exe)
    {
        string vbs = "CreateObject(\"WScript.Shell\").Run Chr(34) & \"" + exe + "\" & Chr(34), 0, False";
        File.WriteAllText(StartupFile(), vbs, Encoding.Unicode);
    }

    void InstallInteractive()
    {
        if (!ScriptPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) { Say("Zum Installieren brauche ich die .exe. Baue sie mit Claude-Pet-bauen.ps1.", 7); return; }
        string src = ProgramDir(), dst = InstallDir();
        if (string.Equals(Path.GetFullPath(src).TrimEnd('\\'), Path.GetFullPath(dst).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) { Say("Ich bin schon dort installiert.", 5); return; }
        bool restart = false;
        using (Form o = DialogOwner())
        {
            if (MessageBox.Show(o, "Das Pet nach\r\n" + dst + "\r\nkopieren und eine Verknüpfung im Startmenü anlegen?\r\n\r\nDein Spielstand bleibt, wo er ist.",
                "Installieren", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string exe = Path.Combine(dst, "ClaudePet.exe");
            try { InstallFiles(src, dst); } catch { }
            if (!File.Exists(exe)) { Say("Das Kopieren hat nicht geklappt.", 5); return; }
            MakeShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Claude Pet.lnk"), exe, dst);
            if (MessageBox.Show(o, "Soll das Pet beim Anmelden automatisch starten?", "Installieren", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                try { WriteAutostartFile(exe); } catch { }
            if (MessageBox.Show(o, "Soll der Claude-Status künftig die installierte Kopie benutzen?\r\n(Das trägt die Hooks neu in deine Claude-Einstellungen ein. Vorher wird eine Sicherung angelegt.)",
                "Installieren", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                try { RunSetup(Path.Combine(dst, "ClaudeStatus-Setup.ps1")); } catch { }
            try
            {
                // die installierte Version startet, sobald diese hier beendet ist
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c ping -n 4 127.0.0.1 >nul & start \"\" \"" + exe + "\"");
                psi.CreateNoWindow = true; psi.UseShellExecute = false;
                Process.Start(psi); restart = true;
            }
            catch { }
        }
        Say(restart ? "Installiert! Ich starte gleich neu." : "Installiert. Starte mich über das Startmenü.", 5);
        if (restart) Close();
    }
}
