# Блокеры настоящего подключения

StaticConfirmed означает наличие сигнатуры по исследованию, не доказанную семантику работающей игры. RuntimeConfirmed API в PoC 0.2 отсутствуют. Синтетические тесты не повышают этот уровень.

| Область | Доказательство / статус | Что требуется |
|---|---|---|
| UnitComponent.Owner/UnitData, Units.Id/Role | StaticConfirmed Pass 02 | Проверить wrapper, чтение и активный сценарий |
| Health.CurrentHealth/MaxHealth/PotentialDead, Infantry.AliveSoldierCount, Stress | StaticConfirmed | Валидировать смысл и согласованную фазу |
| LuaUnit.GetPosition/Entity/UID | StaticConfirmed | Связь с canonical identity, точность и свежесть позиции |
| NetEntity/RootEntity и NetID | Частично установлены | Registry generation, reuse и reconnect; NetID не вечный ключ |
| Полный census | RuntimeRequired | Spatial tree не годится без доказательства: visibility removal и отстающие позиции |
| Death/Dead/RemoteDeath/despawn | StaticConfirmed различия Pass 03 | Проверенный death delegate; Unknown отклоняется |
| Loaded/InfantryLoading, desertion | Поля/части статически известны | Подтвердить lifecycle и источник собственного desertion состояния |
| Player UID/TeamUid/TeamSide | StaticConfirmed | Полный 5v5 roster и момент смены владельца |
| Active database | Приоритет сценария известен | Provider фактической базы, SHA256 и версии; synthetic ID не игровые |
| Маска карты, защищённые клетки, roots | Контракт реализован | Реальный источник геометрии и координат обоих spawn |
| NavigationGraph/CalculatePath/status | StaticConfirmed Pass 03 | Реальный backend, бюджет main thread, направление, маски, cost; engine revision не найден |
| Visibility / contacts | Различия флагов известны | Безопасный team-scoped API не подтверждён; сейчас экспорт только своих |
| Authority / sender permissions | Не установлено | Выбор авторитетного узла, права RPC и reconnect до сетевой интеграции |
| MelonLoader/IL2CPP | Сборка подтверждена | Совместимость реальных wrapper и загрузки только отдельным runtime-тестом |

Основания: `C:/Users/pl-ni/Documents/ChatGPT/ба/research/combat_systems_audit/pass_02` и `pass_03/FRONTLINE_DEVELOPMENT_HANDOFF.md`, CHECKPOINT_A и RESEARCH_STATUS. Исследовательские файлы не изменялись. Новые проверки направлений маршрута, unknown death, ownership transfer и native partial/entry-exit статусов отражены в интеграционных тестах.

Ничто из этого не препятствует автономной сборке, тестам и synthetic диагностике. Эти условия блокируют заявление о рабочей live-интеграции. Готовая DLL не обнаруживает матч автоматически: требуются providers, их runtime-проверка и явное подключение.
