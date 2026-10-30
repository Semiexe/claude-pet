using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

// Rechtsklick-Menü und Autostart
public partial class PetForm
{
    ContextMenuStrip menu;
    StatItem miStat; ToolStripMenuItem miSleep; Pal pal; ToolStripMenuItem[] zItems; float[] zVals;
    ToolStripMenuItem[] hatItems, achItems, furItems;
    ToolStripMenuItem miRem;
    List<ToolStripItem> remItems = new List<ToolStripItem>();    // eine Zeile je offene Erinnerung
    ToolStripSeparator remSep;
    List<ToolStripItem> sessItems = new List<ToolStripItem>();   // eine Zeile je Claude-Sitzung
    ToolStripSeparator sessSep;

    ToolStripMenuItem Item(string text, EventHandler click)
    {
        ToolStripMenuItem it = new ToolStripMenuItem(text);
        int px8 = (int)(10 * baseSc), py5 = (int)(5 * baseSc);
        it.Padding = new Padding(px8, py5, px8, py5);
        if (click != null) it.Click += click;
        return it;
    }
    void StyleDrop(ToolStripDropDown dd, ToolStripRenderer r)
    {
        ToolStripDropDownMenu m = dd as ToolStripDropDownMenu;
        if (m != null) { m.ShowImageMargin = false; m.ShowCheckMargin = false; }
        dd.Renderer = r; dd.Font = menu.Font;
    }
    static string Tick(bool on) { return on ? "✓" : ""; }

    void BuildMenu()
    {
        pal = Pal.Make();
        menu = new ContextMenuStrip();
        menu.Font = new Font("Segoe UI", 10f);
        PetRenderer rend = new PetRenderer(pal, baseSc);
        StyleDrop(menu, rend);
        menu.Padding = new Padding(0, (int)(4 * baseSc), 0, (int)(4 * baseSc));

        miStat = new StatItem(pal, baseSc);
        sessSep = new ToolStripSeparator(); sessSep.Visible = false;
        ToolStripMenuItem miGoto = Item("Zu Claude wechseln", delegate { if (!GoToClaude()) Say("Ich finde das Fenster nicht."); });
        ToolStripMenuItem miFeed = Item("Füttern", delegate { Feed(); });
        ToolStripMenuItem miPlay = Item("Spielen", delegate { Play(); });
        miSleep = Item("Schlafen", delegate { ToggleSleep(); });

        // Größe
        ToolStripMenuItem miSize = Item("Größe", null);
        StyleDrop(miSize.DropDown, rend);
        zVals = new float[] { 0.75f, 1f, 1.5f };
        string[] zn = new string[] { "Klein", "Normal", "Groß" };
        zItems = new ToolStripMenuItem[3];
        for (int zi = 0; zi < 3; zi++)
        {
            float zz = zVals[zi];
            zItems[zi] = Item(zn[zi], delegate { SetZoom(zz); });
            miSize.DropDownItems.Add(zItems[zi]);
        }

        // Kleidung
        ToolStripMenuItem miHat = Item("Kleidung", null);
        StyleDrop(miHat.DropDown, rend);
        hatItems = new ToolStripMenuItem[HatKeys.Length];
        for (int hi = 0; hi < HatKeys.Length; hi++)
        {
            string key = HatKeys[hi];
            hatItems[hi] = Item(HatNames[hi], delegate { hatMode = key; SaveState(); Say(key == "none" ? "Ganz ohne." : key == "auto" ? "Ich kleide mich nach dem Kalender." : "Neuer Hut!", 3); });
            miHat.DropDownItems.Add(hatItems[hi]);
        }
        miHat.DropDownItems.Add(new ToolStripSeparator());
        miHat.DropDownItems.Add(Item("Eigenen Hut zeichnen...", delegate { EditCustomHat(); }));

        // Figur: das Original oder die Fan-Version
        ToolStripMenuItem miSpec = Item("Figur", null);
        StyleDrop(miSpec.DropDown, rend);
        ToolStripMenuItem[] specItems = new ToolStripMenuItem[SpeciesKeys.Length];
        for (int si = 0; si < SpeciesKeys.Length; si++)
        {
            string sk = SpeciesKeys[si];
            specItems[si] = Item(SpeciesNames[si], delegate {
                species = sk; ApplyFur(); SaveState(); hop = 22; Burst("★", 3);
                Say(sk == "fox" ? "Ich bin jetzt ein kleiner Fuchs!" : sk == "jelly" ? "Blubb! Ich schwebe jetzt als Qualle." : "Zurück zu mir selbst.", 4);
            });
            miSpec.DropDownItems.Add(specItems[si]);
        }

        // Farbe des Pets
        ToolStripMenuItem miFur = Item("Farbe", null);
        StyleDrop(miFur.DropDown, rend);
        furItems = new ToolStripMenuItem[FurKeys.Length];
        for (int fi = 0; fi < FurKeys.Length; fi++)
        {
            string key = FurKeys[fi]; int[] c = FurRgb[fi];
            furItems[fi] = Item("      " + FurNames[fi], delegate { furKey = key; ApplyFur(); SaveState(); hop = 22; Say("Neue Farbe!", 3); });
            furItems[fi].Tag = Color.FromArgb(c[0], c[1], c[2]);
            miFur.DropDownItems.Add(furItems[fi]);
        }

        // Erinnerungen
        miRem = Item("Erinnerung", null);
        StyleDrop(miRem.DropDown, rend);
        int[] remMin = new int[] { 10, 20, 30, 60, 0 };
        string[] remName = new string[] { "In 10 Minuten", "In 20 Minuten", "In 30 Minuten", "In 1 Stunde", "In ... Minuten" };
        for (int ri = 0; ri < remMin.Length; ri++)
        {
            int mm = remMin[ri];
            miRem.DropDownItems.Add(Item(remName[ri], delegate { AskReminder(mm); }));
        }
        miRem.DropDownItems.Add(new ToolStripSeparator());
        ToolStripMenuItem miWater = Item("Wasser-Erinnerung (stündlich)", delegate {
            waterOn = !waterOn; nextWater = DateTime.MinValue; SaveState();
            Say(waterOn ? "Ich erinnere dich stündlich ans Trinken." : "Keine Wasser-Erinnerung mehr.", 3);
        });
        miRem.DropDownItems.Add(miWater);
        remSep = new ToolStripSeparator(); remSep.Visible = false;
        miRem.DropDownItems.Add(remSep);

        // Erfolge und Tagesrückblick
        ToolStripMenuItem miRecap = Item("Tagesrückblick", delegate { hop = 22; Say(RecapText(), 14); });
        ToolStripMenuItem miWeek = Item("Wochenrückblick", delegate {
            string w = WeekText(DateTime.Now); hop = 22;
            Say(w != null ? w : "Diese und letzte Woche gab es noch keine Aufgaben.", 14);
        });
        ToolStripMenuItem miDiary = Item("Tagebuch öffnen", delegate {
            try
            {
                if (!File.Exists(DiaryPath())) { Directory.CreateDirectory(DataDir()); File.WriteAllText(DiaryPath(), "Pet-Tagebuch\r\n\r\n", Encoding.UTF8); }
                Process.Start(DiaryPath());
            }
            catch { Say("Ich kann das Tagebuch nicht öffnen.", 4); }
        });
        ToolStripMenuItem miLook = Item("Rückblick und Erfolge", null);
        StyleDrop(miLook.DropDown, rend);
        ToolStripMenuItem miCard = Item("Pet-Karte speichern...", delegate { ExportCard(); });
        miLook.DropDownItems.AddRange(new ToolStripItem[] { miRecap, miWeek, miDiary, new ToolStripSeparator(), miCard });
        ToolStripMenuItem miGame = Item("Tokens fangen", delegate { StartGame(); });
        ToolStripMenuItem miBall = Item("Ball rausholen", delegate { ToggleBall(); });
        ToolStripMenuItem miPause = Item("Pause machen", delegate { StartExercise("Dehnübung! Mach einfach mit.", 3); });
        ToolStripMenuItem miAch = Item("Erfolge", null);
        StyleDrop(miAch.DropDown, rend);
        achItems = new ToolStripMenuItem[Achs.Length];
        for (int ai = 0; ai < Achs.Length; ai++)
        {
            Ach a = Achs[ai];
            achItems[ai] = Item(a.name, delegate { Say(a.name + ": " + a.desc + (HasAch(a.id) ? "\nGeschafft!" : ""), 5); });
            miAch.DropDownItems.Add(achItems[ai]);
        }

        ToolStripMenuItem miName = Item("Name ändern...", delegate {
            string n = Microsoft.VisualBasic.Interaction.InputBox("Wie soll dein Pet heißen?", "Claude Pet", petName, -1, -1);
            if (!string.IsNullOrWhiteSpace(n)) { petName = n.Trim(); if (petName.Length > 14) petName = petName.Substring(0, 14); Say("Ich heiße jetzt " + petName + "!"); SaveState(); }
        });

        // Einstellungen
        ToolStripMenuItem miSet = Item("Einstellungen", null);
        StyleDrop(miSet.DropDown, rend);
        ToolStripMenuItem miFocus = Item("Fokus-Modus", delegate {
            focusMode = !focusMode; nextPoll = DateTime.MinValue; SaveState();
            Say(focusMode ? "Fokus: Ich melde mich nur bei Fragen, Fehlern und Fertig." : "Ich zeige wieder alles.", 4);
        });
        ToolStripMenuItem miMinis = Item("Sitzungs-Pets", delegate {
            minisOn = !minisOn; if (!minisOn) DisposeMinis(); SaveState();
            Say(minisOn ? "Jede Sitzung bekommt ein kleines Pet." : "Keine kleinen Pets mehr.", 3);
        });
        ToolStripMenuItem miBreak = Item("Pausen-Erinnerung", delegate {
            breakOn = !breakOn; SaveState();
            Say(breakOn ? "Ich erinnere dich an Pausen." : "Keine Pausen-Erinnerung mehr.", 3);
        });
        ToolStripMenuItem miFs = Item("Bei Vollbild ausblenden", delegate {
            autoHideOn = !autoHideOn; nextFs = DateTime.MinValue; SaveState();
            Say(autoHideOn ? "Bei Vollbild bin ich still." : "Ich bleibe auch bei Vollbild da.", 3);
        });
        ToolStripMenuItem miSound = Item("Ton bei Fragen", delegate {
            soundOn = !soundOn; SaveState();
            if (soundOn) Chime(false);
            Say(soundOn ? "Ich gebe einen Ton, wenn Claude etwas braucht." : "Ich bin leise.", 3);
        });
        ToolStripMenuItem miVoice = Item("Blip-Stimme", delegate {
            voiceOn = !voiceOn; nextVoice = DateTime.MinValue; SaveState();
            Say(voiceOn ? "Bluppbluppblupp! So klinge ich." : "Dann bin ich still.", 3);
        });
        ToolStripMenuItem miBat = Item("Akku-Warnung", delegate {
            batteryOn = !batteryOn; batWarned = 0; SaveState();
            Say(batteryOn ? "Ich sage Bescheid, wenn dein Akku leer wird." : "Keine Akku-Warnung mehr.", 3);
        });
        ToolStripMenuItem miPrev = Item("Antwort-Vorschau", delegate {
            bool on = !PreviewOn(); SetPreview(on);
            Say(on ? "Ich zeige dir die erste Zeile von Claudes Antwort.\nSie steht kurz in einer Datei auf deinem Rechner." : "Keine Vorschau mehr. Die Datei ist gelöscht.", 6);
        });
        ToolStripMenuItem miSeason = Item("Jahreszeiten-Effekte", delegate {
            seasonOn = !seasonOn; SaveState();
            Say(seasonOn ? "Schnee, Blätter und Feuerwerk sind an." : "Keine Jahreszeiten-Effekte mehr.", 3);
        });
        ToolStripMenuItem miEyes = Item("Augenpause (20-20-20)", delegate {
            eyesOn = !eyesOn; eyeActive = 0; eyeUntil = DateTime.MinValue; SaveState();
            Say(eyesOn ? "Alle 20 Minuten schauen wir 20 Sekunden in die Ferne." : "Keine Augenpause mehr.", 4);
        });
        ToolStripMenuItem miDance = Item("Musik-Tanz", delegate {
            danceOn = !danceOn; danceRaw = false; musicSecs = 0; SaveState();
            Say(danceOn ? "Ich tanze, wenn Musik läuft.\nIch höre nur die Lautstärke, nie den Ton." : "Ich tanze nicht mehr.", 5);
        });
        ToolStripMenuItem miCtx = Item("Kontext-Anzeige", delegate {
            ctxOn = !ctxOn; SaveState();
            Say(ctxOn ? "Mein Bauch zeigt, wie voll Claudes Kopf ist." : "Keine Kontext-Anzeige mehr.", 4);
        });
        ToolStripMenuItem miCtxSize = Item("Kontextgröße", delegate {
            ctxMode = ctxMode == "auto" ? "200" : ctxMode == "200" ? "1000" : "auto"; SaveState();
            foreach (Sess cs in sessions) { CtxInfo ci; if (ctxInfo.TryGetValue(cs.id, out ci)) ci.next = DateTime.MinValue; }
            Say("Kontextfenster: " + CtxModeText() + (ctxMode == "auto" ? "\n(200 Tsd., ab mehr Tokens 1 Mio.)" : ""), 5);
        });
        ToolStripMenuItem miEvo = Item("Entwicklungen ansehen", delegate { PreviewStages(); });
        ToolStripMenuItem miErr = Item("Fehlerprotokoll öffnen", delegate {
            try
            {
                if (File.Exists(ErrLogPath())) Process.Start(ErrLogPath());
                else Say("Bisher ist kein Fehler aufgetreten.", 4);
            }
            catch { Say("Ich kann das Protokoll nicht öffnen.", 4); }
        });
        ToolStripMenuItem miDiag = Item("Verbindung prüfen...", delegate { ShowDiagnosis(); });
        ToolStripMenuItem miInst = Item("Installieren...", delegate { InstallInteractive(); });
        ToolStripMenuItem miAuto = Item("Mit Windows starten", delegate { ToggleAutostart(); });
        ToolStripMenuItem miPhr = Item("Eigene Sätze...", delegate { EditPhrases(); });
        ToolStripMenuItem miGuest = Item("Gast einladen", delegate {
            string[] why = GuestReason(DateTime.Now, born);
            SpawnGuest(why != null ? why[0] : "Hallo, Nachbar!", why != null ? why[1] : "party");
        });
        ToolStripMenuItem miSave = Item("Spielstand sichern...", delegate { ExportSave(); });
        ToolStripMenuItem miLoad = Item("Spielstand laden...", delegate { ImportSave(); });
        miSet.DropDownItems.AddRange(new ToolStripItem[] { miFocus, miMinis, miBreak, miFs, miSound, miVoice, miBat, miPrev, miSeason, new ToolStripSeparator(), miEyes, miDance, miCtx, miCtxSize, new ToolStripSeparator(), miAuto,
            new ToolStripSeparator(), miPhr, miGuest, new ToolStripSeparator(), miSave, miLoad, new ToolStripSeparator(), miDiag, miInst, miErr });

        ToolStripMenuItem miHide = Item("Verstecken", delegate { ToggleVisible(); });
        miHide.ShortcutKeyDisplayString = "Win+Alt+P";
        ToolStripMenuItem miQuit = Item("Beenden", delegate { Close(); });

        // Gruppen, damit das Hauptmenü kurz bleibt
        ToolStripMenuItem miFun = Item("Spiele und Spaß", null);
        StyleDrop(miFun.DropDown, rend);
        miFun.DropDownItems.AddRange(new ToolStripItem[] { miGame, miBall, miPause });
        ToolStripMenuItem miLooks = Item("Aussehen", null);
        StyleDrop(miLooks.DropDown, rend);
        miLooks.DropDownItems.AddRange(new ToolStripItem[] { miSpec, miHat, miFur, miSize, miName, new ToolStripSeparator(), miEvo });
        miLook.DropDownItems.Insert(3, miAch);                       // Erfolge stehen beim Rückblick

        menu.Items.AddRange(new ToolStripItem[] { miStat, sessSep, miGoto, new ToolStripSeparator(), miFeed, miPlay, miSleep,
            new ToolStripSeparator(), miFun, miLooks, miLook, miRem, miSet, miHide, new ToolStripSeparator(), miQuit });

        menu.Opening += delegate {
            RebuildSessionItems();
            miStat.Title = petName; miStat.Food = food; miStat.Joy = joy; miStat.Energy = energy;
            miStat.Extra = "Stufe " + Level() + "  ·  heute " + TodayCount() + " erledigt";
            miStat.ClaudeText = (cRun + cNeed + cReady + cBlock == 0) ? "Keine aktive Claude-Sitzung"
                : "Claude: " + cRun + " arbeitet, " + cNeed + " wartet, " + cReady + " fertig" + (cBlock > 0 ? ", " + cBlock + " blockiert" : "");
            miStat.ClaudeDot = cNeed > 0 ? Color.FromArgb(255, 200, 40) : cBlock > 0 ? Color.FromArgb(214, 84, 72) : cReady > 0 ? Color.FromArgb(80, 200, 120)
                : cRun > 0 ? Color.FromArgb(110, 150, 230) : pal.Dim;
            miStat.Invalidate(); miGoto.Enabled = focusHwnd != IntPtr.Zero;
            miSleep.Text = sleeping ? "Wecken" : "Schlafen";
            for (int i = 0; i < 3; i++) zItems[i].ShortcutKeyDisplayString = Tick(Math.Abs(zoom - zVals[i]) < 0.01f);
            for (int i = 0; i < hatItems.Length; i++)
            {
                bool locked = !HatUnlocked(i);
                hatItems[i].Enabled = !locked;
                hatItems[i].ShortcutKeyDisplayString = locked ? HatLockText(i) : Tick(hatMode == HatKeys[i]);
            }
            for (int i = 0; i < furItems.Length; i++)
            {
                bool locked = !FurUnlocked(i);
                furItems[i].Enabled = !locked;
                furItems[i].ShortcutKeyDisplayString = locked ? FurLockText(i) : Tick(furKey == FurKeys[i]);
            }
            for (int i = 0; i < specItems.Length; i++) specItems[i].ShortcutKeyDisplayString = Tick(species == SpeciesKeys[i]);
            miFur.Enabled = species == "claude";                     // bei den Fan-Figuren gehört die Farbe zur Figur
            miFur.ShortcutKeyDisplayString = species != "claude" ? "gehört zur Figur" : "";
            miWater.ShortcutKeyDisplayString = Tick(waterOn);
            RebuildReminderItems();
            int got = 0;
            for (int i = 0; i < Achs.Length; i++)
            {
                bool has = HasAch(Achs[i].id);
                if (has) got++;
                achItems[i].Enabled = has;                       // gesperrte Erfolge sind grau und zeigen, was zu tun ist
                achItems[i].ShortcutKeyDisplayString = has ? "✓" : Achs[i].desc;
            }
            miAch.ShortcutKeyDisplayString = got + "/" + Achs.Length;
            miFocus.ShortcutKeyDisplayString = Tick(focusMode);
            miMinis.ShortcutKeyDisplayString = Tick(minisOn);
            miBreak.ShortcutKeyDisplayString = Tick(breakOn);
            miFs.ShortcutKeyDisplayString = Tick(autoHideOn);
            miSound.ShortcutKeyDisplayString = Tick(soundOn);
            miVoice.ShortcutKeyDisplayString = Tick(voiceOn);
            miSeason.ShortcutKeyDisplayString = Tick(seasonOn);
            miEyes.ShortcutKeyDisplayString = Tick(eyesOn);
            miDance.ShortcutKeyDisplayString = Tick(danceOn);
            miCtx.ShortcutKeyDisplayString = Tick(ctxOn);
            miCtxSize.ShortcutKeyDisplayString = CtxModeText();
            miCtxSize.Enabled = ctxOn;
            miEvo.ShortcutKeyDisplayString = StageName(RealStage());
            bool inst = false;
            try { inst = string.Equals(Path.GetFullPath(ProgramDir()).TrimEnd('\\'), Path.GetFullPath(InstallDir()).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); } catch { }
            miInst.Enabled = ScriptPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !inst;
            miInst.ShortcutKeyDisplayString = inst ? "✓ installiert" : "";
            miBall.Text = ball != null ? "Ball wegräumen" : "Ball rausholen";
            miBat.ShortcutKeyDisplayString = Tick(batteryOn);
            miPrev.ShortcutKeyDisplayString = Tick(PreviewOn());
            miAuto.ShortcutKeyDisplayString = Tick(File.Exists(StartupFile()));
        };
        menu.Opened += delegate {
            try { int pref = 2; DwmSetWindowAttribute(menu.Handle, 33, ref pref, 4); } catch { }   // runde Ecken unter Windows 11
        };
    }

    // Je eine Zeile pro Sitzung unter der Kopfzeile; ein Klick holt das Fenster dieser Sitzung nach vorn
    void RebuildSessionItems()
    {
        foreach (ToolStripItem old in sessItems) menu.Items.Remove(old);
        sessItems.Clear();
        int idx = menu.Items.IndexOf(miStat) + 1;
        for (int i = 0; i < sessions.Count && i < 8; i++)
        {
            Sess s = sessions[i];
            long hw = s.hwnd;
            ToolStripMenuItem it = Item("      " + (s.Label.Length > 26 ? s.Label.Substring(0, 26) + "..." : s.Label),
                delegate { if (!FocusWindow(new IntPtr(hw))) Say("Ich finde das Fenster nicht."); });   // Leerzeichen lassen Platz für den Punkt
            it.Tag = s.state == "needs" ? Color.FromArgb(255, 200, 40) : s.state == "blocked" ? Color.FromArgb(214, 84, 72)
                : s.state == "ready" ? Color.FromArgb(80, 200, 120) : Color.FromArgb(110, 150, 230);
            int mins = SessMinutes(s);
            it.ShortcutKeyDisplayString = StateWords(s) + (mins >= 1 ? "  ·  " + FmtMins(mins) : "");
            menu.Items.Insert(idx++, it);
            sessItems.Add(it);
        }
        sessSep.Visible = sessItems.Count > 0;
    }

    // Offene Erinnerungen unter den festen Einträgen; ein Klick löscht sie
    void RebuildReminderItems()
    {
        foreach (ToolStripItem old in remItems) miRem.DropDownItems.Remove(old);
        remItems.Clear();
        foreach (Reminder r in reminders)
        {
            Reminder rr = r;
            ToolStripMenuItem it = Item(rr.at.ToString("HH:mm") + "  " + (rr.text.Length > 28 ? rr.text.Substring(0, 28) + "..." : rr.text),
                delegate { reminders.Remove(rr); SaveReminders(); Say("Erinnerung gelöscht.", 3); });
            it.ShortcutKeyDisplayString = rr.repeat > 0 ? "wiederholt" : "löschen";
            miRem.DropDownItems.Add(it); remItems.Add(it);
        }
        remSep.Visible = remItems.Count > 0;
        miRem.ShortcutKeyDisplayString = remItems.Count > 0 ? remItems.Count.ToString() : "";
    }

    static string StateWords(Sess s)
    {
        if (s.state == "needs") { string w = ToolWords(s.tool, s.detail); return w.Length > 0 ? "braucht dich: " + w : "braucht dich"; }
        if (s.state == "blocked") return "blockiert";
        if (s.state == "ready") return "fertig";
        string t = ToolWords(s.tool, s.detail);
        return t.Length > 0 ? t : "arbeitet";
    }

    static string StartupFile()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "ClaudePet.vbs");
    }
    void ToggleAutostart()
    {
        try
        {
            string p = StartupFile();
            if (File.Exists(p)) { File.Delete(p); Say("Ich starte nicht mehr automatisch."); return; }
            if (ScriptPath.Length == 0) { Say("Ich kenne meinen Speicherort nicht."); return; }
            // Als .exe startet der Autostart das Programm direkt, sonst das PowerShell-Skript
            string vbs = ScriptPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? "CreateObject(\"WScript.Shell\").Run Chr(34) & \"" + ScriptPath + "\" & Chr(34), 0, False"
                : "CreateObject(\"WScript.Shell\").Run \"powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"\"" + ScriptPath + "\"\"\", 0, False";
            File.WriteAllText(p, vbs, System.Text.Encoding.Unicode);
            Say("Ich starte jetzt mit Windows.");
        }
        catch { Say("Das hat nicht geklappt."); }
    }
}
