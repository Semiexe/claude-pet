using System;
using System.Drawing;

// Figuren: "claude" ist das Original. "fox" (Fuchs) und "jelly" (Qualle) sind eigene Pixel-Figuren.
// Der Fuchs hat dasselbe Skelett und dieselben Animationen wie das Original, die Qualle hat eine eigene Form und schwebt.
// Jede Figur hat drei Gestalten (siehe PetEvolve.cs): Stufe 1, ab Stufe 10 und ab Stufe 20.
public partial class PetForm
{
    string species = "claude";
    static readonly string[] SpeciesKeys = new string[] { "claude", "fox", "jelly" };
    static readonly string[] SpeciesNames = new string[] { "Claude (Original)", "Fuchs", "Qualle" };

    Brush bFoxWhite, bFoxDark, bFoxPink, bFoxScarf, bFoxMoon;
    Brush bJelB, bJelS, bJelL, bJelT, bJelG, bJelStar;      // werden in ApplySpeciesColors je nach Gestalt angelegt

    bool Fox() { return species == "fox"; }
    bool Jelly() { return species == "jelly"; }

    static Brush Sb(int r, int g, int b) { return new SolidBrush(Color.FromArgb(r, g, b)); }

    // Farben der Figur; true, wenn sie die Farben vorgibt (dann gilt die Fellfarbe nicht)
    bool ApplySpeciesColors()
    {
        int st = Stage();
        if (Jelly())
        {
            // lila, dann leuchtend türkis, dann tiefblau mit Sternen
            int[][] pal = new int[][] {
                new int[] { 186, 148, 232, 150, 110, 200, 226, 206, 250, 206, 176, 244 },
                new int[] { 110, 200, 226, 76, 156, 196, 190, 236, 248, 150, 220, 240 },
                new int[] { 84, 86, 176, 60, 60, 140, 150, 150, 226, 110, 110, 200 } };
            int[] c = pal[Math.Max(0, Math.Min(2, st))];
            bJelB = Sb(c[0], c[1], c[2]); bJelS = Sb(c[3], c[4], c[5]); bJelL = Sb(c[6], c[7], c[8]); bJelT = Sb(c[9], c[10], c[11]);
            bJelG = st >= 1 ? Sb(255, 176, 214) : Sb(246, 230, 255); bJelStar = Sb(255, 246, 200);
            bPet = bJelB; bShade = bJelS; bBelly = bJelL; bBlush = Sb(250, 190, 214);
            return true;
        }
        if (!Fox()) return false;
        // orange, dann silberblau, dann nachtviolett
        int[][] pf = new int[][] {
            new int[] { 232, 140, 64, 198, 106, 40, 250, 240, 226, 92, 56, 40 },
            new int[] { 150, 168, 204, 118, 136, 176, 244, 246, 252, 62, 70, 100 },
            new int[] { 88, 84, 154, 64, 60, 122, 224, 224, 250, 34, 32, 74 } };
        int[] f = pf[Math.Max(0, Math.Min(2, st))];
        bPet = Sb(f[0], f[1], f[2]); bShade = Sb(f[3], f[4], f[5]);
        bFoxWhite = Sb(f[6], f[7], f[8]); bFoxDark = Sb(f[9], f[10], f[11]);
        bBelly = bFoxWhite; bBlush = Sb(244, 170, 160); bFoxPink = Sb(240, 170, 160);
        bFoxScarf = Sb(204, 66, 70); bFoxMoon = Sb(252, 232, 140);
        return true;
    }

    // ---- Fuchs ----

    // Buschiger Schwanz hinter dem Körper mit heller Spitze; ab Stufe 20 ein zweiter auf der anderen Seite
    void DrawFoxTail(System.Drawing.Graphics g, int ox, int by, int pu, int stage)
    {
        string[] tail = new string[] { "...W", "..WW", ".###", ".###", "####", "###.", "##.." };
        for (int pass = 0; pass < (stage >= 2 ? 2 : 1); pass++)
            for (int r = 0; r < tail.Length; r++)
                for (int c = 0; c < 4; c++)
                {
                    char ch = tail[r][c];
                    if (ch == '.') continue;
                    Brush b = ch == 'W' ? bFoxWhite : (r == tail.Length - 1 ? bShade : bPet);
                    Px(g, b, ox, by, pu, pass == 0 ? 20 + c : 3 - c, 7 + r, 1);
                }
    }

    // Kopf und Körper in einem Stück: Spitzohren, helle Wangen und Schnauze, dunkle Nase, heller Bauch
    void DrawFoxBody(System.Drawing.Graphics g, int ox, int by, int pu, int stage)
    {
        for (int r = 0; r <= 11; r++) Px(g, bPet, ox, by, pu, 4, r, 16);
        Px(g, bShade, ox, by, pu, 4, 11, 16);

        // Ohren mit dunklen Spitzen und rosa Innenseite
        Px(g, bPet, ox, by, pu, 5, -3, 1); Px(g, bPet, ox, by, pu, 5, -2, 2); Px(g, bPet, ox, by, pu, 5, -1, 3);
        Px(g, bPet, ox, by, pu, 18, -3, 1); Px(g, bPet, ox, by, pu, 17, -2, 2); Px(g, bPet, ox, by, pu, 16, -1, 3);
        Px(g, bFoxDark, ox, by, pu, 5, -3, 1); Px(g, bFoxDark, ox, by, pu, 18, -3, 1);
        Px(g, bFoxPink, ox, by, pu, 6, -1, 1); Px(g, bFoxPink, ox, by, pu, 17, -1, 1);

        // Wangen und Schnauze
        for (int r = 4; r <= 6; r++) { Px(g, bFoxWhite, ox, by, pu, 4, r, 5); Px(g, bFoxWhite, ox, by, pu, 15, r, 5); }
        Px(g, bFoxWhite, ox, by, pu, 9, 5, 6); Px(g, bFoxWhite, ox, by, pu, 9, 6, 6);
        Px(g, bFoxDark, ox, by, pu, 11, 4, 2);
        for (int r = 7; r <= 10; r++) Px(g, bFoxWhite, ox, by, pu, 8, r, 8);

        if (stage >= 1)
        {
            // Schal um den Hals, ein Zipfel hängt herunter
            Px(g, bFoxScarf, ox, by, pu, 4, 7, 16);
            Px(g, bFoxScarf, ox, by, pu, 15, 8, 2); Px(g, bFoxScarf, ox, by, pu, 15, 9, 2);
        }
        if (stage >= 2)
        {
            // Mondsichel auf der Stirn
            Px(g, bFoxMoon, ox, by, pu, 11, 0, 2); Px(g, bFoxMoon, ox, by, pu, 10, 1, 1);
        }
    }

    // dunkle Pfoten an den Armenden (la / ra = oberste Armzeile)
    void DrawFoxPaws(System.Drawing.Graphics g, int ox, int by, int pu, int la, int ra)
    {
        Px(g, bFoxDark, ox, by, pu, 0, la + 2, 1); Px(g, bFoxDark, ox, by, pu, 0, la + 3, 2);
        Px(g, bFoxDark, ox, by, pu, 23, ra + 2, 1); Px(g, bFoxDark, ox, by, pu, 22, ra + 3, 2);
    }

    // ---- Qualle: schwebende Glocke mit wehenden Fangarmen (Raster 24 x 20, die obersten 3 Zeilen ragen über das normale Sprite hinaus) ----
    void DrawJellyBody(System.Drawing.Graphics g, int ox, int by, int pu, int la, int ra, int st)
    {
        // Glocke Zeile für Zeile (von Zeile -3 bis 6), mit hellem Glanz und Schatten unten
        int[] L = new int[] { 9, 7, 6, 5, 5, 5, 5, 5, 5, 4 }, R = new int[] { 14, 16, 17, 18, 18, 18, 18, 18, 18, 19 };
        for (int i = 0; i < L.Length; i++) Px(g, bJelB, ox, by, pu, L[i], i - 3, R[i] - L[i] + 1);
        Px(g, bJelS, ox, by, pu, 5, 5, 14);
        Px(g, bJelL, ox, by, pu, 8, -2, 2); Px(g, bJelL, ox, by, pu, 7, -1, 1); Px(g, bJelL, ox, by, pu, 6, 0, 1);
        for (int c = 4; c <= 19; c++) Px(g, c % 2 == 0 ? bJelL : bJelB, ox, by, pu, c, 6, 1);       // gewellter Rand

        if (st == 1)
        {
            // leuchtende Punkte, die abwechselnd aufblitzen
            int[,] spots = new int[,] { { 9, -1 }, { 14, -1 }, { 11, 4 } };
            for (int k = 0; k < 3; k++) if (((int)(t * 2) + k) % 3 == 0) Px(g, bJelG, ox, by, pu, spots[k, 0], spots[k, 1], 1);
        }
        if (st >= 2)
        {
            Px(g, bJelStar, ox, by, pu, 8, 0, 1); Px(g, bJelStar, ox, by, pu, 15, 1, 1); Px(g, bJelStar, ox, by, pu, 11, -1, 1);
            if (CurrentHat() == null)
            {
                Px(g, bGoldS, ox, by, pu, 9, -5, 6);            // goldener Heiligenschein
                Px(g, bGoldD, ox, by, pu, 8, -4, 1); Px(g, bGoldD, ox, by, pu, 15, -4, 1);
            }
        }

        // Fangarme: fünf Stränge, die sich wellen
        float phase = sleeping ? t * 1.0f : t * (walking ? 5f : 2.6f);
        int[] bases = new int[] { 6, 9, 11, 14, 17 }, widths = new int[] { 1, 1, 2, 1, 1 }, ends = new int[] { 13, 15, 15, 15, 13 };
        for (int k = 0; k < 5; k++)
            for (int r = 7; r <= ends[k] + (st >= 1 ? 0 : -1); r++)
            {
                float amp = Math.Min(1.6f, (r - 6) * 0.25f);
                int x = bases[k] + (int)Math.Round(Math.Sin(phase - r * 0.55 + k * 1.4) * amp);
                Brush b = r % 3 == 0 ? bJelB : bJelT;
                if (r == ends[k] && st >= 1) b = bJelG;
                Px(g, b, ox, by, pu, x, r, widths[k]);
            }

        // kleine Flossenarme an der Seite; sie folgen den Armbewegungen des Pets
        int dl = Math.Max(-2, Math.Min(2, (la - 5) / 2)), dr = Math.Max(-2, Math.Min(2, (ra - 5) / 2));
        Px(g, bJelB, ox, by, pu, 2, 3 + dl, 3); Px(g, bJelT, ox, by, pu, 1, 4 + dl, 1);
        Px(g, bJelB, ox, by, pu, 19, 3 + dr, 3); Px(g, bJelT, ox, by, pu, 22, 4 + dr, 1);
    }
}
