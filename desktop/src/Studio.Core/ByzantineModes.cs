namespace Studio.Core;
public sealed record ByzantineMode(string Id,string Name,int Degree){public override string ToString()=>Name;}
public static class ByzantineModes
{
    // Indicative traditional drone bases, not a complete intonation model of each echos.
    public static readonly ByzantineMode[] All=[new("first","Α΄ — Πρώτος",1),new("second","Β΄ — Δεύτερος",4),new("third","Γ΄ — Τρίτος",3),new("fourth","Δ΄ — Τέταρτος",4),new("plagal-first","Πλ. Α΄ — Πλάγιος πρώτος",1),new("plagal-second","Πλ. Β΄ — Πλάγιος δεύτερος",1),new("grave","Βαρύς",6),new("plagal-fourth","Πλ. Δ΄ — Πλάγιος τέταρτος",0)];
}
