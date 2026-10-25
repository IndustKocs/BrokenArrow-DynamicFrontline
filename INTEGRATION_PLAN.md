# План интеграции с Broken Arrow

## Границы текущей реализации

Вся новая логика находится в отдельной сборке `DynamicFrontline.Frontline`, зависящей от Core. Она не ссылается на игровые библиотеки и не загружается существующим MelonLoader-модом автоматически. Core/DesertionController/CombatFormulas и исходный GameAdapter не изменены. Глобальный вход FrontlineSimulation — доверенный авторитетный снимок двух команд; локальный клиентский reader, читающий только свои юниты, не может служить полноценным источником такого снимка.

## Исследовательские основания

Использованы существующие REPORT.md, SYMBOLS.md, full_code_audit/FORMULAS.md, combat_systems_audit/ и mod_comparison/COMPARISON.md исходной исследовательской папки. Дополнительно прочитаны сигнатуры в `research/signatures/DiffableCs/BrokenArrow/BrokenArrow/`:

| Источник | Назначение будущего адаптера | Что не следует предполагать |
|---|---|---|
| Shared/Ecs/Components/UnitComponent.cs | UnitData и Owner/OriginalOwner: Unit ID, владелец, перевод в Team ID | Owner UID не равен индексу команды |
| Client/Ecs/Infantry/Components/InfantryUnitComponent.cs | AliveSoldierCount | HP и CurrentSquadSize не заменяют численность |
| Client/Ecs/BattleSystem/Components/StressComponent.cs | CurrentStressLevel, CurrentStressValue и реальные пороги | Коэффициенты тестов не являются балансом любого матча |
| MissionEditor/Systems/ObjectiveZoneSystem в SYMBOLS.md | CaptureZone, ChangeObjectiveOwner и существующий UnitsPositionsTree как исследовательские ориентиры | Новая сетка не равна штатным ObjectiveZone и не должна автоматически менять доход |
| Client/Ecs/Spawn/Components/SpawnerComponent.cs | OwnTeam, SpawnPointData/LinkScript для проверки статичных корней | Фронт не перемещает спавнеры |
| Client/Ecs/Spawn/SpawnUtils.cs | Список/выбор спавнеров как источник дальнейшего исследования | TryGetBestSpawner не вызывается для динамической смены корня |
| Client/Ecs/Navigation/Systems/NavMeshPathfindingSystem.cs | PlayableZone, навигационные маски и поиск маршрута | Straight-line GridLinkGeometry не доказывает наличие игрового пути |
| Client/Ecs/FogOfWar/Components/FogOfWarComponent.cs | VisibleTo и другие контексты обнаружения для политики knowledge | WasDetectedOnce не означает право выдавать точные актуальные координаты |

Файлы DiffableCs содержат исследовательские сигнатуры, а не исполняемые обёртки. Эти поля не читались в живом матче. Новые CF-формулы исследования не нужны для территориальной силы: урон, бронепробитие и повторное начисление стресса намеренно не смешиваются с P.

## Последовательность следующего этапа

1. Утвердить источник авторитетной симуляции PvP после исследования сети. Ни клиент-владелец, ни предполагаемый хост не назначены автоматически.
2. Получить проверенные IL2CPP-обёртки в отдельно разрешённой тестовой среде. Реализовать provider Match ID, стабильного UID/Generation, Team ID и полной lifecycle-информации.
3. Подготовить явный каталог реальных Unit ID. Значения 1–6 в example JSON исключительно синтетические. Запретить догадки по названию, цене или общей категории Infantry.
4. Нормализовать игровые позиции X/Z в Point2, маску PlayableZone и статичные исходные корни. Маска контроля и физическая навигационная маска могут отличаться; это нужно сохранить в адаптере.
5. Передавать AliveSoldierCount, LoadedComponent, фактический StressLevel и DesertionStateAdapter. Состояния контроллера считывать в согласованной фазе основного потока до расчётного тика.
6. Вызывать Step с последовательными тиками 0,1,2… и фиксированным TickMilliseconds. Пропуск времени обрабатывается несколькими фиксированными шагами, а не увеличением dt внутри одного вызова. Смена правил требует новой версии/сессии.
7. Передавать ILinkGeometry, IRetreatAccess и ILogisticsConnectivity независимо. Unknown route сохранять до достоверного ответа. Реальная логистика пока отсутствует.
8. Потреблять IsolationBatch через будущий IIsolationEffectSink. Решить, какие модификаторы допустимы и как они складываются со StressComponent/BuffDebuffSystem. Не записывать стресс повторно по F03/F05.
9. Утвердить правила TeamKnowledge: какие клетки публичны, нужна ли задержка и что означает наблюдаемая граница. По умолчанию DenyTerritoryKnowledge не отдаёт клетки и пути. Если будет задержка, хранить отдельные исторические снимки; передача старого tick при свежих координатах недопустима.
10. Только после этого добавить сетевой транспорт с аутентификацией отправителя/команды, последовательностью, ограничением размеров пакетов, полным resync и тестами разрывов. Сейчас нет Steam-лобби и сетевых hooks.

## DTO и репликация

SnapshotHeader содержит MatchId, RulesVersion, Revision и Tick. UnitKey из Core сохраняет UID и Generation. Полный TerritoryStateSnapshot содержит геометрию карты и ячейки; TerritoryDelta — BaseRevision и изменённые ячейки. TerritoryReplica отклоняет чужой матч, другую версию правил, дубли и нарушение последовательности. Это проверка формата/порядка доверенных данных, не криптографическая аутентификация.

FrontlineSnapshot содержит геометрию, OperationalStateSnapshot — узлы/рёбра, IsolationBatch — признаки и таймеры. AuthoritativeFrame используется только внутри доверенной симуляции и диагностического инструмента. Нельзя сериализовать его в публичный пакет: он содержит полные позиции обеих команд.

TeamPresentationFilter исключает противников из FriendlyUnits, Nodes, Edges, LocalAreas и Isolation. Рёбра допускаются только между узлами своей команды; номера компонентов перенумеровываются в локальный список. ValidateRecipient сверяет адресата, Match ID и версию. Конкретную команду следует получать из аутентифицированного контекста, а не из поля, присланного клиентом. Вмешательство разведчиков не создаёт территориальных дельт.

HTML-диагностика содержит синтетические данные обеих команд, чтобы проверять алгоритм. Она не является безопасным PvP-интерфейсом. Никакие данные установленной игры не отправляются по сети.
