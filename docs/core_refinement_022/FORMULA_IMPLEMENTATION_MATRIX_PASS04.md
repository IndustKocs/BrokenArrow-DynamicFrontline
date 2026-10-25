# Матрица Pass04

Verified Fragment означает конкретный статический фрагмент. Для любого подключения к реальной игре нужны отдельные runtime-проверки; сейчас их нет.

| Формула | Уровень | Статус | Реализация / ограничение | Runtime перед подключением |
|---|---|---|---|---|
| P4-F01 | B | Implemented partially | ArtilleryDispersionMath.Build; inverse-table порядок точный, LinearTestCurve Experimental | active CDF, tangents/wrap |
| Polar #24225 | B/C | Implemented partially | Polar angle/index/trunc/radius; CosSin только Experimental | trig orientation, RNG lineage |
| P4-F02 | B | Implemented in 0.2.2 | Apply/GenerateAndApply, effectiveRange gate, float lower bound | geometry/caller/trajectory |
| P4-F03 | B | Implemented in 0.2.2 | FastDimond, trunc и equal-bound consumption | caller RNG consistency |
| P4-F04 | B | Already implemented | CyclicRandom CF18 сохранён; consumption counter добавлен | native PRNG,seed/order,reconnect |
| P4-F05 | A | Implemented in 0.2.2 | NavigationMath.AstarPriority; single sum,double constants | graph costs/ownership,не новый solver |
| P4-F06 | B | Experimental | LastKnownNavigationFragment — только внутренний numeric diagnostic | team-safe API всё ещё Unknown |
| P4-F07 | B | Implemented in 0.2.2 | AircraftMath yaw/pitch interpolation | active presets/units/caller |
| P4-F08 | B | Experimental | H1FiniteResearch finite rational; не AtanNative | CPU FMA/ULP/NaN/flags и CF19 composition |
| P4-F09 | A | Implemented in 0.2.2 | GetSingleSphereSmokeInterval finite branch | near-zero/special floats,game coordinate contract |
| Buff order #24106/9/10 | B | Implemented partially | ten accumulators; stress provider supplied; critical/apply diagnostic | actual stress inputs/producers,command side effects |
| Version-aware identity | E на основе B | Experimental | отдельная IdentityValidation; GameUnitIdentityResolver сохранён | world/id/version/session live feed |
| Multi-smoke resolver | C | Research only | P3-F07 принимает заранее разрешённые интервалы | containment/opacity branches |
| Full Seeker/CF19 | C | Research only | ограниченные прежние fragments сохранены | полные guards/state/trajectory |

Ссылки Pass01–03 сохранены в `../core_refinement_021/FORMULA_IMPLEMENTATION_MATRIX.md`; прежние статусы не переписаны задним числом. Source cards: `research/combat_systems_audit/pass_04/COMBAT_FORMULAS_PASS04.md`, тематические отчёты; hashes в `evidence/core022/research-source-hashes.json`.
