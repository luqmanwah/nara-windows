# Development Environment Baseline

Tanggal pemeriksaan: 2026-08-14

## Hasil read-only

| Komponen | Hasil |
|---|---|
| Identitas Windows | Beberapa sumber registry dapat saling bertentangan; jangan dipercaya secara tunggal |
| CPU dan RAM | Terdeteksi oleh profiler; nilai mesin pengembangan tidak dipublikasikan |
| PowerShell | 7.6.4 |
| Git | Tersedia |
| .NET SDK global | Tidak ditemukan |
| .NET SDK lokal proyek | 10.0.400; SHA-512 cocok dengan metadata resmi Microsoft |
| .NET runtimes | .NET 8 dan .NET 10 tersedia |
| winget dari sandbox | Tidak ditemukan |
| CIM/WMI dari sandbox | Access denied |

## Interpretasi

Nama produk registry, display version, dan build tidak konsisten sebagai identitas pemasaran Windows. Nara tidak boleh mengandalkan satu registry value untuk menentukan versi OS. Hardware Profiler harus mengumpulkan beberapa sinyal, menyimpan raw evidence, dan menandai konflik daripada menebak.

Keterbatasan CIM/WMI terjadi pada lingkungan sandbox Codex dan belum membuktikan bahwa akun Windows pengguna biasa tidak dapat mengaksesnya. Proof of concept harus memiliki adapter fallback dan menghasilkan status `unavailable` tanpa meminta elevation otomatis.

Build, restore offline, publish framework-dependent, eksekusi, JSON Schema, dan privacy contract untuk profiler C# 0.1.0 telah lulus. Self-contained publish belum diuji karena runtime pack NuGet tidak tersedia pada sandbox; ini tetap menjadi release gate, bukan dependency tambahan aplikasi.

RAM dan CPU sudah diverifikasi oleh profiler proof of concept. GPU, model laptop, dan status virtualisasi tetap membutuhkan adapter tambahan atau pengujian dari terminal pengguna. Inventori mesin nyata disimpan lokal dan tidak menjadi bagian repository publik.
