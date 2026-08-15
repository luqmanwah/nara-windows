# Roadmap sampai instalasi

## Stage 1 — Safe Prototype

Hasil: inventory, profile compiler, dan dry-run tanpa perubahan sistem.

**Status:** Lulus untuk baseline `Lite + Recommended` pada 2026-08-14. Adapter inventory lanjutan tetap dibutuhkan untuk GPU sebelum rekomendasi driver dapat dibuka.

Gate kelulusan:

- Berjalan tanpa Administrator untuk inventory dasar.
- Tidak menulis registry, service, package, driver, atau Windows Update.
- Output stabil, tervalidasi schema, dan bebas secret.

## Stage 2 — Transaction Engine

Hasil: playbook bertanda tangan dengan precheck, checkpoint, apply, verify, ledger, dan revert.

**Status:** Core simulator 0.3.0 dan Approval Broker Core 0.1.0 lulus pada 2026-08-14. Development trust manifest, key rotation/retirement/revocation, signed consent, challenge eksplisit, locally signed Receipt v3, expiry, installation binding, dan replay rejection telah diuji. Adapter kunci CNG/DPAPI, consent UI nyata, fixed protected ledger, dan production key ceremony masih menjadi release gate sebelum adapter Windows diaktifkan.

Gate kelulusan:

- Tindakan idempotent.
- Kegagalan di tengah transaksi tidak meninggalkan state tanpa penjelasan.
- Recovery tidak bergantung pada AI.
- AI tidak dapat memanggil signing boundary persetujuan secara langsung.
- Consent ditampilkan hanya setelah trust manifest dan playbook lolos verifikasi.

## Stage 3 — VM Validation

Hasil: Nara teruji pada snapshot Windows 11 Home dan Pro stock.

Gate kelulusan:

- Windows Update, Defender, Microsoft Store, sleep/resume, audio, jaringan, dan recovery tetap bekerja.
- Profil Lite memenuhi batas CPU, RAM, disk, dan background process yang ditentukan.
- Aplikasi compatibility suite lulus.

## Stage 4 — Provisioning Pack

Hasil: pengguna dapat memasang Nara pada Windows stock yang berjalan.

## Stage 5 — ISO Builder

Hasil: builder lokal menerima ISO Windows resmi dan menghasilkan media clean-install dengan bootstrap Nara.

## Stage 6 — Physical Install

Hasil: backup telah diverifikasi, media recovery tersedia, installer diuji, lalu clean install dilakukan dengan keputusan pengguna pada setiap titik berisiko.
