# Tests

Lapisan pengujian:

- Unit: schema, rules, dan deterministic compiler.
- Golden: input inventory menghasilkan action plan yang diharapkan.
- Integration: Windows API adapter dan ledger.
- VM: install, update, rollback, aplikasi, dan performance.
- Release: signature, hash, SBOM, clean build, serta recovery.

`policy-compiler.tests.ps1` memvalidasi schema, urutan rule deterministik, baseline Defender, filter update Lite, provenance hash, keputusan berdasarkan RAM, blokir driver ketika inventory belum lengkap, dan fail-closed untuk field profil yang tidak dikenal.

`playbook-engine.tests.ps1` memakai Approval Broker dan adapter `fake-windows` untuk membuktikan trust diverifikasi sebelum consent, challenge request ID, signature consent/receipt, commit, idempotensi, rollback byte-exact, receipt single-use, expiry, installation binding, key rotation/retirement/revocation, binding hash, dan deteksi ledger yang dimanipulasi. Tes juga memastikan kunci privat simulasi tidak bocor ke artefak publik. Tes ini tidak memanggil API Windows atau jaringan.
