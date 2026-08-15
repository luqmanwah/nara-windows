# Nara Windows

Repository kerja untuk mengubah Nara Windows Final Blueprint menjadi sistem yang dapat diuji dan dipasang.

## Prinsip kerja

1. Windows utama tidak dimodifikasi selama Stage 1.
2. Semua deteksi pertama bersifat read-only.
3. Semua rencana perubahan harus dapat ditampilkan sebagai dry-run.
4. Perubahan sistem baru boleh diuji setelah memiliki verifikasi dan rollback.
5. Pengujian perubahan dilakukan pada VM snapshot sebelum perangkat fisik.
6. ISO selalu berasal dari Windows resmi yang disediakan pengguna.

## Urutan implementasi

```text
Inventory read-only
        ↓
Profile + Policy Compiler
        ↓
Dry-run Action Plan
        ↓
Signed Consent + Approval Broker
        ↓
Transactional Playbook + Recovery
        ↓
Windows VM validation
        ↓
Provisioning Pack
        ↓
ISO Builder
        ↓
Clean-install validation
```

## Struktur

- `docs/` — keputusan arsitektur, roadmap, keamanan, dan panduan.
- `schemas/` — kontrak data yang stabil.
- `profiles/` — profil Lite, Balanced, Creator, Gaming, dan AI Workstation.
- `src/` — kode Nara.
- `tests/` — pengujian unit, integrasi, VM, dan release gates.
- `tools/` — alat build dan validasi yang tidak menjadi Nara Core.

Status aktif dicatat dalam `PROJECT_STATUS.md`.

## Build dan verifikasi Stage 1–2

```powershell
pwsh -NoProfile -File .\tools\build\Invoke-NaraBuild.ps1
pwsh -NoProfile -File .\tests\hardware-profiler-csharp.tests.ps1
pwsh -NoProfile -File .\tests\hardware-profiler-publish.tests.ps1
pwsh -NoProfile -File .\tests\policy-compiler.tests.ps1
pwsh -NoProfile -File .\tests\playbook-engine.tests.ps1
```

Semua dependency development disimpan di `.tools` dan semua output sementara disimpan di `artifacts`; keduanya tidak dimasukkan ke Git.

Policy Compiler hanya menghasilkan dry-run JSON. Ia tidak memiliki jalur untuk mengubah registry, service, driver, aplikasi, Defender, atau Windows Update.

Approval Broker 0.1.0 memverifikasi trust manifest dan playbook sebelum membuat consent bertanda tangan. Receipt v3 hanya diterbitkan setelah challenge request ID yang tepat, ditandatangani oleh kunci lokal instalasi, berlaku maksimal sepuluh menit, dan sekali pakai.

Playbook Engine 0.3.0 memverifikasi kembali root-signed trust manifest, status release key, playbook ECDSA signature, consent hash, serta signature Approval Receipt v3. Adapter tetap `fake-windows` dan seluruh alur masih berlabel `simulation-only`; kunci privat simulasi disimpan sebagai plaintext test-only di `artifacts` yang diabaikan Git. Penyimpanan kunci CNG/DPAPI dan UI persetujuan nyata tetap menjadi release gate VM.
