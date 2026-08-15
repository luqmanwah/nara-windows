# Profiles

Folder profil deklaratif. `lite-recommended.json` adalah profil pertama dan menargetkan perangkat dengan RAM minimal 12 GB. Hardware yang lebih besar tetap memakai runtime AI on-demand agar penggunaan resource mengikuti beban kerja, bukan sekadar kapasitas maksimum.

Profil tidak boleh memuat PowerShell, command line, registry path, secret, atau tindakan imperatif. Semua perubahan nyata nantinya dipetakan dari rule ID terversi di Playbook Engine.
