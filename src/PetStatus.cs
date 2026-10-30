using System;
using System.Collections.Generic;
using System.IO;

// eine Claude-Code-Sitzung, wie sie ClaudePetHook.ps1 in eine Statusdatei schreibt
class Sess
{
    public string id = "", state = "", project = "", tool = "", detail = "", msg = "", title = "", err = "";
    public int agents;                  // laufende Helfer (Subagenten)
    public int ctx, ctxPct;             // Kontext: Tokens und Prozent vom Fenster (0 = unbekannt)
    public bool compact;                // Claude fasst gerade den Kontext zusammen
    // Anzeigename: der Titel der Sitzung, sonst der Ordnername
    public string Label { get { return title.Length > 0 ? title : (project.Length > 0 ? project : "Sitzung"); } }
    public List<string> hist = new List<string>();     // letzte Schritte "Art|Angabe", neuester zuerst
    public long hwnd;
    public DateTime time;
    public int rank;            // needs 4, blocked 3, ready 2, running 1
}

// Status der Claude-Code-Sitzungen
public partial class PetForm
{
    List<Sess> sessions = new List<Sess>();       // sortiert: wichtigste zuerst
    Dictionary<string, string> lastSess = new Dictionary<string, string>();
    string cState = "none", lastState = "none";   // none | running | needs | blocked | ready
    string cTool = "", cDetail = "";              // Werkzeug der wichtigsten Sitzung
    int cRun, cNeed, cReady, cBlock;
    bool focusMode;                               // meldet nur Fragen, Fehler und Fertig
    IntPtr focusHwnd;
    DateTime nextPoll = DateTime.MinValue, nextAttn = DateTime.MinValue;

    void Poll(DateTime now)
    {
        if (now < nextPoll) return;
        nextPoll = now.AddSeconds(1);
        List<Sess> list = new List<Sess>();
        try
        {
            if (Directory.Exists(stateDir))
                foreach (string fp in Directory.GetFiles(stateDir, "*.txt"))
                {
                    Sess s = new Sess();
                    s.id = Path.GetFileNameWithoutExtension(fp);
                    s.time = File.GetLastWriteTime(fp);
                    double age = (now - s.time).TotalMinutes;
                    foreach (string line in File.ReadAllLines(fp))
                    {
                        int i = line.IndexOf('='); if (i < 0) continue;
                        string k = line.Substring(0, i), v = line.Substring(i + 1);
                        if (k == "state") s.state = v; else if (k == "project") s.project = v; else if (k == "tool") s.tool = v;
                        else if (k == "detail") s.detail = v; else if (k == "hwnd") long.TryParse(v, out s.hwnd);
                        else if (k == "msg") s.msg = v;
                        else if (k == "title") s.title = v;
                        else if (k == "err") s.err = v;
                        else if (k == "agents") int.TryParse(v, out s.agents);
                        else if (k == "compact") s.compact = v == "1";
                        else if (k.Length == 2 && k[0] == 'h' && char.IsDigit(k[1]) && v.Length > 0) s.hist.Add(v);
                    }
                    if (s.state == "running" && age < 45) s.rank = 1;
                    else if (s.state == "needs" && age < 20) s.rank = 4;
                    else if (s.state == "blocked" && age < 30) s.rank = 3;
                    else if (s.state == "ready" && age < 3) s.rank = 2;
                    if (s.rank > 0) { list.Add(s); RefreshContext(s, now); }
                }
        }
        catch { return; }
        list.Sort(delegate(Sess a, Sess b) { int c = b.rank.CompareTo(a.rank); return c != 0 ? c : b.time.CompareTo(a.time); });
        sessions = list;
        cRun = 0; cNeed = 0; cReady = 0; cBlock = 0;
        foreach (Sess s in list) { if (s.rank == 1) cRun++; else if (s.rank == 4) cNeed++; else if (s.rank == 3) cBlock++; else cReady++; }

        // Eine Sitzung, die von "arbeitet/fragt/Fehler" auf "fertig" wechselt, zählt als erledigte Aufgabe
        TrackSessions(list, now);
        foreach (Sess s in list)
        {
            string prev;
            if (s.state == "ready" && lastSess.TryGetValue(s.id, out prev) && (prev == "running" || prev == "needs" || prev == "blocked")) TaskDone(s, now);
        }
        lastSess.Clear();
        foreach (Sess s in list) lastSess[s.id] = s.state;
        CheckAchievements(now);
        CheckContextWarn(list);

        Sess top = list.Count > 0 ? list[0] : null;
        focusHwnd = top != null ? new IntPtr(top.hwnd) : IntPtr.Zero;
        string ns = top != null ? top.state : "none";
        cTool = top != null && top.state == "running" ? top.tool : "";
        cDetail = top != null ? top.detail : "";

        // Claude räumt auf (fasst den Kontext zusammen)
        bool compNow = false;
        foreach (Sess s in list) if (s.compact) compNow = true;
        if (compNow != cCompact)
        {
            cCompact = compNow;
            SayQ(compNow ? "Claude räumt kurz seinen Kopf auf...\nDas dauert einen Moment." : "Aufgeräumt! Es geht gleich weiter.", 4);
        }

        bool levelShown = false;
        if (ns != lastState)
        {
            string proj = top != null ? top.Label : "";
            string tag = proj.Length > 0 ? " (" + proj + ")" : "";
            if (ns == "needs")
            {
                string what = ToolWords(top.tool, top.detail);
                sleeping = false; hop = 22; walkFrames = 0; Burst("★", 2);
                SayQ("Claude braucht dich!" + tag + (what.Length > 0 ? "\n" + what : ""), 8); nextAttn = now.AddSeconds(4);
                Chime(false);
            }
            else if (ns == "blocked")
            {
                sleeping = false; hop = 22; walkFrames = 0; nextComfort = now.AddSeconds(25);
                blockErr = top.err;
                SayQ(BlockHeadline(top.err) + tag + "\nKopf hoch, ich bleibe bei dir.", 9);
            }
            else if (ns == "ready" && (lastState == "running" || lastState == "needs" || lastState == "blocked"))
            {
                hop = 22; Burst("♥", 3);
                string vorschau = PreviewOn() && top.msg.Length > 0 ? "\n„" + top.msg + "“" : "";     // nur mit Schalter "Antwort-Vorschau"
                SayQ("Claude ist fertig!" + tag + longNote + vorschau + (levelMsg.Length > 0 ? "\n" + levelMsg : ""), vorschau.Length > 0 ? 10 : 6);
                longNote = "";
                if (levelMsg.Length > 0) { levelShown = true; levelMsg = ""; }
            }
            else if (ns == "running")
            {
                if (lastState == "blocked") Say("Na also, es geht weiter!", 4);
                else if (!focusMode) SayQ("Claude arbeitet..." + tag, 3);
            }
            lastState = ns;
        }
        // lange Aufgabe, während du woanders warst: größere Feier
        if (bigParty)
        {
            bigParty = false; hop = 22; Burst("★", 8);
            Chime(true);
            if (ns != "ready") { SayQ("Eine lange Aufgabe ist fertig" + longNote + "!", 8); longNote = ""; }
        }
        if (levelMsg.Length > 0 && !levelShown) { Say(levelMsg, 6); hop = 22; Burst("★", 3); levelMsg = ""; }

        // im Fokus-Modus bleibt "arbeitet" unsichtbar
        cState = (focusMode && ns == "running") ? "none" : ns;
        if (ns == "needs" && !dragging && now >= nextAttn && hop <= 0) { hop = 22; nextAttn = now.AddSeconds(4); }
        TrackWork(now);
        // Bei einem Fehler bleibt das Pet in der Nähe und baut ab und zu auf
        if (ns == "blocked" && !dragging && now >= nextComfort)
        {
            nextComfort = now.AddSeconds(25);
            Say(BlockComfort(), 5);
            Burst("♥", 1);
        }
        CheckRecap(now);
    }

    // Seit wie vielen Minuten läuft die Aufgabe dieser Sitzung?
    int SessMinutes(Sess s)
    {
        DateTime st;
        if (s.state != "ready" && sessStart.TryGetValue(s.id, out st)) return (int)(DateTime.Now - st).TotalMinutes;
        return 0;
    }
    static string FmtMins(int m) { return m < 60 ? m + " Min." : (m / 60) + " Std. " + (m % 60) + " Min."; }

    // Text für den Mauszeiger über dem Pet: Laufzeit und letzte Schritte (bei einer Sitzung), sonst eine Zeile je Sitzung
    string InfoText()
    {
        if (sessions.Count == 0) return "";
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        if (sessions.Count == 1)
        {
            Sess s = sessions[0]; int m = SessMinutes(s);
            sb.Append(s.Label + (m >= 1 ? " · seit " + FmtMins(m) : "") + (s.ctxPct > 0 ? " · " + CtxText(s) : ""));
            List<string> lines = new List<string>();
            foreach (string h in s.hist)
            {
                string[] p = h.Split('|');
                string w = ToolWords(p[0], p.Length > 1 ? p[1] : "");
                if (w.Length > 0) lines.Add(w);
            }
            if (lines.Count == 0) lines.Add(StateWords(s));
            for (int i = 0; i < lines.Count && i < 5; i++) sb.Append("\n" + (i == 0 ? "› " : "· ") + lines[i]);
        }
        else
        {
            for (int i = 0; i < sessions.Count && i < 4; i++)
            {
                Sess s = sessions[i]; int m = SessMinutes(s);
                sb.Append((i > 0 ? "\n" : "") + s.Label + ": " + StateWords(s) + (m >= 1 ? " (" + FmtMins(m) + ")" : "") + (s.ctxPct > 0 ? " · " + s.ctxPct + " %" : ""));
            }
        }
        return sb.ToString();
    }

    // ---- Fehlerarten: Was ist schiefgegangen? (Feld "error" des Hooks StopFailure) ----
    string blockErr = "";
    bool cCompact;

    static string BlockHeadline(string err)
    {
        switch (err)
        {
            case "rate_limit": return "Das Limit ist erreicht.";
            case "overloaded": return "Die Server sind überlastet.";
            case "authentication_failed": return "Die Anmeldung ist abgelaufen.\nBitte in Claude neu anmelden.";
            case "billing_error": return "Es gibt ein Problem mit der Abrechnung.";
            case "invalid_request": return "Claude konnte die Anfrage nicht verarbeiten.";
            case "model_not_found": return "Das Modell wurde nicht gefunden.";
            case "server_error": return "Auf dem Server ist ein Fehler passiert.";
            case "max_output_tokens": return "Die Antwort war zu lang und wurde abgeschnitten.";
            case "cloud_credential_error": return "Die Cloud-Zugangsdaten stimmen nicht.";
            default: return "Claude steckt fest (Fehler).";
        }
    }
    // Zuspruch, der zur Fehlerart passt
    string BlockComfort()
    {
        switch (blockErr)
        {
            case "rate_limit": return Pick("Ein bisschen Geduld, das Limit läuft wieder ab.", "Zeit für eine kleine Pause?", "Das Limit ist nicht deine Schuld.");
            case "overloaded": return Pick("Gleich nochmal versuchen, die Server beruhigen sich.", "Das liegt nicht an dir.");
            case "authentication_failed": return Pick("Einmal neu anmelden, dann geht es weiter.", "Das kommt vor.");
            default: return Pick("Das wird schon.", "Ich bin da.", "Kurz durchatmen.", "Das war nicht deine Schuld.");
        }
    }

    // "liest Datei.cs", "führt npm aus" ...
    internal static string ToolWords(string tool, string detail)
    {
        string d = detail != null && detail.Length > 0 ? " " + detail : "";
        switch (tool)
        {
            case "read": return "liest" + d;
            case "write": return "schreibt" + d;
            case "search": return "sucht im Web" + d;
            case "exec": return detail != null && detail.Length > 0 ? "führt " + detail + " aus" : "führt einen Befehl aus";
            case "agent": return "startet einen Helfer";
            case "other": return "nutzt ein Werkzeug";
            default: return "";
        }
    }

    // Holt ein Fenster nach vorn
    bool FocusWindow(IntPtr h)
    {
        if (h == IntPtr.Zero || !IsWindow(h)) return false;
        if (IsIconic(h)) ShowWindow(h, 9);
        SetForegroundWindow(h);
        hop = 22; act = "none"; actFrames = 0; Say("Los geht's!", 2);
        return true;
    }
    // ... das der wichtigsten Sitzung
    bool GoToClaude() { return FocusWindow(focusHwnd); }
}
