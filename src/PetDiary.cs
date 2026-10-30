using System;
using System.Collections.Generic;
using System.Text;

// ein Erfolg
class Ach
{
    public string id, name, desc, hat;
    public Ach(string i, string n, string d, string h) { id = i; name = n; desc = d; hat = h; }
}

// Tagesrückblick, Erfolge und Tageszeit
public partial class PetForm
{
    static readonly Ach[] Achs = new Ach[] {
        new Ach("first", "Erster Auftrag", "1 Aufgabe", null),
        new Ach("t10", "Eingespielt", "10 Aufgaben", null),
        new Ach("t100", "Hundert", "100 Aufgaben", "cap"),
        new Ach("night", "Nachtschicht", "Aufgabe nachts (0-5 Uhr)", "nightcap"),
        new Ach("early", "Früher Vogel", "Aufgabe früh (5-7 Uhr)", null),
        new Ach("streak7", "Sieben Tage", "7 Tage in Folge", "halo"),
        new Ach("marathon", "Marathon", "Aufgabe über 1 Std.", null),
        new Ach("longhaul", "Langstrecke", "Aufgabe über 3 Std.", null),
        new Ach("comeback", "Wieder auf den Beinen", "Nach einem Fehler fertig werden", null),
        new Ach("juggler", "Jongleur", "3 Sitzungen gleichzeitig", null),
        new Ach("happy", "Rundum glücklich", "Alle Werte über 90", null),
        new Ach("oldtimer", "Alter Hase", "1 Woche alt", null),
        new Ach("catcher", "Token-Fänger", "20 Tokens in einem Spiel", null),
    };

    List<string> achDone = new List<string>();
    List<string> projToday = new List<string>();
    List<string> blockedIds = new List<string>();                    // Sitzungen, die einen Fehler hatten
    Dictionary<string, DateTime> sessStart = new Dictionary<string, DateTime>();
    int streak, longestMin;
    string lastDay = "", longestProj = "", recapDay = "", nightOverride = "";
    bool nightSleep, bigParty;
    string longNote = "";                   // " nach 23 Min." für die Meldung bei einer langen Aufgabe
    DateTime nextTod = DateTime.MinValue, nextComfort = DateTime.MinValue;

    bool HasAch(string id) { return achDone.Contains(id); }

    void Unlock(string id)
    {
        if (HasAch(id)) return;
        achDone.Add(id);
        foreach (Ach a in Achs)
            if (a.id == id)
                levelMsg += (levelMsg.Length > 0 ? "\n" : "") + "Erfolg: " + a.name + "!" + (a.hat != null ? "\nNeu: " + HatName(a.hat) : "");
        SaveState();
    }

    // Eine Claude-Antwort ist fertig: das Pet bekommt etwas zu fressen, sammelt Erfahrung und führt Buch
    void TaskDone(Sess s, DateTime now)
    {
        string key = now.ToString("yyyyMMdd");
        RolloverIfNeeded(now);
        if (todayKey != key) todayKey = key;     // erste Aufgabe des Tages
        if (lastDay != key)
        {
            streak = lastDay == now.AddDays(-1).ToString("yyyyMMdd") ? streak + 1 : 1;
            lastDay = key;
        }
        int before = Level();
        tasksTotal++; tasksToday++;
        food = C(food + 12); joy = C(joy + 8);

        string proj = s.project.Length > 0 ? s.project : "Sitzung";
        if (!projToday.Contains(proj)) projToday.Add(proj);
        int mins = 0; DateTime st;
        if (sessStart.TryGetValue(s.id, out st)) { mins = (int)(now - st).TotalMinutes; sessStart.Remove(s.id); }
        if (mins > longestMin) { longestMin = mins; longestProj = proj; }
        AppendHistory(now, proj, mins);
        // Lange Aufgabe, und du bist gerade in einem anderen Fenster: das Pet feiert größer
        if (mins >= 10 && !ForegroundIs(s.hwnd)) { longNote = "\nnach " + FmtMins(mins); bigParty = true; }

        // Erfolge
        Unlock("first");
        if (tasksTotal >= 10) Unlock("t10");
        if (tasksTotal >= 100) Unlock("t100");
        if (now.Hour < 5) Unlock("night"); else if (now.Hour < 7) Unlock("early");
        if (streak >= 7) Unlock("streak7");
        if (mins >= 60) Unlock("marathon");
        if (mins >= 180) Unlock("longhaul");
        if (blockedIds.Contains(s.id)) { blockedIds.Remove(s.id); Unlock("comeback"); }

        int after = Level();
        if (after > before)
        {
            string neu = after == 2 ? "\nNeu: Schleife" : after == 5 ? "\nNeu: Krone" : after == 8 ? "\nNeu: Zylinder" : "";
            levelMsg = "Stufe " + after + " erreicht!" + neu + (levelMsg.Length > 0 ? "\n" + levelMsg : "");
        }
        SaveState();
    }

    // Erfolge, die sich aus dem laufenden Zustand ergeben
    void CheckAchievements(DateTime now)
    {
        if (cRun >= 3) Unlock("juggler");
        if (food > 90 && joy > 90 && energy > 90) Unlock("happy");
        if ((now - born).TotalDays >= 7) Unlock("oldtimer");
    }

    // Merkt sich, wann eine Aufgabe begonnen hat, und welche Sitzungen einen Fehler hatten
    void TrackSessions(List<Sess> list, DateTime now)
    {
        foreach (Sess s in list)
        {
            if (s.state != "ready" && !sessStart.ContainsKey(s.id)) sessStart[s.id] = now;
            if (s.state == "blocked" && !blockedIds.Contains(s.id)) blockedIds.Add(s.id);
        }
        // Sitzungen, die ganz verschwunden sind, vergessen
        List<string> gone = new List<string>();
        foreach (string id in sessStart.Keys)
        {
            bool found = false;
            foreach (Sess s in list) if (s.id == id) found = true;
            if (!found) gone.Add(id);
        }
        foreach (string id in gone) sessStart.Remove(id);
    }

    // ---- Tagesrückblick ----
    string RecapText() { return RecapFor(TodayCount()); }

    // Text für n erledigte Aufgaben des aktuell geführten Tages
    string RecapFor(int n)
    {
        if (n == 0) return "Heute ist noch nichts fertig geworden.";
        StringBuilder sb = new StringBuilder();
        sb.Append("Tagesrückblick: " + n + (n == 1 ? " Aufgabe" : " Aufgaben"));
        sb.Append("\nin " + projToday.Count + (projToday.Count == 1 ? " Projekt" : " Projekten") + ": ");
        for (int i = 0; i < projToday.Count && i < 3; i++) sb.Append((i > 0 ? ", " : "") + projToday[i]);
        if (projToday.Count > 3) sb.Append(" ...");
        if (longestMin > 0) sb.Append("\nLängste Aufgabe: " + longestMin + " Min. (" + longestProj + ")");
        string tl = ToolLine(todayKey);
        if (tl.Length > 0) sb.Append("\n" + tl);
        return sb.ToString();
    }

    // Abends einmal am Tag von selbst, wenn nichts anderes anliegt
    void CheckRecap(DateTime now)
    {
        if (now.Hour < 18 || autoHidden || !Visible) return;
        if (cState == "needs" || cState == "blocked" || dragging || now < bubbleUntil) return;
        if (TodayCount() > 0 && recapDay != todayKey)
        {
            recapDay = todayKey;
            hop = 22; Burst("★", 3);
            Say(RecapText(), 16);
            WriteDayDiary(todayKey);
            SaveState();
            return;
        }
        // Sonntagabend, nach dem Tagesrückblick: Vergleich mit der Vorwoche
        string wk = WeekStart(now).ToString("yyyyMMdd");
        if (now.DayOfWeek == DayOfWeek.Sunday && weekKey != wk && (TodayCount() == 0 || recapDay == todayKey))
        {
            weekKey = wk;
            string wt = WeekText(now);
            if (wt != null)
            {
                hop = 22; Burst("★", 3);
                Say(wt, 18);
                WriteDiary("Woche ab " + WeekStart(now).ToString("d. MMMM yyyy", De), wt.Replace("Wochenrückblick: ", ""));
            }
            SaveState();
        }
    }

    // ---- Tageszeit: nachts schläft das Pet, morgens gähnt es ----
    void CheckDaytime(DateTime now)
    {
        if (now < nextTod) return;
        nextTod = now.AddSeconds(5);
        int h = now.Hour;
        string key = now.ToString("yyyyMMdd");
        bool night = h < 6;
        bool active = cRun + cNeed + cBlock > 0;
        RolloverIfNeeded(now);
        if (!night) nightOverride = "";
        if (night && !sleeping && !active && cState == "none" && !dragging && hop <= 0 && !jumping && nightOverride != key)
        {
            sleeping = true; nightSleep = true; walkFrames = 0; act = "none"; actFrames = 0;
            Say("Gute Nacht...", 3);
        }
        else if (nightSleep && sleeping && (!night || active))
        {
            sleeping = false; nightSleep = false;
            if (active) Say("Oh, Nachtschicht? Ich bin dabei.", 5);
            else { Say("Guten Morgen!", 4); act = "stretch"; actFrames = 60; yawn = 50; }
        }
    }

    string Greeting()
    {
        int h = DateTime.Now.Hour;
        return h < 10 ? "Guten Morgen!" : h < 18 ? "Hallo!" : h < 23 ? "Guten Abend!" : "Noch wach?";
    }
}
