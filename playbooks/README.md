# Signed playbooks

`lite-safe-settings.json` is the first declarative Nara playbook. It targets only the `fake-windows` adapter and maps three approved Policy Compiler rule IDs to four allowlisted fake settings.

`lite-safe-settings.signature.json` is its detached ECDSA P-256/SHA-256 signature. Engine 0.1.0 embeds the matching Stage 2 development public key. The private key was discarded and is not stored in this repository. A production release must use an offline production key, documented rotation, and a revocation path.

Changing any byte of the playbook invalidates both its signature and any existing approval receipt.
