# Ακριβές σημείο συνέχισης προς 1.0.0

Η υλοποίηση έχει φτάσει στην **0.5.0**, με 28 μουσικές κατηγορίες, ενσωματωμένες εξετάσεις και δοκιμασμένα core/audio modules. Δεν βαφτίζουμε την έκδοση «τελική επαγγελματική 1.0.0» πριν από πραγματική Windows/NVDA/ακουστική αποδοχή και ειλικρινή αντιμετώπιση των γνωστών περιορισμών.

1. Διάβασε `KNOWN-LIMITATIONS.md`, `VALIDATION.md`, `VERSION-0.5.0.md` και `ACCEPTANCE.md`.
2. Από `desktop`: `./scripts/cloud-check.sh --maestro /workspace/.music-tools/maestro-private-piano/maestro.studio-piano.json --soundfont /tmp/music-test.sf2 --report artifacts/tests-0.5.0.json` εφόσον τα private test inputs υπάρχουν. Διαφορετικά δήλωσε τα integrations NOT RUN ή εγκατέστησε νόμιμα inputs. Μην αναδιανείμεις τα private samples.
3. Windows: `scripts/build-windows.ps1 -Installer -Accessibility`. Τρέξε `tools/Test-Accessibility.ps1`, τον piano installer και όλη τη ροή με πραγματικό NVDA/κάρτα. Διόρθωσε τα πραγματικά ευρήματα και αποθήκευσε το report· ένα Linux cross-build δεν αρκεί.
4. Ολοκλήρωσε/τεκμηρίωσε τις απαιτήσεις που παραμένουν ανοικτές: current ABRSM syllabus mapping, τυχόν κενά pedagogy, πραγματική φυσικότητα του piano adapter, runtime accessibility και hardware bugs. Μην προσθέσεις ψεύτικα success flags.
5. Τα Windows artifacts δημιουργούνται στο `artifacts/`. Linux packaging: `NSISDIR=/workspace/.music-tools/nsis-root/usr/share/nsis python scripts/package-linux.py --nsis /workspace/.music-tools/nsis-root/usr/bin/makensis`. Έπειτα `python scripts/assemble-delivery.py`.
6. Το `deliverables/releases/0.5.0` είναι η τρέχουσα ενιαία παράδοση. Παλιά checkpoints 0.1–0.4 δεν αντικαθίστανται. Για αναβάθμιση άλλαξε κεντρικά `Directory.Build.props` μόνο όταν ο αντίστοιχος ισχυρισμός έκδοσης δικαιολογείται.
7. Εξωτερική παράδοση: το GitHub API μπλοκαρίστηκε με CONNECT 403 και προστέθηκαν σε draft τα `api.github.com`/`uploads.github.com`. Το native Git push dry-run πέτυχε για `codex/pets-delivery`. Μια επιτυχημένη πραγματική μεταφορά/λήψη πρέπει να καταγραφεί ως ξεχωριστό αποτέλεσμα. Το `.github/workflows/windows-delivery.yml` δημιουργεί Windows packages/Actions artifacts και προαιρετικό draft Release αν εκτελεστεί στο repository. Μην ισχυριστείς ότι εκτελέστηκε πριν υπάρχουν logs.

Σύντομη εντολή επόμενης συνέχισης: «Συνέχισε από τη 0.5.0, διάβασε desktop/docs/CONTINUE-TO-1.0.md, επαλήθευσε τα Windows/παράδοση και διόρθωσε τα ευρήματα πριν προαγάγεις σε 1.0.0».

Έλεγχος τελικής ετοιμότητας: `python scripts/release-readiness.py --output artifacts/readiness.json --require-final`. Αυτή τη στιγμή αποτυγχάνει σκόπιμα επειδή λείπουν πραγματικά Windows/NVDA/hardware/listening/download και αποδοχή των ανοικτών scope θεμάτων. Δεν συμπληρώνουμε manual flags χωρίς αντίστοιχη δοκιμή. Η συναρμολόγηση αρνείται να δηλώσει ατεκμηρίωτη τελική `1.0.0`.
