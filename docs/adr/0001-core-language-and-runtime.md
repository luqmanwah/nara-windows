# ADR-0001: Bahasa dan runtime Nara Core

- Status: Accepted
- Tanggal: 2026-08-14
- Pemilik: Nara maintainers

## Konteks

Nara membutuhkan integrasi Windows yang kuat, aplikasi mandiri, penggunaan resource rendah, schema JSON yang stabil, signing, logging, recovery, dan pengujian yang mudah. Engine juga harus tetap dapat memakai API Windows yang belum tentu kompatibel dengan trimming atau NativeAOT.

## Opsi yang dibandingkan

### C# dan .NET 10

- Integrasi Windows dan tooling pengujian kuat.
- .NET 10 adalah rilis LTS tiga tahun.
- Mendukung self-contained dan single-file deployment.
- NativeAOT tersedia untuk startup cepat dan memory footprint lebih kecil.
- NativeAOT membatasi dynamic loading dan tidak menyediakan built-in COM pada Windows.

### Rust

- Binary native dan kontrol resource sangat baik.
- Cocok untuk komponen kecil yang membutuhkan determinisme atau isolasi kuat.
- Integrasi Windows management, UI, deployment, dan contributor workflow v1 akan membutuhkan lebih banyak glue code.

### PowerShell

- Sangat baik sebagai bootstrap, prototipe, recovery helper, dan adapter Windows yang transparan.
- Tidak ideal sebagai bahasa utama untuk policy engine besar, type safety lintas modul, UI, dan supply-chain packaging.

## Keputusan

1. **Nara Core dan Policy Engine memakai C# dengan target .NET 10 LTS.**
2. Build produksi utama memakai self-contained Windows x64. Single-file dan ReadyToRun hanya diaktifkan setelah compatibility test.
3. **NativeAOT tidak menjadi kewajiban seluruh aplikasi.** Ia menjadi target Nara Host dan leaf component yang lolos analyzer, functional test, startup benchmark, dan memory benchmark.
4. Hardware Profiler tidak boleh bergantung hanya pada WMI/COM. Ia memakai adapter registry, native Windows API, CIM bila tersedia, dan status `unavailable` bila sumber dibatasi.
5. PowerShell 7 dipakai untuk proof of concept read-only, bootstrap build, diagnostics, dan recovery yang dapat diaudit. Business rules tetap dipindahkan ke C#.
6. Rust bukan runtime utama v1, tetapi dapat digunakan kemudian untuk komponen terisolasi jika ADR dan benchmark menunjukkan manfaat nyata.
7. UI dipisahkan dari engine dan diputuskan melalui ADR lain setelah CLI/dry-run stabil.

## Konsekuensi

- Mesin developer membutuhkan .NET 10 SDK, tetapi pengguna akhir tidak harus memasang runtime jika paket dibuat self-contained.
- Nara tidak mengejar NativeAOT dengan mengorbankan compatibility Windows.
- Engine dan UI dapat memiliki deployment model berbeda.
- Setiap dependency harus diperiksa untuk trimming, single-file, dan AOT compatibility sebelum mode tersebut diaktifkan.

## Verifikasi

- Unit dan golden tests harus berjalan pada `net10.0-windows`.
- Publish `win-x64` self-contained wajib lulus pada VM bersih.
- NativeAOT hanya diterima bila tidak ada warning yang tidak dijelaskan dan semua Windows adapter lulus.
- Ukuran disk, startup time, private working set, idle CPU, dan failure recovery dibandingkan antar deployment mode.

## Referensi keputusan

- [Microsoft: What's new in .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [Microsoft: Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [Microsoft: Single-file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
