# Επαλήθευση αποκωδικοποίησης Maestro

Η φυσικότητα και η ακουστική ισορροπία απαιτούν πραγματική ακρόαση. Το παρόν ελέγχει διαφορετικό, προγραμματιστικά μετρήσιμο κριτήριο: ότι η αποκωδικοποίηση αναπαράγει τα ίδια PCM δεδομένα του επιλεγμένου πρωτότυπου δείγματος.

- Πηγή: το αρχικό archive και SHA-256 στο PIANO-SOURCE.md. Δεν παρακάμπτεται ο έλεγχος της λήψης.
- Το αρχικό υλικό έχει stereo 44.1 kHz PCM16 και γνωστό πλήθος frames στο FLAC STREAMINFO, αλλά τα εσωτερικά MD5 πεδία είναι μηδενικά. Δεν δηλώνουμε επιτυχή σύγκριση MD5 όταν λείπει το αποτύπωμα.
- Το metadata-only `Assets/MaestroPcmFingerprints.json` περιέχει SHA-256 και πλήθος frames για 440 attack, 352 release και 2 pedal δείγματα. Δεν περιέχει ήχο και δεν επιτρέπει ανακατασκευή δειγμάτων.
- Τα 794 αποτυπώματα αναπαράχθηκαν ανεξάρτητα με FFmpeg από το αρχικό SHA-256-verified archive, περιλαμβανομένης της δημοσιευμένης διόρθωσης SampleFix/mcg_ff_083.flac. Εντολή developer: `python scripts/generate-pcm-fingerprints.py /private/original.zip --check src/Studio.Windows/Assets/MaestroPcmFingerprints.json`.
- Ο κοινός WindowsPianoDecoder χρησιμοποιείται από τον πραγματικό οδηγό εγκατάστασης και τον native έλεγχο. Το Media Foundation αποδίδει σε κανονικό PCM16 WAV· συγκρίνονται τα bytes του data chunk, χωρίς αλλαγή δυναμικής ή resampling. Ασυμφωνία σταματά την προετοιμασία, δεν αντικαθίσταται με συνθετικό ήχο.
- Ο native Windows έλεγχος προετοιμάζει την πλήρη ιδιωτική βιβλιοθήκη σε RUNNER_TEMP, επαληθεύει 794 PCM hashes και κάνει πραγματικό rendering και των 28 κατηγοριών στη .NET 10. Έπειτα εκτελούνται οι Core/Maestro δοκιμές Windows. Το αποτέλεσμα καταγράφεται στα windows-piano-check.json και windows-maestro-tests.json. Η προσθήκη του ελέγχου δεν σημαίνει ότι έχει περάσει: διάβασε το πραγματικό αποτέλεσμα.

Τα WAV/FLAC/source archive παραμένουν ιδιωτικά και εκτός των CI uploads. Τα packages και το source ZIP περιέχουν μόνο κώδικα, οδηγίες και metadata αποτυπωμάτων. Πραγματικό NVDA, φυσικό WASAPI/ASIO endpoint και επαγγελματική ακρόαση δεν ελέγχονται από το file/PCM test.
