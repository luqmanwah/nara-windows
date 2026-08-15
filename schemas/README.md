# Data contracts

Schema pertama yang akan dibuat:

1. `hardware-inventory.schema.json` — dibuat dan lulus proof-of-concept test.
2. `profile.schema.json` — kontrak profil deklaratif tanpa skrip/perintah mentah.
3. `action-plan.schema.json` — kontrak hasil dry-run yang dapat diaudit.
4. `playbook.schema.json` — tindakan deklaratif yang dipetakan ke adapter allowlist.
5. `playbook-signature.schema.json` — tanda tangan ECDSA terpisah dari playbook.
6. `consent-request.schema.json` — tampilan tindakan exact yang ditandatangani broker sebelum persetujuan.
7. `approval-receipt.schema.json` — receipt v3 bertanda tangan lokal dan terikat hash consent serta input.
8. `fake-approval-key.schema.json` — kunci plaintext khusus pengujian; dilarang untuk produksi.
9. `fake-windows-state.schema.json` — state Windows palsu beserta identitas kunci publik instalasi.
10. `ledger-event.schema.json` — event append-only dengan rantai hash.
11. `transaction-report.schema.json` — ringkasan commit, revert, atau rejection.
12. `trust-manifest.schema.json` — daftar release key aktif, retired, dan revoked.
13. `trust-manifest-signature.schema.json` — signature manifest oleh root key Nara.

Semua schema menggunakan version field dan menolak field rahasia seperti password, cookie, token, atau API key.
