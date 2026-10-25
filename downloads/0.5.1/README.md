> Η [0.5.2](../0.5.2/README.md) διατηρεί τη διόρθωση ήχου και προσθέτει προαιρετική αυτόματη συνέχεια χωρίς Ctrl+R ανά ερώτηση. Η παρούσα αναφορά 0.5.1 διατηρείται ιστορικά.

# Διορθωτική 0.5.1 — αποκατάσταση αναπαραγωγής ήχου

Η αναφορά χρήστη «Σφάλμα συσκευής ήχου» διερευνήθηκε στον πραγματικό μετατροπέα εξόδου. Αναπαράχθηκε ArrayTypeMismatchException: το NAudio δίνει float view ενός byte buffer και ο παλιός LoopingPcmCursor χρησιμοποιούσε Array.Copy προς buffer διαφορετικού runtime τύπου. Η παραγωγή πιάνου περνούσε, αλλά η γέφυρα προς την έξοδο δεν είχε δοκιμαστεί. Η 0.5.1 χρησιμοποιεί σωστή αντιγραφή PCM bytes, χωρίς αλλαγή μουσικών δειγμάτων ή τονικού ύψους.

Δεν έχουμε το πλήρες αρχικό μήνυμα ή τον οδηγό της προσωπικής κάρτας. Η διόρθωση αντιμετωπίζει την αναπαραγώγιμη αστοχία του κώδικα· η λειτουργία στον υπολογιστή του χρήστη απαιτεί νέα δοκιμή.

## Νέος εγκαταστάτης

[Κατέβασε τον εγκαταστάτη 0.5.1 για Windows](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.1-preview.59/ProfessionalEarTrainingStudio-0.5.1-Setup.exe)

1. Κλείσε την παλιά εφαρμογή με Alt+F4.
2. Άνοιξε τον νέο εγκαταστάτη. Tab/Shift+Tab και Enter χειρίζονται την εγκατάσταση.
3. Άνοιξε Professional Ear Training Studio. Ο τίτλος πρέπει να ανακοινώνει **VERSION 0.5.1**.
4. Πάτησε Ctrl+N και έπειτα Ctrl+R για ακρόαση.

Δεν χρειάζεται να ξανακατεβάσεις το Maestro. Οι διαδρομές και οι μορφές αποθήκευσης πιάνου, προόδου και ρυθμίσεων διατηρούνται.

Αν εμφανιστεί νέο πρόβλημα, πάτησε **F6**. Ολόκληρη η αναφορά θα βρίσκεται σε προσβάσιμο πεδίο, με τον δρομέα στην πρώτη γραμμή· μπορείς να τη διαβάσεις με τα βέλη/NVDA. Δεν απαιτείται αντιγραφή από αναδυόμενο παράθυρο. Αποθηκεύεται επίσης τοπικά στο `%LOCALAPPDATA%\ProfessionalEarTrainingStudio\last-audio-error.txt`, χωρίς αυτόματη αποστολή. Περιλαμβάνει συσκευή και τους αρχικούς κωδικούς σφάλματος.

## Άλλα παραδοτέα

- [Portable ZIP](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.1-preview.59/ProfessionalEarTrainingStudio-0.5.1-Portable.zip)
- [Αυτοτελές EXE](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.1-preview.59/ProfessionalEarTrainingStudio-0.5.1-Standalone.exe)
- [Πηγαίος κώδικας](https://github.com/kyriakoslouka-crypto/music-app/releases/download/v0.5.1-preview.59/ProfessionalEarTrainingStudio-0.5.1-Source.zip)
- [Πλήρες GitHub Release, οδηγίες, SHA-256 και αναφορές](https://github.com/kyriakoslouka-crypto/music-app/releases/tag/v0.5.1-preview.59)

Και τα **37 αρχεία** κατεβάστηκαν πραγματικά και επαληθεύτηκαν με SHA-256. Portable/Source CRC πέρασαν. Η πηγή της διόρθωσης συμφωνεί με το Git commit μετά από κανονικοποίηση μόνο των Windows checkout line endings. Επαναϋπολογίστηκε επιτυχώς το readiness από τις ίδιες κατεβασμένες αναφορές. [Αποδεικτικό](DELIVERY-VERIFICATION.json).

## Εκτελεσμένες δοκιμές

Κώδικας πακέτου: `94b2353bddccd9ac3972b325527210f8fcc16199`. [Επιτυχές πλήρες Windows workflow](https://github.com/kyriakoslouka-crypto/music-app/actions/runs/40171724741).

- Linux: **68 υπάρχοντες και 8 νέοι έλεγχοι**, χωρίς αποτυχία.
- Windows: **67 με πραγματικό Maestro και 8 νέοι έλεγχοι εξόδου**, χωρίς αποτυχία.
- Πραγματικός NAudio float-to-byte adapter: ανεξάρτητο αναμενόμενο hex PCM, μη μηδενικό offset, επανάληψη και αμετάβλητα γειτονικά bytes.
- Ελεγχόμενοι fake drivers: αποτυχία Init/ασύγχρονη ανάκαμψη, μία μόνο επαναπροσπάθεια στην ίδια WASAPI συσκευή, διατήρηση ASIO, ακύρωση με Stop, παλιές callbacks και αποτυχία cleanup. Δεν αποτελούν φυσική δοκιμή κάρτας.
- **20 typed UIA**, **20 keyboard** με νέο F6, **6 exam boundary** checks και native audit του πεδίου αναφοράς ήχου.
- Πραγματικό NVDA 2026.2: πέντε ελληνικές ανακοινώσεις καρτελών και επιτυχής χειρισμός πληκτρολογίου, με σιωπηλό συνθέτη.
- **794/794** σωστές PCM αντιστοιχίες Maestro και rendering 28 κατηγοριών.
- Πραγματική εγκατάσταση, hashes **508 αρχείων**, standalone, Portable και διατήρηση προσωπικού αρχείου κατά την απεγκατάσταση.
- Καθαρή ανακατασκευή από το παραδοτέο Source ZIP και έξι tests του αυστηρού readiness gate.

Το [READINESS.json](READINESS.json) έχει και τις **12 αυτοματοποιημένες κατηγορίες true**, ReadyForWindows11Trial=true. ReadyForProfessionalFinal=false: προσωπική κάρτα/Windows 11, ανθρώπινη ακρόαση NVDA/πιάνου και τελική αποδοχή scope παραμένουν ανοικτά. Δεν παρουσιάζεται η έκδοση ως τελική 1.0.0.

## Συνέχιση και ιστορικό

Πλήρης τοπική παράδοση στο `desktop/deliverables/FINAL`, προηγούμενη στο `FINAL-before-0.5.1-preview59`. Όλα τα ιστορικά πακέτα διατηρούνται. Το [AUDIO-OUTPUT-RECOVERY.md](../../desktop/docs/AUDIO-OUTPUT-RECOVERY.md) εξηγεί τη βασική αιτία, τις δοκιμές και την πρόσθετη ανθεκτικότητα WASAPI.

Επόμενο βήμα είναι η επιβεβαίωση με Ctrl+N/Ctrl+R στον προσωπικό υπολογιστή. Αν αποτύχει, χρησιμοποίησε την πραγματική αναφορά F6/last-audio-error.txt για στοχευμένη διόρθωση του συγκεκριμένου οδηγού, χωρίς νέα λήψη δειγμάτων ή αλλαγή προσβασιμότητας. Μην αφαιρέσεις τον πραγματικό NAudio adapter από τις δοκιμές και μην μετατρέψεις fake-driver PASS σε δήλωση φυσικής κάρτας.
