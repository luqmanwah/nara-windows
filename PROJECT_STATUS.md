# Nara Project Status

**Blueprint:** Final Blueprint v1.0  
**Tahap aktif:** Stage 3 — Stock Windows VM preparation  
**Aturan aktif:** Tidak mengubah Windows utama

## Milestone

- [x] Konsep dan batas produk dikunci.
- [x] Workspace proyek dibuat.
- [x] ADR teknologi inti diputuskan.
- [x] Hardware Profiler read-only proof of concept berjalan.
- [x] Schema inventory tervalidasi.
- [x] .NET 10 SDK lokal terverifikasi SHA-512.
- [x] Hardware Profiler C# 0.1.0 lulus build dan publish prototype.
- [x] Schema profile dan action plan tervalidasi.
- [x] Policy Compiler 0.1.0 menghasilkan dry-run deterministik.
- [x] Profil Lite + Recommended memiliki golden test untuk mesin saat ini serta batas RAM 11/12/64 GB.
- [x] Playbook Engine 0.3.0 mendukung precheck, checkpoint, apply, verify, commit, dan revert pada adapter palsu.
- [x] Persetujuan simulasi terikat hash action plan dan playbook.
- [x] Playbook Lite pertama memakai detached ECDSA signature dengan development trust root.
- [x] Ledger JSONL memiliki rantai SHA-256 dan mendeteksi perubahan event.
- [x] Commit, idempotensi, rollback byte-exact, approval rejection, signature/hash rejection, dan ledger tamper test lulus.
- [x] Development trust manifest mendukung key rotation, retirement, dan revocation.
- [x] Approval Broker Core 0.1.0 memverifikasi trust sebelum menampilkan consent dan membutuhkan challenge request ID yang tepat.
- [x] Consent Request bertanda tangan mengikat tindakan, action plan, playbook, release key, instalasi, dan batas waktu.
- [x] Approval Receipt v3 ditandatangani kunci lokal simulasi serta terikat consent hash, installation ID acak, signing-key ID, TTL maksimal 10 menit, dan single-use ledger check.
- [x] Pemalsuan consent/receipt, challenge salah, replay, expiry, instalasi lain, key rotation/retirement/revocation, rollback, serta ledger tamper ditolak atau dipulihkan sesuai kontrak.
- [ ] Adapter kunci protected Windows CNG/DPAPI dan consent UI nyata dibuat serta diuji hanya di VM.
- [ ] Production key ceremony untuk root dan release signing dibuat.
- [x] Oracle VirtualBox 7.2.14 terpasang dan installer cocok dengan checksum resmi Oracle.
- [x] Skeleton VM `Nara-Windows-Stock` dibuat: Windows 11 x64, 12 GB RAM, 4 vCPU, EFI64, TPM 2.0, NAT, dan disk VDI dinamis 50 GB.
- [ ] ISO resmi Windows 11 25H2 English x64 selesai diunduh dan diverifikasi SHA-256.
- [ ] VM Windows stock lulus installation gate.
- [ ] Provisioning Pack lulus VM gate.
- [ ] ISO Builder lulus clean-install gate.
- [ ] Instalasi perangkat fisik disetujui pemilik.

## Langkah berikutnya

Selesaikan unduhan ISO Windows resmi, verifikasi SHA-256, pasang ISO ke `Nara-Windows-Stock`, instal Windows stock, lalu buat snapshot `stock-clean`. Setelah itu implementasikan adapter CNG/DPAPI dan consent UI di VM; Windows utama tetap tidak disentuh.

## Catatan lingkungan awal

- PowerShell 7 tersedia.
- Git tersedia melalui runtime workspace.
- .NET SDK 10.0.400 tersedia secara lokal di `.tools`; instalasi .NET global tidak diubah.
- `winget` tidak terlihat dari lingkungan Codex saat pemeriksaan awal.
- Akses CIM/WMI dibatasi dari sandbox Codex; Hardware Profiler harus diuji juga dari terminal pengguna biasa dan tidak boleh meminta Administrator hanya untuk inventory dasar.
- Publish framework-dependent prototype lulus. Publish self-contained menunggu official runtime packs pada build environment dengan akses NuGet.
- Virtualisasi firmware aktif; Windows melaporkan hypervisor dan VBS/HVCI sedang berjalan.
- Detail host pengembangan disimpan lokal dan tidak dipublikasikan. Secure Boot host tidak diubah.
