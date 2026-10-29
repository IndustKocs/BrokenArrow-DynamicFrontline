# Dynamic Frontline PoC 0.3 RC2 — диагностическая поставка ТЗ №17

Ветка: `feature/poc030-rc2-two-client-integration`; продолжение RC1 `7251635` и checkpoints `56411ec`, `11199c2`. Игра, загрузчик и матчи **не запускались**, установка в настоящую папку игры **не выполнялась**. Launcher и исследовательские материалы не изменялись. Native writers отсутствуют; разрешение в конфигурации не может включить эффекты.

## Назначение

Подготовлен путь: собственные игровые ECS-наблюдения → сверка native FightId/Seed/сценария/участников → TLS/HMAC full/delta → канонический мир → существующий `Simulation023` в Observe lane → собственная диагностическая проекция/HUD. Для независимого пересчёта на B оба участника явно выбирают `TrustedDiagnosticReplica`. В `TeamSafe` B получает только свою проекцию; независимый пересчёт скрытого мира в этом режиме не заявляется.

Это ограниченный диагностический мод для Airport / Airport_network, закрытого PvP двух людей на противоположных командах. `RuntimeValidated=false` для всех игровых возможностей. Первое разрешённое использование — **оба клиента LoadReadProbe**, затем, после проверки источников, **оба ObserveTwoClient**. Разрешение на установку и запуск ещё не получено.

## Матрица требований

| Требование | Реализовано и проверяется offline | Оставшееся подтверждение |
|---|---|---|
| R1, mutable config | Настройки и секреты сохраняются; RC1 receipt мигрирует; изменённые чужие файлы не удаляются | Выбор реальных адресов и путей на двух ПК |
| R2, транзакции | WAL, staged writes, atomic receipt, recovery после fail-fast; общий кэш имеет отдельное владение и backup; старые имена кэша выводятся из эксплуатации и возвращаются при remove | Физическое отключение питания не имитировалось; runtime загрузчика NotRun |
| R4, Interop | Свежий Cpp2IL и генерация с точными Unity dependencies; 198 DLL + 2 базы; 30 групп metadata bindings; хеши всего кэша, runtime dependencies, loaded assemblies | Реальный вызов IL2CPP generic/ref-return getter и readonly Harmony hooks |
| R3, source health | Ревизия/возраст источника отдельно от heartbeat; атомарная пара snapshot/health; пауза и staggered sampling не создают фиктивные тики | Проверить реальную фазу игрового callback и полноту owner roster |
| Совместимость | Четыре pinned игровых файла, Steam build 26273833, Unity 6000.3.24f1, metadata39; payload/rules/map/profiles/ABI в handshake | Повторить preflight на ПК B; loaded verification на обоих |
| R5, ядро/HUD | Реальный reader подключён к каноническому worker и Simulation023; actual HP/stress/level отделены от forecast; автономный stress backend в Observe не вызывается | Игровая трасса A/B и HUD NotRun |
| R6, общий мир | Full/delta, поколения, исчезновение из полного roster, смена owner/entity version, idempotence, checkpoint, ACK/digest, reconnect; восстановление активного tau | Реальный сетевой матч, повторное подключение и сверка логов обоих ПК |
| Карта | Реальные Airport.ng и Airport_network.bascr, zone55, реальные наземные roots, conservative native-edge quotient, 50/100м; runtime bounds/scenario checks | Активная зона и команды в матче; dynamic navigation не подтверждена |
| Профили | 18 явных кандидатов; active Role/S0/threshold checks; capture только разрешённым 30/31; 710/711/713 и нулевой S0 отвергаются | Наличие в реальных колодах и активной базе обоих клиентов |
| Снабжение | Пассивные positive Repair/AddAmmo операции, причинная связь внутри native вызова, owner/local-generation binding, bounded history, дедупликация и resync | Полнота callback-покрытия и длительность положительных portions; дискретные точки **не дают** непрерывный интервал/grace |
| Геометрия | Два раздельных pocket certificates, независимый concave-polygon oracle с поворотами/зеркалами/50–100м, старые ring/bridge/gap cases; breakthrough использует постоянную территорию | Сопоставление с реальными проходами и изменениями навигации Airport |
| R7 | Полный copied-owner→canonical→wire codec→core/digests→projection benchmark, raw samples, stages, GC/memory; отдельные длительные прогоны | Main-thread native reads, HUD, сеть между ПК и live soak NotRun; инженерный allocation target не объявлен достигнутым |
| R8 | Фактические очищенные журналы, результаты, команды, collector/compare/replay, package SHA256 inventory | Собрать реальные журналы после отдельно разрешённой пробы |

## Результаты и ограничения

- 356 прежних регрессионных проверок и 44/44 RC1 проходят; 47 групп RC2 проходят. Включены реальные дочерние процессы с fail-fast на 12 границах install и дополнительные rollback/cache/service/geometry/replay проверки.
- Два отдельных процесса с loopback TLS восстанавливают checkpoint после принудительного disconnect и получают одинаковые канонические результаты. Это **не** два клиента Broken Arrow.
- Оба полных локальных клиентских install manifest, включая свежий interop, прошли install/verify/remove в sandbox; прежний кэш восстановлен.
- Полный расчёт имеет явный лимит по умолчанию **100 суммарных юнитов**. Превышение не превращается в усечённый «полный» мир. Измерения 500/1000 — характеристика нагрузки, не автоматический допуск такого матча.
- Завершены 18 основных и 3 дополнительных event-soak прогона, всего **80 160 шагов**. Дополнительный стенд проверяет нагрузку событий на явно синтетической карте, не заменяет геометрию Airport. Цель 0,5 MiB/шаг не достигнута из-за копирования/JSON/канонических объектов; применён объявленный ограниченный диагностический режим, допускаемый ТЗ.
- Показатели длительных измерений и границы scope находятся в `PERFORMANCE_RC2.md` и фактических JSON/CSV.GZ, включённых в evidence. Неизмеренные native/TLS/HUD задержки не приравниваются к нулю.
- `SupplyComplete=false` и `VictoryComplete=false` сохраняются в живом canonical lane. Можно проверять фронт/ZOC/геометрический прогноз и actual stress; полноценное подтверждение штрафа с реальным обслуживанием не заявляется.
- Нет штатного visibility feed: публичные круги показывают только собственные юниты. Даже видимый противник не рисуется до отдельной проверки видимости; скрытый контур не раскрывается.
- Статический map descriptor не доказывает актуальность динамических препятствий. HUD явно отмечает этот предел. Прогноз не изменяет игру, а недостаточные геометрические/снабженческие данные не дают native штрафа.

## Что действительно требует работающей игры

1. Loaded assembly hashes и IL2CPP generic/ref-return чтение без исключений.
2. Полнота owner roster, FightId/participants/team roots и стабильность callback-фазы.
3. Реальные активные профили и transport/death/despawn transitions.
4. Runtime ScenarioMeta.Name, zone55 bounds и навигация; покрытия dynamic topology недостаточно для общего заявления о всех участках.
5. Безопасная работа readonly Harmony observer и положительная причинная операция; непрерывные интервалы не угадываются по соседним точкам.
6. Реальные A/B checksum traces, pause/disconnect/resync, собственный HUD, main-thread budgets и live soak.

Для этого подготовлен runbook. Никакой из пунктов не отмечен выполненным по компиляции, metadata inspection или синтетическому replay. Возможные несовпадения реальных bindings требуют исправления по журналам пробы; заранее гарантировать их отсутствие нельзя.

## Передача и откат

Приватный ZIP содержит собственные DLL, CLI, конфигурацию проекта, derived map descriptors, инструкции и фактическое очищенное evidence. GameAssembly, metadata, сгенерированные игровые wrappers, Unity DLL, loader binaries, ключи, PFX и расшифрованные сценарии не распространяются. На каждом ПК interop создаётся локально по pinned исходным файлам.

Архив-кандидат распакован: его собственный CLI, полный локальный interop, install/verify/remove для A/B, возврат старого кэша и replay прошли. Упакованный interop-driver отдельно подтвердил 30 metadata contracts без включения сторонних DLL в архив. Финальная поставка содержит те же DLL; последующая упаковка меняет документацию/evidence/build record.

Точные состав/хеши ZIP — в package inventory и внешнем `.sha256`. Исходная ревизия и хеши собранных DLL — в build record. Для отката использовать `recover`, затем `remove` из RC2 CLI при закрытой игре; не удалять каталоги Mods/UserData/MelonLoader целиком. Подробности — `INSTALL_RECOVERY_AND_CONFIG.md`.
