# Maestro Concert Grand — πηγή, άδεια και προσαρμογή

Επιλεγμένο όργανο: **Maestro Concert Grand**, Yamaha CF-3 των αρχών του 1990, δημιουργός Mats Helgesson (2003). Πραγματικά stereo δείγματα με Neumann KM84 X/Y, πέντε δυναμικές, χρωματική δειγματοληψία 88 νοτών. Η δημόσια μετατροπή SFZ/FLAC έγινε από kinwie, με άδεια του δημιουργού κατά το README του εκδότη (16 Μαΐου 2020).

Πρωτότυπη πηγή: https://github.com/sfzinstruments/MatsHelgesson.MaestroConcertGrandPiano/tree/717078b450eccc4a7733755f4b5973426ebc23e9

- Αμετάβλητο commit: `717078b450eccc4a7733755f4b5973426ebc23e9`.
- ZIP από `https://codeload.github.com/sfzinstruments/MatsHelgesson.MaestroConcertGrandPiano/zip/717078b450eccc4a7733755f4b5973426ebc23e9`.
- SHA-256: `2507c213538b850889bea4fbd90ca2a54abcc32d6a42febae53b81df1f677264`.
- Ο έλεγχος hash γίνεται πριν την αποσυμπίεση. Δεν υπάρχουν εναλλακτικές μη ελεγμένες διευθύνσεις ή παράκαμψη TLS/checksum.

## Όροι

Το πρωτότυπο `mcgreadme.txt` γράφει μεταξύ άλλων:

> All samples are Copyright [copyright sign] Mats Helgesson, 2003. All rights reserved.
> You may not sell this sound set or any of the samples provided with it.
> You may not modify and spread this soundfont without the author's written permission.
> If you want to publish this soundfont, please contact the author.

Δεν θεωρούμε αυτή την άδεια CC, public domain ή ανεπιφύλακτη άδεια αναδιανομής. **Δεν περιλαμβάνουμε τα δείγματα ή το ZIP τους στα artifacts της εφαρμογής.** Ο προσβάσιμος οδηγός πραγματοποιεί λήψη από τη δημοσιευμένη πηγή και ιδιωτική προετοιμασία στον υπολογιστή του χρήστη. Τα αρχικά README/νομικοί όροι διατηρούνται μαζί με το τοπικό πιάνο. Ο χρήστης δεν πρέπει να αναδιανέμει το παραγόμενο cache· η εφαρμογή δεν παρέχει τέτοια λειτουργία. Δεν έγινε επικοινωνία με τον δημιουργό ούτε ισχυριζόμαστε πρόσθετη άδεια αναδημοσίευσης.

## Τοπική προετοιμασία και renderer

Ο οδηγός επαληθεύει το ZIP, αποσυμπιέζει σε staging και αποκωδικοποιεί τα πρωτότυπα **stereo 44.1 kHz/16-bit FLAC σε PCM16 WAV χωρίς απωλεστική συμπίεση**. Στα Windows χρησιμοποιεί το ενσωματωμένο Media Foundation. Στο Linux ο ίδιος preparer δοκιμάζεται με ffmpeg decoder, όχι με τον Windows codec.

Χρησιμοποιούνται 440 attack δείγματα (5 × 88), 352 release δείγματα (4 × 88) και δύο δείγματα sustain-pedal noise. Για το `mcg_ff_083` χρησιμοποιείται το διορθωμένο αρχείο `SampleFix`, όπως περιγράφεται στην πηγή. Οι χρόνοι απόσβεσης διαβάζονται από τα πρωτότυπα `Data/rel_*.txt`. Κάθε attack/release WAV αποκτά τοπικό SHA-256, ελεγχόμενο πριν από τη χρήση του.

Η μηχανή είναι **ειδικός adapter της συγκεκριμένης έκδοσης Maestro**, όχι πλήρης SFZ/ARIA engine. Διατηρεί τις διαφορετικές πραγματικές ηχογραφήσεις δυναμικής, το stereo και το τονικό ύψος κάθε πλήκτρου. Περιλαμβάνει natural sample decay, damper envelope, release noises, πλήρες CC64 sustain και pedal noises. Δεν υλοποιεί half-pedal, sostenuto, sympathetic resonance ή όλα τα ARIA opcodes. Τα δείγματα μηχανισμών είναι σε συγκρατημένη σταθερή στάθμη. Η προσαρμογή δεν ισχυρίζεται ταυτόσημο ήχο με sforzando.

Η τεχνική επαλήθευση δειγμάτων/PCM δεν είναι ακουστική αξιολόγηση. Η φυσικότητα, η ισορροπία δυναμικών και η καταλληλότητα για επαγγελματική χρήση χρειάζονται ακρόαση από τον μουσικό σε πραγματικό εξοπλισμό.
