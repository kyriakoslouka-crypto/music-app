using Studio.Core;
namespace Studio.Windows;
public sealed partial class MainForm
{
    private readonly Button scoreView=new(){Text="Προβολή παρτιτούρας",AccessibleName="Προβολή παρτιτούρας μετά την απάντηση",AutoSize=true,Enabled=false};
    private readonly Button coachPractice=new(){Text="Εξάσκηση προτεινόμενης αδυναμίας",AutoSize=true};
    private readonly CheckedListBox surpriseCategories=new(){AccessibleName="Κατηγορίες απρόοπτου διαγωνίσματος",CheckOnClick=true,Dock=DockStyle.Fill};
    private readonly ComboBox surpriseLevel=Combo("Επίπεδο απρόοπτου διαγωνίσματος",["Αρχάριος","Στοιχειώδες","Μεσαίο","Προχωρημένο","Ειδικός","Professional / Pro"]);
    private readonly NumericUpDown surpriseCount=Number("Πλήθος ερωτήσεων απρόοπτου διαγωνίσματος",1,100,20);
    private readonly NumericUpDown surpriseMinutes=Number("Χρονικό όριο απρόοπτου σε λεπτά, 0 χωρίς όριο",0,180,0);
    private readonly NumericUpDown surprisePlays=Number("Ακροάσεις ανά ερώτηση απρόοπτου",1,10,3);
    private readonly TextBox surpriseReport=new(){AccessibleName="Κατάσταση και αναφορά απρόοπτου διαγωνίσματος",ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill};
    private readonly ListBox surpriseHistory=new(){AccessibleName="Ιστορικό απρόοπτων διαγωνισμάτων",Dock=DockStyle.Fill};
    private sealed record CategoryItem(Category Category){public override string ToString()=>CategoryName(Category);}
    private int ExamTab=>3;
    private void UpdateScoreAccess()=>scoreView.Enabled=session.Current is not null&&ScoreNotation.CanReveal(answered,exam?.State);
    private void ViewScore()
    {
        try{
            ScoreNotation.RequireReveal(answered,exam?.State);
            var exercises=exam is not null?exam.State.Questions.Select(q=>q.Exercise).ToArray():session.Current is { } e?[e]:throw new InvalidOperationException("Δεν υπάρχει άσκηση.");
            CancelAutomaticPractice();Stop();using var viewer=new ScoreViewer(exercises);viewer.ShowDialog(this);
        }catch(Exception e){Error(e.Message);}
    }
    private void ViewCompletedScore(ExamState state)
    {
        try{
            if(exam is not null&&exam.State.Finished is null)throw new InvalidOperationException("Ολοκλήρωσε πρώτα την τρέχουσα εξέταση.");
            ScoreNotation.RequireReveal(false,state);CancelAutomaticPractice();Stop();using var viewer=new ScoreViewer(state.Questions.Select(q=>q.Exercise).ToArray());viewer.ShowDialog(this);
        }catch(Exception e){Error(e.Message);}
    }
    private async Task CoachPractice()
    {
        if(exam is not null&&exam.State.Finished is null)throw new InvalidOperationException("Ολοκλήρωσε πρώτα την εξέταση.");
        if(piano is null)throw new InvalidOperationException("Εγκατάστησε πρώτα το πιάνο.");
        var target=StudyCoach.Target(history,ReadSettings());ExitExam();ApplyTrainingSettings(target);await PresentExercise(generator.Next(target),target);
    }
    private void BuildSurprise()
    {
        var page=new AccessibleTabPage("Απρόοπτα διαγωνίσματα");var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new(12)};
        table.RowStyles.Add(new(SizeType.AutoSize));table.RowStyles.Add(new(SizeType.AutoSize));table.RowStyles.Add(new(SizeType.Percent,40));table.RowStyles.Add(new(SizeType.AutoSize));table.RowStyles.Add(new(SizeType.Percent,40));table.RowStyles.Add(new(SizeType.Percent,20));
        table.Controls.Add(new Label{Text="Τυχαία θέματα από τις επιλεγμένες κατηγορίες. Λύσεις και παρτιτούρες μετά το τέλος. Space επιλέγει/αποεπιλέγει κατηγορία. 0 λεπτά: χωρίς χρονικό όριο.",AutoSize=true});
        var options=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};options.Controls.AddRange([new Label{Text="Επίπεδο",AutoSize=true},surpriseLevel,new Label{Text="Ερωτήσεις",AutoSize=true},surpriseCount,new Label{Text="Λεπτά",AutoSize=true},surpriseMinutes,new Label{Text="Ακροάσεις",AutoSize=true},surprisePlays]);table.Controls.Add(options);
        foreach(var c in Enum.GetValues<Category>())surpriseCategories.Items.Add(new CategoryItem(c),c is Category.Intervals or Category.Chords or Category.Cadences or Category.Scales or Category.Modes or Category.RhythmPatterns or Category.Meters);
        table.Controls.Add(surpriseCategories);var commands=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true};
        void Command(string text,Action action){var b=new Button{Text=text,AutoSize=true};b.Click+=(_,_)=>{try{action();}catch(Exception e){Error(e.Message);}};commands.Controls.Add(b);}
        void Async(string text,Func<Task> action){var b=new Button{Text=text,AutoSize=true};b.Click+=async(_,_)=>{try{await action();}catch(Exception e){Error(e.Message);}};commands.Controls.Add(b);}
        Async("Έναρξη απρόοπτου διαγωνίσματος",StartSurprise);Async("Συνέχιση αποθηκευμένης εξέτασης",ResumeExam);Command("Παράλειψη ερώτησης",SkipExamQuestion);Command("Τερματισμός απρόοπτου και αναφορά",FinishExam);Command("Επιστροφή στην εξάσκηση",ExitExam);
        Command("Παρτιτούρες επιλεγμένου ολοκληρωμένου απρόοπτου",()=>{if(surpriseHistory.SelectedItem is not SavedExam saved)throw new InvalidOperationException("Επίλεξε ολοκληρωμένο διαγώνισμα.");ViewCompletedScore(saved.State);});
        table.Controls.Add(commands);table.Controls.Add(surpriseReport);table.Controls.Add(surpriseHistory);page.Controls.Add(table);tabs.TabPages.Add(page);
        surpriseHistory.SelectedIndexChanged+=(_,_)=>{if(surpriseHistory.SelectedItem is SavedExam saved){if(exam is not null&&exam.State.Finished is null)return;surpriseReport.Text=ExamEngine.Restore(saved.State).Report(true);}};
        surpriseReport.Text="Επίλεξε κατηγορίες με Space, επίπεδο, πλήθος ερωτήσεων και προαιρετικό χρόνο. Το διαγώνισμα αποθηκεύεται αυτόματα. Δεν αποτελεί επίσημη εξέταση.";
    }
    private async Task StartSurprise()
    {
        if(exam is not null&&exam.State.Finished is null)throw new InvalidOperationException("Συνέχισε ή τερμάτισε πρώτα την ενεργή εξέταση.");
        var saved=examStore.LoadCurrent();if(saved is not null&&saved.Finished is null)throw new InvalidOperationException("Υπάρχει αποθηκευμένη ανολοκλήρωτη εξέταση. Επίλεξε Συνέχιση.");
        if(piano is null)throw new InvalidOperationException("Εγκατάστησε πρώτα το πιάνο από τις Ρυθμίσεις.");
        var selected=surpriseCategories.CheckedItems.Cast<CategoryItem>().Select(i=>i.Category).ToArray();
        var created=SurpriseExams.Create(selected,(Level)surpriseLevel.SelectedIndex,(int)surpriseCount.Value,(int)surprisePlays.Value,(int)surpriseMinutes.Value,Random.Shared.Next());
        Stop();exam=created;SaveExam();SetExamLocks(true);await ShowExamQuestion();
    }
    internal object AuditNewStudy()
    {
        var e=new ExerciseGenerator(580).Next(new(Category.MelodyDictation));
        if(ScoreNotation.CanReveal(false,null))throw new InvalidOperationException("Unanswered notation leak");
        var state=ExamEngine.Create(ExamCatalog.Profiles[0] with{QuestionCount=1},580).State;
        if(ScoreNotation.CanReveal(true,state))throw new InvalidOperationException("Active exam notation leak");
        var errors=new List<string>();regressionError=errors.Add;
        try{
            session.Begin(e);answered=false;exam=null;UpdateScoreAccess();if(scoreView.Enabled)throw new InvalidOperationException("Unanswered score button enabled");ViewScore();
            exam=ExamEngine.Restore(state);answered=true;UpdateScoreAccess();if(scoreView.Enabled)throw new InvalidOperationException("Active exam score button enabled");ViewScore();
            if(errors.Count!=2)throw new InvalidOperationException("Native notation privacy gate bypass");
        }finally{regressionError=null;exam=null;session.Clear();answered=false;}
        using var viewer=new ScoreViewer([e]);var controls=AccessibilityContract.Audit(viewer);
        using var canvas=new NotationCanvas{Document=ScoreNotation.Build(e)};
        using var bitmap=new Bitmap(canvas.Width,canvas.Height);canvas.DrawToBitmap(bitmap,canvas.ClientRectangle);
        using var stream=new MemoryStream();bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
        var dense=new ExerciseGenerator(589).Next(new(Category.RhythmPatterns,Level.Professional));
        using var denseCanvas=new NotationCanvas{Document=ScoreNotation.Build(dense)};
        using var denseBitmap=new Bitmap(denseCanvas.Width,denseCanvas.Height);denseCanvas.DrawToBitmap(denseBitmap,denseCanvas.ClientRectangle);
        using var denseStream=new MemoryStream();denseBitmap.Save(denseStream,System.Drawing.Imaging.ImageFormat.Png);
        if(!NotationCanvas.MusicFontName.Contains("Bravura",StringComparison.Ordinal))throw new InvalidOperationException("Missing music font");
        return new{Passed=true,TabName="Παρτιτούρες",TabCount=tabs.TabCount,NativeScoreControls=controls,MusicFont=NotationCanvas.MusicFontName,PreviewPngBase64=Convert.ToBase64String(stream.ToArray()),DensePreviewPngBase64=Convert.ToBase64String(denseStream.ToArray()),PrivacyChecksPassed=true};
    }
}
