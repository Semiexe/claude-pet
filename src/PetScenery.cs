using System;
using System.Collections.Generic;
using System.Drawing;

// Möbel (Bett, Futternapf) und Jahreszeiten-Effekte (Schnee, Blätter, Feuerwerk)
public partial class PetForm
{
    bool seasonOn = true;
    List<float[]> seasonParts = new List<float[]>();        // x, y, vx, vy, Lebensdauer, Art (0 Flocke, 1-3 Blatt, 10-13 Funke)
    DateTime nextBurst = DateTime.MinValue;

    Brush bWood = new SolidBrush(Color.FromArgb(140, 96, 66));
    Brush bMat = new SolidBrush(Color.FromArgb(116, 148, 210));
    Brush bPillow = new SolidBrush(Color.FromArgb(238, 238, 244));
    Brush bBowl = new SolidBrush(Color.FromArgb(170, 112, 84));
    Brush bBowlDark = new SolidBrush(Color.FromArgb(120, 76, 56));
    Brush bKibble = new SolidBrush(Color.FromArgb(232, 184, 70));
    Brush bSnow = new SolidBrush(Color.FromArgb(225, 255, 255, 255));
    Brush[] bLeaf = new Brush[] { new SolidBrush(Color.FromArgb(224, 140, 50)), new SolidBrush(Color.FromArgb(176, 96, 52)), new SolidBrush(Color.FromArgb(226, 190, 60)) };
    static readonly Color[] SparkCol = new Color[] { Color.FromArgb(255, 210, 80), Color.FromArgb(255, 110, 110), Color.FromArgb(110, 200, 255), Color.FromArgb(170, 255, 150) };

    // ---- Möbel: Bett beim Schlafen, leerer Napf bei Hunger (Spritezellen wie bei Px) ----
    void DrawBed(System.Drawing.Graphics g, int ox, int oy, int pu)
    {
        Px(g, bMat, ox, oy, pu, -2, 14, 28);                    // Matratze
        Px(g, bWood, ox, oy, pu, -2, 15, 28);                   // Rahmen
        Px(g, bWood, ox, oy, pu, -2, 11, 1); Px(g, bWood, ox, oy, pu, -2, 12, 1); Px(g, bWood, ox, oy, pu, -2, 13, 1);   // Kopfteil
        Px(g, bWood, ox, oy, pu, 25, 13, 1);                    // Fußteil
        Px(g, bPillow, ox, oy, pu, -1, 12, 5); Px(g, bPillow, ox, oy, pu, -1, 13, 5);   // Kissen
    }
    void DrawBowl(System.Drawing.Graphics g, int ox, int oy, int pu)
    {
        Px(g, bBowlDark, ox, oy, pu, 26, 13, 5);                // Rand
        Px(g, bBowl, ox, oy, pu, 26, 14, 5);
        Px(g, bBowl, ox, oy, pu, 27, 15, 3);
        if (food >= 25) { Px(g, bKibble, ox, oy, pu, 27, 12, 1); if (food >= 40) Px(g, bKibble, ox, oy, pu, 29, 12, 1); }   // etwas drin
    }

    // ---- Jahreszeiten ----
    static string SeasonKind(DateTime d)
    {
        if ((d.Month == 12 && d.Day == 31 && d.Hour >= 18) || (d.Month == 1 && d.Day == 1 && d.Hour < 6)) return "fireworks";
        if (d.Month == 12 || (d.Month == 1 && d.Day <= 6)) return "snow";
        if (d.Month == 10 || (d.Month == 11 && d.Day <= 20)) return "leaves";
        return null;
    }

    void UpdateSeason(DateTime now)
    {
        string kind = (seasonOn && Visible && !autoHidden && !still) ? SeasonKind(now) : null;
        UpdateSeasonCore(kind, now);
    }

    // Teilchen bewegen und neue erzeugen; kind = null lässt die vorhandenen auslaufen
    internal void UpdateSeasonCore(string kind, DateTime now)
    {
        for (int i = seasonParts.Count - 1; i >= 0; i--)
        {
            float[] p = seasonParts[i];
            p[0] += p[2]; p[1] += p[3]; p[4] -= 1f;
            if (p[5] >= 10) p[3] += 0.05f;                                              // Funken fallen
            else p[2] = (float)Math.Sin((p[4] + i * 7) * 0.08) * 0.55f;               // Flocken und Blätter schaukeln
            if (p[4] <= 0 || p[1] > LH) seasonParts.RemoveAt(i);
        }
        if (kind == null) return;
        if (kind == "snow" && seasonParts.Count < 45 && rnd.Next(100) < 28)
            seasonParts.Add(new float[] { rnd.Next(LW), -4, 0, 0.5f + (float)rnd.NextDouble() * 0.7f, 400, 0 });
        else if (kind == "leaves" && seasonParts.Count < 18 && rnd.Next(100) < 7)
            seasonParts.Add(new float[] { rnd.Next(LW), -6, 0, 0.35f + (float)rnd.NextDouble() * 0.5f, 600, 1 + rnd.Next(3) });
        else if (kind == "fireworks" && now >= nextBurst)
        {
            nextBurst = now.AddMilliseconds(900 + rnd.Next(900));
            float cx = 40 + rnd.Next(LW - 80), cy = 20 + rnd.Next(90); int col = 10 + rnd.Next(4);
            for (int i = 0; i < 18; i++)
            {
                double a = i / 18.0 * Math.PI * 2; float sp = 1.2f + (float)rnd.NextDouble() * 0.8f;
                seasonParts.Add(new float[] { cx, cy, (float)Math.Cos(a) * sp, (float)Math.Sin(a) * sp, 40 + rnd.Next(20), col });
            }
        }
    }

    // in logischen Koordinaten, wird mit ScaleTransform gezeichnet
    void DrawSeason(System.Drawing.Graphics g)
    {
        foreach (float[] p in seasonParts)
        {
            int k = (int)p[5];
            if (k == 0) g.FillRectangle(bSnow, p[0], p[1], 2.4f, 2.4f);
            else if (k < 10) g.FillRectangle(bLeaf[(k - 1) % 3], p[0], p[1], 4f, 3f);
            else
            {
                int a = (int)(Math.Max(0f, Math.Min(1f, p[4] / 25f)) * 255);
                Color c = SparkCol[k - 10];
                using (Brush b = new SolidBrush(Color.FromArgb(a, c))) g.FillRectangle(b, p[0], p[1], 2.4f, 2.4f);
            }
        }
    }
}
