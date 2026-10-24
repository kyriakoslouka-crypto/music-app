# Κριτήρια πραγματικής αποδοχής πριν από 1.0.0

Η υλοποίηση έφτασε στη 0.5.0. Οι 0.2–0.4 έχουν versioned snapshots και η 0.5 ενοποιεί τις λειτουργίες. Τα παλιά κριτήρια MVP διατηρούνται ως `ACCEPTANCE-0.1.0.md`.

## Έλεγχοι που εκτελέστηκαν εδώ

63 tests στο Linux: reference θεωρίας, 21.600 βασικές παραγωγές και χιλιάδες νέες, πτώσεις/κλίμακες/ρυθμός/ντικτέ/πολυφωνία/χρωματική/σειρές, όλες οι κατηγορίες σε πραγματικό Maestro PCM, SF2 integration, timing/segments/voice channels, ακεραιότητα download, αποθήκευση/restore, εξεταστική μηχανή, adaptive practice και stale-cache regression. Build με 0 warnings/errors. Αυτά είναι αναγκαία, όχι επαρκή για Windows/NVDA αποδοχή.

## Προγραμματιστικοί έλεγχοι Windows

Τα typed UIA και `tools/Test-Accessibility.ps1` εκτελέστηκαν σε Windows Server CI (20 structural και 19 keyboard checks). Επανεκτελούνται σε κάθε νέο build και απαιτείται επίσης ο πραγματικός Windows 11/NVDA έλεγχος χρήστη:

- Managed AccessibleObject audit όλων των controls και του piano setup, μη κενές ελληνικές προσβάσιμες ονομασίες, roles και μοναδικά TabIndex ανά container.
- Native παρουσίαση και των **28 κατηγοριών** χωρίς προεπιλεγμένη απάντηση, χωρίς αυτόματη μουσική πάνω από την οδηγία, με σωστό choice/text/tap πεδίο και invalidation παλιού audio cache.
- Εξωτερική UI Automation επιθεώρηση **και των πέντε καρτελών**, SelectionItem/Invoke στους tabs/πλήκτρα και στο piano setup. Η δοκιμή δεν ξεκινά λήψη δειγμάτων ή hardware audio.
- Καταγραφή αποτελέσματος ως πραγματικό PASS/FAIL, όχι μετατροπή cross-compile σε PASS. Τα δεδομένα GitHub CI (αν υπάρχουν) βρίσκονται στο `WINDOWS-CHECKS.json`.

## Χειροκίνητη δοκιμή με τον χρήστη/NVDA

1. Εγκατάσταση, standalone, portable, Start Menu, επανεκκίνηση, αναβάθμιση/απεγκατάσταση χωρίς διαγραφή προόδου/πιάνου.
2. Πρώτη εγκατάσταση Maestro με Tab/Enter/Esc, άδεια/πρόοδος, λήψη, πραγματικό Windows Media Foundation FLAC decoding, cancel/retry/disk-full/repair, offline δεύτερη εκκίνηση.
3. Όλες οι κατηγορίες και οι έξι επιλογές επιπέδου. Ctrl+N προετοιμάζει/ανακοινώνει, Ctrl+R παίζει όταν είναι έτοιμος ο χρήστης. Η εστίαση απάντησης έρχεται αφού τελειώσει η μουσική ή με Esc.
4. Όχι αυτόματη ομιλία πάνω στη μουσική, σωστές ελληνικές ανακοινώσεις/τιμές, Ctrl+I οδηγία, Ctrl+Shift+S κατάσταση, F1, focus/Tab χωρίς παγίδες, scaling/Contrast Themes και όλες οι νέες γραπτές απαντήσεις.
5. Ακρόαση από επαγγελματία: όλη η έκταση, πέντε πραγματικές δυναμικές, sample-layer transitions, φυσικό attack/decay/sustain/release/pedal noises, πολυφωνία, clicks/clipping, ισορροπία voices. Δεν υποκαθίσταται από finite-PCM έλεγχο.
6. WASAPI/ASIO στην πραγματική κάρτα, default output, x64 driver/stereo 44.1 kHz, αλλαγή/αποσύνδεση συσκευής, pause/resume/stop, input/output latency και μεγάλη διάρκεια χρήσης.
7. Exams: 10 προφίλ, πρώτη/επόμενη/skip, replay limits/refund failure, χωρίς ή με χρονικό όριο, διακοπή/συνέχιση, τελικό report και συνδυασμός με history/adaptive/review. Οι αναπάντητες μένουν μηδέν και δεν βαθμολογείται δύο φορές η ίδια ερώτηση.
8. Data recovery: corrupt JSON, denied write, backup, restore exam/history και προστασία από απώλεια λόγω δεύτερης εφαρμογής στην ίδια Windows session.
9. Παράδοση: λήψη από πραγματικό εξωτερικό κανάλι στον υπολογιστή του χρήστη και συμφωνία SHA-256, όχι απλώς link σε τοπικό `/workspace`.

## Scope πριν από τελικό ισχυρισμό

Διάβασε `KNOWN-LIMITATIONS.md`. Ειδικά το ABRSM-inspired δεν είναι πλήρης επίσημη syllabus simulation, το σολφέζ δεν καταγράφει φωνή και οι πολυφωνίες δεν είναι γενική μηχανή αυστηρής αντίστιξης. Οι απαιτήσεις αυτές πρέπει είτε να συμπληρωθούν είτε να γίνουν σαφώς αποδεκτό scope πριν ονομαστεί η εφαρμογή τελική επαγγελματική 1.0.0. Δεν δημιουργούμε αληθοφανή success flags.
