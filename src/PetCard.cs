using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

// Pet-Karte: ein Bild mit Pet, Stufe, Werten und Erfolgen zum Teilen
public partial class PetForm
{
    // Zeichnet das Pet allein (ohne Sprechblase, Status und Partikel) in einer festen Größe
    Bitmap RenderPetImage(float scale)
    {
        // Zustand sichern
        float sc0 = sc; int W0 = W, H0 = H, act0 = actFrames, hop0 = hop, yawn0 = yawn;
        string cs0 = cState, a0 = act;
        bool sl0 = sleeping, wk0 = walking, dr0 = dragging;
        List<float[]> parts0 = parts; List<Sess> ses0 = sessions;
        DateTime bu0 = bubbleUntil, nu0 = noteUntil;
        List<float[]> sp0 = seasonParts; float food0 = food; bool comp0 = cCompact; int kick0 = kickFrames;
        try
        {
            seasonParts = new List<float[]>(); food = 100; cCompact = false; kickFrames = 0;
            sc = scale; W = (int)(LW * sc); H = (int)(LH * sc);
            cState = "none"; act = "none"; actFrames = 0; hop = 0; yawn = 0; sleeping = false; walking = false; dragging = false;
            parts = new List<float[]>(); sessions = new List<Sess>(); bubbleUntil = DateTime.MinValue; noteUntil = DateTime.MinValue;
            Bitmap bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(bmp)) { g.Clear(Color.Transparent); Draw(g); }
            return bmp;
        }
        finally
        {
            sc = sc0; W = W0; H = H0; actFrames = act0; hop = hop0; yawn = yawn0; cState = cs0; act = a0;
            sleeping = sl0; walking = wk0; dragging = dr0; parts = parts0; sessions = ses0; bubbleUntil = bu0; noteUntil = nu0;
            seasonParts = sp0; food = food0; cCompact = comp0; kickFrames = kick0;
        }
    }

    string AgeText()
    {
        int d = (int)(DateTime.Now - born).TotalDays;
        return d < 1 ? "weniger als ein Tag" : d == 1 ? "1 Tag" : d + " Tage";
    }

    internal Bitmap BuildCard()
    {
        int cw = 760, ch = 400;
        int fi = Math.Max(0, Array.IndexOf(FurKeys, furKey));
        Color acc = Jelly() ? Color.FromArgb(186, 148, 232) : Fox() ? Color.FromArgb(232, 140, 64) : Color.FromArgb(FurRgb[fi][0], FurRgb[fi][1], FurRgb[fi][2]);
        Color fg = Color.FromArgb(238, 236, 232), dim = Color.FromArgb(150, 154, 166), track = Color.FromArgb(64, 62, 74);
        Bitmap card = new Bitmap(cw, ch, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(card))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using (LinearGradientBrush bg = new LinearGradientBrush(new Rectangle(0, 0, cw, ch), Color.FromArgb(36, 34, 46), Color.FromArgb(22, 21, 28), 60f))
                g.FillRectangle(bg, 0, 0, cw, ch);

            // linke Fläche mit dem Pet; das Bild wird 1:1 (ohne Skalieren) eingesetzt, damit die Pixel scharf bleiben
            using (GraphicsPath gp = Gfx.Round(24, 24, 308, 352, 24)) using (Brush b = new SolidBrush(Color.FromArgb(48, 46, 60))) g.FillPath(b, gp);
            const float PS = 1.4f;
            using (Bitmap pet = RenderPetImage(PS))
            {
                Rectangle src = new Rectangle((int)(20 * PS), (int)(70 * PS), (int)(200 * PS), (int)(170 * PS));
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(pet, new Rectangle(38, 100, src.Width, src.Height), src, GraphicsUnit.Pixel);
                g.PixelOffsetMode = PixelOffsetMode.Default;
            }

            int x0 = 372;
            using (Font fName = new Font("Segoe UI Semibold", 30f), fSub = new Font("Segoe UI", 14f), fLab = new Font("Segoe UI", 11f),
                        fTxt = new Font("Segoe UI", 12.5f), fSmall = new Font("Segoe UI", 9.5f))
            using (Brush bf = new SolidBrush(fg), bd = new SolidBrush(dim), ba = new SolidBrush(acc), bt = new SolidBrush(track))
            {
                g.DrawString(petName, fName, bf, x0 - 2, 22);
                int lv = Level();
                g.DrawString("Stufe " + lv + " · " + LevelTitle(lv) + (Stage() > 0 ? " · " + StageName() : ""), fSub, ba, x0, 78);

                string[] names = new string[] { "Hunger", "Laune", "Energie" };
                float[] vals = new float[] { food, joy, energy };
                for (int i = 0; i < 3; i++)
                {
                    float y = 126 + i * 28;
                    g.DrawString(names[i], fLab, bd, x0, y - 3);
                    using (GraphicsPath tp = Gfx.Round(x0 + 84, y + 2, 230, 10, 5)) g.FillPath(bt, tp);
                    float v = Math.Max(0f, Math.Min(100f, vals[i]));
                    Color bc = v < 25 ? Color.FromArgb(214, 84, 72) : v < 55 ? Color.FromArgb(226, 170, 50) : Color.FromArgb(70, 176, 118);
                    using (Brush bb = new SolidBrush(bc)) using (GraphicsPath fp = Gfx.Round(x0 + 84, y + 2, Math.Max(10f, 230 * v / 100f), 10, 5)) g.FillPath(bb, fp);
                    g.DrawString(((int)v).ToString(), fLab, bd, x0 + 324, y - 3);
                }

                int got = 0; foreach (Ach a in Achs) if (HasAch(a.id)) got++;
                string hat = CurrentHat();
                string[] lines = new string[] {
                    "Aufgaben insgesamt: " + tasksTotal,
                    "Heute erledigt: " + TodayCount(),
                    "Erfolge: " + got + " von " + Achs.Length,
                    "Alter: " + AgeText(),
                    "Hut: " + (hat != null ? HatName(hat) : "keiner") };
                for (int i = 0; i < lines.Length; i++) g.DrawString(lines[i], fTxt, bf, x0, 228 + i * 28);

                g.DrawString("Claude Pet · " + DateTime.Now.ToString("d. MMMM yyyy", De), fSmall, bd, x0, 366);
            }
        }
        return card;
    }

    void ExportCard()
    {
        using (Form o = DialogOwner())
        using (SaveFileDialog d = new SaveFileDialog())
        {
            d.Title = "Pet-Karte speichern";
            d.Filter = "PNG-Bild (*.png)|*.png";
            d.FileName = "ClaudePet-Karte.png";
            if (d.ShowDialog(o) != DialogResult.OK) return;
            try
            {
                using (Bitmap card = BuildCard()) card.Save(d.FileName, ImageFormat.Png);
                hop = 22; Burst("★", 3);
                Say("Karte gespeichert! Auf ihr stehen nur Zahlen, keine Projektnamen.", 6);
            }
            catch { Say("Das Speichern hat nicht geklappt.", 4); }
        }
    }
}
