# RC2 development checkpoint — NOT a release candidate

Base: RC1 commit `7251635`, branch `feature/poc030-rc2-two-client-integration`.
Game installation, loader startup and gameplay were NOT performed. Launcher and research sources are unchanged.

Implemented: transactional sandbox installer with write-ahead recovery; mutable configuration preservation; independent source health; owned snapshot/canonical input transport over pinned TLS plus HMAC; two privacy modes; existing Simulation023 observation lane; worker and diagnostic HUD wiring; actual stress separated from forecast; checkpoint round trip.

Evidence: 356 existing regression groups and 44 RC1 checks passed. Fifteen additional RC2 groups passed, including fail-fast child termination at 12 installation boundaries. Separate A/B processes exchanged a synthetic canonical input over TLS and produced equal core digests. These are offline results, not runtime validation. The plugin and development CLI build without warnings.

This checkpoint does **not** satisfy all of TZ17. Do not install it into the game. Open work includes the local loader cache ownership transaction and  verified real map/navigation and profiles, passive positive service-operation evidence, complete lifecycle/delta/resync negative tests, full geometry/performance matrix, replay/collector and delivery package. The CLI deliberately permits sandbox operations only. The old RC1 install scripts are not an RC2 installation procedure.

Static game-file preflight matches the four pinned UA01 files. Steam manifest and runtime loaded cache have not been validated. Fourteen expanded generated binding groups pass metadata inspection; generic getter invocation remains untested. `MapDescriptor.GeometryValidated` in test fixtures describes synthetic fixtures only, and must never be copied to a live configuration.

The first game experience still requires separate consent and must involve both clients, first LoadReadProbe and then ObserveTwoClient. All live results remain NotRun.
