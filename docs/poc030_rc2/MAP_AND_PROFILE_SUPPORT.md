# Airport / Airport_network

Selected by user: closed PvP, exactly two human players on opposite teams. First LoadReadProbe, then ObserveTwoClient after separate consent. The shipped scenario has ten possible player slots; an exactly-two active roster must be checked from the actual match, not inferred from this file.

Read-only extraction found native `Airport.ng` and `Airport_roads.ng`, plus the scenario archive. `airport-source.json` records hashes and counts. The main graph contains 154056 nodes and 1202471 links. Its node extents are not proof of playable bounds. Link masks correspond to NavMeshAreas, not NavMeshMetaLayers.

The decoded scenario contains a 9000 × 9000 large zone centred at (4500,4500), and a 6500 × 6500 small zone centred at (4994.954,4833.005). Initial `spz=10` names the large zone, but the script also activates small zone 55 through an automatic player-count branch. Therefore using only the initial head file would be wrong. Active zone selection must be checked against the script path and runtime state.

Ground spawn candidates: Alpha at XZ (6373.75146,2491.39966), Bravo at (3036.983,6830.55957). These are extracted scenario spawn points, not yet approved mod roots. Dynamic obstacles, bridge/portal state, local connectivity, metric corridor tests and scoring mask remain unvalidated. No all-passable live fallback is enabled.

RC1 profile candidates remain provenance inputs: BTR-82 (1), BMP-3 (3), T-72B (9), line infantry 57/60, assault 59/108. Active UnitData role/S0 and native stress thresholds are compared by the new reader/profile resolver. There is no valid blanket S0=1000 fallback. IDs 710/711/713 remain denied. A completed supported live profile set is still pending; research candidates alone are not runtime proof.

`inspect-scenario-local.ps1` decodes only into ignored local artifacts. Decoded scenario contents and generated wrappers must not be redistributed.
