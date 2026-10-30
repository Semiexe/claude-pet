using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Kern: Fenster, Zustand, Speichern, Start. Verhalten, Zeichnen, Status und Menü stehen in den anderen Dateien.
public partial class PetForm : Form
{
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLEND { public byte Op, Flags, Alpha, Format; }
    [DllImport("user32.dll", SetLastError = true)] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLEND pblend, int dwFlags);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr o);

    public static string ScriptPath = "";
    const int LW = 240, LH = 240;                       // logische Fenstergröße
    float sc = 1f, baseSc = 1f, zoom = 1f; bool still;  // sc = DPI-Faktor * Zoom
    int W, H;
    Random rnd = new Random();
    int px, py, vy, dir = 1;
    float t;
    System.Windows.Forms.Timer timer;
    string path, stateDir;

    // Werte und Name
    float food = 80, joy = 80, energy = 90;
    bool sleeping;
    string petName = "Claude";
    DateTime born = DateTime.Now, lastSaved = DateTime.Now;
    int savedX = int.MinValue;

    public PetForm(bool headless)
    {
        if (!headless) SetProcessDPIAware();
        using (Graphics g0 = Graphics.FromHwnd(IntPtr.Zero)) { sc = headless ? 1f : g0.DpiX / 96f; }
        pRay.StartCap = LineCap.Round; pRay.EndCap = LineCap.Round;
        pMouth.StartCap = LineCap.Round; pMouth.EndCap = LineCap.Round;
        path = Path.Combine(DataDir(), "state.txt");
        stateDir = Path.Combine(DataDir(), "status");
        LoadState();
        LoadPhrases();
        LoadReminders();
        LoadCustomHat();
        PruneToolLog();
        ApplyFur();
        baseSc = sc; sc = baseSc * zoom; W = (int)(LW * sc); H = (int)(LH * sc);
        still = !SystemInformation.UIEffectsEnabled;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(W, H);
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        px = wa.Left + wa.Width / 2;
        if (savedX != int.MinValue)
        {
            int mid = savedX + W / 2;
            foreach (Screen s2 in Screen.AllScreens)
                if (mid >= s2.WorkingArea.Left && mid < s2.WorkingArea.Right) { px = savedX; wa = s2.WorkingArea; }
        }
        py = wa.Bottom - H + (int)(14 * sc);
        Location = new Point(px, py);

        BuildMenu();

        timer = new System.Windows.Forms.Timer(); timer.Interval = 33;
        // Gezeichnet wird voll (30 Mal pro Sekunde) nur, wenn sich etwas bewegt; im Stand etwa 10 Mal pro Sekunde
        timer.Tick += delegate { Step(); if (Visible && (NeedFast() || ++idleTick % 3 == 0)) Render(); };
        FormClosing += delegate { SaveState(); UnregisterHotKey(Handle, 1); DisposeMinis(); DisposeBall(); CloseGuest(); };
        Shown += delegate {
            Say(DateTime.Now - lastSaved > TimeSpan.FromMinutes(10) ? (food < 25 ? "Du warst lange weg. Ich habe Hunger!" : "Schön, dass du wieder da bist!") : Greeting() + " Ich bin " + petName + ".");
            timer.Start();
        };
        MouseDown += OnDown; MouseMove += OnMove; MouseUp += OnUp;
    }

    protected override CreateParams CreateParams
    {
        get { CreateParams cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x80; return cp; }
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterHotKey(Handle, 1, 0x0008 | 0x0001, 0x50);   // Win+Alt+P
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) { ToggleVisible(); return; }
        base.WndProc(ref m);
    }

    int FloorY() { return AreaAt(new Point(px + W / 2, py + H / 2)).Bottom - H + (int)(14 * sc); }
    static float C(float v) { return Math.Max(0f, Math.Min(100f, v)); }

    void LoadState() { LoadState(true); }
    void LoadState(bool decay)
    {
        try
        {
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path))
            {
                int i = line.IndexOf('='); if (i < 0) continue;
                string k = line.Substring(0, i), v = line.Substring(i + 1);
                if (k == "name") petName = v;
                else if (k == "x") savedX = int.Parse(v);
                else if (k == "focus") focusMode = v == "1";
                else if (k == "minis") minisOn = v == "1";
                else if (k == "break") breakOn = v == "1";
                else if (k == "autohide") autoHideOn = v == "1";
                else if (k == "sound") soundOn = v == "1";
                else if (k == "hat") hatMode = v;
                else if (k == "tasks") tasksTotal = int.Parse(v);
                else if (k == "today") todayKey = v;
                else if (k == "todayn") tasksToday = int.Parse(v);
                else if (k == "ach") { achDone.Clear(); foreach (string x in v.Split(',')) if (x.Length > 0) achDone.Add(x); }
                else if (k == "streak") streak = int.Parse(v);
                else if (k == "lastday") lastDay = v;
                else if (k == "todayproj") { projToday.Clear(); foreach (string x in v.Split('|')) if (x.Length > 0) projToday.Add(x); }
                else if (k == "longest") longestMin = int.Parse(v);
                else if (k == "longestproj") longestProj = v;
                else if (k == "recapday") recapDay = v;
                else if (k == "species") species = Array.IndexOf(SpeciesKeys, v) >= 0 ? v : "claude";
                else if (k == "projcolors")
                {
                    projTint.Clear();
                    foreach (string x in v.Split('|'))
                    {
                        int c = x.LastIndexOf(':'), idx;
                        if (c > 0 && int.TryParse(x.Substring(c + 1), out idx) && idx >= 0 && idx < 4) projTint[x.Substring(0, c)] = idx;
                    }
                }
                else if (k == "season") seasonOn = v == "1";
                else if (k == "eyes") eyesOn = v == "1";
                else if (k == "dance") danceOn = v == "1";
                else if (k == "ctx") ctxOn = v == "1";
                else if (k == "ctxmode") ctxMode = v == "200" || v == "1000" ? v : "auto";
                else if (k == "evo") int.TryParse(v, out evoSeen);
                else if (k == "fur") furKey = v;
                else if (k == "voice") voiceOn = v == "1";
                else if (k == "battery") batteryOn = v == "1";
                else if (k == "water") waterOn = v == "1";
                else if (k == "diaryday") diaryDay = v;
                else if (k == "weekkey") weekKey = v;
                else if (k == "guestday") guestDay = v;
                else if (k == "gamebest") gameBest = int.Parse(v);
                else if (k == "zoom") zoom = float.Parse(v, CultureInfo.InvariantCulture);
                else if (k == "food") food = float.Parse(v, CultureInfo.InvariantCulture);
                else if (k == "joy") joy = float.Parse(v, CultureInfo.InvariantCulture);
                else if (k == "energy") energy = float.Parse(v, CultureInfo.InvariantCulture);
                else if (k == "sleeping") sleeping = v == "1";
                else if (k == "born") born = new DateTime(long.Parse(v));
                else if (k == "saved") lastSaved = new DateTime(long.Parse(v));
            }
            float away = (float)Math.Min((DateTime.Now - lastSaved).TotalMinutes, 720);
            if (decay && away > 0) Decay(away);
        }
        catch { }
    }
    void SaveState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, new string[] {
                "name=" + petName, "zoom=" + zoom.ToString(CultureInfo.InvariantCulture), "x=" + px, "focus=" + (focusMode ? "1" : "0"),
                "minis=" + (minisOn ? "1" : "0"), "break=" + (breakOn ? "1" : "0"), "autohide=" + (autoHideOn ? "1" : "0"),
                "sound=" + (soundOn ? "1" : "0"), "hat=" + hatMode, "tasks=" + tasksTotal, "today=" + todayKey, "todayn=" + tasksToday,
                "ach=" + string.Join(",", achDone.ToArray()), "streak=" + streak, "lastday=" + lastDay,
                "todayproj=" + string.Join("|", projToday.ToArray()), "longest=" + longestMin, "longestproj=" + longestProj, "recapday=" + recapDay,
                "species=" + species, "projcolors=" + ProjColorsText(), "season=" + (seasonOn ? "1" : "0"), "fur=" + furKey, "voice=" + (voiceOn ? "1" : "0"), "battery=" + (batteryOn ? "1" : "0"), "water=" + (waterOn ? "1" : "0"),
                "eyes=" + (eyesOn ? "1" : "0"), "dance=" + (danceOn ? "1" : "0"), "ctx=" + (ctxOn ? "1" : "0"), "ctxmode=" + ctxMode, "evo=" + evoSeen,
                "diaryday=" + diaryDay, "weekkey=" + weekKey, "guestday=" + guestDay, "gamebest=" + gameBest,
                "food=" + food.ToString(CultureInfo.InvariantCulture),
                "joy=" + joy.ToString(CultureInfo.InvariantCulture),
                "energy=" + energy.ToString(CultureInfo.InvariantCulture),
                "sleeping=" + (sleeping ? "1" : "0"),
                "born=" + born.Ticks, "saved=" + DateTime.Now.Ticks });
        }
        catch { }
    }
    string ProjColorsText()
    {
        List<string> parts2 = new List<string>();
        foreach (KeyValuePair<string, int> kv in projTint) parts2.Add(kv.Key + ":" + kv.Value);
        return string.Join("|", parts2.ToArray());
    }

    void SetZoom(float z)
    {
        zoom = z; sc = baseSc * zoom; W = (int)(LW * sc); H = (int)(LH * sc);
        Size = new Size(W, H); py = FloorY(); SaveState();
    }
    // Verstecken per Hand merkt sich, dass das Pet nach einem Vollbild nicht von selbst zurückkommen soll
    void ToggleVisible()
    {
        if (Visible) { Hide(); userHidden = true; }
        else { Show(); userHidden = false; autoHidden = false; }
    }

    public static void Run() { RunGuarded(); }

    // ---- Testbilder (nur für die Entwicklung) ----
    public static void MenuSnapshot(string file)
    {
        PetForm f = new PetForm(true);
        f.cNeed = 1; f.cState = "needs"; f.tasksTotal = 40; f.todayKey = DateTime.Now.ToString("yyyyMMdd"); f.tasksToday = 5; f.hatMode = "crown"; f.achDone.Add("first"); f.achDone.Add("t10"); f.achDone.Add("night");
        string[,] demo = new string[,] {
            { "needs", "Webshop", "exec", "npm" }, { "running", "ClaudePet", "write", "PetDraw.cs" },
            { "running", "Notizen", "read", "plan.md" }, { "ready", "Blog", "", "" } };
        for (int i = 0; i < 4; i++)
        {
            Sess s = new Sess(); s.state = demo[i, 0]; s.project = demo[i, 1]; s.tool = demo[i, 2]; s.detail = demo[i, 3];
            f.sessions.Add(s);
        }
        f.menu.Show(new Point(60, 60));
        for (int i = 0; i < 10; i++) { Application.DoEvents(); Thread.Sleep(30); }
        using (Bitmap b = new Bitmap(f.menu.Width, f.menu.Height))
        {
            f.menu.DrawToBitmap(b, new Rectangle(0, 0, b.Width, b.Height));
            b.Save(file, ImageFormat.Png);
        }
        f.menu.Close();
    }

    public static void Snapshot(string file, string st)
    {
        PetForm f = new PetForm(true);
        f.food = 70; f.sleeping = false;
        for (int i = 0; i < 40; i++) f.Step();
        f.Say("Hallo, ich bin Claude!");
        if (st == "sit" || st == "stretch" || st == "wave") f.act = st;
        else if (st != null && st.StartsWith("tool:")) { f.cState = "running"; f.cTool = st.Substring(5); }
        else if (st != null && st.StartsWith("hat:")) { f.tasksTotal = 200; foreach (Ach a in Achs) f.achDone.Add(a.id); f.hatMode = st.Substring(4); }
        else if (st != null && st.StartsWith("lv:")) { int lv = int.Parse(st.Substring(3)); f.tasksTotal = 2 * (lv - 1) * (lv - 1); f.hatMode = "none"; }
        else if (st != null && st.StartsWith("fur:")) { f.tasksTotal = 200; foreach (Ach a in Achs) f.achDone.Add(a.id); f.furKey = st.Substring(4); f.ApplyFur(); }
        else if (st != null && st.StartsWith("stage:"))
        {
            // stage:<figur>:<0-2>[:dance|gaze|ctx]
            string[] sp = st.Split(':');
            f.species = sp[1]; f.hatMode = sp[1] == "claude" && sp[2] == "2" ? "crown" : "none"; f.bubbleUntil = DateTime.MinValue; f.evoPreview = int.Parse(sp[2]); f.evoPrevUntil = DateTime.Now.AddHours(1);
            f.tasksTotal = f.evoPreview == 2 ? 800 : f.evoPreview == 1 ? 200 : 20; f.ApplyFur();
            if (sp.Length > 3 && sp[3] == "dance") { f.danceRaw = true; f.musicSecs = 5; f.danceStep = 1; f.beatPulse = 0.6f; }
            if (sp.Length > 3 && sp[3] == "gaze") f.eyeUntil = DateTime.Now.AddSeconds(8);
            if (sp.Length > 3 && sp[3] == "ctx")
            {
                Sess cs = new Sess(); cs.state = "running"; cs.ctxPct = int.Parse(sp[4]); f.sessions.Add(cs); f.cState = "running"; f.cTool = "read";
            }
        }
        else if (st != null && st.StartsWith("ctx:"))
        {
            Sess cs = new Sess(); cs.state = "running"; cs.ctxPct = int.Parse(st.Substring(4)); f.sessions.Add(cs);
        }
        else if (st == "dance") { f.danceRaw = true; f.musicSecs = 5; f.danceStep = 1; f.beatPulse = 0.6f; }
        else if (st == "gaze") { f.eyeUntil = DateTime.Now.AddSeconds(8); }
        else if (st == "evoflash") { f.tasksTotal = 2 * 9 * 9; f.evoSeen = 1; f.evoFrames = 80; f.hatMode = "none"; }
        else if (st == "fox") { f.species = "fox"; f.ApplyFur(); }
        else if (st == "foxhop") { f.species = "fox"; f.ApplyFur(); f.hatMode = "none"; f.hop = 12; f.t = 0.25f; }
        else if (st == "jellyhop") { f.species = "jelly"; f.ApplyFur(); f.hatMode = "none"; f.hop = 12; f.t = 0.25f; }
        else if (st == "jelly") { f.species = "jelly"; f.ApplyFur(); f.hatMode = "none"; }
        else if (st == "jellysleep") { f.species = "jelly"; f.ApplyFur(); f.sleeping = true; f.hatMode = "none"; }
        else if (st == "jellywalk") { f.species = "jelly"; f.ApplyFur(); f.walking = true; f.t = 1.7f; f.hatMode = "none"; }
        else if (st == "jellyneeds") { f.species = "jelly"; f.ApplyFur(); f.cState = "needs"; f.hatMode = "crown"; f.tasksTotal = 60; }
        else if (st == "jellycard")
        {
            f.species = "jelly"; f.ApplyFur(); f.hatMode = "none"; f.tasksTotal = 47; f.tasksToday = 3; f.todayKey = DateTime.Now.ToString("yyyyMMdd");
            using (Bitmap card = f.BuildCard()) card.Save(file, ImageFormat.Png);
            return;
        }
        else if (st == "foxwave") { f.species = "fox"; f.ApplyFur(); f.act = "wave"; }
        else if (st == "bed") f.sleeping = true;
        else if (st == "bowl") f.food = 15;
        else if (st == "compact") f.cCompact = true;
        else if (st == "helpers")
        {
            Sess hs = new Sess(); hs.state = "running"; hs.tool = "agent"; hs.agents = 3; f.sessions.Add(hs); f.cState = "running"; f.cTool = "agent";
        }
        else if (st == "snow" || st == "leaves" || st == "fireworks")
        {
            DateTime t0 = DateTime.Now;
            for (int i = 0; i < 260; i++) f.UpdateSeasonCore(st, t0.AddMilliseconds(i * 40));
        }
        else if (st == "customhat")
        {
            customArt = new string[] { "....##......", "...####.....", "..######....", ".#oooooo#...", "############" };
            customMain = Color.FromArgb(40, 160, 120); customAcc = Color.FromArgb(250, 120, 60);
            f.hatMode = "custom";
        }
        else if (st == "note") { f.noteUntil = DateTime.Now.AddSeconds(30); f.Say("Erinnerung:\nTrink einen Schluck Wasser!", 30); }
        else if (st == "foxcard")
        {
            f.species = "fox"; f.ApplyFur(); f.hatMode = "none"; f.tasksTotal = 47; f.tasksToday = 3; f.todayKey = DateTime.Now.ToString("yyyyMMdd");
            using (Bitmap card = f.BuildCard()) card.Save(file, ImageFormat.Png);
            return;
        }
        else if (st == "card")
        {
            f.tasksTotal = 47; f.tasksToday = 3; f.todayKey = DateTime.Now.ToString("yyyyMMdd"); f.achDone.Add("first"); f.achDone.Add("t10"); f.hatMode = "bow";
            using (Bitmap card = f.BuildCard()) card.Save(file, ImageFormat.Png);
            return;
        }
        else if (st == "minis")
        {
            // Hauptbild plus drei Mini-Pets in verschiedenen Zuständen
            f.hatMode = "none"; f.bubbleStart = DateTime.Now.AddSeconds(-5);
            string[] ms = new string[] { "needs", "running", "ready" }, mt = new string[] { "", "write", "" };
            using (Bitmap big = new Bitmap(f.W + 440, f.H, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(big))
                {
                    g.Clear(Color.FromArgb(223, 230, 238));
                    f.Draw(g);
                    g.ResetTransform();
                    for (int i = 0; i < 3; i++)
                    {
                        Sess s = new Sess(); s.state = ms[i]; s.tool = mt[i]; s.project = new string[] { "Webshop", "ClaudePet", "Notizen" }[i];
                        MiniPet m = new MiniPet(f, 0, 1f, i);
                        m.S = s;
                        using (Bitmap mb = m.Snap()) g.DrawImage(mb, f.W + i * 146, 130);
                    }
                }
                big.Save(file, ImageFormat.Png);
            }
            return;
        }
        else if (st == "multi")
        {
            f.cState = "needs";
            string[] ss = new string[] { "needs", "running", "ready", "blocked" };
            foreach (string x in ss) { Sess s = new Sess(); s.state = x; f.sessions.Add(s); }
        }
        else if (!string.IsNullOrEmpty(st)) f.cState = st;
        f.Burst("♥", 3);
        using (Bitmap bmp = new Bitmap(f.W, f.H, PixelFormat.Format32bppPArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.FromArgb(223, 230, 238)); f.Draw(g); }
            bmp.Save(file, ImageFormat.Png);
        }
    }
}
