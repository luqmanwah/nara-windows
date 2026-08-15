# Nara Deployment Workspace

Template lokal untuk handoff instalasi Windows fresh kepada Codex. Template ini belum merupakan rilis produksi dan tidak boleh dipublikasikan sebagai installer siap pakai.

## Alur

1. Bootstrap mengisi data di `Inventory/` tanpa mengubah Windows.
2. Pengguna membuka folder ini sebagai workspace Codex dan menjalankan instruksi di `START-HERE.md`.
3. Codex membaca `AGENTS.md`, inventaris, kebijakan, profil, katalog paket, dan playbook.
4. Codex membuat `Reports/proposed-plan.json`. Tidak ada perubahan sistem pada tahap ini.
5. Setelah persetujuan eksplisit, runner tepercaya menjalankan hanya tindakan dari paket yang terverifikasi.
6. Hasil, bukti verifikasi, dan rollback dicatat di `Reports/`, `Logs/`, dan `Backups/`.

Jalankan `Tools/Validate-Workspace.ps1` untuk memeriksa kelengkapan template.
