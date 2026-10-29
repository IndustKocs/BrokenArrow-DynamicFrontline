# Игровые источники и идентичность

LiveReader вызывается на OnUpdate не чаще4Гц; worker получает только managed immutable records. Entity/Unity references не пересекают очередь worker. Reflection types/members/generic methods кэшируются. Перечисление ограничено10000 посещений/8мс; превышение маркирует snapshot incomplete и останавливает достоверный прогноз, не выдаёт первые N сущностей за полный roster.

Источник: GameController.GameSession/GameContext, native CurrentPlayer UID, NetworkRoomService.State FightId/Seed и GetPlayers roster. Owner gate предшествует чтению position/health/stress/network. Для своих юнитов читаются NetID, Entity WorldId/EntityId/Version, UnitData ID/Role/MaxStress, Transform.Position, HP/MaxHP, actual stress/max/level/thresholds, soldiers, native TeamUid, death/dead, loaded/loading/unloading. В конце повторно проверяются GameTime и World identity; смена фазы даёт incomplete.

Local Entity identity не является каноническим ID. Canonical generations поддерживает координатор по полным owner snapshots. Dead, return-after-missing, owner/definition/entity-version transitions, repeat/conflict и checkpoint покрыты offline tests. Наличие всех metadata members подтверждено, **реальная работа generic ref-return wrappers не подтверждена**.

RosterContractConfirmed не устанавливается автоматически после успешной компиляции. LoadReadProbe должен сравнить observed own roster с реально вызванными юнитами на обоих ПК, включая transport, death и выход/вход сессии. До этого SourceState=LivePartial. Затем пользователь явно готовит Observe config с RosterValidated; flag — запись результата проверки, не доказательство сам по себе.

SourceMetric включает owner/participants/count, native map sample, revision/age, read time/bytes и blockers. SourceState отделён от состояния TLS. Heartbeat не обновляет ObservationRevision или success time. После ошибки старый снимок не продолжает выглядеть новым. Неизвестный профиль/ID/обязательное поле не заменяются условными значениями.

Default MaximumCanonicalUnits=100 суммарно. Source arrays и registry имеют отдельные верхние границы; overflow/stale/partial не превращаются в неполный полный мир. Immutable config/map/profile agreement требуется до Step. Точное совпадение source revision/timestamp и допустимого125мс skew проверяется; при staggered reads worker ждёт подходящую пару.
