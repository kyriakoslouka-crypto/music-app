# Κριτήρια ολοκλήρωσης 0.1.0 — βελτιωμένη

Η έκδοση παραμένει **0.1.0**, με διαστήματα, συγχορδίες, αναστροφές και μεμονωμένες νότες. Δεν υλοποιούνται κατηγορίες της 0.2.0. Η επαγγελματική αποδοχή προϋποθέτει Windows/NVDA/ακρόαση, πέρα από το cross-build και τα PCM tests.

## Αυτοματοποιημένα κριτήρια Linux/core

- Solution build με warnings-as-errors, μηδέν warnings/errors και locked restore.
- 21.600 παραγωγές για έξι επίπεδα × τέσσερις κατηγορίες × τρεις τρόπους, όλες εντός έκτασης και με έγκυρες απαντήσεις.
- Ανεξάρτητοι τύποι και αναστροφές και των δέκα συγχορδιών, reference pitch sets για τονικότητες, σωστή φορά/ημιτόνια, απόρριψη αδύνατων/αλλοιωμένων ασκήσεων.
- Κάλυψη τονικών περιορισμών και των 24 τονικοτήτων, συμπεριλαμβανομένων έγκυρων αναστροφών με μη ακουόμενη θεμέλιο κάτω από το χαμηλότερο πλήκτρο.
- Απεριόριστος looping cursor με ακριβή wrap, finite replay χωρίς υπέρβαση και ασφαλής κενός buffer.
- BPM αλλάζει μόνο event timestamps· πραγματικό ίδιο attack waveform στο Maestro σε δύο ταχύτητες, όχι μεταφορά τονικού ύψους.
- Μία βαθμολόγηση, καμία αυθαίρετη απάντηση, progress/preferences roundtrip και backup· συντακτικά/σημασιολογικά κατεστραμμένο ιστορικό διατηρείται.
- Ακεραιότητα πραγματικής λήψης Maestro, SHA-256 πριν από extraction, απόρριψη λάθος archive, προετοιμασία 794 δειγμάτων από επαληθευμένη αρχική βιβλιοθήκη.
- Πραγματικό render Maestro σε κάθε κατηγορία, coverage 88 πλήκτρων/5 attack/4 release layers, sustain/pedal events, finite stereo PCM, peak limit και cancellation. Πρόσθετο SF2 integration για την εναλλακτική μηχανή.
- Ενημερωμένα self-contained Windows x64 EXE, portable ZIP και NSIS installer, CRC και SHA-256. Δημιουργία αρχείων, όχι εκτέλεση σε Windows.

## Προγραμματιστικοί έλεγχοι Windows — δεν εκτελέστηκαν στο Linux

1. Κλείσε τυχόν ανοικτή εφαρμογή και εκτέλεσε `scripts/Test-Accessibility.ps1` από το repository ή `tools/Test-Accessibility.ps1` από την εγκατάσταση/portable φάκελο.
2. Το EXE εκτελεί managed AccessibleObject audit σε **όλα τα controls όλων των καρτελών και στον PianoSetupDialog**: μη κενές ελληνικές ονομασίες, roles, TabStop, μοναδικά TabIndex ανά container. Το JSON report γράφεται από τη συγκεκριμένη εκτέλεση, όχι από stale report.
3. Το εξωτερικό UIA script ανοίγει την εφαρμογή σε test mode χωρίς audio/download, επιλέγει κάθε μία από τις τέσσερις καρτέλες μέσω SelectionItemPattern, εξετάζει ονόματα των ορατών keyboard-focusable controls και ενεργοποιεί το κουμπί εγκατάστασης μέσω InvokePattern.
4. Εξετάζει επίσης το native παράθυρο οδηγού πιάνου και το κλείνει χωρίς να ξεκινήσει λήψη. Καταγράφει συγκεκριμένη αποτυχία αν κάτι δεν εκτίθεται.
5. Με Accessibility Insights/Inspect ελέγξτε RangeValue στα αριθμητικά πεδία/πρόοδο, Selection στις απαντήσεις, Invoke/Toggle στα κουμπιά/checkbox και το αποτέλεσμα UIA notification. Τα programmatic names δεν αποδεικνύουν ομιλία NVDA.

## Χειροκίνητη αποδοχή Windows 11/NVDA/ήχου — ανοικτή

Κατέγραψε έκδοση Windows/NVDA, φωνή, DPI/scaling, κάρτα/driver, library/preset και sample rate.

- **App installer:** πρώτη εγκατάσταση ανά χρήστη χωρίς admin, Start Menu, offline εκκίνηση μετά την εγκατάσταση πιάνου, portable extraction, αναβάθμιση/απεγκατάσταση με διατήρηση προόδου και πιάνου. Επιβεβαίωση ότι δεν διαγράφονται αρχεία που πρόσθεσε ο χρήστης.
- **Piano wizard:** πλήρης Tab/Shift+Tab/Enter/Esc ροή, ανάγνωση άδειας/πηγής, λήψη και progress, Media Foundation FLAC decoding, cancellation σε λήψη/προετοιμασία, retry δικτύου, έλλειψη χώρου/codec, επισκευή με διατήρηση προηγούμενης εγκατάστασης.
- **Πληκτρολόγιο/NVDA:** όλες οι καρτέλες/ρυθμίσεις/επιλογές/αποτελέσματα, σωστή εστίαση χωρίς παγίδες, ελληνικές ονομασίες/τιμές, ανακοίνωση αποτελέσματος μία φορά και Ctrl+Shift+S fallback. Δοκίμασε ελληνική φωνή, browse/forms συμπεριφορά όπου εφαρμόζεται, 100/200/400% scaling και Contrast Themes.
- **Μουσική και ομιλία:** όχι προγραμματισμένες λεκτικές ειδοποιήσεις κατά την αναπαραγωγή, καμία αλλαγή global NVDA settings, προαιρετικό επίσημο controller DLL. Αναμενόμενη ομιλία από ρητή πλοήγηση/αίτημα χρήστη.
- **Ασκήσεις:** και οι τέσσερις κατηγορίες, απλά/σύνθετα/ανιόντα/κατιόντα/αρμονικά, αναστροφές τρίφωνων/τετράφωνων, πλήρης έκταση, τονικοί περιορισμοί, καμία διπλή βαθμολόγηση, σωστή πρόοδος μετά από επανεκκίνηση.
- **Replay/tempo:** απεριόριστο Ctrl+R, συνεχής βρόχος 0 που σταματά αμέσως με Esc/submit/new, pause/resume στην ίδια θέση. BPM/velocity/pedal changes κρατούν το ίδιο παράδειγμα και δεν επιτρέπουν να αναπαραχθεί stale buffer μετά από ακύρωση rendering.
- **Ακουστική αποδοχή από επαγγελματία μουσικό:** όλη η έκταση και οι δυναμικές του πραγματικού Maestro, stereo, καθαρό attack, φυσικό sustain/release, pedal noise στάθμη, ρεαλιστική απόσβεση, έλλειψη clicks/clipping. Το συγκεκριμένο adapter δεν ισχυρίζεται πλήρη ARIA/sforzando αναπαραγωγή.
- **WASAPI/ASIO:** προεπιλεγμένη και εξωτερική συσκευή, συμβατότητα ASIO x64 stereo 44.1 kHz, disconnected driver, αλλαγή εξόδου, pause/resume, repeated stop/replay και κλείσιμο κατά το rendering. Καταγραφή πραγματικού latency/dropouts.

## Αποδεκτοί δηλωμένοι περιορισμοί MVP

Όχι πλήρης SFZ engine, half-pedal/sostenuto/resonance, hardware MIDI είσοδος, audio/microphone assessment, νέες κατηγορίες 0.2+, differentiated Expert/Pro ή επίσημες εξετάσεις. Το πιάνο δεν αναδιανέμεται λόγω άδειας αλλά εγκαθίσταται από την πηγή. Δεν υπάρχει υπογραφή παραγωγής EXE/installer. Η πραγματική πιστοποίηση NVDA και επαγγελματικής ηχητικής ποιότητας παραμένει ανοικτή.
