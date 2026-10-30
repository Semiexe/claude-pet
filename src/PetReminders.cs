using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

// eine Erinnerung; repeat = Minuten bis zur Wiederholung, 0 = einmalig
class Reminder
{
    public DateTime at;
    public string text = "";
    public int repeat;
}

// Erinnerungen: Das Pet hält einen Notizzettel hoch, wenn es Zeit ist
public partial class PetForm
{
    List<Reminder> reminders = new List<Reminder>();
    bool waterOn;                                           // stündliche Wasser-Erinnerung
    DateTime nextWater = DateTime.MinValue, noteUntil = DateTime.MinValue;

    static string RemPath() { return Path.Combine(DataDir(), "Erinnerungen.txt"); }

    // Zeile: yyyyMMddHHmmss|Wiederholung|Text
    void LoadReminders()
    {
        reminders.Clear();
        try
        {
            if (!File.Exists(RemPath())) return;
            foreach (string line in File.ReadAllLines(RemPath(), Encoding.UTF8))
            {
                string[] p = line.Split(new char[] { '|' }, 3);
                DateTime at; int rep;
                if (p.Length < 3 || !DateTime.TryParseExact(p[0], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out at)) continue;
                int.TryParse(p[1], out rep);
                Reminder r = new Reminder(); r.at = at; r.repeat = rep; r.text = p[2];
                if (reminders.Count < 50) reminders.Add(r);
            }
        }
        catch { }
    }
    void SaveReminders()
    {
        try
        {
            Directory.CreateDirectory(DataDir());
            List<string> lines = new List<string>();
            foreach (Reminder r in reminders) lines.Add(r.at.ToString("yyyyMMddHHmmss") + "|" + r.repeat + "|" + r.text.Replace('|', '/').Replace("\r", " ").Replace("\n", " "));
            File.WriteAllLines(RemPath(), lines.ToArray(), Encoding.UTF8);
        }
        catch { }
    }

    internal void AddReminder(int minutes, string text)
    {
        Reminder r = new Reminder(); r.at = DateTime.Now.AddMinutes(minutes); r.text = text.Length > 100 ? text.Substring(0, 100) : text;
        reminders.Add(r);
        reminders.Sort(delegate(Reminder a, Reminder b) { return a.at.CompareTo(b.at); });
        SaveReminders();
        Say("Okay, ich erinnere dich um " + r.at.ToString("HH:mm") + " Uhr.", 4);
    }

    void AskReminder(int minutes)
    {
        if (minutes <= 0)
        {
            string m = Microsoft.VisualBasic.Interaction.InputBox("In wie vielen Minuten?", "Claude Pet", "45", -1, -1);
            int v;
            if (!int.TryParse(m.Trim(), out v) || v < 1 || v > 1440) { if (m.Length > 0) Say("Das habe ich nicht verstanden. Bitte eine Zahl zwischen 1 und 1440.", 5); return; }
            minutes = v;
        }
        string t = Microsoft.VisualBasic.Interaction.InputBox("Woran soll ich dich erinnern? (in " + (minutes < 60 ? minutes + " Minuten" : FmtMins(minutes)) + ")", "Claude Pet", "Erinnerung", -1, -1);
        if (string.IsNullOrWhiteSpace(t)) return;
        AddReminder(minutes, t.Trim());
    }

    // Nur wenn das Pet sichtbar ist; fällige Erinnerungen warten sonst, bis es wieder da ist
    void CheckReminders(DateTime now)
    {
        if (!Visible || autoHidden) return;
        FireDue(now);
    }

    // Löst die erste fällige Erinnerung aus und, falls eingeschaltet, die Wasser-Erinnerung
    internal void FireDue(DateTime now)
    {
        Reminder due = null;
        foreach (Reminder r in reminders) if (r.at <= now) { due = r; break; }
        if (due != null)
        {
            bool late = (now - due.at).TotalMinutes > 2;
            sleeping = false; nightSleep = false; hop = 22; act = "none"; actFrames = 0;
            noteUntil = now.AddSeconds(14);
            Say("Erinnerung" + (late ? " (verpasst)" : "") + ":\n" + due.text, 14);
            Chime(false);
            if (due.repeat > 0) due.at = now.AddMinutes(due.repeat); else reminders.Remove(due);
            SaveReminders();
            return;
        }
        if (waterOn)
        {
            if (nextWater == DateTime.MinValue) nextWater = now.AddMinutes(60);
            else if (now >= nextWater)
            {
                nextWater = now.AddMinutes(60);
                if (now.Hour >= 8 && now.Hour < 22 && !sleeping && cState != "needs" && cState != "blocked")
                {
                    noteUntil = now.AddSeconds(10); hop = 22;
                    Say(Pick("Trink einen Schluck Wasser!", "Zeit für ein Glas Wasser!", "Wasser marsch! Dein Körper sagt danke."), 10);
                }
            }
        }
    }
}
