# Αξιόπιστη δημοσίευση διαγνωστικών

Η αποτυχία δημοσίευσης Windows reports δεν οφειλόταν στη μουσική μηχανή. Το περιεχομένου-only GITHUB_TOKEN προσπαθούσε να δημιουργήσει branch με ιστορικό που περιείχε παλαιότερο workflow snapshot ενώ η ανάπτυξη είχε ήδη αλλάξει το workflow. Το GitHub το απέρριπτε ως δημιουργία/ενημέρωση workflow χωρίς workflows permission.

Το Publish-Diagnostics.ps1 χρησιμοποιεί ξεχωριστό προσωρινό Git index και νέο root tree μόνο με JSON/text reports. Δεν αλλάζει κανονικό index, HEAD, αρχεία εφαρμογής ή workflows και δεν ζητά ισχυρότερο token. Τα παλιά diagnostics παραμένουν ιστορικά. Το SourceCommit κάθε report συνδέει τα τεκμήρια με τον πραγματικό κώδικα.

Το fast NVDA workflow χρησιμοποιεί αποκλειστικά το επαληθευμένο Portable preview.48 και αρνείται να εκτελεστεί αν έχει αλλάξει κώδικας εφαρμογής/βιβλιοθηκών/πόρων ή έκδοση. Καταγράφει χωριστά ApplicationCommit και HarnessCommit. Δεν αντικαθιστά το πλήρες Windows build, Maestro, εγκαταστάτη ή Source ZIP gate· επιταχύνει μόνο τη διάγνωση πραγματικών NVDA/keyboard δοκιμών.
