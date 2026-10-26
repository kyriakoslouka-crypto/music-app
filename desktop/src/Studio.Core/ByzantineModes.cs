namespace Studio.Core;
public sealed record ByzantineMode(string Id,string Name,int Degree){public override string ToString()=>Name;}
public static class ByzantineModes
{
    // Default drone bases. Intonation and variants are explicit in ByzantineIntonation.
    public static readonly ByzantineMode[] All=[new("first","Α΄ — Πρώτος",1),new("second","Β΄ — Δεύτερος",4),new("third","Γ΄ — Τρίτος",3),new("fourth","Δ΄ — Τέταρτος — Λέγετος",2),new("plagal-first","Πλ. Α΄ — Πλάγιος πρώτος",1),new("plagal-second","Πλ. Β΄ — Πλάγιος δεύτερος",1),new("grave","Βαρύς — Διατονικός",6),new("plagal-fourth","Πλ. Δ΄ — Πλάγιος τέταρτος",0)];
}
