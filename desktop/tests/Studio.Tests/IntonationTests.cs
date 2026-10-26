using Studio.Core;
namespace Studio.Tests;
public static class IntonationTests
{
    static void Check(bool condition,string text="Intonation assertion failed"){if(!condition)throw new Exception(text);}
    public static void Register(Action<string,Action> test)
    {
        test("Intonation: explicit Ni-based patterns, all octaves and eight mode assignments",()=>{
            foreach(var genus in ByzantineIntonation.All.Where(g=>g.Id!="custom")){ByzantineTuning.Validate(genus.Intervals);foreach(var mode in ByzantineModes.All){var low=ByzantineIntonation.Frequency(130,0,mode.Degree,genus.Intervals,true,0,new double[7]);var high=ByzantineIntonation.Frequency(130,7,mode.Degree,genus.Intervals,true,0,new double[7]);Check(Math.Abs(high/low-2)<1e-12);var root=ByzantineIntonation.Frequency(130,mode.Degree,mode.Degree,genus.Intervals,true,0,new double[7]);Check(Math.Abs(root-130*Math.Pow(2,ByzantineTuning.Diatonic.Take(mode.Degree).Sum()/72))<1e-9,"Genus changed the mode basis");}}
            foreach(var mode in ByzantineModes.All){var steps=ByzantineIntonation.ModeIntervals(mode.Id);ByzantineTuning.Validate(steps);Check(ByzantineIntonation.Offset(mode.Degree,mode.Degree,steps,true)==ByzantineTuning.Diatonic.Take(mode.Degree).Sum());var root=ByzantineIntonation.Frequency(130,mode.Degree,mode.Degree,steps,true,0,new double[7]);var octave=ByzantineIntonation.Frequency(130,mode.Degree+7,mode.Degree,steps,true,0,new double[7]);Check(Math.Abs(octave/root-2)<1e-12,"Full mode octave missing");}
            try{ByzantineIntonation.FromRoot(1,[6,20,6,12,6,20,6]);throw new Exception("76 moria accepted as an octave");}catch(ArgumentException){}
            Check(ByzantineModes.All.Count(m=>ByzantineIntonation.DefaultGenre(m.Id)=="enharmonic")==1);Check(ByzantineIntonation.DefaultGenre("second")=="soft-chromatic"&&ByzantineIntonation.DefaultGenre("plagal-second")=="hard-chromatic");
        });
        test("Intonation: independent hard/soft chromatic offsets avoid tetrachord-origin rotation errors",()=>{
            var hard=ByzantineIntonation.All.Single(g=>g.Id=="hard-chromatic");var soft=ByzantineIntonation.All.Single(g=>g.Id=="soft-chromatic");
            Check(Enumerable.Range(0,8).Select(n=>ByzantineIntonation.Offset(n,1,hard.Intervals,true)).SequenceEqual(new double[]{8,12,18,38,42,54,60,80}));
            Check(Enumerable.Range(0,8).Select(n=>ByzantineIntonation.Offset(n,4,soft.Intervals,true)).SequenceEqual(new double[]{0,12,19,37,42,49,67,72}));
            Check(hard.Intervals.Skip(1).Take(3).SequenceEqual(new double[]{6,20,4}));Check(soft.Intervals.Skip(4).Take(3).SequenceEqual(new double[]{7,18,5}));
        });
        test("Intonation: eight user-confirmed mode sequences rotate from their actual named bases",()=>{
            var references=new (string Id,int Base,double[] Steps)[]{("first",1,[10,8,12,10,8,12,12]),("second",4,[7,18,5,12,7,18,5]),("third",3,[12,12,6,12,12,6,12]),("fourth",2,[8,12,10,8,12,12,10]),("plagal-first",1,[10,8,12,10,8,12,12]),("plagal-second",1,[6,20,4,12,6,20,4]),("grave",6,[12,10,8,12,10,8,12]),("plagal-fourth",0,[12,10,8,12,12,10,8])};
            foreach(var r in references){var mode=ByzantineModes.All.Single(m=>m.Id==r.Id);Check(mode.Degree==r.Base);var ni=ByzantineIntonation.ModeIntervals(r.Id);Check(Enumerable.Range(0,7).Select(i=>ni[(r.Base+i)%7]).SequenceEqual(r.Steps));}
        });
        test("Intonation: microtonal sharps/flats are moria, not implicit Western semitones",()=>{
            var g=ByzantineIntonation.All[0];var edits=new double[7];var natural=ByzantineIntonation.Frequency(130,2,1,g.Intervals,true,0,edits);edits[2]=4;var sharp=ByzantineIntonation.Frequency(130,2,1,g.Intervals,true,0,edits);Check(Math.Abs(1200*Math.Log2(sharp/natural)-4*1200.0/72)<1e-9);Check(ByzantineIntonation.NoteName(2,1,g.Intervals,true,edits).Contains("δίεση 4"));edits[2]=-4;Check(ByzantineIntonation.NoteName(2,1,g.Intervals,true,edits).Contains("ύφεση 4"));edits[0]=2;Check(Math.Abs(ByzantineIntonation.Frequency(130,7,1,g.Intervals,true,0,edits)/ByzantineIntonation.Frequency(130,0,1,g.Intervals,true,0,edits)-2)<1e-12);
        });
        test("Intonation: enharmonic Ga differs from tempered hard diatonic and legacy custom stays exact",()=>{
            var enh=ByzantineIntonation.All.Single(g=>g.Id=="enharmonic");var hard=ByzantineIntonation.All.Single(g=>g.Id=="hard-diatonic");Check(enh.Intervals.SequenceEqual(new double[]{12,6,12,12,12,6,12}));Check(hard.Intervals.SequenceEqual(new double[]{12,12,6,12,12,12,6}));Check(ByzantineIntonation.NoteName(6,3,enh.Intervals,true,new double[7]).Contains("ύφεση 4"));Check(ByzantineIntonation.NoteName(2,3,enh.Intervals,true,new double[7]).Contains("ύφεση 4"));
            for(var n=0;n<8;n++)Check(Math.Abs(ByzantineIntonation.Frequency(130,n,3,enh.Intervals,false,2,new double[7])-ByzantineTuning.Frequency(130,n,2,enh.Intervals))<1e-9);
            try{ByzantineIntonation.ValidateAlterations([0,0,double.NaN,0,0,0,0]);throw new Exception("NaN accepted");}catch(ArgumentException){}
        });
    }
}
