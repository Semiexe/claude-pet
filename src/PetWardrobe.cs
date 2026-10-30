using System;
using System.Collections.Generic;
using System.Drawing;

// Stufen und Kleidung
public partial class PetForm
{
    int tasksTotal, tasksToday;
    string todayKey = "";
    string hatMode = "auto";                 // auto | none | ein Schlüssel aus HatKeys
    string levelMsg = "";                    // Hinweise auf neue Stufe oder Erfolge, werden in der nächsten Sprechblase gezeigt
    Dictionary<string, Brush[]> hatBrushes = new Dictionary<string, Brush[]>();

    // Reihenfolge, Namen, Stufe und Erfolg, der einen Hut freischaltet
    static readonly string[] HatKeys = new string[] { "auto", "none", "party", "witch", "santa", "shades", "bow", "crown", "top", "cap", "nightcap", "halo", "custom" };
    static readonly string[] HatNames = new string[] { "Automatisch", "Keine", "Partyhut", "Hexenhut", "Weihnachtsmütze", "Sonnenbrille", "Schleife", "Krone", "Zylinder", "Basecap", "Schlafmütze", "Heiligenschein", "Eigener Hut" };
    static readonly int[] HatLevels = new int[] { 1, 1, 1, 1, 1, 1, 2, 5, 8, 1, 1, 1, 1 };
    static readonly string[] HatAch = new string[] { null, null, null, null, null, null, null, null, null, "t100", "night", "streak7", null };

    // Stufe aus der Zahl erledigter Aufgaben: 2 -> 2, 8 -> 3, 18 -> 4, 32 -> 5 ...
    int Level() { return 1 + (int)Math.Floor(Math.Sqrt(tasksTotal / 2.0)); }
    int TodayCount() { return todayKey == DateTime.Now.ToString("yyyyMMdd") ? tasksToday : 0; }

    bool HatUnlocked(int i)
    {
        if (HatKeys[i] == "custom") return customArt != null;          // erst nach dem Zeichnen
        return Level() >= HatLevels[i] && (HatAch[i] == null || HasAch(HatAch[i]));
    }
    string HatLockText(int i)
    {
        if (HatKeys[i] == "custom") return "erst zeichnen";
        if (Level() < HatLevels[i]) return "ab Stufe " + HatLevels[i];
        foreach (Ach a in Achs) if (a.id == HatAch[i]) return "Erfolg: " + a.name;
        return "gesperrt";
    }
    static string HatName(string key)
    {
        int i = Array.IndexOf(HatKeys, key);
        return i >= 0 ? HatNames[i] : key;
    }

    // Hut zu Feiertagen und Jahreszeit: Geburtstag, Halloween, Weihnachten, Silvester, sonniger Sommertag
    string SeasonalHat()
    {
        DateTime d = DateTime.Now;
        if (d.Month == born.Month && d.Day == born.Day && d.Year > born.Year) return "party";
        if ((d.Month == 10 && d.Day >= 24) || (d.Month == 11 && d.Day == 1)) return "witch";
        if (d.Month == 12 && d.Day >= 20 && d.Day <= 26) return "santa";
        if ((d.Month == 12 && d.Day == 31) || (d.Month == 1 && d.Day == 1)) return "party";
        if (d.Month >= 6 && d.Month <= 8 && d.Hour >= 10 && d.Hour < 18) return "shades";
        return null;
    }
    string CurrentHat()
    {
        if (hatMode == "none") return null;
        if (hatMode == "auto")
        {
            string sh = SeasonalHat();
            return sh == null && !Fox() && !Jelly() && Stage() >= 2 ? "crown" : sh;       // der König trägt von selbst seine Krone
        }
        int i = Array.IndexOf(HatKeys, hatMode);
        if (i < 0 || !HatUnlocked(i)) return null;      // noch nicht freigeschaltet
        return hatMode;
    }

    // Pixelbilder: '#' Hauptfarbe, 'o' Akzent
    internal static string[] HatArt(string k)
    {
        switch (k)
        {
            case "party": return new string[] { "...oo...", "...##...", "..####..", ".#o##o#.", "########" };
            case "witch": return new string[] { ".....##...", "....###...", "...####...", "...#oo#...", "..######..", "##########" };
            case "santa":
            case "nightcap": return new string[] { ".......oo.", "......###.", "....#####.", "..#######.", "oooooooooo" };
            case "crown": return new string[] { "#.#..#.#", "########", "##o##o##" };
            case "bow": return new string[] { "##.#.##", ".#####.", "##.#.##" };
            case "top": return new string[] { "..####..", "..####..", "..####..", "..#oo#..", "########" };
            case "cap": return new string[] { "..####....", ".######...", "##########" };
            case "halo": return new string[] { "..####..", ".#....#.", "..####..", "........" };   // schwebt über dem Kopf
            case "custom": return customArt;
        }
        return null;
    }
    internal static Color[] HatColors(string k)
    {
        switch (k)
        {
            case "party": return new Color[] { Color.FromArgb(116, 92, 230), Color.FromArgb(250, 210, 70) };
            case "witch": return new Color[] { Color.FromArgb(52, 44, 70), Color.FromArgb(240, 140, 40) };
            case "santa": return new Color[] { Color.FromArgb(214, 60, 60), Color.FromArgb(246, 246, 246) };
            case "nightcap": return new Color[] { Color.FromArgb(60, 80, 170), Color.FromArgb(250, 230, 120) };
            case "crown": return new Color[] { Color.FromArgb(240, 200, 60), Color.FromArgb(214, 80, 90) };
            case "bow": return new Color[] { Color.FromArgb(230, 90, 140), Color.FromArgb(255, 200, 220) };
            case "cap": return new Color[] { Color.FromArgb(60, 130, 200), Color.FromArgb(240, 240, 240) };
            case "halo": return new Color[] { Color.FromArgb(250, 215, 80), Color.FromArgb(250, 215, 80) };
            case "custom": return new Color[] { customMain, customAcc };
            default: return new Color[] { Color.FromArgb(46, 44, 56), Color.FromArgb(200, 70, 70) };
        }
    }

    // Zeichnet den Hut auf den Kopf (Zeilen -h bis -1 über der Kopfkante)
    void DrawHat(System.Drawing.Graphics g, int ox, int by, int pu)
    {
        string k = CurrentHat();
        if (k == null) return;
        string[] art = HatArt(k);
        if (art == null) return;
        Brush[] br;
        if (!hatBrushes.TryGetValue(k, out br))
        {
            Color[] cs = HatColors(k);
            br = new Brush[] { new SolidBrush(cs[0]), new SolidBrush(cs[1]) };
            hatBrushes[k] = br;
        }
        int col0 = k == "bow" ? 15 : 12 - art[0].Length / 2;
        for (int r = 0; r < art.Length; r++)
            for (int c = 0; c < art[r].Length; c++)
            {
                char ch = art[r][c];
                if (ch == '#') Px(g, br[0], ox, by, pu, col0 + c, r - art.Length, 1);
                else if (ch == 'o') Px(g, br[1], ox, by, pu, col0 + c, r - art.Length, 1);
            }
    }

    // ---- Fellfarben: einige gleich, andere ab einer Stufe oder durch einen Erfolg ----
    string furKey = "coral";
    static readonly string[] FurKeys = new string[] { "coral", "mint", "sky", "rose", "violet", "gold", "graphite" };
    static readonly string[] FurNames = new string[] { "Korall", "Minze", "Himmel", "Rosa", "Lila", "Gold", "Graphit" };
    static readonly int[] FurLevels = new int[] { 1, 1, 1, 3, 5, 8, 1 };
    static readonly string[] FurAch = new string[] { null, null, null, null, null, null, "t100" };
    static readonly int[][] FurRgb = new int[][] {
        new int[] { 219, 119, 98 }, new int[] { 104, 184, 148 }, new int[] { 104, 152, 214 }, new int[] { 222, 132, 168 },
        new int[] { 150, 122, 206 }, new int[] { 222, 176, 72 }, new int[] { 96, 104, 120 } };

    bool FurUnlocked(int i) { return Level() >= FurLevels[i] && (FurAch[i] == null || HasAch(FurAch[i])); }
    string FurLockText(int i)
    {
        if (Level() < FurLevels[i]) return "ab Stufe " + FurLevels[i];
        foreach (Ach a in Achs) if (a.id == FurAch[i]) return "Erfolg: " + a.name;
        return "gesperrt";
    }
    // Setzt die Pinsel für Körper, Schatten, Bauch und Wangen nach der gewählten Farbe
    void ApplyFur()
    {
        if (ApplySpeciesColors()) return;              // eine andere Figur bringt ihre eigenen Farben mit
        int i = Array.IndexOf(FurKeys, furKey);
        if (i < 0 || !FurUnlocked(i)) i = 0;
        int[] c = FurRgb[i];
        bPet = new SolidBrush(Color.FromArgb(c[0], c[1], c[2]));
        bShade = new SolidBrush(Color.FromArgb(c[0] * 89 / 100, c[1] * 89 / 100, c[2] * 89 / 100));
        bBelly = new SolidBrush(Color.FromArgb(Math.Min(255, c[0] + 26), Math.Min(255, c[1] + 26), Math.Min(255, c[2] + 26)));
        bBlush = new SolidBrush(Color.FromArgb((c[0] + 255) / 2, (c[1] + 130) / 2, (c[2] + 140) / 2));
    }

    // Titel zur Stufe, für die Pet-Karte
    static string LevelTitle(int lv)
    {
        string[] t = new string[] { "Frischling", "Lehrling", "Gefährte", "Kollege", "Profi", "Veteran", "Meister", "Legende" };
        return t[Math.Min(lv, t.Length) - 1];
    }

    // Die Sonnenbrille sitzt vor den Augen und wird nach ihnen gezeichnet
    void DrawShades(System.Drawing.Graphics g, int ox, int by, int pu)
    {
        Brush[] br;
        if (!hatBrushes.TryGetValue("shades", out br))
        {
            br = new Brush[] { new SolidBrush(Color.FromArgb(28, 32, 48)), new SolidBrush(Color.FromArgb(100, 112, 150)) };
            hatBrushes["shades"] = br;
        }
        for (int r = 2; r <= 3; r++) { Px(g, br[0], ox, by, pu, 4, r, 6); Px(g, br[0], ox, by, pu, 14, r, 6); }
        Px(g, br[0], ox, by, pu, 10, 2, 4);
        Px(g, br[1], ox, by, pu, 5, 2, 1); Px(g, br[1], ox, by, pu, 15, 2, 1);
    }
}
