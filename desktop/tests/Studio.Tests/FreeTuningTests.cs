using Studio.Core;
using System.Text.Json;
namespace Studio.Tests;
public static class FreeTuningTests
{
    static void Check(bool condition){if(!condition)throw new Exception("Free tuning assertion failed");}
    public static void Register(Action<string,Action> test)
    {
        test("Free tuning: large Zo alteration leaves every other pitch unchanged",()=>{
            var steps=ByzantineIntonation.ModeIntervals("first");var clean=new double[7];var changed=new double[7];changed[6]=90;
            for(var degree=0;degree<15;degree++){
                var before=ByzantineIntonation.Frequency(100,degree,1,steps,true,0,clean);
                var after=ByzantineIntonation.Frequency(100,degree,1,steps,true,0,changed);
                Check(Math.Abs(after/before-(degree%7==6?Math.Pow(2,90.0/72):1))<1e-9);
            }
            foreach(var value in new[]{-7200.0,7200.0}){changed[6]=value;ByzantineIntonation.ValidateAlterations(changed);var restored=JsonSerializer.Deserialize<double[]>(JsonSerializer.Serialize(changed))!;Check(restored[6]==value);}
            foreach(var value in new[]{-7200.01,7200.01,double.NaN,double.PositiveInfinity}){changed[6]=value;try{ByzantineIntonation.ValidateAlterations(changed);throw new Exception("Invalid alteration accepted");}catch(ArgumentException){}}
        });
        test("Free tuning: octave arrows preserve pitch class and stop at boundaries",()=>{
            for(var degree=0;degree<15;degree++)foreach(var direction in new[]{-1,1}){
                var target=ByzantineIntonation.ShiftOctave(degree,direction);Check(target%7==degree%7);
                var expected=degree+direction*7;Check(target==(expected is >=0 and <=14?expected:degree));
                if(target!=degree){var before=ByzantineIntonation.Frequency(100,degree,0,ByzantineTuning.Diatonic,false,0,new double[7]);var after=ByzantineIntonation.Frequency(100,target,0,ByzantineTuning.Diatonic,false,0,new double[7]);Check(Math.Abs(after/before-Math.Pow(2,direction))<1e-9);}
            }
        });
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
