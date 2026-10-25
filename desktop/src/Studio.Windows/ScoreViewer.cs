using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Studio.Core;
namespace Studio.Windows;
internal sealed class ScoreViewer:Form
{
    internal ScoreViewer(Exercise[] exercises)
    {
        Text="Παρτιτούρα — ζητούμενο μουσικό μέρος";AccessibleName=Text;Size=new(1100,820);StartPosition=FormStartPosition.CenterParent;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.Percent,65));layout.RowStyles.Add(new(SizeType.Percent,35));
        var commands=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};var selector=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=380,AccessibleName="Ερώτηση παρτιτούρας"};
        selector.Items.AddRange(exercises.Select((e,i)=>(object)$"Ερώτηση {i+1}: {CategoryLabels.Name(e.Category)}").ToArray());commands.Controls.Add(selector);
        var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,AccessibleName="Οπτικό πεντάγραμμο"};var canvas=new NotationCanvas{Location=new(0,0)};scroll.Controls.Add(canvas);
        var description=new TextBox{Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Both,AccessibleName="Αναλυτική προσβάσιμη περιγραφή παρτιτούρας"};
        void Button(string name,Action action){var b=new Button{Text=name,AutoSize=true};b.Click+=(_,_)=>action();commands.Controls.Add(b);}
        Button("Μεγέθυνση",()=>{canvas.Zoom=Math.Min(2,canvas.Zoom+0.25f);});Button("Σμίκρυνση",()=>{canvas.Zoom=Math.Max(0.5f,canvas.Zoom-0.25f);});
        Button("Αριστερά",()=>scroll.AutoScrollPosition=new(Math.Max(0,-scroll.AutoScrollPosition.X-500),-scroll.AutoScrollPosition.Y));Button("Δεξιά",()=>scroll.AutoScrollPosition=new(-scroll.AutoScrollPosition.X+500,-scroll.AutoScrollPosition.Y));Button("Πάνω",()=>scroll.AutoScrollPosition=new(-scroll.AutoScrollPosition.X,Math.Max(0,-scroll.AutoScrollPosition.Y-350)));Button("Κάτω",()=>scroll.AutoScrollPosition=new(-scroll.AutoScrollPosition.X,-scroll.AutoScrollPosition.Y+350));Button("Κλείσιμο",Close);
        selector.SelectedIndexChanged+=(_,_)=>{var document=ScoreNotation.Build(exercises[selector.SelectedIndex]);canvas.Document=document;description.Text=document.Description;scroll.AutoScrollPosition=new(0,0);};
        layout.Controls.Add(commands);layout.Controls.Add(scroll);layout.Controls.Add(description);Controls.Add(layout);AccessibilityContract.Apply(this);selector.SelectedIndex=0;
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keys){if(keys==Keys.Escape){Close();return true;}return base.ProcessCmdKey(ref msg,keys);}
}
internal sealed class NotationCanvas:Control
{
    private static readonly PrivateFontCollection MusicFonts=LoadFonts();
    private static PrivateFontCollection LoadFonts(){
        using var stream=typeof(NotationCanvas).Assembly.GetManifestResourceStream("Studio.Windows.Bravura.otf")??throw new InvalidOperationException("Λείπει η γραμματοσειρά μουσικής.");
        var data=new byte[stream.Length];stream.ReadExactly(data);var memory=Marshal.AllocHGlobal(data.Length);Marshal.Copy(data,0,memory,data.Length);
        var collection=new PrivateFontCollection();collection.AddMemoryFont(memory,data.Length);return collection; // font bytes must remain alive for process lifetime
    }
    private NotationDocument? document;private float zoom=1;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal NotationDocument? Document{get=>document;set{document=value;ResizeScore();}}
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal float Zoom{get=>zoom;set{zoom=value;ResizeScore();}}
    internal static string MusicFontName=>MusicFonts.Families[0].Name;
    internal NotationCanvas(){DoubleBuffered=true;BackColor=Color.White;AccessibleName="Πεντάγραμμο με νότες";AccessibleRole=AccessibleRole.Graphic;TabStop=false;}
    private double[] Times()=>document is null?[]:document.Symbols.SelectMany(n=>new[]{n.Beat,n.Beat+n.Duration}).Append(0).Append(document.Length).Distinct().Order().ToArray();
    private bool Bass(int staff)=>document is not null&&(!document.SeparateVoices?staff==1:document.Symbols.Where(n=>n.Staff==staff&&n.Midi.HasValue).Select(n=>n.Midi!.Value).DefaultIfEmpty(60).Average()<60);
    private static (int Step,int Accidental) Pitch(int midi){int[] steps=[0,0,1,1,2,3,3,4,4,5,5,6];var pc=midi%12;return((midi/12-1)*7+steps[pc],new[]{1,3,6,8,10}.Contains(pc)?1:0);}
    private float[] StaffPositions(){
        if(document is null)return[];var result=new float[document.Staves];float cursor=90;
        for(var s=0;s<result.Length;s++){
            var origin=Bass(s)?18:30;var pitches=document.Symbols.Where(n=>n.Staff==s&&n.Midi.HasValue).Select(n=>Pitch(n.Midi!.Value).Step).DefaultIfEmpty(origin+4).ToArray();
            var above=Math.Max(0,(pitches.Max()-origin-8)*5+35);var below=Math.Max(0,(origin-pitches.Min())*5+25);
            result[s]=cursor+above;cursor=result[s]+40+below+55;
        }
        return result;
    }
    private void ResizeScore(){var ys=StaffPositions();var padding=document is {Numerator:>0}?document.Length/(document.Numerator*4.0/document.Denominator)*26:0;Size=new((int)Math.Ceiling(Math.Max(900,140+Times().Length*46+padding)*zoom),(int)Math.Ceiling((ys.Length==0?350:ys[^1]+160)*zoom));Invalidate();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(document is null)return;var g=e.Graphics;g.ScaleTransform(zoom,zoom);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
        using var text=new Font("Segoe UI",12,FontStyle.Regular,GraphicsUnit.Pixel);using var title=new Font("Segoe UI",17,FontStyle.Bold,GraphicsUnit.Pixel);using var music=new Font(MusicFonts.Families[0],40,FontStyle.Regular,GraphicsUnit.Pixel);
        using var pen=new Pen(Color.Black,1);using var stem=new Pen(Color.Black,1.5f);var times=Times();var ys=StaffPositions();var measureLength=document.Numerator==0?double.PositiveInfinity:document.Numerator*4.0/document.Denominator;float X(double beat)=>110+Array.BinarySearch(times,beat)*46+(double.IsPositiveInfinity(measureLength)?0:(int)(beat/measureLength)*26);
        var ascent=music.FontFamily.GetCellAscent(FontStyle.Regular)/(float)music.FontFamily.GetEmHeight(FontStyle.Regular)*music.Size;
        void Glyph(int code,float x,float y)=>g.DrawString(char.ConvertFromUtf32(code),music,Brushes.Black,x,y-ascent,StringFormat.GenericTypographic);
        g.DrawString(document.Title,title,Brushes.Black,15,12);g.DrawString("Ζητούμενο μέρος χωρίς εισαγωγή. Ρητές αλλοιώσεις· οι συζεύξεις διατηρούν τις διάρκειες.",text,Brushes.Black,15,40);
        for(var s=0;s<ys.Length;s++){
            var y=ys[s];for(var line=0;line<5;line++)g.DrawLine(pen,15,y+line*10,Width/zoom-20,y+line*10);
            Glyph(Bass(s)?0xE062:0xE050,24,y+(Bass(s)?10:30));
            if(document.Numerator>0){g.DrawString(document.Numerator.ToString(),title,Brushes.Black,70,y+1);g.DrawString(document.Denominator.ToString(),title,Brushes.Black,70,y+22);}
            if(document.Staves>2)g.DrawString("Φωνή "+(s+1),text,Brushes.Black,15,y-25);
            var measure=document.Numerator==0?double.PositiveInfinity:document.Numerator*4.0/document.Denominator;
            if(double.IsFinite(measure))for(var b=measure;b<document.Length;b+=measure)g.DrawLine(pen,X(b)-30,y,X(b)-30,y+40);
            g.DrawLine(stem,X(document.Length)+15,y,X(document.Length)+15,y+40);
        }
        foreach(var n in document.Symbols){
            var y=ys[n.Staff];var x=X(n.Beat);
            if(!n.Midi.HasValue){var code=n.Denominator switch{1=>0xE4E3,2=>0xE4E4,4=>0xE4E5,8=>0xE4E6,16=>0xE4E7,32=>0xE4E8,_=>0xE4E9};Glyph(code,x,y+(n.Denominator==1?10:20));if(n.Dots>0)g.FillEllipse(Brushes.Black,x+18,y+17,3,3);continue;}
            var pitch=Pitch(n.Midi.Value);var origin=Bass(n.Staff)?18:30;var ny=y+40-(pitch.Step-origin)*5;
            for(var step=origin-2;step>=pitch.Step;step-=2){var ly=y+40-(step-origin)*5;g.DrawLine(pen,x-5,ly,x+18,ly);}
            for(var step=origin+10;step<=pitch.Step;step+=2){var ly=y+40-(step-origin)*5;g.DrawLine(pen,x-5,ly,x+18,ly);}
            if(!n.TieFrom)Glyph(pitch.Accidental==1?0xE262:0xE261,x-18,ny);
            Glyph(n.Denominator==1?0xE0A2:n.Denominator==2?0xE0A3:0xE0A4,x,ny);
            if(n.Denominator>1){var down=ny<y+20;var sx=down?x:x+12;var end=ny+(down?33:-33);g.DrawLine(stem,sx,ny,sx,end);if(n.Denominator>=8)Glyph((n.Denominator switch{8=>0xE240,16=>0xE242,32=>0xE244,_=>0xE246})+(down?1:0),sx,end); }
            if(n.Accent)g.DrawString(">",title,Brushes.Black,x,ny-50);
            if(n.Dots>0)g.FillEllipse(Brushes.Black,x+18,ny-5,3,3);
            if(n.TieTo)g.DrawBezier(pen,x+10,ny+7,(x+X(n.Beat+n.Duration))/2,ny+19,(x+X(n.Beat+n.Duration))/2,ny+19,X(n.Beat+n.Duration)+3,ny+7);
        }
    }
}
