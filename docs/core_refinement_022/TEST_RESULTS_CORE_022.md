# Тесты Core 0.2.2

До изменений воспроизведены 242 baseline проверки. После изменений те же 184+58 проходят без изменения исходных assertions. Новых групп: 42, ошибок: 0; новые группы проходят Release и Debug. Всего 284 групп. Дополнительно 3000 независимых Python reference cases, 12 монотонных synthetic curves и 20 пар интеграционных шагов.

Python не вызывает C# или GameAssembly. Эталоны: формулы Pass04, явное single rounding суммы A*, double rational H1 и простой finite smoke domain. Допуск обычно2e-5, A*1e-12, H1 1e-7; identity/RNG/bounds проверяются точно. Это не runtime/native differential testing.

Парный integration fixture сравнивает territory, полный frame (кроме измерительных Metrics), командную проекцию и profiles. Включён наблюдаемый Deserting в нескольких шагах. Старые controller tests отдельно подтверждают дезертирство. Новые функции не вызывают команды или адаптер.

| Группа | Результат |
|---|---|
| CDF identity length keys endpoints | PASS |
| CDF nonlinear monotone synthetic and repeatability | PASS |
| CDF invalid strict monotonic rejects | PASS |
| CDF explicit version cache and immutable table | PASS |
| Polar ordered draws and truncation | PASS |
| Polar endpoint1000 explicit failure consumes both draws | PASS |
| FastDimond exact edge samples | PASS |
| FastDimond fractional band not ideal diamond | PASS |
| FastDimond CyclicRandom endpoint does not consume second | PASS |
| Trajectory scale direction and native lower bound | PASS |
| Zero effective range no draw and degenerate reject | PASS |
| Buff reset ten accumulators order and final | PASS |
| Buff assignment differs from multiply and reversed order | PASS |
| Buff zero crit no added effect for identity zero rows | PASS |
| Buff invalid table and stress provider reject | PASS |
| Buff potential movement review no commands | PASS |
| Single sphere six control intervals | PASS |
| Smoke strict inside and endpoint surface | PASS |
| Smoke magnitude projection not signed clipping | PASS |
| Smoke zero radius tangent and unsupported zero ray | PASS |
| Single sphere does not resolve multiple intervals | PASS |
| Aircraft yaw two segments and max equality | PASS |
| Aircraft pitch independent rates | PASS |
| Aircraft denominator threshold zero equal presets | PASS |
| Astar weighted priority example and single addition | PASS |
| Navigation consumer valid not adapter proof | PASS |
| CyclicRandom upper endpoint wrap counts no-draw | PASS |
| CyclicRandom different initial indices | PASS |
| Entity version reuse rejects old handle | PASS |
| Entity stale snapshot world session and owner | PASS |
| Lifecycle death remove fog and spatial distinct from disposal | PASS |
| LastKnown diagnostic squared XZ boundaries no contact | PASS |
| H1 finite experimental boundaries and invalid domain | PASS |
| Explorer new metadata intermediates bounded CDF output | PASS |
| Explorer full table opt-in and stageful error | PASS |
| Explorer Polar FastDimond and identity support | PASS |
| 3000 independent Python reference computations | PASS |
| CDF valid monotone property across 12 synthetic curves | PASS |
| Pass04 twenty paired integration steps preserve all public and core outputs | PASS |
| Identity Explorer serialization preserves lifecycle distinctions | PASS |
| RNG finite domain errors and reproducible sequences | PASS |
| Core assembly remains independent of game libraries | PASS |
