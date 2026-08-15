# Nara trust metadata

This folder contains the root-signed development trust manifest used by Playbook Engine 0.2.0.

Only public keys, manifests, and detached signatures belong in source control. Root and release private keys must never be committed. The development private keys used to create the current fixtures were discarded.

Production release requires an offline root-key ceremony, protected release-signing key, dual-control recovery procedure, key-rotation schedule, signed revocation publication, and an emergency revocation path.
