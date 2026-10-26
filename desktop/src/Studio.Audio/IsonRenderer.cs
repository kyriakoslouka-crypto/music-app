namespace Studio.Audio;
public sealed record IsonLoop(float[] Samples,double EstimatedRootHz,double TargetHz,double Confidence,string SourcePath);
public static class IsonRenderer
{
    public static (double Hz,double Confidence) EstimateRoot(PcmWave wave,double expected)
    {
        if(!double.IsFinite(expected)||expected is <20 or >4000)throw new ArgumentException("Μη έγκυρη συχνότητα δείγματος.");
        const int rate=24000;var frequencies=new List<double>();var confidence=new List<double>();
        foreach(var fraction in new[]{0.3,0.5,0.7}){
            var count=Math.Min(6000,(int)(wave.Frames*0.2*rate/wave.SampleRate));if(count<1000)continue;var data=new double[count];var start=wave.Frames*fraction;
            for(var i=0;i<count;i++){var p=(int)(start+(double)i*wave.SampleRate/rate);p=Math.Clamp(p,0,wave.Frames-1);data[i]=(wave.Samples[p*2]+wave.Samples[p*2+1])/2;}
            var mean=data.Average();for(var i=0;i<count;i++)data[i]-=mean;
            var lo=Math.Max(2,(int)(rate/(expected*1.06)));var hi=Math.Min(count/2,(int)Math.Ceiling(rate/(expected*0.94)));if(hi<=lo+2)continue;
            var correlations=new double[hi+1];
            for(var lag=lo;lag<=hi;lag++){double xy=0,xx=0,yy=0;for(var i=0;i<count-lag;i++){var a=data[i];var b=data[i+lag];xy+=a*b;xx+=a*a;yy+=b*b;}correlations[lag]=xy/Math.Sqrt(Math.Max(1e-30,xx*yy));}
            var best=Enumerable.Range(lo,hi-lo+1).MaxBy(i=>correlations[i]);if(best==lo||best==hi||correlations[best]<0.6)continue;
            var denominator=correlations[best-1]-2*correlations[best]+correlations[best+1];var offset=Math.Abs(denominator)<1e-12?0:0.5*(correlations[best-1]-correlations[best+1])/denominator;
            frequencies.Add(rate/(best+Math.Clamp(offset,-0.5,0.5)));confidence.Add(correlations[best]);
        }
        return frequencies.Count==0?(expected,0):(frequencies.Order().ElementAt(frequencies.Count/2),confidence.Average());
    }
    public static IsonSample Select(IsonBank bank,string voice,double frequency)=>bank.Samples.Where(s=>s.VoiceId==voice).MinBy(s=>Math.Abs(Math.Log2(frequency/s.DeclaredHz)))??throw new ArgumentException("Επίλεξε φωνή.");
    public static IsonLoop Render(PcmWave wave,double declared,double frequency,double volume,string sourcePath,CancellationToken token=default)
    {
        if(!double.IsFinite(frequency)||frequency is <35 or >2000||!double.IsFinite(volume)||volume is <=0 or >1||wave.Frames<wave.SampleRate)throw new ArgumentException("Μη έγκυρη συχνότητα, ένταση ή διάρκεια φωνής.");
        token.ThrowIfCancellationRequested();var estimate=EstimateRoot(wave,declared);var ratio=frequency/estimate.Hz;if(ratio is <0.5 or >2)throw new ArgumentException("Η βάση απέχει πάνω από οκτάβα από τη φωνή. Επίλεξε άλλη φωνή ή κοντινότερη βάση.");
        const int rate=44100;var step=(double)wave.SampleRate/rate*ratio;
        var start=wave.Frames*0.2;var available=wave.Frames*0.65;var frames=(int)Math.Min(rate*4,available/step);var fade=Math.Min(rate/8,frames/8);var length=frames-fade;
        if(length<rate/4)throw new ArgumentException("Πολύ σύντομο δείγμα για σταθερό ισοκράτημα.");
        var rendered=new float[frames*2];const int radius=12;var cutoff=Math.Min(1,1/step)*0.94;
        for(var n=0;n<frames;n++){
            if((n&4095)==0)token.ThrowIfCancellationRequested();var position=start+n*step;var centre=(int)position;
            double left=0,right=0,weight=0;
            for(var k=-radius+1;k<=radius;k++){
                var index=centre+k;if(index<0||index>=wave.Frames)continue;var distance=position-index;if(Math.Abs(distance)>=radius)continue;
                var x=Math.PI*distance*cutoff;var sinc=Math.Abs(x)<1e-12?1:Math.Sin(x)/x;var window=0.5+0.5*Math.Cos(Math.PI*distance/radius);var w=sinc*window*cutoff;
                left+=wave.Samples[index*2]*w;right+=wave.Samples[index*2+1]*w;weight+=w;
            }
            rendered[n*2]=(float)(left/weight);rendered[n*2+1]=(float)(right/weight);
        }
        var loop=new float[length*2];
        for(var n=0;n<length;n++)for(var c=0;c<2;c++){
            var blend=n<fade?(float)(0.5-0.5*Math.Cos(Math.PI*n/(fade-1))):1;
            loop[n*2+c]=n<fade?rendered[(length+n)*2+c]*(1-blend)+rendered[n*2+c]*blend:rendered[n*2+c];
        }
        var peak=loop.Max(x=>Math.Abs(x));if(peak<1e-6)throw new InvalidDataException("Το δείγμα δεν περιέχει χρήσιμο ήχο.");var gain=Math.Min(1,0.85f/peak)*(float)volume;
        var pivot=1;double bestEnergy=double.MaxValue;
        for(var n=1;n<length;n++){double energy=0;for(var c=0;c<2;c++)energy+=loop[n*2+c]*loop[n*2+c]+loop[(n-1)*2+c]*loop[(n-1)*2+c];if(energy<bestEnergy){bestEnergy=energy;pivot=n;}}
        var result=new float[loop.Length];for(var n=0;n<length;n++)for(var c=0;c<2;c++)result[n*2+c]=loop[((n+pivot)%length)*2+c]*gain;
        return new(result,estimate.Hz,frequency,estimate.Confidence,sourcePath);
    }
}
