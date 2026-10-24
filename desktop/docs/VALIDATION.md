# Πραγματικά αποτελέσματα δοκιμών — ανάπτυξη 0.5.0

## Εκτελέστηκαν

- Linux: **63/63**, με πραγματικό Maestro και ιδιωτικό SF2. Διατηρήθηκαν οι προηγούμενοι 61 έλεγχοι και προστέθηκαν δύο έλεγχοι PCM/FLAC integrity. Περιλαμβάνονται χιλιάδες παραγόμενα/βαθμολογημένα παραδείγματα, μουσικοθεωρητικές αναφορές, αποθήκευση/restore, κοινός renderer, ρυθμός, εξετάσεις και cache regression.
- Windows: **55 core** και **62/62 με πραγματικό Maestro**. Το αποτέλεσμα διαχωρίζεται από ακρόαση/κάρτα ήχου.
- Ανεξάρτητος WindowsDesktop UIA client: **20/20**, πέντε μοναδικές καρτέλες, επιλογή κάθε καρτέλας, native περιεχόμενο, TextPattern, ComboBox ExpandCollapse/Value, numeric edit και ελληνικά όρια.
- Εξωτερικό Windows UIA/keyboard script: **19 καταγεγραμμένοι έλεγχοι**, κύκλος/επιστροφή Ctrl+Tab, Ctrl+Shift+Tab, F1, Tab/Shift+Tab σε κάθε καρτέλα, πραγματική αλλαγή/επαναφορά profile και ταχύτητας με βέλη. Ο piano installer άνοιξε με Enter και επιθεωρήθηκαν τα controls του χωρίς έναρξη λήψης σε αυτό το UI test.
- Πραγματικός Windows Media Foundation decoder: **794/794 PCM SHA-256** συμφώνησαν με ανεξάρτητη αναφορά, και έγιναν **28 πραγματικά Maestro renders στη .NET 10**. Η πηγή δεν διαθέτει εσωτερικά MD5· δεν δηλώνεται πλαστό MD5 PASS. Βλέπε PIANO-FIDELITY.md.
- Windows Setup: πραγματική silent εγκατάσταση, SHA-256 σύγκριση όλων των **503 εγκατεστημένων αρχείων**, native managed audit installed και standalone EXE, αφαίρεση εφαρμογής και διατήρηση user-added αρχείου.
- Portable: πραγματικό ZIP CRC, root EXE, ασφαλείς διαδρομές και απουσία ιδιωτικών piano samples.
- Εξωτερική λήψη από GitHub Releases: Setup και Portable του preview.37 λήφθηκαν πραγματικά μέσω HTTPS και συμφώνησαν με τα δημοσιευμένα SHA-256. Η λήψη στον προσωπικό υπολογιστή χρήστη παραμένει διαφορετικός, μη εκτελεσμένος έλεγχος.

Πλήρες επιτυχημένο Windows run με πραγματικό πιάνο: https://github.com/kyriakoslouka-crypto/music-app/actions/runs/40088076400 . Επόμενο επιτυχημένο run με ρητά global Ctrl+Tab shortcuts: https://github.com/kyriakoslouka-crypto/music-app/actions/runs/40088716443 . Τα πρωτότυπα JSON διατηρούνται στα codex/pets-ci-<run-id> branches και στα αντίστοιχα GitHub prereleases. Κάθε νέα παράδοση πρέπει να ελέγχεται από το δικό της report/commit, όχι από παλιά screenshots ή απλή ύπαρξη αρχείων.

## Δεν εκτελέστηκαν

Πραγματική NVDA χρήση/εκφωνήσεις, προσωπικός Windows 11 υπολογιστής, φυσική κάρτα WASAPI/ASIO, latency/hotplug, επαγγελματική ακρόαση, υπογεγραμμένος installer και πλήρης παιδαγωγική αντιστοίχιση ABRSM syllabus. Δεν υπάρχει ισχυρισμός ολοκληρωμένης επαγγελματικής 1.0.0.
