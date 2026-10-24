# Professional Ear Training Studio

**ΠΡΩΤΗ ΕΚΔΟΣΗ — VERSION 0.1.0 (MVP), βελτιωμένη.** Native Windows 11 x64, ελληνικό περιβάλλον και πληκτρολόγιο.

- Προσβάσιμος οδηγός λήψης/τοπικής εγκατάστασης **Maestro Concert Grand — Yamaha CF-3**, 5 πραγματικές δυναμικές, stereo samples, release και sustain-pedal noises. Δεν χρειάζεται να αναζητήσει βιβλιοθήκη ο χρήστης.
- Διαστήματα, συγχορδίες/αναστροφές, μεμονωμένες νότες, επιλογή τονικότητας/έκτασης, απεριόριστη επανάληψη και BPM χωρίς αλλαγή τονικού ύψους.
- Αυτόματη αποθήκευση, WASAPI/ASIO, φυσικά Windows Forms controls και UI Automation notifications.

Τα δείγματα **δεν αναδιανέμονται** στα artifacts λόγω της περιοριστικής άδειας. Ο οδηγός κατεβάζει το αρχικό ZIP από τη δημοσιευμένη πηγή και προετοιμάζει ιδιωτικό τοπικό αντίγραφο. Μετά την πρώτη εγκατάσταση η χρήση είναι offline.

[Πρώτη χρήση με NVDA χωρίς ποντίκι](desktop/docs/FIRST-RUN-NVDA.md) · [Πλήρεις οδηγίες και δομή](desktop/README.md) · [Πηγή/άδεια πιάνου](desktop/docs/PIANO-SOURCE.md) · [Πραγματική επαλήθευση](desktop/docs/VALIDATION.md).

Τα αρχεία διανομής δημιουργούνται στο `desktop/artifacts/`. Η πραγματική λειτουργία Windows UI Automation/NVDA, ο Windows FLAC decoder, οι audio drivers και η ακουστική ποιότητα χρειάζονται δοκιμές σε Windows 11. Cross-build και PCM tests δεν αποτελούν τέτοια πιστοποίηση.

```powershell
cd desktop
.\scripts\build-windows.ps1 -Installer
```

Το παλιό web prototype παραμένει στα root `src/`, `index.html`, `package.json` για αναφορά. Η κύρια εφαρμογή είναι το native solution στο `desktop/` και δεν χρειάζεται npm/browser. Δεν υλοποιήθηκαν λειτουργίες της 0.2.0.
