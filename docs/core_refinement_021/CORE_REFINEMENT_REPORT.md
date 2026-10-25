# Core Refinement 0.2.1

25 октября 2026. Реализована автономная библиотека боевой математики и CLI Formula Explorer для .NET 6. База — PoC02 `cfe1b59`; отдельная ветка `feature/core-refinement-021`, worktree `D:/!Razrabotki/Broken Arrow Dynamic Frontline/core-refinement-021`. Merge не выполнялся.

## Фактический результат

Существующие F01–F09/F13 сохранены без изменения тел методов. CombatFormulas стал partial и дополнен F10–F12. Новые классы VerifiedCombat реализуют AoE, CF12/CF13, выбранный ammo container, critical attempts/selection/table modifiers, helicopter shift/time, requested fuel, численные фрагменты FOW и smoke. ExperimentalCombat содержит реконструкцию CF18, ограниченные gates Seeker и диагностическую оценку боеспособности. Эти функции не вызываются территориальным tick, DesertionController или игровым адаптером.

Функции возвращают значения, не выполняют ECS-команд. Расчётные результаты — readonly records; выбор критов возвращает доступную только для чтения трассу переходов. Domain guards вынесены в FormulaDomain; ограничения поддерживаемой области не выдаются за native guards. Никаких Unity, MelonLoader, игровых DLL или сетевых ссылок у Core и Explorer нет. Тестовый проект использует прежний integration fixture только для проверки совместимости.

Пройдено **184 прежних + 58 новых групп = 242**, в Debug и Release новые группы проходят одинаково. Дополнительно 5000 независимых Python double эталонов, 2500 seeded итераций и 20 пар интеграционных шагов с вызовами/без вызовов новой математики. Это не сравнение с runtime Broken Arrow. [Результаты](TEST_RESULTS_CORE_021.md).

14 микробенчмарков Debug/Release; повторены 24 сценария старого и нового integration runner. Контрольный Release: старая сборка сейчас 2,301 ms, новая 2,246 ms, около 3,276 MB/tick у обеих. Небольшая разница не трактуется как ускорение. Первое измерение отдельным micro runner было медленнее и сохранено в отчёте. [Замеры](BENCHMARKS_CORE_021.md).

## Воспроизведение

```powershell
Set-Location 'D:\!Razrabotki\Broken Arrow Dynamic Frontline\core-refinement-021'
.\build-core021.ps1
$dotnet='C:\Users\pl-ni\Documents\ChatGPT\ба\DynamicFrontline\tools\dotnet\dotnet.exe'
& $dotnet artifacts/core021/assemblies/DynamicFrontline.Combat.Explorer.dll config/core021/explorer-examples.json
```

SDK 8.0.426 и runtime 6.0.36 используются из уже существующего toolchain. Локальный NuGet feed скопирован в собственный ignored tools/feed. На другом компьютере нужно указать свой путь pinned dotnet в двух build/test scripts и подготовить feed по исходному bootstrap; установленная игра для сборки не нужна.

Explorer принимает JSON-массив `{Formula,Profile,Parameters}`, выдаёт отдельный результат каждого набора с входами, provenance, источником, уровнем A/B, ограничениями и доступными промежуточными значениями. Примеры: `config/core021/explorer-examples.json`; сохранённый результат: `evidence/core021/explorer-results.json`. Вектор — массив `[x,y,z]`; сложные records — JSON-объекты с именами свойств C#. Ошибка входа выдаётся JSON в stderr и exit code 2. Частичный массив успешных результатов при ошибке не печатается.

`prepare-core021-profiles.py` воспроизводит четыре исследовательских профиля из сохранённых evidence; `generate-core021-reference.py` — численные эталоны. Они не читают активную базу игры. `report-core021.py` собирает тестовые/benchmark отчёты из завершённых прогонов; для сравнительных таблиц нужны сохранённые baseline-run/current-run результаты. Старые evidence не перезаписываются.

## Сохранность и граница готовности

Игра не запускалась и не устанавливалась. Состав, размер и mtime всех файлов игры совпали с исследовательским baseline; SHA256 шести контрольных файлов совпали. Это не полный hash всех файлов. [Проверка](../../evidence/core021/game-preservation.json). Рабочие деревья старых прототипов и Pass04 не изменялись; исследовательские материалы использовались только для чтения.

Не реализованы полный damage pipeline, баллистический CF19 solver, полная SeekerSystem, artillery dispersion CDF, сетевой code15 и игровые hooks. Блокеры и точные границы перечислены в [UNRESOLVED_CORE_021.md](UNRESOLVED_CORE_021.md). Следующий игровой этап PoC03 не выполнялся.

Этапы A–H сохранены отдельными проверенными коммитами: b741739, 46839b5, 23f4098, 9cd7164, df274ee, b55ca54, 1c1b4c7, dba9451. Финальная документация — отдельный этап I.
