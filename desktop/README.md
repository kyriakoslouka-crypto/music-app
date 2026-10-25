# Professional Ear Training Studio — Windows

**Νέο στη 0.5.2:** προαιρετική αυτόματη συνέχεια και ακρόαση, με ρυθμιζόμενη αναμονή και ακύρωση με Esc. [Οδηγίες](docs/AUTOMATIC-PRACTICE.md).

**Διορθωτική 0.5.1:** αποκατάσταση της γέφυρας PCM προς NAudio και αναφορά τελευταίου σφάλματος ήχου με F6. [Διάγνωση και δοκιμές](docs/AUDIO-OUTPUT-RECOVERY.md).

Ενεργή σειρά ανάπτυξης native Windows 11 x64: C#/.NET 10 LTS (GUI· Core/Audio net8.0), ελληνικά Windows Forms controls, Maestro Concert Grand και WASAPI/ASIO. Η ακριβής έκδοση βρίσκεται στο `Directory.Build.props`, στον τίτλο της εφαρμογής και στον αντίστοιχο φάκελο παραδοτέων. **Η 1.0.0 δεν θεωρείται ολοκληρωμένη χωρίς πραγματική αποδοχή Windows/NVDA/ήχου.**

## Πρώτη χρήση, μόνο με πληκτρολόγιο

Εκτέλεσε τον εγκαταστάτη ή αποσυμπίεσε ολόκληρο το Portable ZIP. Το ξεχωριστό μεγάλο standalone EXE περιέχει το runtime· το μικρό EXE του portable φακέλου απαιτεί τα συνοδευτικά DLL. Στην πρώτη εκκίνηση ο προσβάσιμος οδηγός εγκαθιστά το Maestro Concert Grand — Yamaha CF-3 από την επιλεγμένη πηγή, με επαλήθευση SHA-256, περίπου 263 MB λήψης και 3 GB διαθέσιμου χώρου. Έπειτα η χρήση είναι offline. Δεν απαιτείται αναζήτηση βιβλιοθήκης από τον χρήστη.

Τα δείγματα **δεν περιλαμβάνονται στα πακέτα διανομής** λόγω της περιοριστικής άδειας. Τοπική ιδιωτική προετοιμασία: 440 attack samples, 352 release samples, δύο pedal noises, πέντε πραγματικές δυναμικές και 88 πλήκτρα. Δες [PIANO-SOURCE](docs/PIANO-SOURCE.md). Δεν υλοποιείται πλήρης SFZ/ARIA, half-pedal, sostenuto ή physical MIDI input.

Tab/Shift+Tab, βέλη, Space και Enter χειρίζονται τα φυσικά controls. Οι νέες ακολουθίες πρώτα ανακοινώνουν οδηγία με Ctrl+N και περιμένουν Ctrl+R για μουσική. Ctrl+I επαναδιαβάζει την οδηγία. Δεν αποστέλλονται UIA ανακοινώσεις αποτελέσματος πάνω από μουσική· ο ήχος σταματά πριν τη βαθμολόγηση.

| Πλήκτρα | Ενέργεια |
|---|---|
| Ctrl+N | Νέα άσκηση |
| Ctrl+R | Πρότυπο ξανά, χωρίς όριο |
| Ctrl+Shift+R | Ακρόαση γραπτής απάντησης όταν υποστηρίζεται |
| Ctrl+Enter | Υποβολή |
| Ctrl+P / Esc | Παύση–συνέχεια / διακοπή |
| Ctrl+Home / Ctrl+Tab | Ρυθμίσεις / επόμενη καρτέλα |
| Ctrl+I / Ctrl+Shift+S | Οδηγία / κατάσταση |
| F8 | Κτύπημα σε ρυθμική αναπαραγωγή |
| F1 | Πλήρεις οδηγίες |

Στις Ρυθμίσεις: έξι επίπεδα, τονικό κέντρο/μείζων–φυσική ελάσσων, έκταση 21–108, ταχύτητα χωρίς pitch shifting, velocity, πεντάλ, επαναλήψεις 0 για συνεχή βρόχο ή 1–5. Οι ρυθμικές ασκήσεις αγνοούν sustain ώστε να ακούγονται οι παύσεις. Τα πεδία αρχής/τέλους τμήματος μετρούν χρόνους τετάρτου, όχι δευτερόλεπτα· τέλος 0 σημαίνει ολόκληρο παράδειγμα.

## Κατάσταση υλοποίησης ανά έκδοση

- [0.5.0](docs/VERSION-0.5.0.md): ανεπίσημα εξεταστικά προφίλ, resume, βαθμολόγηση, analytics, adaptive practice και επανάληψη λαθών.
- 0.1.0: διαστήματα έως δύο οκτάβες, 10 ποιότητες συγχορδιών, ακουστικά διακριτές αναστροφές και μεμονωμένες νότες, πιάνο/αποθήκευση/native UI.
- [0.2.0](docs/VERSION-0.2.0.md): πτώσεις, 11 κλίμακες, επτά τρόποι, ρυθμικά σχήματα, 12 μέτρα/ομαδοποιήσεις, γραπτή ρυθμική υπαγόρευση και κτυπήματα.
- [0.4.0](docs/VERSION-0.4.0.md): ανεξάρτητες φωνές, εσωτερική γραμμή, χρωματική/ατονική/τροπική ανάλυση και εκτεταμένα διαστήματα/συγχορδίες.
- [0.3.0](docs/VERSION-0.3.0.md): μελωδικό/αρμονικό ντικτέ, βαθμίδες/λειτουργίες, fixed-do σολφέζ και μελωδική μνήμη, ακρόαση απάντησης και τμημάτων. **Δεν καταγράφεται ή βαθμολογείται φωνή.**

Οι φάκελοι παλιότερων εκδόσεων διατηρούν τα δικά τους binaries/source/tests. Δεν σημαίνει ότι μια παλιότερη έκδοση περιέχει δυνατότητες νεότερης. Οι ασκήσεις παράγονται από πραγματικούς τύπους/μεταθέσεις/timelines, όχι από dummy καταλόγους ή έτοιμα αρχεία ερωτήσεων. Δεν γίνεται πλαστή υπόσχεση χιλιάδων ξεχωριστών ονομάτων όταν το θεωρητικό σύνολο είναι πεπερασμένο.

## Πηγαίος κώδικας και έλεγχοι

- `Studio.Core`: θεωρία, Scores, parsers, παραγωγή, verification, βαθμολόγηση, snapshots και atomic αποθήκευση.
- `Studio.Audio`: MIDI timeline, voice channels, sample renderer Maestro, SF2, loop cursor και verified piano installer.
- `Studio.Windows`: ελληνικά native controls, γραπτές απαντήσεις, taps, UIA audit/notifications, έξοδοι ήχου.
- `tests/Studio.Tests`: console harness με πραγματικό exit status και προαιρετικό JSON report, όχι μηδενικά `dotnet test`.

```powershell
dotnet restore ProfessionalEarTrainingStudio.sln --locked-mode
dotnet build ProfessionalEarTrainingStudio.sln -c Release --no-restore
dotnet run --project tests/Studio.Tests -c Release --no-build
.\scripts\build-windows.ps1 -Installer -Accessibility
python scripts/assemble-delivery.py
```

Linux: `./scripts/cloud-check.sh`, προαιρετικά `--maestro /private/maestro.studio-piano.json --soundfont /private/test.sf2 --report artifacts/tests-VERSION.json`. Τα integrations χωρίς βιβλιοθήκες δηλώνονται NOT RUN. Η πραγματική προετοιμασία Maestro στον έλεγχο Linux χρησιμοποιεί ffmpeg· ο Windows Media Foundation codec χρειάζεται δική του δοκιμή.

`Test-Accessibility.ps1` εξετάζει όλα τα native controls/καρτέλες και τον piano installer **όταν εκτελεστεί σε Windows**. Η προσθήκη/μεταγλώττισή του δεν πιστοποιεί NVDA. Πραγματικά NVDA, Windows GUI, drivers και ακουστική ποιότητα δεν δοκιμάστηκαν στο Linux.

## Αποθήκευση και παράδοση

`%LOCALAPPDATA%\ProfessionalEarTrainingStudio`: αυτόματη αποθήκευση απαντήσεων/έγκυρων ρυθμίσεων, JSON με atomic rename και `.bak`. Άκυρα ιστορικά δεν αντικαθίστανται σιωπηλά. Δεν υπάρχει τηλεμετρία ή cloud sync. Μία εφαρμογή ανά χρήστη/Windows session.

`deliverables/releases/VERSION/`: Setup, portable, standalone, πλήρες native source ZIP, οδηγίες, test JSON και SHA-256. Το `LATEST.json` δεν περιέχει πλαστό public URL. Ο τοπικός φάκελος δεν ισοδυναμεί με download στον υπολογιστή του χρήστη.

Το `.github/workflows/windows-delivery.yml` παρέχει πραγματικό workflow Windows build/ελέγχων και upload σε GitHub Actions Artifacts όταν εκτελεστεί στο repository. Προαιρετικά δημιουργεί **draft** Release για έλεγχο από τον ιδιοκτήτη. Το workflow εκτελέστηκε πραγματικά: το αρχικό Windows build απέτυχε και ακολουθεί διαγνωστική επανεκτέλεση. Ο ξεχωριστός κλάδος `codex/pets-delivery` περιέχει τα cloud-built πακέτα στο `downloads/0.5.0`, με επαληθευμένη HTTPS λήψη του εγκαταστάτη. Το GitHub API παρέμεινε αποκλεισμένο, αλλά το native Git επέτρεψε κανονική μεταφορά. Δεν δημοσιεύονται κλειδιά ή private samples.

Τρέχουσα συνέχεια: [CONTINUE-TO-1.0](docs/CONTINUE-TO-1.0.md), [γνωστοί περιορισμοί](docs/KNOWN-LIMITATIONS.md), [πραγματική επαλήθευση](docs/VALIDATION.md).
