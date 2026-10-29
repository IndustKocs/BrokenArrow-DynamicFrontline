# Tests at checkpoint

Actual logs are under evidence/poc030_rc2. Baseline and modified regression: 356 existing groups (72+36+59+89+58+42) and 44 RC1 checks. Fifteen additional RC2 groups pass. Crash logs include nonzero child exit codes at all 12 apply fault boundaries and successful recovery assertions.

Independent process test: TLS + canonical input + separately constructed existing cores yielded equal stage digests. A second test uses RuntimeObserver on both sides: copied synthetic owner observations → TLS → canonical world → existing core → public HUD projection, with equal canonical input hashes. This is a real two-process offline pipeline, not gameplay.

Build: SDK 8.0.426, target net6.0, pinned local MelonLoader references. Plugin and development CLI build with zero warnings/errors. Commands are in build-prototype023.ps1, scripts/poc030/build-poc030.ps1 and scripts/poc030-rc2/test-two-process.ps1; use -RuntimePipeline for the source-to-projection test.

NotRun: two games, loaded generic bindings, actual HUD, dynamic map, service operation feed, full reconnect negative suite, full performance matrix, live soak. These omissions prevent release acceptance.
