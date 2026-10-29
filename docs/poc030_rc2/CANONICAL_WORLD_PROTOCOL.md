# Protocol implementation and limits

World protocol uses explicit private IPv4/loopback endpoints, pinned TLS server certificate, a shared-key HMAC and fresh nonces on every connection. Sequence numbers and handshake fingerprints reject mismatched sessions, payloads, rules, maps, profiles and ABI. No firewall/relay changes are made. Windows Schannel needs an accessible temporary private key container; the two-process TLS test requires execution outside restricted cryptographic sandbox. It creates no trusted certificate-store entry.

Messages are bounded to 16 MiB, split into 48 KiB chunks, validated before deserialization; outbound and inbound queues are bounded to 32. Full owner snapshots, canonical inputs, source health, input acknowledgments, result digests and checkpoints are wired to the worker. Canonical time uses paired fresh source clocks without extrapolation. Input hashes include canonical context and source revisions, excluding local ECS handles.

TeamSafe sends B's owned observations to coordinator A, and only B's own public projection back. TrustedDiagnosticReplica requires explicit consent on both peers before full canonical inputs or checkpoints. Independent equality tests use the latter. Enemy visibility integration is not implemented; public circles are conservatively own-team only.

Checkpoint contains existing core state and world/source generation state. A digest mismatch suspends computation and requests resync. Full reconnect/replay history tests, full replay history and lifecycle/service semantics are still incomplete. Do not interpret the passing one-input process test as full resync validation.
