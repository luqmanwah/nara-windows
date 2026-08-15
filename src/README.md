# Source

- `Nara.HardwareProfiler` — collector Windows read-only berbasis C#/.NET 10 yang menghasilkan kontrak inventory JSON v1.0.0.
- `Nara.PolicyCompiler` — compiler deterministik berbasis C#/.NET 10 yang mengubah inventory + profil menjadi action plan dry-run v1.0.0.
- `Nara.ApprovalContracts` — kontrak dan canonical signing untuk signed consent serta Approval Receipt v3.
- `Nara.ApprovalBroker` — broker simulasi 0.1.0 yang memverifikasi trust, menyusun consent, memvalidasi challenge, dan menandatangani receipt dengan kunci lokal test-only.
- `Nara.PlaybookEngine` — transaction engine 0.3.0 dengan trust manifest, signature receipt lokal, consent binding, checkpoint, apply, verify, revert, dan ledger memakai adapter `fake-windows` saja.
