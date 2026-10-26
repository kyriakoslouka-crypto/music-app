using Studio.Core;
namespace Studio.Windows;
internal sealed class IsonTuningDialog:Form
{
    private readonly NumericUpDown[] steps=new NumericUpDown[7],alterations=new NumericUpDown[7];
    private readonly TextBox summary=new(){ReadOnly=true,Multiline=true,Height=75,Dock=DockStyle.Fill,AccessibleName="Άθροισμα και οδηγίες προσωπικού κουρδίσματος"};
    private readonly Button apply=new(){Text="Εφαρμογή",AutoSize=true};
    public IsonTuning? Result{get;private set;}
    private readonly bool anchored;
    public IsonTuningDialog(IsonTuning original)
    {
        original.Validate();anchored=original.Anchored;Text="Προσωπικό κούρδισμα ανά φθόγγο";AccessibleName=Text;Font=new("Segoe UI",11);Size=new(800,720);MinimumSize=new(650,600);StartPosition=FormStartPosition.CenterParent;
        var table=new TableLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,ColumnCount=3,Padding=new(12)};
        table.Controls.Add(new Label{Text="Φθόγγος / διάστημα",AutoSize=true});table.Controls.Add(new Label{Text="Διάστημα σε μόρια",AutoSize=true});table.Controls.Add(new Label{Text="Πρόσθετη αλλοίωση ±",AutoSize=true});
        for(var i=0;i<7;i++){
            var name=$"{ByzantineTuning.Notes[i]} προς {ByzantineTuning.Notes[(i+1)%7]}";
            steps[i]=new(){Minimum=0.01m,Maximum=71.94m,DecimalPlaces=2,Increment=0.1m,Value=(decimal)original.Intervals[i],Width=180,AccessibleName="Διάστημα "+name+" σε μόρια"};
            alterations[i]=new(){Minimum=-72,Maximum=72,DecimalPlaces=2,Increment=0.1m,Value=(decimal)original.Alterations[i],Width=180,AccessibleName="Πρόσθετη ύφεση ή δίεση "+ByzantineTuning.Notes[i]+" σε μόρια"};
            table.Controls.Add(new Label{Text=name,AutoSize=true});table.Controls.Add(steps[i]);table.Controls.Add(alterations[i]);
            steps[i].ValueChanged+=(_,_)=>UpdateSummary();alterations[i].ValueChanged+=(_,_)=>UpdateSummary();
        }
        table.Controls.Add(summary);table.SetColumnSpan(summary,3);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};var cancel=new Button{Text="Ακύρωση",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.AddRange([apply,cancel]);table.Controls.Add(buttons);table.SetColumnSpan(buttons,3);Controls.Add(table);
        apply.Click+=(_,_)=>{try{var result=BuildTuning();result.Validate();Result=result;DialogResult=DialogResult.OK;}catch(ArgumentException e){summary.Text=e.Message;summary.Focus();}};AcceptButton=apply;CancelButton=cancel;AccessibilityContract.Apply(this);UpdateSummary();
    }
    private IsonTuning BuildTuning()=>new(steps.Select(n=>(double)n.Value).ToArray(),alterations.Select(n=>(double)n.Value).ToArray(),"custom",anchored);
    private void UpdateSummary(){var sum=steps.Where(s=>s is not null).Sum(s=>s.Value);summary.Text=$"Άθροισμα: {sum:0.##} από 72 μόρια. Ρύθμισε όλα τα διαστήματα πριν την Εφαρμογή. Αρνητική αλλοίωση: ύφεση, θετική: δίεση, μηδέν: χωρίς πρόσθετη αλλοίωση. Καμία αλλαγή δεν εφαρμόζεται πριν την Εφαρμογή.";apply.Enabled=steps.All(s=>s is not null)&&sum==72;}
    internal void Probe(bool accept)
    {
        var first=steps[0].Value;steps[0].Value=first+0.1m;if(apply.Enabled||Result is not null)throw new InvalidOperationException("Unbalanced editor applied changes");steps[1].Value-=0.1m;if(!apply.Enabled)throw new InvalidOperationException("Balanced editor rejected changes");if(accept)apply.PerformClick();else DialogResult=DialogResult.Cancel;
    }
    internal static object Audit(){using var dialog=new IsonTuningDialog(new(ByzantineTuning.Diatonic.ToArray(),new double[7],"soft-diatonic",true));return new{Passed=true,IntervalControls=dialog.steps.Length,AlterationControls=dialog.alterations.Length,GreekControls=AccessibilityContract.Audit(dialog),Transactional=true};}
}
