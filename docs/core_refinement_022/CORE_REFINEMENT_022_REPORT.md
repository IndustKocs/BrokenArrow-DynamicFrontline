# Core Refinement 0.2.2 — Pass04

25 октября 2026. Автономное расширение существующего Core021 завершено в отдельном worktree `D:/!Razrabotki/Broken Arrow Dynamic Frontline/core-refinement-022`, ветка `feature/core-refinement-022-pass04`, база `34ec3be`. Это статически обоснованные вычислительные фрагменты и явно обозначенные экспериментальные модели, не runtime-интеграция Broken Arrow.

## Реализовано

- ArtilleryDispersionMath:10001 inverse keys,1000 samples, строгая валидация CDF, интерфейсы evaluator/factory, immutable table, ограниченный version/content cache, подтверждённая часть Polar, FastDimond и trajectory deviation.
- BuffEvaluationPipeline:10 накопителей, reset→supplied stress→существующий CriticalMath→final diagnostic. Сохранены multiplication скорости движения и assignment вращения.
- VisibilityMath: конечный интервал одной сферы с magnitude projection. Прежние Sphere/Smoke helpers не переписаны.
- AircraftMath: yaw/pitch двухучастковая интерполяция с denominator≤.001f→t0. Прежние helicopter/fuel функции сохранены.
- CyclicRandom проверен и дополнен только счётчиком потребления/IFloatRandom; нового конкурирующего RNG нет.
- NavigationMath: weighted A* и consumer-specific Complete/Line. Existing navigation adapter policy не изменена.
- Experimental: version-aware identity proposal, внутренний LastKnown numeric gate без экспорта координат, finite H1 rational transcription. Последнее не подключено к CF19.
- Расширен существующий CLI Explorer: evidence/method/domain/equivalence metadata, CDF opt-in arrays, staged errors. Новый CLI не создавался.

До изменений воспроизведены242 проверки. После изменений **242 прежние +42 новые =284 группы**, новые проходят Release/Debug. Дополнительно3000 независимых Python reference cases,12 monotone synthetic curves и20 парных integration steps. Территория, граф, окружение, observed desertion, profiles, tick и TeamSafeProjection совпадают. [Тесты](TEST_RESULTS_CORE_022.md).

Измерены11 microbenchmarks Debug/Release и24 интеграционных сценария на одном harness с двумя наборами DLL. Для256²/1000/250: median old2.014/new2.119ms, p99 old22.189/new19.800ms, allocations≈3.276MB/tick. Малые изменения не трактуются как ускорение; единичный прогон не доказывает значимую регрессию. GC0/1/2, p95/p99 и working set сохранены. [Benchmarks](BENCHMARKS_CORE_022.md).

## Воспроизведение и сборки

```powershell
Set-Location 'D:\!Razrabotki\Broken Arrow Dynamic Frontline\core-refinement-022'
.\build-core022.ps1
$dotnet='C:\Users\pl-ni\Documents\ChatGPT\ба\DynamicFrontline\tools\dotnet\dotnet.exe'
& $dotnet artifacts/core022/assemblies/DynamicFrontline.Combat.Explorer.dll config/core022/explorer-examples.json
```

`-SkipBenchmarks` пропускает измерения, сохраняя regression/tests/Explorer build. `benchmark-core022.ps1` использует DLL соседнего Core021 только для чтения и копирует их в собственный artifacts/core022/probe-baseline; current — отдельная папка. Сам LoadProbe binary одинаковый, hashes библиотек записаны. SDK8.0.426/runtime6.0.36 и local feed используются из существующего toolchain; пути dotnet в scripts нужно заменить при переносе. Игровые DLL для сборки не нужны.

`generate-core022-reference.py` воспроизводит3000 независимых fixtures. `report-core022.py` собирает отчёты из завершённых прогонов и фиксирует SHA256 использованных research sources. `artifacts/core022/assemblies` содержит Core и Explorer DLL, deps/runtimeconfig/PDB; hashes в evidence/core022/build-hashes.json. Старые evidence/core021 и evidence/poc02 не перезаписаны.

## Сохранность и ограничения

Игра не запускалась, MelonLoader не устанавливался/не включался. Состав всех игровых файлов, размеры и mtime совпали с research baseline; SHA256 шести контрольных файлов совпали. Это не полный hash каждого файла. [Результат](../../evidence/core022/game-preservation.json). Старые worktrees чисты; Pass04 и прежние отчёты не редактировались. Merge отсутствует.

Core/Explorer не имеют Unity, MelonLoader или игровых assembly references. GameAdapter, Integration, Frontline и прежние tests не изменены. Новая математика не встроена в территориальный tick. Статические A/B не означают проверенные API игры.

Не восстановлены Unity AnimationCurve tangents/wrap, trig orientation Polar, active CDF/profile, native RNG equivalence, multi-smoke overlap/opacity, полный stress producer pipeline, complete missile state machine, full CF19/native H1 special semantics, server authority и live identity feed. [Матрица](FORMULA_IMPLEMENTATION_MATRIX_PASS04.md), [блокеры](UNRESOLVED_CORE_022.md), [handoff](INTEGRATION_HANDOFF_CORE_022.md).

Этапы сохранены отдельно: A a9a443b, B5698ee1, Ced0a146, Dd5dd12b, E3153064, F69f2399, G1fd25b0, H640e9d5, I73848a9; документация — этап J.
