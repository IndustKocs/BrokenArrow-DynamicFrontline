# Baseline PoC 0.2.3 / ТЗ13 редакция 1.2

Основа 9243136, feature/core-refinement-022-pass04; cfe1b59 (PoC 0.2) — предок, merge не нужен. Исходное дерево чистое. Рабочая ветка feature/prototype-023-zoc-encirclement, отдельный worktree.

Воспроизведены 58 Combat + 36 Core + 59 Frontline + 89 Integration + 42 Pass04 = 284 групп, ошибок 0. Логи сохранены в evidence/prototype023. Старые tests/assertions и evidence не заменяются.

Переиспользуются UnitKey, Point2, TerritoryGrid, TerritorialConnectivity, TerritoryCaptureSystem, LocalControlAreaSystem, SpatialIndex, F02–F06, Integration/Explorer и отключённый read-only loader. Legacy FrontlineSimulation сохраняется. Zoc_023/1.2 — отдельный режим внутри существующего Frontline, без OperationalConnectivitySystem в новом цикле. VerifiedCombat и Core не меняются.

Новые слои: ZOC/embedded separators, FSM/supply, in-memory стресс, score projection/ledgers/победа, runner/replay. Источники: ТЗ13 1.2; docs/core_refinement_022; POC02_REPORT; TERRITORIAL_CONTROL/ENCIRCLEMENT_SYSTEM/UNIT_PROFILE_RESOLUTION; research/full_code_audit/FORMULAS.md F02 #24449, F03 #24002, F04 #24003, F05 #24229, F06 #24231; research/combat_systems_audit/pass_04/FRONTLINE_DEVELOPMENT_HANDOFF_PASS04.md. Live roster, identity, network authority и radial ruler остаются Runtime Required.
