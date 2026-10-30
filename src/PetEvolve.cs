using System;
using System.Drawing;
using System.Reflection;

// Entwicklung: Bei Stufe 10 und Stufe 20 verändert sich die Figur.
//   Claude:   Held (Wappen, goldener Umhangsaum) und König (dazu Krone und goldene Funken)
//   Fuchs:    Silberfuchs (Schal) und Mondfuchs (Mondsichel, zweiter Schwanz, Funken)
//   Qualle:   Leuchtqualle (türkis, leuchtende Punkte) und Sternenqualle (tiefblau, Sterne, Heiligenschein)
public partial class PetForm
{
    int evoSeen;                                   // höchste Stufe, die schon gefeiert wurde (wird gespeichert)
    internal int evoFrames;                        // läuft die Entwicklungs-Animation? (120 Bilder)
    int evoPreview = -1; DateTime evoPrevUntil = DateTime.MinValue;
    int colStage = -1;
    Brush bSpark = new SolidBrush(Color.FromArgb(255, 250, 230));
    Brush bGoldS = new SolidBrush(Color.FromArgb(240, 196, 60)), bGoldD = new SolidBrush(Color.FromArgb(200, 150, 30));
    static readonly int[] StageLevels = new int[] { 1, 10, 20 };

    int RealStage() { int lv = Level(); return lv >= 20 ? 2 : lv >= 10 ? 1 : 0; }

    // Angezeigte Stufe: eine Vorschau aus dem Menü, sonst die echte (während der Animation erst die alte, dann die neue)
    int Stage()
    {
        if (evoPreview >= 0 && DateTime.Now < evoPrevUntil) return evoPreview;
        int s = RealStage();
        if (evoFrames > 60) s = Math.Max(0, s - 1);
        return s;
    }

    static readonly string[][] StageNames = new string[][] {
        new string[] { "Claude", "Claude, der Held", "Claude, der König" },
        new string[] { "Fuchs", "Silberfuchs", "Mondfuchs" },
        new string[] { "Qualle", "Leuchtqualle", "Sternenqualle" } };
    string StageName(int st)
    {
        int k = Array.IndexOf(SpeciesKeys, species); if (k < 0) k = 0;
        return StageNames[k][Math.Max(0, Math.Min(2, st))];
    }
    string StageName() { return StageName(Stage()); }

    // Passen die Farben der Figur noch zur angezeigten Stufe? Sonst neu anlegen.
    void EnsureStageColors()
    {
        int st = Stage();
        if (st != colStage) { colStage = st; ApplyFur(); }
    }

    // Prüft, ob eine neue Stufe erreicht wurde, und startet dann die Animation
    void CheckEvolution(DateTime now)
    {
        if (evoFrames > 0) return;
        int rs = RealStage();
        if (rs < evoSeen) { evoSeen = rs; return; }
        if (rs > evoSeen && !dragging)
        {
            evoSeen = rs; evoFrames = 120; walkFrames = 0; act = "none"; actFrames = 0; sleeping = false; exStep = -1; hop = 0;
            SayQ("Was passiert da?!\n" + petName + " leuchtet...", 3);
            Chime(true);
        }
    }
    // Mitte der Animation ist vorbei: die neue Gestalt ist da
    void EvolutionDone()
    {
        hop = 22; Burst("★", 8); Burst("♥", 3);
        string n = StageName(RealStage());
        Say(petName + " hat sich entwickelt!\nJetzt: " + n + ".", 7);
        try { WriteDiary("Entwicklung", petName + " hat sich zu " + n + " entwickelt (Stufe " + Level() + ")."); } catch { }
    }

    // Menü "Entwicklungen ansehen": zeigt der Reihe nach die drei Gestalten
    void PreviewStages()
    {
        int n = (DateTime.Now < evoPrevUntil && evoPreview >= 0) ? (evoPreview + 1) % 3 : 0;
        evoPreview = n; evoPrevUntil = DateTime.Now.AddSeconds(14);
        hop = 14;
        Say(StageName(n) + (n == 0 ? "" : "\n(ab Stufe " + StageLevels[n] + ")") + (RealStage() >= n ? "" : "\nNoch nicht erreicht: Du bist bei Stufe " + Level() + "."), 6);
    }

    // ---- Weiß aufleuchten: für die Animation werden alle Körperfarben kurz durch Weiß ersetzt ----
    static readonly string[] BodyBrushNames = new string[] {
        "bPet", "bShade", "bBlush", "bBelly", "bCape", "bCapeShade", "bEye", "bFoxWhite", "bFoxDark", "bFoxPink", "bFoxScarf", "bFoxMoon",
        "bJelB", "bJelS", "bJelL", "bJelT", "bJelG", "bJelStar", "bSpark", "bGoldS", "bGoldD" };
    static FieldInfo[] bodyFields;
    Brush[] savedBrushes; Brush flashBrush = new SolidBrush(Color.FromArgb(255, 255, 255));

    bool EvoWhite()
    {
        int f = evoFrames;
        if (f <= 0) return false;
        if (f > 90) return (f / 6) % 2 == 0;
        if (f > 60) return (f / 3) % 2 == 0;
        if (f > 45) return true;
        return f > 12 && (f / 4) % 2 == 0;
    }

    void WhiteBrushes(bool on)
    {
        if (bodyFields == null)
        {
            bodyFields = new FieldInfo[BodyBrushNames.Length];
            for (int i = 0; i < bodyFields.Length; i++) bodyFields[i] = typeof(PetForm).GetField(BodyBrushNames[i], BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }
        if (on)
        {
            savedBrushes = new Brush[bodyFields.Length];
            for (int i = 0; i < bodyFields.Length; i++) if (bodyFields[i] != null) { savedBrushes[i] = (Brush)bodyFields[i].GetValue(this); bodyFields[i].SetValue(this, flashBrush); }
        }
        else if (savedBrushes != null)
        {
            for (int i = 0; i < bodyFields.Length; i++) if (bodyFields[i] != null) bodyFields[i].SetValue(this, savedBrushes[i]);
            savedBrushes = null;
        }
    }

    // ---- Zeichnen der Gestalten ----

    // Claude, ab Stufe 10: goldenes Wappen auf dem Bauch und goldener Saum am Umhang
    void DrawClaudeEmblem(Graphics g, int ox, int by, int pu)
    {
        Px(g, bGoldS, ox, by, pu, 11, 7, 2);
        Px(g, bGoldS, ox, by, pu, 10, 8, 4);
        Px(g, bGoldS, ox, by, pu, 10, 9, 4);
        Px(g, bGoldD, ox, by, pu, 11, 10, 2);
        Px(g, bSpark, ox, by, pu, 11, 8, 1);
    }

    // goldene Funken rund um das Pet (Stufe 2)
    void DrawStageSparks(Graphics g, int ox, int by, int pu, Brush mid, Brush core)
    {
        int[,] spots = new int[,] { { 2, 0 }, { 21, 2 }, { 1, 7 }, { 22, 9 }, { 3, 13 }, { 20, 13 } };
        for (int i = 0; i < 6; i++)
        {
            if (((int)(t * 4) + i * 2) % 5 != 0) continue;
            int c = spots[i, 0], r = spots[i, 1];
            Px(g, core, ox, by, pu, c, r, 1);
            Px(g, mid, ox, by, pu, c - 1, r, 1); Px(g, mid, ox, by, pu, c + 1, r, 1);
            Px(g, mid, ox, by, pu, c, r - 1, 1); Px(g, mid, ox, by, pu, c, r + 1, 1);
        }
    }

    // Test: Entwicklung auslösen
    public static string TestEvolution()
    {
        PetForm f = new PetForm(true);
        string log = "start stage=" + f.RealStage() + " seen=" + f.evoSeen;
        f.Step(); log += " | nach Step evo=" + f.evoFrames;
        f.tasksTotal = 2 * 9 * 9;                  // Stufe 10
        f.Step();
        log += " | Stufe " + f.Level() + " real=" + f.RealStage() + " seen=" + f.evoSeen + " frames=" + f.evoFrames + " gezeigt=" + f.Stage();
        for (int i = 0; i < 61; i++) f.Step();
        log += " | nach 61 Schritten frames=" + f.evoFrames + " gezeigt=" + f.Stage() + " blase=\"" + f.bubble.Replace("\n", " / ") + "\"";
        for (int i = 0; i < 70; i++) f.Step();
        log += " | Ende frames=" + f.evoFrames + " gezeigt=" + f.Stage();
        f.Step(); log += " | kein zweites Mal: frames=" + f.evoFrames;
        return log;
    }
}
