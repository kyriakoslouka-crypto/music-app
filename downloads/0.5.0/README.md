> Ιστορικό πακέτο: δεν περιέχει την τρέχουσα διόρθωση UIA. Χρησιμοποίησε το [νεότερο επαληθευμένο preview.48](../preview.48/README.md). Τα ιστορικά αρχεία και hashes διατηρούνται.

# Λήψη Professional Ear Training Studio 0.5.0

Πραγματική εφαρμογή Windows x64, έκδοση ανάπτυξης προς την 1.0.0. Τα παρακάτω αρχεία είναι διαθέσιμα εκτός του περιβάλλοντος Codex.

- [Εγκαταστάτης Windows — προτεινόμενη λήψη](https://raw.githubusercontent.com/kyriakoslouka-crypto/music-app/8abc1b361eae792ffd51714fca6092ecb4450f61/downloads/0.5.0/ProfessionalEarTrainingStudio-0.5.0-Setup.exe)
- [Portable ZIP](https://raw.githubusercontent.com/kyriakoslouka-crypto/music-app/9f2dac4/downloads/0.5.0/ProfessionalEarTrainingStudio-0.5.0-Portable.zip)
- [Αυτοτελές EXE](https://raw.githubusercontent.com/kyriakoslouka-crypto/music-app/9f2dac4/downloads/0.5.0/ProfessionalEarTrainingStudio-0.5.0-Standalone.exe)
- Πλήρης πηγαίος κώδικας: ProfessionalEarTrainingStudio-0.5.0-Source.zip στον ίδιο φάκελο.
- Ελληνικές οδηγίες με NVDA: φάκελος Οδηγίες, FIRST-RUN-NVDA.md.
- Δοκιμές: TEST-RESULTS.json, γνωστοί περιορισμοί: Οδηγίες/KNOWN-LIMITATIONS.md.

Με NVDA: ενεργοποίησε τον σύνδεσμο εγκαταστάτη με Enter. Αποθήκευσε και εκτέλεσε το ληφθέν αρχείο. Χρησιμοποίησε Tab, Shift+Tab, Space και Enter στον εγκαταστάτη και στον οδηγό Maestro που ανοίγει στην πρώτη εκκίνηση. Χρειάζεται αρχική σύνδεση για εγκατάσταση του πραγματικού Yamaha CF-3· μετά λειτουργεί offline. Μέσα στην εφαρμογή Ctrl+N προετοιμάζει άσκηση, Ctrl+R παίζει, Ctrl+Enter υποβάλλει απάντηση, F1 ανοίγει βοήθεια.

Το EXE δεν έχει ψηφιακή υπογραφή. Έλεγξε τις πληροφορίες εκδότη και τα SHA256SUMS.txt αν τα Windows εμφανίσουν προειδοποίηση. Τα piano samples δεν αναδιανέμονται λόγω περιοριστικής άδειας· τα εγκαθιστά ιδιωτικά ο οδηγός από επαληθευμένη πηγή, χωρίς αναζήτηση βιβλιοθήκης από εσένα.

61/61 αυτοματοποιημένοι έλεγχοι πέρασαν στο Linux, περιλαμβανομένου πραγματικού sample rendering. Επιπλέον 53/53 core έλεγχοι πέρασαν πραγματικά σε Windows CI· ακολούθησε αποτυχία συσκευασίας του standalone EXE, διορθώθηκε η μεταβίβαση παραμέτρων PowerShell και εκκρεμεί επανέλεγχος. Πραγματικό NVDA, Windows 11, Media Foundation, συσκευή WASAPI/ASIO και ακουστική ποιότητα δεν έχουν επιβεβαιωθεί. Η 1.0.0 δεν παραδίδεται ως ολοκληρωμένη. Το DELIVERY-VERIFICATION.json διαχωρίζει πραγματική HTTPS λήψη από δοκιμή στον προσωπικό υπολογιστή.
