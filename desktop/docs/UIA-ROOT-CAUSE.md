# Διάγνωση των πέντε καρτελών και διόρθωση

## Πραγματική αναπαραγωγή

Στο Windows runner ο κανονικός WinForms TabControl έχει 5 TabPages και 5 native επικεφαλίδες (TCM_GETITEMCOUNT). Ο ειδικός WinForms accessibility object προσθέτει και το επιλεγμένο page pane: GetChildCount=6. Η εξωτερική UIA FindAll επιστρέφει 4 TabItems, ενώ η αντίστροφη RawView πλοήγηση επιστρέφει και την πέμπτη. Το ίδιο αναπαράχθηκε σε ελάχιστο παράθυρο χωρίς κώδικα εφαρμογής, με και χωρίς AccessibilityContract.Apply, σε .NET 8.0.32 και .NET 10.0.13. Άρα δεν οφείλεται στην κατασκευή/απουσία της καρτέλας Οδηγίες, σε χρονισμό φόρτωσης ή στο πλάτος της οθόνης.

Τεκμήρια: Windows runs 40078942517 (native/managed counts), 40079433537 (κατευθύνσεις raw πλοήγησης), 40079695463 και 40079946902 (ελάχιστα παράθυρα και δύο runtimes). Τα reports διατηρούνται στα `codex/pets-ci-<run-id>/downloads/windows-checks/<run-id>`.

## Υλοποίηση

Το `AccessibleTabControl` διατηρεί το πραγματικό native WinForms control και τον υπάρχοντα χειρισμό του. Το MSAA child list ανατίθεται στον native control accessible object, ώστε οι αναγνωριστές παιδιών να συμφωνούν με τις πέντε πραγματικές επικεφαλίδες. Το UIA χρησιμοποιεί ενιαίο server-side fragment με πέντε πραγματικούς HeaderProviders, Selection/SelectionItem patterns, μοναδικά IDs και τις τρέχουσες τιμές του πραγματικού SelectedTab.

Το native page περιεχομένου συνδέεται μέσω `IRawElementProviderHwndOverride`, όπως προβλέπει το πρωτόκολλο Non-WPF Provider Repositioning. Δεν αντιγράφονται ή προσομοιώνονται τα κουμπιά/πεδία: διατηρούνται τα native providers τους, τα TextPattern και ExpandCollapse/Value patterns. Η πλοήγηση parent/next/previous είναι συμμετρική και δεν εισάγει δεύτερο legacy κατάλογο καρτελών. Τα focus/selection/structure events αντιστοιχούν στα πραγματικά controls. ProviderOwnsSetFocus δηλώνει ρητά τον χειρισμό εστίασης στις επικεφαλίδες.

Προδιαγραφή: https://github.com/dotnet/docs/blob/main/docs/framework/ui-automation/server-side-ui-automation-provider-implementation.md#non-wpf-provider-repositioning

## Δοκιμές και ειλικρινές όριο

Ο ανεξάρτητος WindowsDesktop client `tests/Studio.UIAutomation` εκτέλεσε 18 επιτυχείς ελέγχους: ακριβώς πέντε ονομασμένες καρτέλες, μοναδικά IDs, επιλογή όλων, native περιεχόμενο και κατάλληλα patterns ανά control. Πρωτότυπο: run 40083227118, `typed-uia-audit.json`.

Το `Test-Accessibility.ps1` ελέγχει επιπλέον πραγματικά Ctrl+Tab, Ctrl+Shift+Tab, F1, Tab/Shift+Tab, αλλαγή profile/tempo με βέλη και άνοιγμα piano setup με Enter. Η είσοδος περιμένει πραγματική foreground εστίαση. Η απλή UIA SetFocus σε standard ComboBox δεν μεταφέρει την πραγματική εστίαση στο δοκιμασμένο runtime: οι δοκιμές χρήσης πληκτρολογίου φτάνουν στα πεδία με Tab. Αυτό δεν αποτελεί πιστοποίηση κάθε UIA API ή συμπεριφοράς NVDA.

Οι παλιές αποτυχίες δεν κρύβονται. Η δημοσίευση νέου GitHub prerelease απαιτεί να περάσει ολόκληρο το Windows UIA/keyboard gate. Η πραγματική NVDA χρήση, Windows 11 χρήστη, κάρτα ήχου και ακρόαση δεν έχουν ελεγχθεί από αυτά τα scripts.
