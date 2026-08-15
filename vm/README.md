# Nara Windows VM workspace

`Nara-Windows-Stock` adalah disposable baseline untuk menguji Nara tanpa mengubah Windows utama.

Konfigurasi terkunci:

- Guest OS: Windows 11 x64 stock
- RAM: 12 GB
- CPU: 4 vCPU
- Firmware: EFI64
- TPM: emulasi 2.0
- Disk: VDI dinamis 50 GB
- Network: NAT
- Clipboard dan drag-and-drop host/guest: disabled
- Boot: DVD lalu disk

File VM, disk, log, dan snapshot diabaikan Git. ISO harus berasal dari Microsoft dan diverifikasi sebelum dipasang. Snapshot pertama setelah instalasi stock dan update penting adalah `stock-clean`.
