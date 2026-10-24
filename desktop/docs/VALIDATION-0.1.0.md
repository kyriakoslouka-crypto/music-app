# Πραγματική επαλήθευση — 0.1.0, βελτιωμένη — 2026-10-24

Περιβάλλον: Debian Linux x64, .NET SDK 8.0.426. Δεν υπάρχει Windows desktop, NVDA, φυσική έξοδος ήχου ή Wine. Η έκδοση δεν χαρακτηρίζεται πιστοποιημένη για επαγγελματική χρήση πριν την Windows αποδοχή.

## Εκτελέστηκαν πραγματικά

| Έλεγχος | Αποτέλεσμα |
|---|---|
| Locked restore + solution build | Επιτυχία, 0 warnings / 0 errors. |
| `scripts/cloud-check.sh --maestro /private/path/maestro.studio-piano.json --soundfont /tmp/music-test.sf2` | **25 tests πέρασαν, 0 απέτυχαν.** Περιλαμβάνει 21 core tests, 3 Maestro integration και 1 SF2 integration. |
| Παραγωγή/θεωρία | 21.600 παραγωγές όλων των βασικών συνδυασμών, 3.000 επιπλέον επαληθεύσεις συγχορδιών, reference τύποι και αναστροφές και των 10 ποιοτήτων, αναπαραγώγιμα seeds, όρια, αποκλεισμός συμμετρικών αναστροφών και απόρριψη αλλοιωμένων ασκήσεων. |
| Τονικότητες/έκταση | Όλες οι 24 τονικότητες και κάθε κατηγορία· ανεξάρτητα reference pitch sets. Ελέγχθηκε και διορθώθηκε έγκυρη αναστροφή με μη ακουόμενη θεμέλιο κάτω από το κατώτερο όριο. |
| Επανάληψη/ταχύτητα | Looping cursor με περισσότερα από 10.000 samples και ακριβή wrap, finite stop, κενό buffer, MIDI timestamps ανά BPM, harmonic unison χωρίς διπλό note-on. |
| Αποθήκευση | Roundtrip, backup, μοναδική βαθμολόγηση, απόρριψη άκυρων απαντήσεων, διατήρηση syntactically corrupt JSON και `[null]` semantic corruption. Η αυτόματη σύνδεση των UI συμβάντων με αποθήκευση χρειάζεται επιπλέον Windows δοκιμή. |
| Πηγή Maestro | Πραγματική λήψη του pinned αρχικού ZIP. Ο **ίδιος C# HttpClient downloader του οδηγού** εκτελέστηκε επιτυχώς στο Linux, με επαληθευμένο SHA-256 `2507c213538b850889bea4fbd90ca2a54abcc32d6a42febae53b81df1f677264`. |
| FLAC πρωτότυπα | Ελέγχθηκαν STREAMINFO και των 805 FLAC του αρχείου: όλα stereo, 44.1 kHz, 16-bit. |
| Τοπική προετοιμασία | Ο ίδιος C# verified extractor/preparer δημιούργησε πραγματικά 794 PCM16 WAV/manifest/license files από την πηγή, με ffmpeg decoder στο Linux. Δεν ήταν dummy ή sine fixtures. |
| Maestro engine | Coverage και των 88 πλήκτρων/5 attack/4 release layers. Επιβεβαιώθηκαν πέντε διαφορετικά SHA-256 attack δεδομένων ανά πλήκτρο. Πραγματικό stereo rendering για όλες τις κατηγορίες με sustain και release/pedal samples, διαφορετικές πραγματικές ηχογραφήσεις δυναμικής, finite PCM, peak limit και cancellation. |
| Pitch σε αλλαγή BPM | Ίδιο attack waveform πραγματικής νότας Ντο4 σε 60 και 120 BPM, με συσχέτιση πάνω από 0.99999 μετά ευθυγράμμιση onset. Δεν χρησιμοποιείται sample-rate pitch shifting για αλλαγή ταχύτητας. |
| Εναλλακτικό SF2 | Πραγματικό Florestan subset SF2 decoding/rendering, velocities, sustain, finite stereo PCM, peak limit και cancellation. Το fixture δεν περιλαμβάνεται στη διανομή. |
| Windows deliverables | Ενημερωμένο self-contained win-x64 publish, apphost και επιπλέον αυτοτελές single-file EXE με bundled native runtime· πραγματικός NSIS installer και portable ZIP, με CRC/contents check και SHA-256 manifest. Η δημιουργία τους δεν είναι εγκατάσταση/εκτέλεση Windows. |

Χωρίς παραμέτρους βιβλιοθηκών το runner εκτελεί **21 core tests** και αναφέρει τα integrations ως NOT RUN. Για επανάληψη της πραγματικής προετοιμασίας: `scripts/cloud-check.sh --prepare-maestro /path/original.zip /private/output`. Για έλεγχο πραγματικής λήψης χωρίς εγκατάσταση: `scripts/cloud-check.sh --download-maestro /private/new-download.zip`. Οι εντολές δεν εξάγουν βιβλιοθήκη για αναδιανομή.

## Δεν εκτελέστηκαν — απαιτούν Windows 11

- Εκκίνηση/εγκατάσταση/απεγκατάσταση του native EXE/NSIS.
- Ο **Windows Media Foundation FLAC decoder** του οδηγού, Windows codec availability, GUI progress/cancel/retry και first-run focus. Η ισοδύναμη lossless αποκωδικοποίηση ffmpeg δεν αποτελεί δοκιμή του Windows codec.
- Managed AccessibleObject audit και external UI Automation script σε όλες τις καρτέλες/οδηγό. Υλοποιήθηκαν και μεταγλωττίστηκε ο C# κώδικας, αλλά δεν υπήρξε Windows UIA runtime εδώ.
- Πραγματικές ανακοινώσεις/χειρισμός με NVDA, focus transitions, keyboard-only traversal και Contrast Themes/scaling.
- WASAPI/ASIO έξοδος, προεπιλεγμένη/εξωτερική κάρτα, pause/resume/stop σε hardware, latency και dropouts.
- Ακρόαση από επαγγελματία μουσικό, φυσικότητα/ισορροπία δυναμικών, sustain και ακουστική ποιότητα. Τα πραγματικά δείγματα και τα PCM tests δεν υποκαθιστούν την ακρόαση.

Τα ακριβή βήματα βρίσκονται στα `FIRST-RUN-NVDA.md` και `ACCEPTANCE.md`. Η βιβλιοθήκη Maestro δεν υπάρχει μέσα στο Setup/ZIP: ο οδηγός τη λαμβάνει από την αρχική δημοσιευμένη πηγή με τους περιορισμούς του `PIANO-SOURCE.md`.
