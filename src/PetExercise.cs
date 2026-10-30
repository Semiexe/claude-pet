using System;
using System.Drawing;

// Pause mit Übung: Das Pet macht eine kleine Dehnübung vor, die man nachmachen kann
public partial class PetForm
{
    static readonly string[] ExText = new string[] {
        "Schultern kreisen lassen",
        "Arme weit nach oben strecken",
        "Tief durchatmen: ein ... und aus ...",
        "Blick für 20 Sekunden in die Ferne",
        "Kurz aufstehen und etwas trinken!" };
    static readonly string[] ExAct = new string[] { "shoulders", "stretch", "breathe", "look", "hop" };

    int exStep = -1;                       // -1 = keine Übung läuft
    DateTime exAt = DateTime.MinValue;     // wann der nächste Schritt beginnt

    // Startet die Übung nach einer kurzen Ansage
    void StartExercise(string intro, double delaySeconds)
    {
        exStep = 0; exAt = DateTime.Now.AddSeconds(delaySeconds);
        act = "none"; actFrames = 0; walkFrames = 0;
        Say(intro, delaySeconds + 1);
    }

    // Ein Schritt der Übung; der nächste folgt, sobald dieser vorbei ist
    void AdvanceExercise(DateTime now)
    {
        if (exStep >= ExText.Length)
        {
            exStep = -1; act = "none"; actFrames = 0;
            hop = 22; Burst("♥", 3);
            Say("Gut gemacht! Weiter geht's.", 5);
            return;
        }
        string a = ExAct[exStep];
        Say("Pause (" + (exStep + 1) + "/" + ExText.Length + "):\n" + ExText[exStep], 6);
        if (a == "hop") { act = "none"; hop = 22; actFrames = 150; }
        else { act = a; actFrames = 180; }
        exStep++;
    }
}
