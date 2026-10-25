> Διατηρημένο ιστορικό preview. Το νεότερο επαληθευμένο πακέτο και οι πρόσθετες διορθώσεις βρίσκονται στο [preview.48](../preview.48/README.md).

# Επαληθευμένη παράδοση — 0.5.0 preview.43

Η εφαρμογή παραμένει έκδοση ανάπτυξης 0.5.0, όχι ολοκληρωμένη 1.0.0. Πηγαίος κώδικας: commit `1ea917961d3fe0f87e8fdecb64ee0691a5eaa03f`, κλάδος `codex/pets-delivery`. Όλες οι παλιές λειτουργίες και τα ιστορικά πακέτα διατηρούνται.

## Πραγματικοί σύνδεσμοι λήψης

- [Εγκαταστάτης Windows](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.43/ProfessionalEarTrainingStudio-0.5.0-Setup.exe)
- [Portable ZIP](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.43/ProfessionalEarTrainingStudio-0.5.0-Portable.zip)
- [Αυτοτελές EXE](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.43/ProfessionalEarTrainingStudio-0.5.0-Standalone.exe)
- [Πλήρης πηγαίος κώδικας](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.43/ProfessionalEarTrainingStudio-0.5.0-Source.zip)
- [Όλες οι οδηγίες, SHA-256 και αναφορές](https://github.com/kyriakoslouka-crypto/music-app/releases/tag/v0.5.0-preview.43)

Κατεβάστηκαν πραγματικά μέσω HTTPS και επαληθεύτηκαν και τα 30 αρχεία του Release. Τα ZIP πέρασαν έλεγχο CRC. Τα τρία dependency lockfiles του Source ZIP συμφωνούν με το συγκεκριμένο commit. Το [DELIVERY-VERIFICATION.json](DELIVERY-VERIFICATION.json) καταγράφει URLs, μεγέθη και hashes. Η λήψη στον προσωπικό υπολογιστή του χρήστη παραμένει μη επαληθευμένη. Δεν απαιτείται GitHub Actions artifact ή πρόσβαση στο εσωτερικό cloud. Ενιαίος τοπικός φάκελος: `desktop/deliverables/FINAL`, με προηγούμενο FINAL διατηρημένο χωριστά.

## Διορθώσεις και πραγματικές δοκιμές

Το ελάχιστο WinForms fixture αναπαρήγαγε πέντε native καρτέλες αλλά μόνο τέσσερα εξωτερικά UIA TabItems. Η αιτία ήταν η ασυμφωνία του μικτού native/virtual accessibility fragment. Το AccessibleTabControl παρέχει συνεπή κατάλογο πέντε headers και HwndOverride για τα πραγματικά native περιεχόμενα. Δεν αφαιρέθηκε καρτέλα και δεν μειώθηκαν οι απαιτήσεις δοκιμής. Δες [διάγνωση](../../desktop/docs/UIA-ROOT-CAUSE.md).

Το τελευταίο pipeline διόρθωσε επίσης τη μη αξιόπιστη αποστολή modifier chords του WScript test: τα Ctrl+Tab/Ctrl+Shift+Tab αποστέλλονται σε ένα native SendInput batch με τις αντίστοιχες απελευθερώσεις. Οι έλεγχοι εξακολουθούν να απαιτούν την πραγματική επιλεγμένη καρτέλα/εστίαση, χωρίς επανάληψη του chord μέχρι να εμφανιστεί τυχαία PASS.

Η συσκευασία standalone χρησιμοποιεί χωριστά publish lockfiles. Πριν τη διόρθωση μετέβαλλε τα κανονικά lockfiles που έμπαιναν στο Source ZIP. Πλέον το ίδιο το παραδοτέο ZIP αποσυμπιέζεται σε καθαρό φάκελο και περνά frozen restore και native builds solution και UIA client ως υποχρεωτική προϋπόθεση δημοσίευσης.

Το [πλήρες Windows workflow](https://github.com/kyriakoslouka-crypto/music-app/actions/runs/40090329441) πέρασε:

- 55 core checks και 62 με πραγματικό Maestro, μηδέν αποτυχίες.
- 20 ανεξάρτητους typed UIA ελέγχους, πέντε καρτέλες με μοναδικά IDs, περιεχόμενο και κατάλληλα patterns.
- 19 πραγματικούς ελέγχους εισόδου πληκτρολογίου/εστίασης.
- Εγκατάσταση, σύγκριση και των 504 αρχείων, εκκίνηση εγκατεστημένου/standalone, απεγκατάσταση με διατήρηση αρχείου χρήστη και CRC Portable.
- 794 πραγματικές αποκωδικοποιήσεις Maestro με native Windows Media Foundation, 794 ακριβείς SHA-256 αντιστοιχίες PCM με ανεξάρτητη αναφορά FFmpeg και renders 28 κατηγοριών στη .NET 10.
- Καθαρή ανακατασκευή από το παραδοτέο Source ZIP.

Linux: 63 επιτυχείς έλεγχοι με ιδιωτικό Maestro και SF2. Τα πραγματικά δείγματα δεν περιλαμβάνονται στα παραδοτέα. Ο προσβάσιμος οδηγός τα εγκαθιστά ιδιωτικά από την προκαθορισμένη πηγή, με έλεγχο archive SHA-256 και PCM fingerprints.

## Πρώτη εγκατάσταση μόνο με πληκτρολόγιο

Κατέβασε το Setup, επίλεξέ το στην Εξερεύνηση και πάτησε Enter. Tab/Shift+Tab και Enter/Space χειρίζονται τον εγκαταστάτη χωρίς απαίτηση administrator. Είναι unsigned· η πολιτική ασφαλείας των Windows μπορεί να εμφανίσει προειδοποίηση. Άνοιξε από Έναρξη γράφοντας Professional Ear Training Studio. Στον πρώτο οδηγό Maestro διάβασε την άδεια, Tab έως «Εγκατάσταση Maestro Concert Grand» και Enter. Χρειάζονται σύνδεση για την πρώτη εγκατάσταση, περίπου 263 MB λήψης και 3 GB ελεύθερα. Μετά λειτουργεί offline.

Ctrl+N δημιουργεί άσκηση, Ctrl+R αναπαράγει, Ctrl+P παύει/συνεχίζει, Ctrl+Enter υποβάλλει, Esc σταματά, Ctrl+Tab/Ctrl+Shift+Tab αλλάζουν καρτέλα, F1 ανοίγει οδηγίες. Δες τις [πλήρεις οδηγίες NVDA](../../desktop/docs/FIRST-RUN-NVDA.md).

## Κριτήρια και ακριβές σημείο συνέχισης

Επόμενη εργασία είναι η αποδοχή της ίδιας έκδοσης στον προσωπικό Windows 11 υπολογιστή με NVDA και πραγματική κάρτα/ακρόαση. Οι Windows CI δοκιμές έγιναν σε Server 2025, όχι στον υπολογιστή του χρήστη. Δεν επιβεβαιώνουν ομιλία NVDA, φυσικό ASIO/WASAPI, υποκειμενική ποιότητα πιάνου ή προσωπική λήψη. Το readiness gate διατηρεί αυτά τα κριτήρια false.

Για 1.0 εκκρεμούν επίσης πλήρης τεκμηριωμένη αντιστοίχιση ABRSM syllabus, απαιτήσεις φωνητικής/οργανικής αξιολόγησης (το υπάρχον σολφέζ είναι γραπτό/self-guided), και παιδαγωγική αποδοχή προχωρημένης πολυφωνίας. Οι ανεπίσημες προσομοιώσεις δεν παρουσιάζονται ως επίσημες εξετάσεις. Προτεραιότητες, παραδοχές και κριτήρια PASS/FAIL/NOT RUN: [πρωτόκολλο αποδοχής](../../desktop/docs/ACCEPTANCE-PROTOCOL.md). Περαιτέρω ανάπτυξη από [CONTINUE-TO-1.0.md](../../desktop/docs/CONTINUE-TO-1.0.md), χωρίς νέο έργο ή διαγραφή ιστορικού.
