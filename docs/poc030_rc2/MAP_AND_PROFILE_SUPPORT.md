# Airport / Airport_network

## Реальное происхождение

Поддержка привязана к build26273833. Airport.ng SHA256:3C61DE14F71F3B8CE8AE861CBC1208AD5D71949E504E45D444B1B5D12F10A68D. Airport_network.bascr SHA256:5AA1E3D132BBB55880686129C07446FDADB4929C67B6183FD78A9628A55331F1. Plugin повторно проверяет combined source hash по локальным игровым файлам. Изменение сценария/навигации не принимается по одному совпадению GameAssembly.

В локально извлечённом сценарии сеть выбора размера связывает малый состав (<=4 игрока) с zone55. Для двух игроков это кандидат активной зоны, **не runtime-подтверждение**. Runtime читает ScenarioMeta.Name, PlayableZone.PlayZoneBounds и NavigationGraph.Name/CellSize и отвергает несовпадение. Header сценария имеет10 возможных слотов; фактический binding требует ровно2 людей, противоположные native teams1/4, без bots.

Zone55:6500×6500м; origin=(1744.954,1583.005), center=(4994.954,4833.005). Наземные spawn objects11/12 задают roots: team0/native1=(6373.75146,2491.39966), team1/native4=(3036.983,6830.55957). Air spawns не подставлены как ground roots. Модовая территория первоначально получает root cells; это правило мода, не утверждение о native владении.

Airport.ng содержит154056 узлов и593330 взаимных наземных связей после проверки masks. Родная сетка23м. Компилятор использует только Walkable/Road/Forest, исключая Water/Buildings/Jump/прочие неподтверждённые bits. Внутри каждой coarse-cell допускается ровно один связный native компонент; неоднозначные клетки исключаются. Каждое сохранённое ортогональное coarse ребро имеет реальный взаимный native witness. Синтетических мостов/прямоугольной all-passable маски нет.

| Resolution | Cells | Supported | Ambiguous excluded | Edges |
|---|---:|---:|---:|---:|
|50м|16900|16795|86|33112|
|100м|4225|4203|21|8231|

Scoring mask здесь — объявленная экспериментальная площадь доказанной наземной области, а не штатная victory mask. Static Infantry/Ground channels консервативно совпадают; отдельные water/vehicle-only passages не утверждаются. Отсутствующее ребро не доказывает отсутствия native маршрута: для окружения добавлена оптимистическая проверка пропущенных маршрутов. Неподдержанная клетка возвращает Unknown. Погрешность coarse geometry — до размера ячейки, не сантиметровая NavMesh-точность.

Dynamic navigation changes не подтверждены. Native headers/bounds проверяются, но это не полный revision feed всех динамических препятствий. HUD показывает StaticAirportGeometry_DynamicChangesUnvalidated. Probe должен отдельно проверить реальные мосты, narrow passages и изменение доступа. Пока они не покрыты, не считать такие результаты полноценным runtime geometric acceptance.

## Профили

config/poc030-rc2/profiles.json содержит18 кандидатов из UA01 table52739; digest исходной Units.json:CBAA1FF4929827F03FE11B65769FD8E95CDF834F802CDFD25620A0A412D479E4. Каждый runtime sample обязан совпасть по Role/loaded S0 и иметь реальные положительные MaxStress/thresholds. Это проверка активного объекта, а не доверие только бинарникам.

- БТР:1 BTR-82;22 Stryker ICV;29 LAV-25 (weight4).
- БМП:3 BMP-3;15/16 Bradley (weight6).
- Танки:2 M1A2;9 T-72B (weight10; не партнёры600м).
- Capture:57 Mech Riflemen,59 Marines,60 Motopekhota,108 VDV DSh1 — только явно разрешённые roles30/31.
- Без capture:61 Force Recon,116 VDV Spetsnaz (пехота role32, weight0.9).
- Supply:199 MT-LB,228 Ural-5323,242 HEMTT,423 Ural-4320; без capture/ZOC/encirclement penalty в этом ограниченном diagnostic profile.

S0 не подставляется, нулевые/неизвестные профили запрещены;710/711/713 не разрешены. Классовая принадлежность не угадывается по имени: NativePassthrough сохраняет m=q=1 до отдельно подтверждённого mapping. Балансные weights — правила проекта, не native-измерение силы. Наличие ID в колоде и совместимость nations/runtime Buff нужно подтвердить на двух клиентах. Любой другой deployed UnitID делает полный вход неподдержанным, не исчезает молча.

Репродукция derived descriptors: compile-airport.py с локальным игровым графом, локально decoded scenario и compatibility.json. Расшифрованный сценарий и native analysis dump в поставку не входят. Готовые derived descriptors поставляются отдельно от игровых assets; их источник проверяется по хешу на каждом ПК.
