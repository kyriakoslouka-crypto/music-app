# Πακέτο για δοκιμαστική εγκατάσταση — 0.5.0 preview.56

**Όλοι οι υποχρεωτικοί αυτοματοποιημένοι έλεγχοι του πακέτου πέρασαν.** Το [READINESS.json](READINESS.json) καταγράφει ReadyForWindows11Trial=true, και τις 11 κατηγορίες AutomatedChecks=true και MissingAutomated κενό.

**Δεν έχει ολοκληρωθεί η πλήρης αποδοχή στον προσωπικό υπολογιστή Windows 11.** ReadyForProfessionalFinal=false: ανθρώπινη χρήση NVDA, πραγματική κάρτα ήχου, επαγγελματική ακρόαση, προσωπική λήψη και εκπαιδευτικό scope παραμένουν ανοικτά. Η ένδειξη δεν είναι πιστοποίηση πλήρους συμβατότητας ούτε τελική έκδοση 1.0.0. Ο έλεγχος του συγκεκριμένου υπολογιστή προϋποθέτει δοκιμαστική εγκατάσταση εκεί.

## Άμεσες λήψεις

- [Εγκαταστάτης Windows 11 x64](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.56/ProfessionalEarTrainingStudio-0.5.0-Setup.exe)
- [Portable ZIP](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.56/ProfessionalEarTrainingStudio-0.5.0-Portable.zip)
- [Αυτοτελές EXE](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.56/ProfessionalEarTrainingStudio-0.5.0-Standalone.exe)
- [Πλήρης πηγαίος κώδικας](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.0-preview.56/ProfessionalEarTrainingStudio-0.5.0-Source.zip)
- [Όλα τα αρχεία, οδηγίες και SHA-256](https://github.com/kyriakoslouka-crypto/music-app/releases/tag/v0.5.0-preview.56)

Κατέβασα και επαλήθευσα πραγματικά και τα **35 αρχεία** μέσω HTTPS/SHA-256. Portable/Source ZIP πέρασαν CRC. Τα lockfiles συμφωνούν με το source commit. Επαναϋπολογίστηκε ο έλεγχος ετοιμότητας από τις ίδιες κατεβασμένες αναφορές και συμφωνεί πλήρως: όλα τα αυτοματοποιημένα PASS, χειροκίνητη τελική αποδοχή ανολοκλήρωτη. Το `--require-final` ορθά απορρίπτει τον τελικό ισχυρισμό. [Αποδεικτικό](DELIVERY-VERIFICATION.json).

Ενιαίος τοπικός φάκελος `desktop/deliverables/FINAL`. Το προηγούμενο FINAL διατηρείται στο `FINAL-before-preview56` και τα παλαιότερα πακέτα δεν διαγράφηκαν. Πηγαίος κώδικας/αναφορές πακέτου: `f878c1a1a1076c2f4e84c5381a7c4a8c35e3f414`, κλάδος `codex/pets-delivery`.

## Πραγματικές δοκιμές

Η τελική επανεκτέλεση Linux στον ίδιο κώδικα πέρασε **68/68** με ιδιωτικό Maestro και SF2· [πραγματική αναφορά](linux-tests.json). Οι έξι δοκιμές του readiness gate πέρασαν επίσης στο Linux και στο Windows CI.

Το [πλήρες Windows workflow 40169001035](https://github.com/kyriakoslouka-crypto/music-app/actions/runs/40169001035) πέρασε:

- 60 core checks και **67 με πραγματικό Maestro**, χωρίς αποτυχίες.
- **20 typed UIA**, **19 keyboard** και **6 πραγματικές εντολές παραθύρου** σε ληγμένη/τερματισμένη εξέταση.
- Εγκατάσταση, σύγκριση SHA-256 **507 αρχείων**, installed/standalone audits, απεγκατάσταση που διατηρεί προσωπικό αρχείο, Portable CRC.
- Native Windows Media Foundation: **794/794** ακριβείς PCM SHA-256 αντιστοιχίες με ανεξάρτητη αναφορά FFmpeg, rendering και των **28 κατηγοριών**.
- Frozen restore και native solution/UIA rebuild από το ίδιο το παραδοτέο Source ZIP.
- **Πραγματικό NVDA 2026.2**, επίσημο installer με σωστό SHA-256 και έγκυρη Authenticode υπογραφή, ελληνική γλώσσα, χωρίς πρόσθετα, σιωπηλός synthesizer. Νέες πραγματικές «Speaking» καταχωρίσεις και για τις πέντε καρτέλες κατά τα Ctrl+Tab και επιτυχής πλοήγηση/αλλαγή τιμών με NVDA ενεργό. [Αναφορά NVDA](nvda-smoke.json).
- Έξι unit tests του αυστηρού readiness gate, με ρητά συνθετικά fixtures. Απουσία/αποτυχία/παλιό commit/διπλή ανακοίνωση αντί πέμπτης καρτέλας αποκλείουν την ετοιμότητα. Τα fixtures δεν είναι πραγματική ανθρώπινη αποδοχή.

Το fast NVDA workflow [40169001042](https://github.com/kyriakoslouka-crypto/music-app/actions/runs/40169001042) πέρασε επίσης ανεξάρτητα στο pinned Portable preview.48, με έλεγχο ότι δεν είχε αλλάξει ο κώδικας εφαρμογής, χωριστά ApplicationCommit/HarnessCommit και επιτυχείς πραγματικές ανακοινώσεις. Δεν αντικατέστησε το πλήρες workflow.

Οι αρχικές αποτυχίες διατηρούνται: εντοπίστηκε μη αξιόπιστη εισαγωγή βελών από WScript υπό NVDA. Ο έλεγχος πλέον στέλνει native scan codes με extended-key flags και περιμένει την πραγματική αλλαγή τιμής, χωρίς να ξαναστέλνει πλήκτρα μέχρι να προκύψει PASS. Διορθώθηκε και η δημοσίευση διαγνωστικών μέσω report-only Git tree, χωρίς ανάγκη workflows permission και χωρίς αλλαγή source HEAD/index.

Δεν διανέμονται δείγματα πιάνου ή NVDA executables στα πακέτα. Η ακρίβεια PCM δεν αποτελεί υποκειμενική ακουστική αποδοχή και οι ανακοινώσεις με σιωπηλό synthesizer δεν επιβεβαιώνουν ανθρώπινη ακρόαση ελληνικής φωνής.

## Εγκατάσταση μόνο με πληκτρολόγιο

1. Κατέβασε το Setup. Στην Εξερεύνηση επίλεξέ το και Enter. Tab/Shift+Tab, Enter/Space χειρίζονται τον εγκαταστάτη. Δεν απαιτείται administrator. Το πακέτο της εφαρμογής είναι unsigned και ισχύει η πολιτική ασφαλείας του υπολογιστή.
2. Με το δικό σου NVDA ενεργό, άνοιξε Έναρξη, γράψε Professional Ear Training Studio και Enter.
3. Στον πρώτο οδηγό διάβασε την άδεια Maestro. Tab μέχρι «Εγκατάσταση Maestro Concert Grand» και Enter. Η πηγή είναι προκαθορισμένη· δεν χρειάζεται αναζήτηση βιβλιοθήκης. Η πρώτη ιδιωτική εγκατάσταση απαιτεί περίπου 263 MB λήψης και 3 GB ελεύθερα. Μετά λειτουργεί offline.
4. Ctrl+N: νέα άσκηση και οδηγία. Ctrl+R: ακρόαση. Βέλη ή γραπτή απάντηση και Ctrl+Enter: υποβολή. Ctrl+P: παύση/συνέχεια. Esc: διακοπή. Ctrl+Tab/Ctrl+Shift+Tab: καρτέλες. F1: οδηγίες.

[Πλήρης πρώτη χρήση](../../desktop/docs/FIRST-RUN-NVDA.md) · [Ακριβές πρωτόκολλο NVDA/χειροκίνητης αποδοχής](../../desktop/docs/NVDA-TEST-PROTOCOL.md).

## Τι απομένει για πλήρη τελική αποδοχή

Στον προσωπικό Windows 11 υπολογιστή: ελληνική ομιλία/πλοήγηση με τη δική σου έκδοση και πρόσθετα NVDA, εγκατάσταση/ακύρωση Maestro, απουσία ανεπιθύμητης ομιλίας πάνω στη μουσική, πραγματική WASAPI/ASIO έξοδος, φυσικότητα/δυναμικές/sustain/πολυφωνία και επαναφορά προόδου μετά από κλείσιμο. Το CI είναι Windows Server 2025 και σιωπηλό NVDA· δεν υποκαθιστά αυτά τα βήματα.

Για τον ισχυρισμό 1.0.0 παραμένει επίσης η καταγεγραμμένη αντιστοίχιση/αποδοχή εκπαιδευτικού scope: ανεπίσημες προσομοιώσεις ABRSM, γραπτό/self-guided σολφέζ χωρίς αυτόματη φωνητική αξιολόγηση και παιδαγωγική αποδοχή ανώτερης πολυφωνίας. [Παραδοχές και κριτήρια](../../desktop/docs/ACCEPTANCE-PROTOCOL.md).

Συνέχιση από αυτό το commit/κλάδο και την ίδια εφαρμογή, χωρίς νέο έργο ή διαγραφή ιστορικού. Διατήρησε το mandatory NVDA gate, τα extended arrow events, τα χωριστά trial/final flags και τις τρέχουσες αναφορές. Δεν συμπληρώνονται χειροκίνητα success flags χωρίς την αντίστοιχη πραγματική αποδοχή του ίδιου Version/SourceCommit.
