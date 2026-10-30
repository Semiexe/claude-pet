using System;
using System.Drawing;

// Gast-Pet: An Geburtstagen und Feiertagen kommt kurz ein Besucher in anderer Farbe vorbei
public partial class PetForm
{
    MiniPet guest;
    DateTime guestEnds, nextGuestCheck = DateTime.MinValue;
    int guestSide = 1;

    // Anlass des Tages: Text für das Gast-Pet und sein Hut, oder null
    static string[] GuestReason(DateTime d, DateTime born)
    {
        if (d.Month == born.Month && d.Day == born.Day && d.Year > born.Year) return new string[] { "Alles Gute!", "party" };
        if (d.Month == 10 && d.Day == 31) return new string[] { "Süßes oder Saures!", "witch" };
        if (d.Month == 12 && d.Day >= 24 && d.Day <= 26) return new string[] { "Frohe Weihnachten!", "santa" };
        if (d.Month == 12 && d.Day == 31) return new string[] { "Guten Rutsch!", "party" };
        if (d.Month == 1 && d.Day == 1) return new string[] { "Frohes Neues!", "party" };
        return null;
    }

    internal void SpawnGuest(string msg, string hat)
    {
        if (guest != null) return;
        Rectangle wa = AreaAt(new Point(px + W / 2, py + H / 2));
        guestSide = rnd.Next(2) * 2 - 1;
        int startCx = guestSide < 0 ? wa.Left - (int)(90 * sc) : wa.Right + (int)(90 * sc);
        guest = new MiniPet(this, startCx, sc, 4);
        guest.S.state = "guest"; guest.S.project = msg; guest.Hat = hat; guest.Speed = 4f;
        guestEnds = DateTime.Now.AddSeconds(35);
        Say("Oh, Besuch!", 4);
    }

    void CloseGuest()
    {
        if (guest == null) return;
        try { guest.Close(); } catch { }
        guest = null;
    }

    // einmal pro Takt: der Gast läuft zum Pet, bleibt eine Weile und geht wieder
    void UpdateGuest(DateTime now, int feet, int cxp, Rectangle wa)
    {
        if (guest == null)
        {
            if (now < nextGuestCheck) return;
            nextGuestCheck = now.AddSeconds(30);
            string key = now.ToString("yyyyMMdd");
            if (guestDay == key || !Visible || autoHidden || cState != "none" || sleeping || now.Hour < 9 || now.Hour >= 21) return;
            string[] why = GuestReason(now, born);
            if (why == null) return;
            guestDay = key; SaveState();
            SpawnGuest(why[0], why[1]);
            return;
        }
        bool leaving = now >= guestEnds;
        int target = leaving ? (guestSide < 0 ? wa.Left - (int)(120 * sc) : wa.Right + (int)(120 * sc))
                             : (int)Math.Max(wa.Left + 60 * sc, Math.Min(wa.Right - 60 * sc, cxp + guestSide * 170 * sc));
        guest.Update(guest.S, target, feet, sc, Visible);
        if (leaving && (guest.CenterX < wa.Left - 60 * sc || guest.CenterX > wa.Right + 60 * sc)) CloseGuest();
    }
}
