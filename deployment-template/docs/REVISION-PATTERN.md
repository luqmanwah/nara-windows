# Pelajaran dari ReviOS Playbook untuk Nara

## Pola yang diadopsi

- Stock fresh Windows sebagai baseline.
- Allowlist build dan arsitektur; build lain berhenti sebelum mutasi.
- Runner terpisah dari konfigurasi: engine menjalankan playbook deklaratif.
- Satu konfigurasi utama menjadi sumber kebenaran untuk opsi dan persyaratan.
- Fase eksekusi tetap, kondisi eksplisit, progress, reboot, dan finalisasi.
- Paket rilis immutable, berversi, memiliki SHA-256 dan catatan perubahan.
- Logika bersama berada di Nara Tool/Core, bukan disalin ke banyak script.
- Opsi pengguna dikumpulkan sebelum run; satu persetujuan mengunci seluruh bundle.
- Hardware-specific tuning dan driver tidak ditebak oleh playbook universal.

## Hal yang tidak disalin

- Menonaktifkan Defender sebagai prasyarat atau tweak.
- Menghapus komponen servicing/WinSxS secara agresif.
- Menjalankan paket dari sumber unduhan yang tidak berada di allowlist Nara.
- Menganggap ISO injection stabil sebelum melewati test matrix.
- Memberi AI kewenangan membuat dan langsung menjalankan mutation script pada perangkat produksi.

## Definisi satu klik Nara

`INSTALL-NARA.cmd` melakukan preflight, membuka handoff Codex bila diperlukan, menyelesaikan paket, menampilkan satu ringkasan persetujuan, mengunci hash rencana, lalu menyerahkan bundle kepada runner. Setelah disetujui, fase berjalan tanpa pertanyaan berulang kecuali scope berubah, paket berubah, verifikasi gagal, atau diperlukan otoritas baru.

## Gerbang rilis

Sebuah build Nara hanya boleh berstatus stable setelah lulus pada stock Windows yang didukung: clean apply, idempotent reapply, reboot/resume, rollback, Defender health, Windows Update scan, Store/App Installer, networking, sleep/wake, aplikasi umum, dan pengukuran idle.
