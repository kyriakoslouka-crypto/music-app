using Studio.Core;
namespace Studio.Windows;
public sealed partial class MainForm
{
    private ExamEngine? exam;
    private readonly ExamStore examStore=new(DataDirectory);
    private readonly ComboBox examProfile=Combo("Εκπαιδευτικό εξεταστικό προφίλ",[]);
    private readonly NumericUpDown examMinutes=Number("Χρονικό όριο λεπτά, 0 για χωρίς όριο",0,180,0);
    private readonly TextBox examInfo=new(){ReadOnly=true,Multiline=true,Height=90,Dock=DockStyle.Fill,AccessibleName="Περιγραφή και ανεπίσημος χαρακτήρας προφίλ"};
    private readonly TextBox examReport=new(){ReadOnly=true,Multiline=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,AccessibleName="Πρόοδος εξέτασης και αναλυτική αναφορά"};
    private readonly ListBox examHistory=new(){Dock=DockStyle.Fill,AccessibleName="Ιστορικό ολοκληρωμένων εξετάσεων"};
    private readonly ListBox wrongHistory=new(){Dock=DockStyle.Fill,AccessibleName="Λανθασμένα παραδείγματα για ακριβή επανάληψη"};
    private readonly System.Windows.Forms.Timer examTimer=new(){Interval=1000};
    private sealed record SavedExam(ExamState State){public override string ToString()=>$"{State.Started.LocalDateTime:g} — {State.Profile.Name}";}
    private sealed record WrongItem(Attempt Attempt){public override string ToString()=>$"{Attempt.At.LocalDateTime:g} — {CategoryName(Attempt.Category)} — {Attempt.Percentage:F0}%";}
    private void BuildExams()
    {
        var page=new AccessibleTabPage("Εξετάσεις και προσαρμογή");var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,Padding=new(12),RowCount=7};
        table.RowStyles.Add(new(SizeType.AutoSize));table.RowStyles.Add(new(SizeType.Absolute,95));table.RowStyles.Add(new(SizeType.AutoSize));table.RowStyles.Add(new(SizeType.Percent,50));table.RowStyles.Add(new(SizeType.Absolute,80));table.RowStyles.Add(new(SizeType.Absolute,80));table.RowStyles.Add(new(SizeType.AutoSize));
        var settings=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};settings.Controls.AddRange([new Label{Text="Ανεξάρτητο προφίλ",AutoSize=true},examProfile,new Label{Text="Λεπτά, 0 = χωρίς όριο",AutoSize=true},examMinutes]);table.Controls.Add(settings);table.Controls.Add(examInfo);
        var commands=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
        void Command(string name,Action action){var button=new Button{Text=name,AutoSize=true};button.Click+=(_,_)=>{try{action();}catch(Exception ex){Error(ex.Message);}};commands.Controls.Add(button);}
        void AsyncCommand(string name,Func<Task> action){var button=new Button{Text=name,AutoSize=true};button.Click+=async(_,_)=>{try{await action();}catch(Exception ex){Error(ex.Message);}};commands.Controls.Add(button);}
        AsyncCommand("Έναρξη εξέτασης",StartExam);AsyncCommand("Συνέχιση αποθηκευμένης",ResumeExam);Command("Παράλειψη τρέχουσας",SkipExamQuestion);Command("Τερματισμός και αναφορά",FinishExam);Command("Επιστροφή σε εξάσκηση",ExitExam);
        table.Controls.Add(commands);table.Controls.Add(examReport);table.Controls.Add(examHistory);table.Controls.Add(wrongHistory);
        var learning=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
        var adaptive=new Button{Text="Προσαρμοσμένη εξάσκηση αδυναμίας",AutoSize=true};adaptive.Click+=async(_,_)=>{try{await AdaptivePractice();}catch(Exception ex){Error(ex.Message);}};
        var review=new Button{Text="Επανάληψη επιλεγμένου λάθους",AutoSize=true};review.Click+=async(_,_)=>{try{await ReviewMistake();}catch(Exception ex){Error(ex.Message);}};
        var refresh=new Button{Text="Ανανέωση ιστορικού/αδυναμιών",AutoSize=true};refresh.Click+=(_,_)=>RefreshExamHistory();learning.Controls.AddRange([adaptive,review,refresh]);table.Controls.Add(learning);
        page.Controls.Add(table);tabs.TabPages.Add(page);
        examProfile.Items.AddRange(ExamCatalog.Profiles.Cast<object>().ToArray());examProfile.SelectedIndex=0;
        examProfile.SelectedIndexChanged+=(_,_)=>DescribeExam();DescribeExam();
        examHistory.SelectedIndexChanged+=(_,_)=>{if(examHistory.SelectedItem is SavedExam item)examReport.Text=ExamEngine.Restore(item.State).Report(true);};
        examTimer.Tick+=(_,_)=>{if(exam?.Expired==true){Stop();exam.Finish("time-limit");SaveExam();SetExamLocks(false);UpdateExamStatus();Tell("Το χρονικό όριο έληξε. Η εξέταση αποθηκεύτηκε με τις αναπάντητες ερωτήσεις στο μηδέν.");}};
        examTimer.Start();
    }
    private void DescribeExam(){if(examProfile.SelectedItem is ExamProfile p)examInfo.Text=p.Disclosure+$" Ερωτήσεις: {p.QuestionCount}. Ακροάσεις ανά ερώτηση: {p.MaxPlays}. Προεπιλογή χωρίς χρονικό όριο.";}
    private async Task StartExam()
    {
        if(exam?.State.Finished is null&&exam is not null)throw new InvalidOperationException("Συνέχισε ή τερμάτισε πρώτα την τρέχουσα εξέταση.");
        var old=examStore.LoadCurrent();if(old?.Finished is null&&old is not null)throw new InvalidOperationException("Υπάρχει αποθηκευμένη ανολοκλήρωτη εξέταση. Επίλεξε Συνέχιση ή Τερματισμό.");
        if(piano is null)throw new InvalidOperationException("Εγκατάστησε πρώτα το πιάνο από τις Ρυθμίσεις.");
        Stop();exam=ExamEngine.Create((ExamProfile)examProfile.SelectedItem!,Random.Shared.Next(),(int)examMinutes.Value);SaveExam();SetExamLocks(true);await ShowExamQuestion();
    }
    private async Task ResumeExam()
    {
        Stop();
        var saved=examStore.LoadCurrent()??throw new InvalidOperationException("Δεν υπάρχει αποθηκευμένη εξέταση.");exam=ExamEngine.Restore(saved);LearningAnalytics.MergeExamAnswers(history,exam.State);if(storageHealthy)store.Save(history);
        if(exam.Expired){FinishExam();return;}if(exam.State.Finished is not null){UpdateExamStatus();return;}SetExamLocks(true);await ShowExamQuestion();
    }
    private async Task ShowExamQuestion()
    {
        if(exam is null)return;
        if(piano is null)throw new InvalidOperationException("Εγκατάστησε ή φόρτωσε πρώτα πιάνο.");
        if(!exam.Running){UpdateExamStatus();tabs.SelectedIndex=3;return;}
        await PresentExercise(exam.Current.Exercise,exam.Current.Settings);
        answered=exam.CurrentAnswered;submit.Enabled=false;replay.Enabled=!answered;
        if(answered)Tell("Η αποθηκευμένη ερώτηση έχει απαντηθεί. Ctrl+N για επόμενη.");
        UpdateExamStatus();
    }
    private async Task AdvanceExam()
    {
        if(exam is null)return;
        if(!exam.Running){UpdateExamStatus();Tell("Η εξέταση ολοκληρώθηκε. Δες αναφορά στην καρτέλα Εξετάσεις ή επέλεξε Επιστροφή σε εξάσκηση.");return;}
        try{exam.Advance();SaveExam();if(exam.Running)await ShowExamQuestion();else{SetExamLocks(false);UpdateHistory();UpdateExamStatus();RefreshExamHistory();Tell("Η εξέταση ολοκληρώθηκε και αποθηκεύτηκε. Η αναφορά βρίσκεται στην καρτέλα Εξετάσεις.");}}
        catch(Exception ex){Error(ex.Message);}
    }
    private void SkipExamQuestion()
    {
        if(exam is null)throw new InvalidOperationException("Δεν υπάρχει ενεργή εξέταση.");Stop();var attempt=exam.Skip();history.Add(attempt);if(storageHealthy)store.Save(history);answered=true;submit.Enabled=false;SaveExam();UpdateHistory();UpdateExamStatus();Tell("Η ερώτηση παραλείφθηκε με μηδενική βαθμολογία. Ctrl+N για συνέχεια.");
    }
    private void FinishExam()
    {
        Stop();if(exam is null){var saved=examStore.LoadCurrent()??throw new InvalidOperationException("Δεν υπάρχει εξέταση.");exam=ExamEngine.Restore(saved);}
        exam.Finish();SaveExam();SetExamLocks(false);UpdateHistory();UpdateExamStatus();RefreshExamHistory();tabs.SelectedIndex=3;examReport.Focus();
    }
    private void ExitExam()
    {
        if(exam?.State.Finished is null&&exam is not null)throw new InvalidOperationException("Τερμάτισε πρώτα την εξέταση ώστε να μη χαθεί η πρόοδος.");
        Stop();exam=null;SetExamLocks(false);session.Clear();playbackCache.Clear();answered=false;heard=false;tabs.SelectedIndex=0;setup.Focus();
    }
    private void SetExamLocks(bool active)
    {
        foreach(var c in new Control[]{category,level,direction,root,tonality,minimum,maximum,meter,bars,tempo,repeats,velocity,pedal,segmentStart,segmentEnd,soloVoice})c.Enabled=!active;
        previewAnswer.Enabled=!active;
    }
    private void SaveExam(){if(exam is null||auditOnly||uiTestMode)return;try{examStore.Save(exam.State);}catch(Exception ex){Error("Η εξέταση δεν αποθηκεύτηκε: "+ex.Message);}}
    private void UpdateExamStatus(){if(exam is not null)examReport.Text=exam.Report(exam.State.Finished is not null);}
    private void RefreshExamHistory()
    {
        if(auditOnly||uiTestMode)return;
        try{
            examHistory.Items.Clear();examHistory.Items.AddRange(examStore.History().Select(s=>(object)new SavedExam(s)).ToArray());
            wrongHistory.Items.Clear();wrongHistory.Items.AddRange(LearningAnalytics.ReviewableMistakes(history).Take(200).Select(a=>(object)new WrongItem(a)).ToArray());
            if(exam is null){var lines=LearningAnalytics.Weaknesses(history).Select(p=>$"{CategoryName(p.Category)}: {p.Percentage:F1}% σε {p.Attempts} πρόσφατες προσπάθειες.");examReport.Text="Ανάλυση αδυναμιών (έως 100 τελευταίες προσπάθειες ανά κατηγορία):\r\n"+string.Join("\r\n",lines);}
        }catch(Exception ex){Error("Δεν διαβάστηκε το ιστορικό εξετάσεων: "+ex.Message);}
    }
    private void ApplyTrainingSettings(Settings s)
    {
        loading=true;
        try{category.SelectedIndex=(int)s.Category;level.SelectedIndex=(int)s.Level;direction.SelectedIndex=(int)s.Direction;minimum.Value=s.MinNote;maximum.Value=s.MaxNote;root.SelectedIndex=s.RootPitchClass+1??0;tonality.SelectedIndex=s.KeyTonic.HasValue?s.KeyTonic.Value+1+(s.KeyMode==TonalityMode.NaturalMinor?12:0):0;tempo.Value=s.Tempo;repeats.Value=s.Repetitions;velocity.Value=s.Velocity;pedal.Checked=s.Pedal;meter.SelectedIndex=Array.FindIndex(TheoryV2.Meters,m=>m.Id==s.MeterId);bars.Value=s.Bars;}
        finally{loading=false;}
    }
    private async Task AdaptivePractice()
    {
        ExitExam();var recommendation=LearningAnalytics.FreshAdaptive(history,ReadSettings(),Random.Shared.Next());ApplyTrainingSettings(recommendation.Settings);await PresentExercise(recommendation.Exercise,recommendation.Settings);
    }
    private async Task ReviewMistake()
    {
        if(wrongHistory.SelectedItem is not WrongItem item)throw new InvalidOperationException("Επίλεξε λάθος από τη λίστα. Παλιά δεδομένα χωρίς snapshot δεν επαναλαμβάνονται ακριβώς.");
        var original=item.Attempt;ExitExam();var settings=(original.Configuration??new(Category:original.Category,Level:original.Level))with{Repetitions=1,SegmentStart=0,SegmentEnd=null,SoloVoice=-1};
        ApplyTrainingSettings(settings);await PresentExercise(original.Snapshot!with{Id=Guid.NewGuid().ToString("N")},settings);
    }
}
