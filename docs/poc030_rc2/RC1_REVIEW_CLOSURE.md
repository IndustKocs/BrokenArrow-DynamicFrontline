# R1–R8 status

| Finding | Implemented evidence | Remaining scope |
|---|---|---|
| R1 | DevInstall separates mutable config and owned immutable binaries; configure/remove/reinstall and modified-DLL tests pass | Complete packaged installation workflow |
| R2 | Durable WAL, staging hashes, exclusive lock, per-file restore; 12 fail-fast child points recovered; foreign file preserved | Shared loader cache ownership; remove/upgrade crash matrix; disk-full/permission failures |
| R3 | SourceHealthTracker independent from transport heartbeat; stale/failed source test passes; worker invalidates projections | Actual menu/pause/session-switch two-game traces |
| R4 | Pinned file preflight; required wrapper provenance; bounded loader wait; expanded metadata binding inspection | Complete local generator/cache bootstrap and actual loaded invocation |
| R5 | Simulation023 Observe lane uses observed HP/stress/level; test 80→20→80; forecast never writes stress backend; terminal forecast does not stop observations | Real map/profile/service contracts and native HUD validation |
| R6 | Bounded chunks, TLS certificate pin, HMAC and connection nonce, canonical input/checkpoint/digests; independent two-process equality | Full lifecycle/service feed,  end-to-end reconnect and privacy negative suite |
| R7 | Async bounded logging and single core worker implemented | Required 10800-step ×3 full performance matrix and geometry oracle are not completed |
| R8 | Actual scrubbed baseline/build/test/crash/A/B logs included in evidence directory | Final reproducible package/build manifest and collector/replay workflow |

No finding is labelled completely closed while its listed required scope remains open.
