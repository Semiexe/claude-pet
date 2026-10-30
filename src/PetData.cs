using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

// Dateien des Pets: Tagebuch, Verlauf, eigene Sätze, Sicherung
// Alles liegt in einem Ordner (Standard: %APPDATA%\ClaudePet). Mit der Umgebungsvariable CLAUDEPET_HOME lässt er sich umleiten.
public partial class PetForm
{
    static readonly CultureInfo De = new CultureInfo("de-DE");
    static readonly string[] BackupFiles = new string[] { "state.txt", "Verlauf.txt", "Tagebuch.txt", "Saetze.txt", "Erinnerungen.txt", "EigenerHut.txt" };

    string diaryDay = "", weekKey = "", guestDay = "";
    int gameBest;
    List<string> phrases = new List<string>();

    static string DataDir()
    {
        string o = Environment.GetEnvironmentVariable("CLAUDEPET_HOME");
        if (!string.IsNullOrEmpty(o)) return o;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudePet");
    }
    static string DiaryPath() { return Path.Combine(DataDir(), "Tagebuch.txt"); }
    static string HistPath() { return Path.Combine(DataDir(), "Verlauf.txt"); }
    static string PhrasePath() { return Path.Combine(DataDir(), "Saetze.txt"); }

    // ---- Verlauf: eine Zeile pro erledigter Aufgabe (Tag;Projekt;Minuten) ----
    class HistRec { public DateTime day; public string proj; public int mins; }

    void AppendHistory(DateTime now, string proj, int mins)
    {
        try
        {
            Directory.CreateDirectory(DataDir());
            File.AppendAllText(HistPath(), now.ToString("yyyyMMdd") + ";" + proj.Replace(';', ',') + ";" + mins + "\r\n", Encoding.UTF8);
        }
        catch { }
    }
    List<HistRec> ReadHistory()
    {
        List<HistRec> res = new List<HistRec>();
        try
        {
            if (!File.Exists(HistPath())) return res;
            foreach (string line in File.ReadAllLines(HistPath(), Encoding.UTF8))
            {
                string[] p = line.Split(';');
                DateTime d; int m;
                if (p.Length < 3 || !DateTime.TryParseExact(p[0], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) continue;
                int.TryParse(p[2], out m);
                HistRec r = new HistRec(); r.day = d; r.proj = p[1]; r.mins = m;
                res.Add(r);
            }
        }
        catch { }
        return res;
    }

    // ---- Tagebuch ----
    void WriteDiary(string title, string body)
    {
        try
        {
            Directory.CreateDirectory(DataDir());
            File.AppendAllText(DiaryPath(), "=== " + title + " ===\r\n" + body.Replace("\n", "\r\n") + "\r\n\r\n", Encoding.UTF8);
        }
        catch { }
    }
    // Schreibt den Eintrag für einen Tag; pro Tag nur einmal
    void WriteDayDiary(string dayKey)
    {
        if (diaryDay == dayKey) return;
        diaryDay = dayKey;
        DateTime d;
        string title = DateTime.TryParseExact(dayKey, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
            ? d.ToString("dddd, d. MMMM yyyy", De) : dayKey;
        WriteDiary(title, RecapFor(tasksToday).Replace("Tagesrückblick: ", "") + "\nStufe " + Level());
    }
    // Tageswechsel: den alten Tag ins Tagebuch schreiben und die Tageszähler zurücksetzen
    void RolloverIfNeeded(DateTime now)
    {
        string key = now.ToString("yyyyMMdd");
        if (todayKey == "" || todayKey == key) return;
        if (tasksToday > 0) WriteDayDiary(todayKey);
        todayKey = key; tasksToday = 0; projToday.Clear(); longestMin = 0; longestProj = "";
    }

    // ---- Wochenrückblick ----
    static DateTime WeekStart(DateTime d) { return d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7)); }   // Montag

    // null, wenn es weder diese noch letzte Woche Aufgaben gab
    string WeekText(DateTime now)
    {
        DateTime ws = WeekStart(now), pw = ws.AddDays(-7), we = ws.AddDays(7);
        int cur = 0, prev = 0;
        Dictionary<string, int> pc = new Dictionary<string, int>();
        foreach (HistRec r in ReadHistory())
        {
            if (r.day >= ws && r.day < we) { cur++; int n; pc.TryGetValue(r.proj, out n); pc[r.proj] = n + 1; }
            else if (r.day >= pw && r.day < ws) prev++;
        }
        if (cur == 0 && prev == 0) return null;
        string top = ""; int topN = 0;
        foreach (KeyValuePair<string, int> kv in pc) if (kv.Value > topN) { top = kv.Key; topN = kv.Value; }
        string diff = cur > prev ? " (+" + (cur - prev) + ")" : cur < prev ? " (-" + (prev - cur) + ")" : " (gleich viele)";
        return "Wochenrückblick: " + cur + (cur == 1 ? " Aufgabe" : " Aufgaben")
            + "\nVorwoche: " + prev + diff
            + (topN > 0 ? "\nAm meisten: " + top + " (" + topN + ")" : "");
    }

    // ---- eigene Sätze ----
    void LoadPhrases()
    {
        phrases.Clear();
        try
        {
            if (!File.Exists(PhrasePath())) return;
            foreach (string l in File.ReadAllLines(PhrasePath(), Encoding.UTF8))
            {
                string t = l.Trim();
                if (t.Length > 0 && t.Length <= 120 && phrases.Count < 100) phrases.Add(t);
            }
        }
        catch { }
    }
    void SavePhrases(string text)
    {
        try
        {
            Directory.CreateDirectory(DataDir());
            File.WriteAllText(PhrasePath(), text, Encoding.UTF8);
        }
        catch { }
        LoadPhrases();
    }
    string CustomPhrase()
    {
        if (phrases.Count == 0) return null;
        return phrases[rnd.Next(phrases.Count)].Replace("{name}", petName);
    }
    void EditPhrases()
    {
        string current = phrases.Count > 0 ? string.Join("\r\n", phrases.ToArray()) : "";
        using (PhraseDialog dlg = new PhraseDialog(pal, baseSc, current))
        {
            if (dlg.ShowDialog() != DialogResult.OK) return;
            SavePhrases(dlg.Result);
            Say(phrases.Count > 0 ? "Ich habe " + phrases.Count + (phrases.Count == 1 ? " neuen Satz" : " neue Sätze") + " gelernt." : "Ich sage wieder nur meine eigenen Sätze.", 4);
        }
    }

    // ---- Sicherung ----
    // unsichtbares Fenster ganz oben, damit Dialoge nicht hinter dem Pet landen
    Form DialogOwner()
    {
        Form o = new Form();
        o.TopMost = true; o.ShowInTaskbar = false; o.FormBorderStyle = FormBorderStyle.None;
        o.StartPosition = FormStartPosition.Manual; o.Location = new Point(-2000, -2000); o.Size = new Size(1, 1);
        o.Show();
        return o;
    }

    // Alle Dateien des Pets in einem lesbaren Text
    static string BuildBackup()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("CLAUDEPET-SICHERUNG 1\r\n");
        foreach (string name in BackupFiles)
        {
            string p = Path.Combine(DataDir(), name);
            if (!File.Exists(p)) continue;
            sb.Append("=====DATEI " + name + "=====\r\n");
            sb.Append(File.ReadAllText(p, Encoding.UTF8).TrimEnd() + "\r\n");
        }
        sb.Append("=====ENDE=====\r\n");
        return sb.ToString();
    }

    // Liest eine Sicherung; nur bekannte Dateinamen werden übernommen
    static Dictionary<string, StringBuilder> ParseBackup(string[] lines)
    {
        Dictionary<string, StringBuilder> files = new Dictionary<string, StringBuilder>();
        StringBuilder cur = null;
        for (int i = 1; i < lines.Length; i++)
        {
            string l = lines[i];
            if (l.StartsWith("=====DATEI ") && l.EndsWith("====="))
            {
                string name = l.Substring(11, l.Length - 11 - 5);
                cur = Array.IndexOf(BackupFiles, name) >= 0 ? new StringBuilder() : null;
                if (cur != null) files[name] = cur;
            }
            else if (l == "=====ENDE=====") break;
            else if (cur != null) cur.Append(l + "\r\n");
        }
        return files;
    }

    void ExportSave()
    {
        SaveState();
        using (Form o = DialogOwner())
        using (SaveFileDialog d = new SaveFileDialog())
        {
            d.Title = "Spielstand sichern";
            d.Filter = "Claude-Pet-Sicherung (*.txt)|*.txt";
            d.FileName = "ClaudePet-Sicherung-" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
            if (d.ShowDialog(o) != DialogResult.OK) return;
            try
            {
                File.WriteAllText(d.FileName, BuildBackup(), Encoding.UTF8);
                Say("Gesichert! Die Datei kannst du auf einem anderen Rechner laden.", 5);
            }
            catch { Say("Das Sichern hat nicht geklappt.", 4); }
        }
    }

    void ImportSave()
    {
        using (Form o = DialogOwner())
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Title = "Spielstand laden";
            d.Filter = "Claude-Pet-Sicherung (*.txt)|*.txt";
            if (d.ShowDialog(o) != DialogResult.OK) return;
            try
            {
                FileInfo fi = new FileInfo(d.FileName);
                if (fi.Length > 5 * 1024 * 1024) { Say("Die Datei ist zu groß für eine Sicherung.", 4); return; }
                string[] lines = File.ReadAllLines(d.FileName, Encoding.UTF8);
                if (lines.Length == 0 || !lines[0].StartsWith("CLAUDEPET-SICHERUNG")) { Say("Das ist keine Claude-Pet-Sicherung.", 4); return; }

                Dictionary<string, StringBuilder> files = ParseBackup(lines);
                if (files.Count == 0) { Say("In der Datei steht kein Spielstand.", 4); return; }
                if (MessageBox.Show(o, "Den aktuellen Spielstand durch die Sicherung ersetzen?\nDer aktuelle wird vorher im Ordner \"Backup-vor-Import\" abgelegt.",
                    "Claude Pet", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                SaveState();
                string back = Path.Combine(DataDir(), "Backup-vor-Import");
                Directory.CreateDirectory(back);
                foreach (string name in BackupFiles)
                {
                    string p = Path.Combine(DataDir(), name);
                    if (File.Exists(p)) File.Copy(p, Path.Combine(back, name), true);
                }
                foreach (KeyValuePair<string, StringBuilder> kv in files)
                    File.WriteAllText(Path.Combine(DataDir(), kv.Key), kv.Value.ToString(), Encoding.UTF8);

                achDone.Clear(); projToday.Clear();
                LoadState(false);
                LoadPhrases(); LoadReminders(); LoadCustomHat(); ApplyFur();
                hop = 22; Burst("★", 3);
                Say("Spielstand geladen. Schön, dich wiederzusehen!", 5);
            }
            catch { Say("Das Laden hat nicht geklappt.", 4); }
        }
    }
}
