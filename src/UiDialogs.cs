using System;
using System.Drawing;
using System.Windows.Forms;

// Kleines Fenster zum Bearbeiten der eigenen Sätze (ein Satz pro Zeile)
class PhraseDialog : Form
{
    TextBox tb;
    public string Result = "";

    public PhraseDialog(Pal p, float k, string current)
    {
        Text = "Eigene Sätze";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size((int)(460 * k), (int)(380 * k));
        BackColor = p.Bg; ForeColor = p.Fg;
        Font = new Font("Segoe UI", 10f);

        Label hint = new Label();
        hint.Text = "Was soll dein Pet ab und zu sagen? Ein Satz pro Zeile.\r\n{name} wird durch den Namen des Pets ersetzt.";
        hint.SetBounds((int)(16 * k), (int)(12 * k), (int)(428 * k), (int)(44 * k));
        hint.ForeColor = p.Dim;

        tb = new TextBox();
        tb.Multiline = true; tb.ScrollBars = ScrollBars.Vertical; tb.AcceptsReturn = true; tb.WordWrap = true;
        tb.BackColor = p.Track; tb.ForeColor = p.Fg; tb.BorderStyle = BorderStyle.FixedSingle;
        tb.SetBounds((int)(16 * k), (int)(60 * k), (int)(428 * k), (int)(260 * k));
        tb.Text = current;

        Button ok = new Button(), cancel = new Button();
        ok.Text = "Speichern"; cancel.Text = "Abbrechen";
        ok.SetBounds((int)(244 * k), (int)(334 * k), (int)(96 * k), (int)(32 * k));
        cancel.SetBounds((int)(348 * k), (int)(334 * k), (int)(96 * k), (int)(32 * k));
        foreach (Button b in new Button[] { ok, cancel })
        {
            b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = p.Border;
            b.BackColor = p.Bg; b.ForeColor = p.Fg;
        }
        ok.BackColor = p.Hover; ok.ForeColor = p.HoverFg; ok.FlatAppearance.BorderColor = p.Hover;
        ok.DialogResult = DialogResult.OK; cancel.DialogResult = DialogResult.Cancel;
        ok.Click += delegate { Result = tb.Text; };
        AcceptButton = ok; CancelButton = cancel;

        Controls.AddRange(new Control[] { hint, tb, ok, cancel });
        Shown += delegate { tb.Focus(); tb.SelectionStart = tb.TextLength; };
    }
}
