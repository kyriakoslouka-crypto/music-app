using Studio.Core;
using System.Text.Json;
namespace Studio.Tests;
public static class FreeTuningTests
{
    static void Check(bool condition){if(!condition)throw new Exception("Free tuning assertion failed");}
    public static void Register(Action<string,Action> test)
    {
        test("Free tuning: fractional per-note alterations beyond the old presets preserve exact pitch",()=>{
            foreach(var value in new[]{-25.5,-12.25,-3.5,1.75,9.5,12.5,24.25}){var edits=new double[7];edits[2]=value;var f=ByzantineIntonation.Frequency(130,2,1,ByzantineTuning.Diatonic,true,0,edits);var natural=ByzantineIntonation.Frequency(130,2,1,ByzantineTuning.Diatonic,true,0,new double[7]);Check(Math.Abs(1200*Math.Log2(f/natural)-value*1200/72)<1e-8);}
        });
        test("Free tuning: arbitrary positive intervals allow more than half an octave while rejecting invalid totals",()=>{
            var tuning=new IsonTuning([40,5,5,5,5,5,7],[0,1.25,-3.5,0,0,0,0],"custom",true);tuning.Validate();Check(tuning.Intervals.Sum()==72);
            foreach(var wrong in new[]{new double[]{40,5,5,5,5,5,8},new double[]{0,12,12,12,12,12,12},new double[]{double.NaN,5,5,5,5,5,7}}){try{(tuning with{Intervals=wrong}).Validate();throw new Exception("Invalid intervals accepted");}catch(ArgumentException){}}
        });
        test("Free tuning: independent personal mode maps survive JSON and do not alias snapshots",()=>{
            var first=new IsonTuning([12,10.25,7.75,12,10,8,12],[0,12.5,-3.25,0,0,0,0],"custom",true);var second=new IsonTuning(ByzantineIntonation.ModeIntervals("second"),new double[7],"soft-chromatic",true);
            var map=new Dictionary<string,IsonTuning>{{"first",first},{"second",second}};var snapshot=IsonTuning.CopyMap(map);map["first"].Alterations[1]=0;Check(snapshot["first"].Alterations[1]==12.5);var restored=IsonTuning.CopyMap(JsonSerializer.Deserialize<Dictionary<string,IsonTuning>>(JsonSerializer.Serialize(snapshot)));Check(restored["first"].Intervals.SequenceEqual(first.Intervals)&&restored["first"].Alterations[1]==12.5&&restored["second"].Intervals.SequenceEqual(second.Intervals));
        });
    }
}
