# Πραγματική επαλήθευση — τρέχουσα 0.5.0

Η σειρά υλοποιήθηκε διαδοχικά: 0.2.0 (35 tests), 0.3.0 (43), 0.4.0 (51), 0.5.0 (61). Κάθε στάδιο έχει χωριστά binaries, source ZIP και JSON αποτελεσμάτων στο `deliverables/releases/`. Η παλιά αναφορά 0.1.0 φυλάσσεται ως `VALIDATION-0.1.0.md`.

**Πραγματικό περιβάλλον εκτέλεσης εδώ: Linux x64, .NET SDK 8.0.426.**

Εκτελέστηκαν:

- Locked restore, build με warnings-as-errors, μηδέν warnings/errors.
- 21.600 συνδυασμοί βασικών ασκήσεων, 2.520 νέων 0.2, 720 νέων 0.3 και 792 νέων 0.4, μαζί με επιπλέον property/reference tests.
- Music theory reference, πτώσεις/λύσεις, κλίμακες/τρόποι, rhythm/meter, γραπτή βαθμολόγηση, taps, πολυφωνία/solo και 12-tone transformations.
- Ακριβής αποθήκευση/επαναφορά προόδου, snapshots/εξετάσεων, προστασία από λάθος counters/βαθμολογίες, διπλό submit και stale audio cache.
- Πραγματική λήψη του Maestro μέσω του ίδιου C# downloader, checksum πριν extraction, ιδιωτική προετοιμασία 794 δειγμάτων με ffmpeg decoder, πραγματικό stereo PCM όλων των κατηγοριών, velocity layers, release/pedal events, pitch preservation και resource bounds.
- SF2 integration για τη δεύτερη μηχανή.
- Πραγματική παραγωγή Windows PE apphost, standalone single-file EXE, NSIS installer, portable ZIP και πλήρους source ZIP. CRC/hash έλεγχοι και αποκλεισμός private samples από τα πακέτα.

Το τρέχον runner εκτελεί **61 tests με Maestro+SF2**. Χωρίς αυτά τα inputs τα αντίστοιχα integrations αναφέρονται ως NOT RUN. Το `TEST-RESULTS.json` αποτυπώνει την πραγματική εκτέλεση και δεν υποκαθίσταται από περιγραφική αναφορά.

Δεν εκτελέστηκαν στο Linux:

- Native Windows GUI/εγκατάσταση/απεγκατάσταση και Windows Media Foundation decoder.
- Programmatic Windows UI Automation scripts: είναι υλοποιημένα για όλες τις καρτέλες, αλλά χρειάζονται Windows runtime.
- Πραγματικό NVDA, keyboard/focus συμπεριφορά, Windows scaling/Contrast Themes.
- WASAPI/ASIO σε φυσική συσκευή, input latency, ακρόαση και επαγγελματική αξιολόγηση του ήχου.

Το GitHub Actions workflow μπορεί να κάνει πραγματικό Windows build/UIA έλεγχο εκτός αυτού του Linux. Αν εκτελεστεί, το `WINDOWS-CHECKS.json` καταγράφει ξεχωριστά επιτυχία ή αποτυχία UIA. Δεν μετατρέπει ποτέ NVDA/hardware/listening σε true επειδή πέρασε το build. Μέχρι να υπάρξει τέτοιο report, η Windows δοκιμή παραμένει άγνωστη.


## Πραγματικός έλεγχος Windows CI

Το run [40068172865](https://github.com/kyriakoslouka-crypto/music-app/actions/runs/40068172865) εκτέλεσε 53 core tests σε Windows: 53 passed, 0 failed. [Αρχικό JSON](https://raw.githubusercontent.com/kyriakoslouka-crypto/music-app/fc9dc07ec66e07b3cac8ce08f218c0cfc37f01ed/downloads/windows-checks/40068172865/tests-0.5.0.json). Τα private piano integrations δεν εκτελέστηκαν στον runner. Η συσκευασία standalone απέτυχε στη μεταβίβαση MSBuild παραμέτρων PowerShell· διορθώθηκαν τα quotes και ξεκίνησε run 40068296151. UIA δεν εκτελέστηκε στο αποτυχημένο run. Αυτή η απόδειξη δεν είναι δοκιμή NVDA, Windows 11 προσωπικού υπολογιστή ή ακουστικής ποιότητας.
