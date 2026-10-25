# Τρέχουσα επαληθευμένη παράδοση — 0.5.0 preview.48

Νέος κώδικας εφαρμογής: `1361b06ad2e8cfa17a92e782bac3a9521cdaa78b`, κλάδος `codex/pets-delivery`. Το προηγούμενο preview.43 και όλα τα ιστορικά παραδοτέα διατηρούνται. Η εφαρμογή παραμένει 0.5.0 preview· δεν δηλώνεται ολοκληρωμένη τελική 1.0.0.

## Άμεση λήψη

- [Εγκαταστάτης Windows](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.48/ProfessionalEarTrainingStudio-0.5.0-Setup.exe)
- [Portable ZIP](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.48/ProfessionalEarTrainingStudio-0.5.0-Portable.zip)
- [Αυτοτελές EXE](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.48/ProfessionalEarTrainingStudio-0.5.0-Standalone.exe)
- [Πλήρης πηγαίος κώδικας](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.48/ProfessionalEarTrainingStudio-0.5.0-Source.zip)
- [Οδηγίες, αναφορές και SHA-256](https://github.com/kyriakoslouka-crypto/music-app/releases/tag/v0.5.0-preview.48)

Και τα **31 αρχεία** κατεβάστηκαν πραγματικά από το δημόσιο GitHub Release και επαληθεύτηκαν με SHA-256. Τα Portable/Source ZIP πέρασαν CRC και τα dependency lockfiles του Source συμφωνούν με το συγκεκριμένο commit. [Αποδεικτικό λήψης](DELIVERY-VERIFICATION.json). Δεν αποτελεί επαλήθευση λήψης στον προσωπικό υπολογιστή του χρήστη. Τοπικά: `desktop/deliverables/FINAL`, προηγούμενο FINAL στο `FINAL-before-preview48` και τα παλαιότερα snapshots διατηρημένα.

## Πραγματικές διορθώσεις αυτού του κύκλου

1. Η λήξη χρονικού ορίου, ακόμη και πριν εκτελεστεί ο UI timer, δεν μετατρέπει την εξεταστική ερώτηση σε practice submission. Ληγμένη/τερματισμένη εξέταση απορρίπτει αναπαραγωγή, υποβολή και προεπισκόπηση, μέχρι ρητή επιστροφή σε εξάσκηση. Έξι Windows regression checks καλούν τις πραγματικές εντολές του παραθύρου και ελέγχουν αμετάβλητο ιστορικό.
2. Επιτυχής ρυθμική απάντηση με 80–99% διατηρεί το πραγματικό ποσοστό στον μέσο όρο και την προσαρμοσμένη ανάλυση. Τα στατιστικά διακρίνουν επιτυχή απάντηση από μέση βαθμολογία. Παλιές σωστές εγγραφές χωρίς Percentage παραμένουν συμβατές.
3. Null/ελλιπή στοιχεία εξέτασης και μουσικής ακολουθίας, απουσία προηγούμενων απαντήσεων και αλλοιωμένα μεταδεδομένα απορρίπτονται ως σφάλματα δεδομένων. Δεν αντικαθίσταται το αρχικό αρχείο.
4. Υπερβολικά μεγάλοι αριθμοί οκτάβας απορρίπτονται πριν από τον υπολογισμό MIDI· δεν γίνονται άλλες νότες μέσω integer overflow. Τα όρια Λα0/Ντο8 και οι εναρμόνιες B#7/Cb1 παραμένουν σωστά.
5. Η προαιρετική επιλογή ακύρωσης ομιλίας NVDA αποθηκεύεται και επανέρχεται μετά το κλείσιμο. Τα παλιά settings παραμένουν συμβατά. Δεν επιβεβαιώνει πραγματική ομιλία NVDA ούτε εγκαθιστά το προαιρετικό DLL.

Τεχνική εξήγηση: [HARDENING-0.5.0.md](../../desktop/docs/HARDENING-0.5.0.md). Διατηρούνται οι διορθώσεις πέντε καρτελών UIA, native SendInput chords, stale audio cache και αναπαραγώγιμου Source ZIP.

## Εκτελεσμένες δοκιμές

Το [Windows workflow 40167389694](https://github.com/kyriakoslouka-crypto/music-app/actions/runs/40167389694) ολοκληρώθηκε επιτυχώς για τον παραπάνω κώδικα:

- **60 core checks**, **67 με πραγματικό Maestro**, μηδέν αποτυχίες.
- **20 typed UIA**, **19 keyboard**, **6 exam-boundary** checks. Όλες οι πέντε καρτέλες και τα περιεχόμενά τους εκτίθενται.
- Πραγματική εγκατάσταση, σύγκριση **505 αρχείων**, installed/standalone native audits, απεγκατάσταση με διατήρηση προσωπικού αρχείου, Portable CRC.
- Native Windows Media Foundation: **794/794** ακριβείς PCM SHA-256 αντιστοιχίες με ανεξάρτητη αναφορά FFmpeg· renders **28 κατηγοριών** στη .NET 10.
- Frozen restore και native solution/UIA builds από το ίδιο το παραδοτέο Source ZIP.

Linux: **68/68** με ιδιωτικό Maestro και SF2. Το Linux report καταγράφει το `c722e04`· τα Core/Audio/tests δεν άλλαξαν ως το `1361b06` (μόνο η ονομασία στατιστικών στο GUI). Η ακριβής συσχέτιση καταγράφεται στο DELIVERY-VERIFICATION.json. Τα JSON αυτού του φακέλου είναι πραγματικά αποτελέσματα, όχι συμπληρωμένες υποθέσεις.

## Εγκατάσταση με πληκτρολόγιο και NVDA

Κατέβασε Setup, επίλεξέ το στην Εξερεύνηση και Enter. Tab/Shift+Tab και Enter/Space χειρίζονται τον εγκαταστάτη, χωρίς απαίτηση administrator. Είναι unsigned· ισχύει η πολιτική ασφαλείας των Windows. Από Έναρξη γράψε Professional Ear Training Studio και Enter. Στον πρώτο οδηγό διάβασε την άδεια, Tab μέχρι «Εγκατάσταση Maestro Concert Grand» και Enter. Η εφαρμογή αναζητά τη συγκεκριμένη πηγή, όχι ο χρήστης. Απαιτούνται περίπου 263 MB λήψης/3 GB ελεύθερα για την πρώτη ιδιωτική εγκατάσταση και μετά λειτουργεί offline. Τα δείγματα δεν αναδιανέμονται.

Ctrl+N: νέα άσκηση. Ctrl+R: ακρόαση. Ctrl+P: παύση/συνέχεια. Ctrl+Enter: υποβολή. Esc: διακοπή. Ctrl+Tab/Ctrl+Shift+Tab: καρτέλες. F1: οδηγίες. [Πλήρης οδηγός](../../desktop/docs/FIRST-RUN-NVDA.md).

## Ακριβές σημείο συνέχισης και κριτήρια 1.0

Επόμενο βήμα είναι πραγματική αποδοχή του preview.48 στον υπολογιστή Windows 11/NVDA και διόρθωση των ευρημάτων. Τα Windows CI είναι Server 2025. Δεν έχουν επαληθευτεί προσωπική λήψη, ομιλία NVDA, φυσικό ASIO/WASAPI ή επαγγελματική ακρόαση. Το readiness gate διατηρεί αυτά τα πεδία false.

Εκκρεμούν επίσης πλήρης τεκμηριωμένη αντιστοίχιση ABRSM syllabus και επίλυση/αποδοχή του scope φωνητικής/οργανικής αξιολόγησης και προχωρημένης πολυφωνίας. Το υπάρχον σολφέζ είναι γραπτό/self-guided· δεν αξιολογεί τραγούδι. Οι προσομοιώσεις είναι ανεπίσημες. Δεν χαρακτηρίζεται η εφαρμογή τελική 1.0.0 χωρίς την αντίστοιχη απόδειξη.

Συνέχισε από τον ίδιο checkout/κλάδο, διαβάζοντας αυτό το αρχείο και [πρωτόκολλο προτεραιοτήτων/παραδοχών/αποδοχής](../../desktop/docs/ACCEPTANCE-PROTOCOL.md). Linux: `desktop/scripts/cloud-check.sh` και τα προαιρετικά private integration inputs του [CONTINUE-TO-1.0.md](../../desktop/docs/CONTINUE-TO-1.0.md). Windows: `desktop/scripts/build-windows.ps1 -Installer -Accessibility`. Διατήρησε όλες τις δοκιμές και τα ιστορικά πακέτα· κανένα manual success flag χωρίς πραγματική δοκιμή.
