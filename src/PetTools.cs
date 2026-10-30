using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

// Tageszähler der Werkzeuge (aus tools.log, geschrieben vom Hook; nur Art, keine Namen) und die Helfer-Pets
public partial class PetForm
{
    static string ToolsPath() { return Path.Combine(DataDir(), "tools.log"); }

    // Wie oft wurde an einem Tag welche Art Werkzeug benutzt?
    Dictionary<string, int> ToolCounts(string dayKey)
    {
        Dictionary<string, int> res = new Dictionary<string, int>();
        try
        {
            if (!File.Exists(ToolsPath())) return res;
            foreach (string line in File.ReadAllLines(ToolsPath()))
            {
                int i = line.IndexOf('|');
                if (i != 8 || !line.StartsWith(dayKey)) continue;
                string cat = line.Substring(i + 1).Trim();
                int n; res.TryGetValue(cat, out n); res[cat] = n + 1;
            }
        }
        catch { }
        return res;
    }

    // "Werkzeuge: 142 gelesen · 37 geschrieben · 20 Befehle"; leer, wenn nichts benutzt wurde
    string ToolLine(string dayKey)
    {
        Dictionary<string, int> c = ToolCounts(dayKey);
        string[] cats = new string[] { "read", "write", "exec", "search", "agent", "other" };
        string[] one = new string[] { "gelesen", "geschrieben", "Befehl", "Websuche", "Helfer", "sonstiges" };
        string[] many = new string[] { "gelesen", "geschrieben", "Befehle", "Websuchen", "Helfer", "sonstige" };
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < cats.Length; i++)
        {
            int n; if (!c.TryGetValue(cats[i], out n) || n == 0) continue;
            sb.Append((sb.Length > 0 ? " · " : "") + n + " " + (n == 1 ? one[i] : many[i]));
        }
        return sb.Length > 0 ? "Werkzeuge: " + sb.ToString() : "";
    }

    // Alte Tage aus dem Protokoll werfen (behält 45 Tage), damit die Datei klein bleibt
    void PruneToolLog()
    {
        try
        {
            if (!File.Exists(ToolsPath())) return;
            string limit = DateTime.Today.AddDays(-45).ToString("yyyyMMdd");
            string[] lines = File.ReadAllLines(ToolsPath());
            List<string> keep = new List<string>();
            foreach (string l in lines) if (l.Length > 8 && string.CompareOrdinal(l.Substring(0, 8), limit) >= 0) keep.Add(l);
            if (keep.Count < lines.Length) File.WriteAllLines(ToolsPath(), keep.ToArray());
        }
        catch { }
    }

    // ---- Helfer-Pets: winzige Pets neben dem Sprite, solange Subagenten laufen ----
    static readonly string[] HelperArt = new string[] { ".#####.", ".#e#e#.", "#######", ".#####.", ".#...#.", ".#...#." };

    // x, y = linke obere Ecke in Pixeln; ph = Pixelgröße des Helfers
    internal static void DrawHelper(Graphics g, Brush body, Brush eye, int x, int y, int ph)
    {
        for (int r = 0; r < HelperArt.Length; r++)
            for (int c = 0; c < HelperArt[r].Length; c++)
            {
                char ch = HelperArt[r][c];
                if (ch == '#') g.FillRectangle(body, x + c * ph, y + r * ph, ph, ph);
                else if (ch == 'e') g.FillRectangle(eye, x + c * ph, y + r * ph, ph, ph);
            }
    }

    // Plätze der Helfer links und rechts vom großen Sprite (Spritebreite = 24 * pu)
    void DrawHelpers(Graphics g, int count, int ox, int oy, int pu)
    {
        int ph = Math.Max(1, pu / 2), n = Math.Min(3, count);
        for (int i = 0; i < n; i++)
        {
            int x = i == 0 ? ox + 24 * pu + 2 * pu : i == 1 ? ox - 2 * pu - 7 * ph : ox + 24 * pu + 2 * pu + 8 * ph;
            int hop = (((int)(t * 5 + i * 2)) % 4 == 0) ? ph : 0;
            DrawHelper(g, bPet, bEye, x, oy + 16 * pu - 6 * ph - hop, ph);
        }
    }
}
