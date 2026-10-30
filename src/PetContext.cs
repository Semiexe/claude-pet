using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;

// Kontext-Füllstand: Wie voll ist Claudes Gedächtnis in einer Sitzung?
// Gelesen wird nur die Zahl der Tokens aus der letzten Antwort in Claudes eigener Sitzungsdatei (~\.claude\projects\...\<Sitzung>.jsonl).
// Der Inhalt der Unterhaltung wird dabei nicht ausgewertet und nirgends gespeichert.
class CtxInfo
{
    public string path;
    public long len;
    public int tokens;
    public DateTime next = DateTime.MinValue, nextFind = DateTime.MinValue;
}

public partial class PetForm
{
    bool ctxOn = true;
    string ctxMode = "auto";                        // auto | 200 | 1000 (Größe des Fensters in tausend Tokens)
    Dictionary<string, CtxInfo> ctxInfo = new Dictionary<string, CtxInfo>();
    Dictionary<string, bool> ctxWarned = new Dictionary<string, bool>();
    DateTime ctxSettingsAt = DateTime.MinValue; bool ctxSettings1M;

    static string ProjectsRoot()
    {
        string env = Environment.GetEnvironmentVariable("CLAUDEPET_PROJECTS");
        if (!string.IsNullOrEmpty(env)) return env;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
    }

    // Steht in Claudes Einstellungen ein Modell mit [1m] (eine Million Tokens Fenster)?
    bool Settings1M()
    {
        DateTime now = DateTime.Now;
        if ((now - ctxSettingsAt).TotalSeconds < 60) return ctxSettings1M;
        ctxSettingsAt = now; ctxSettings1M = false;
        try
        {
            string p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
            if (File.Exists(p)) ctxSettings1M = Regex.IsMatch(File.ReadAllText(p), "\"model\"\\s*:\\s*\"[^\"]*\\[1m\\]\"", RegexOptions.IgnoreCase);
        }
        catch { }
        return ctxSettings1M;
    }

    // Größe des Kontextfensters in Tokens
    int CtxWindow(int tokens)
    {
        if (ctxMode == "200") return 200000;
        if (ctxMode == "1000") return 1000000;
        return tokens > 195000 || Settings1M() ? 1000000 : 200000;
    }
    string CtxModeText() { return ctxMode == "200" ? "200 Tsd." : ctxMode == "1000" ? "1 Mio." : "automatisch"; }

    // Die letzte Kontextgröße aus dem Ende der Sitzungsdatei: Eingabe + Cache-Anteile der letzten Antwort
    internal static int LastContextTokens(string file)
    {
        foreach (int chunk in new int[] { 256 * 1024, 2 * 1024 * 1024 })
        {
            string text;
            using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long take = Math.Min(fs.Length, chunk);
                fs.Seek(fs.Length - take, SeekOrigin.Begin);
                byte[] buf = new byte[take];
                int got = 0; while (got < take) { int n = fs.Read(buf, got, (int)take - got); if (n <= 0) break; got += n; }
                text = System.Text.Encoding.UTF8.GetString(buf, 0, got);
                if (fs.Length <= chunk) { int t0 = ScanUsage(text); return t0; }
            }
            int t = ScanUsage(text);
            if (t > 0) return t;
        }
        return 0;
    }

    static int ScanUsage(string text)
    {
        string[] lines = text.Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string ln = lines[i];
            int u = ln.LastIndexOf("\"usage\":{", StringComparison.Ordinal);
            if (u < 0) continue;
            if (ln.IndexOf("\"isSidechain\":true", StringComparison.Ordinal) >= 0 || ln.IndexOf("<synthetic>", StringComparison.Ordinal) >= 0) continue;
            string part = ln.Substring(u, Math.Min(500, ln.Length - u));
            int sum = Num(part, "input_tokens") + Num(part, "cache_creation_input_tokens") + Num(part, "cache_read_input_tokens");
            if (sum > 0) return sum;
        }
        return 0;
    }
    static int Num(string s, string key)
    {
        Match m = Regex.Match(s, "\"" + key + "\":(\\d+)");
        int v;
        return m.Success && int.TryParse(m.Groups[1].Value, out v) ? v : 0;
    }

    // Holt den Füllstand für eine Sitzung (höchstens alle paar Sekunden und nur, wenn die Datei sich geändert hat)
    void RefreshContext(Sess s, DateTime now)
    {
        if (!ctxOn || s.id.Length == 0) return;
        CtxInfo ci;
        if (!ctxInfo.TryGetValue(s.id, out ci)) { ci = new CtxInfo(); ctxInfo[s.id] = ci; }
        if (now >= ci.next)
        {
            ci.next = now.AddSeconds(s.state == "running" ? 4 : 15);
            try
            {
                if (ci.path == null || !File.Exists(ci.path))
                {
                    ci.path = null;
                    if (now >= ci.nextFind)
                    {
                        ci.nextFind = now.AddSeconds(60);
                        string root = ProjectsRoot();
                        if (Directory.Exists(root))
                            foreach (string d in Directory.GetDirectories(root))
                            {
                                string f = Path.Combine(d, s.id + ".jsonl");
                                if (File.Exists(f)) { ci.path = f; ci.len = -1; break; }
                            }
                    }
                }
                if (ci.path != null)
                {
                    long len = new FileInfo(ci.path).Length;
                    if (len != ci.len) { ci.len = len; int t = LastContextTokens(ci.path); if (t > 0) ci.tokens = t; }
                }
            }
            catch { }
        }
        s.ctx = ci.tokens;
        s.ctxPct = ci.tokens > 0 ? Math.Min(100, (int)Math.Round(ci.tokens * 100.0 / CtxWindow(ci.tokens))) : 0;
    }

    // Einmal warnen, wenn eine Sitzung fast voll ist; erneut erst, nachdem sie wieder unter 60 % war (zum Beispiel nach dem Aufräumen)
    void CheckContextWarn(List<Sess> list)
    {
        if (!ctxOn) return;
        foreach (Sess s in list)
        {
            bool w;
            ctxWarned.TryGetValue(s.id, out w);
            if (s.ctxPct >= 85 && !w)
            {
                ctxWarned[s.id] = true;
                string proj = s.Label.Length > 0 && sessions.Count > 1 ? " (" + s.Label + ")" : "";
                hop = 22; sleeping = false;
                SayQ("Mein Kopf ist fast voll: " + s.ctxPct + " %." + proj + "\nGleich wird zusammengefasst.", 7);
            }
            else if (w && s.ctxPct > 0 && s.ctxPct < 60) ctxWarned[s.id] = false;
        }
    }

    static string CtxText(Sess s) { return s.ctxPct > 0 ? "Kontext " + s.ctxPct + " %" : ""; }

    // Bauchanzeige: ein Streifen an der Unterkante des Körpers (bei der Qualle ein Balken unter dem Pet), der sich mit dem Kontext füllt
    void DrawCtxGauge(Graphics g, int ox, int oy, int by, int pu)
    {
        if (!ctxOn || sessions.Count == 0 || sessions[0].ctxPct <= 0) return;
        int pct = sessions[0].ctxPct;
        Brush fill = pct >= 85 ? bBad : pct >= 65 ? bNeed : bOk;
        int x0, y0, w, h;
        if (Jelly()) { x0 = ox + 8 * pu; y0 = oy + 16 * pu + pu / 2; w = 8 * pu; h = Math.Max(2, pu / 2); }
        else { x0 = ox + 5 * pu; y0 = by + 11 * pu; w = 14 * pu; h = pu; }
        g.FillRectangle(bShadow, x0, y0, w, h);
        int fw = Math.Max(pu / 2, (int)(w * pct / 100.0));
        g.FillRectangle(fill, x0, y0, fw, h);
        if (pct >= 85 && ((int)(t * 3)) % 2 == 0) g.FillRectangle(bGlowWhite, x0, y0, fw, Math.Max(1, h / 3));        // blinkt leicht, wenn es voll wird
    }
    Brush bGlowWhite = new SolidBrush(Color.FromArgb(110, 255, 255, 255));

    // Test: Sitzungsdatei vortäuschen
    public static string TestContext()
    {
        string root = Path.Combine(Path.GetTempPath(), "cp_ctx_test_" + Guid.NewGuid().ToString("N").Substring(0, 6));
        string proj = Path.Combine(root, "C--demo"); Directory.CreateDirectory(proj);
        string id = "11111111-2222-3333-4444-555555555555";
        string file = Path.Combine(proj, id + ".jsonl");
        string big = new string('x', 300000);
        File.WriteAllText(file,
            "{\"type\":\"assistant\",\"message\":{\"model\":\"claude-x\",\"usage\":{\"input_tokens\":3,\"cache_creation_input_tokens\":1000,\"cache_read_input_tokens\":99000,\"output_tokens\":5,\"output_tokens_details\":{\"thinking_tokens\":1}}}}\n" +
            "{\"type\":\"user\",\"message\":{\"content\":\"" + big + "\"}}\n" +
            "{\"type\":\"assistant\",\"isSidechain\":true,\"message\":{\"usage\":{\"input_tokens\":7,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}\n");
        Environment.SetEnvironmentVariable("CLAUDEPET_PROJECTS", root);
        PetForm f = new PetForm(true);
        Sess s = new Sess(); s.id = id; s.state = "running"; s.project = "demo";
        f.RefreshContext(s, DateTime.Now);
        string a = "gefunden=" + (f.ctxInfo[id].path != null) + " tokens=" + s.ctx + " pct=" + s.ctxPct + "% (erwartet 100000 / 50%)";
        // Datei wächst: neue Antwort mit 180000 Tokens -> 90 %, Warnung
        File.AppendAllText(file, "{\"type\":\"assistant\",\"message\":{\"usage\":{\"input_tokens\":10,\"cache_creation_input_tokens\":5000,\"cache_read_input_tokens\":174990,\"output_tokens\":1}}}\n");
        f.ctxInfo[id].next = DateTime.MinValue;
        f.RefreshContext(s, DateTime.Now);
        List<Sess> l = new List<Sess>(); l.Add(s);
        f.sessions = l;
        f.CheckContextWarn(l);
        string b = " | danach tokens=" + s.ctx + " pct=" + s.ctxPct + "% warnung=\"" + f.bubble.Replace("\n", " / ") + "\"";
        // mehr als 195k: Fenster 1 Mio.
        File.AppendAllText(file, "{\"type\":\"assistant\",\"message\":{\"usage\":{\"input_tokens\":10,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":300000}}}\n");
        f.ctxInfo[id].next = DateTime.MinValue;
        f.RefreshContext(s, DateTime.Now);
        string c = " | gross: tokens=" + s.ctx + " pct=" + s.ctxPct + "% (erwartet 30%)";
        // nach dem Aufräumen wieder klein
        File.AppendAllText(file, "{\"type\":\"assistant\",\"message\":{\"usage\":{\"input_tokens\":2,\"cache_creation_input_tokens\":3000,\"cache_read_input_tokens\":10000}}}\n");
        f.ctxInfo[id].next = DateTime.MinValue;
        f.RefreshContext(s, DateTime.Now);
        f.CheckContextWarn(l);
        string d = " | nachCompact pct=" + s.ctxPct + "% warnungZurueck=" + (!f.ctxWarned[id]);
        try { Directory.Delete(root, true); } catch { }
        return a + b + c + d;
    }
}
