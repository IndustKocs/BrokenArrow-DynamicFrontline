# Dynamic Frontline — Game Integration PoC 0.2

25 октября 2026. Разработаны интеграционные контракты, read-only инспектор, нормализация снимков, профили, команды, координаты, карта, кэш навигации, simulation bridge, безопасная командная диагностика и отдельная MelonLoader-сборка. **Это программно собранный и автономно проверенный прототип, не подтверждённая интеграция с работающей Broken Arrow.** Игра не запускалась, DLL в её каталог не устанавливались.

## Размещение и история

Worktree: `D:/!Razrabotki/Broken Arrow Dynamic Frontline/game-integration-poc02`. Ветка: `feature/game-integration-poc02`, база `9a4dcb1`. Исходный worktree `frontline-connectivity` и его ветка не изменялись. Новый независимый репозиторий не создавался. Этапы зафиксированы отдельными коммитами: архитектура A; совместная работа контрактов B–F с 80 новыми тестами; оптимизация G; отдельный loader H; уточнения по опубликованному Pass 03.

Старые 36+59 тестов исполняются без ослабления проверок, но с рабочим каталогом `artifacts/poc02/legacy`. Поэтому предыдущие отчёты, evidence и результаты PoC 0.1 не перезаписываются. Все текущие исходники и build-script находятся в новой ветке.

## Реализация

| Уровень | Компоненты | Проверенный объём |
|---|---|---|
| A. Доступ | ReadOnlyComponentInspector, ReflectionGameDataAccess, IGameObjectFeed | Чтение перечисленных статических полей/методов на mock ECS; отсутствуют setters и команды записи |
| B. DTO | BrokenArrowUnitSnapshotProvider, GameUnitIdentityResolver, GameTeamResolver, GameUnitProfileResolver, CoordinateTransform, GameTerritoryMapProvider | Полные атомарно валидируемые снимки, явный lifecycle, 5v5, базы сценариев, симметричная карта |
| Навигация | GameNavigationAdapter, NavigationConnectivityAdapter, NativeNavigationStatusPolicy | Каналы, поколения, позиции, маски, ревизии, бюджеты, TTL, тайм-ауты, запоздалые ответы; Unknown не равен Blocked |
| C. Ядро | Существующая FrontlineSimulation | Алгоритмы сохранены; добавлены обратно совместимые точки расширения для локальных областей, техники, защищённых ячеек и identity-aware геометрии |
| D. Представление | TeamSafeProjection, GameVisibilityAdapter, FrontlineDiagnosticsService | Отдельный минимальный DTO без вражеских данных, графа, изоляции и скрыто-зависимых счётчиков |
| Цикл | FrontlineSimulationBridge, GameIntegrationHealthMonitor | Фиксированный период, ревизии входа, сохранение последнего результата при ошибке, сброс сессии при смене контекста |
| Loader | DynamicFrontline.Integration.MelonLoader.dll | Компиляция против настоящего MelonLoader 0.7.3; запуск и реальные IL2CPP-обёртки не проверены |

Четыре настройки loader по умолчанию false: EnableGameDataCollection, EnableFrontlineSimulation, EnableDebugExport, EnableGameStateModification. Последняя отклоняется, если включена; реализации изменения игры нет. AttachReadOnlySession требует предоставленных providers и отдельного разрешения read-only runtime; сервисы игры автоматически не угадываются.

## Уточнения Pass 03

Во время разработки появился `research/combat_systems_audit/pass_03/FRONTLINE_DEVELOPMENT_HANDOFF.md`. Контракты сверены: добавлены GameDeathState, provenance/возраст позиции; неизвестный death lifecycle активной сущности отклоняет снимок. Для ненаправленного графа поддержки проверяются оба направления маршрута. NotFull/NotFoundStart/NotFoundEnd, EntryPossible/ExitPossible и ненулевой NavPath не означают достижимость. NavigationRevision — ревизия, предоставляемая адаптером; штатная версия игрового графа не обнаружена.

Наблюдаемые позиции из spatial tree могут отставать; исчезновение из visibility-driven дерева не является доказательством despawn. Поэтому такой tree нельзя объявить полным census без отдельного контракта. Точные источники и незакрытые условия перечислены в [INTEGRATION_BLOCKERS.md](INTEGRATION_BLOCKERS.md).

## Проверки и запуск

Результаты функциональных прогонов: [TEST_RESULTS_POC02.md](TEST_RESULTS_POC02.md). Нагрузка, отдельные стадии и сравнение до/после оптимизации: [BENCHMARKS_POC02.md](BENCHMARKS_POC02.md). Первая оптимизация почти вдвое сократила выделения интеграционного цикла в контрольном сценарии; финальная стоимость включает дополнительные проверки направленных маршрутов. Накладные расходы полного слоя остаются выше одного ядра PoC 0.1.

```powershell
Set-Location 'D:\!Razrabotki\Broken Arrow Dynamic Frontline\game-integration-poc02'
.\build-poc02.ps1 -DotnetPath 'C:\Users\pl-ni\Documents\ChatGPT\ба\DynamicFrontline\tools\dotnet\dotnet.exe'
```

Скрипт собирает старые компоненты, новое Integration и отдельный loader; запускает все старые и новые тесты, создаёт диагностические JSON/HTML и нагрузочные отчёты. SDK 8.0.426, целевая среда и тесты .NET 6.0.36. Локальные feed/ссылки MelonLoader скопированы в tools; при переносе доступны исходный bootstrap и toolchain.lock. Результаты находятся только в `artifacts/poc02`, проверенные evidence — `evidence/poc02`.

Две HTML-страницы используют исходный renderer PoC 0.1: `frontline-diagnostic-poc02.html` содержит полные **Synthetic** кадры адаптера и metadata; `team-safe-diagnostic.html` содержит только свои позиции/состояния. Developer Debug не допускает экспорт LiveReadOnly даже при включённом флаге. RecordedFixture поддерживается отдельным источником и не переименовывается в live.

## Граница готовности

Нет подтверждённого runtime census, registry lifecycle/NetID, active database provider, согласованной фазы чтения ECS, рабочего backend NavigationGraph и установленного сетевого авторитета. Имеются интерфейсы, действующий reflection-inspector и автономные реализации; это не скрытые заглушки, заявленные как работающая игра. Сборка loader сама по себе не обнаруживает матч и не начинает наблюдение. Следующий этап требует отдельного разрешения и выполнения [RUNTIME_TEST_PLAN.md](RUNTIME_TEST_PLAN.md).

## Финальная проверка

Пройдено 89 новых + 95 прежних тестов, 24 нагрузочные комбинации. Обе HTML-страницы проверены в браузере: шкала кадров и переключатель графа работают, ошибок JavaScript не зарегистрировано; own-only страница показывает только четыре собственных подразделения. Скриншот: `artifacts/poc02/diagnostic-preview.png`.

Проверка сохранности 25 октября 2026: состав всех игровых файлов, размеры и mtime совпали с research baseline; SHA256 шести контрольных файлов совпали. Полный hash каждого файла не выполнялся. Результат: [evidence/poc02/game-preservation.json](evidence/poc02/game-preservation.json). Исходный worktree чист; Core, прежние 95 тестов и прежний benchmark evidence не изменены относительно 9a4dcb1. Для повторной проверки: `python verify-preservation-poc02.py --baseline "C:/Users/pl-ni/Documents/ChatGPT/ба/research/evidence"`, затем `python report-poc02.py`.
