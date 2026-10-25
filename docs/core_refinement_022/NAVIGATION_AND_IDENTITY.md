# Навигация и identity

P4-F05 #26017,A: abs(dx),abs(dy) и их сумма вычисляются в float; затем double coefficient−0.5857864376269051,weight1.25,g. Для3,4,10→16.553300858899107. Результат содержит single sum и double heuristic/priority. Коэффициент не пересчитан в float через sqrt2. Это не solver, длина в метрах и не гарантия optimal path.

SubGraphConsumerAccepts принимает raw statuses1/2 по #12295. Это отдельный consumer helper. NativeNavigationStatusPolicy не изменён: Line по умолчанию остаётся Unknown, Complete требует runtime-validated semantics, Impossible становится Blocked только при отдельном подтверждении. NotFull не full route. Unknown не становится Encircled; прежние regression tests сохранены.

VersionAwareIdentity=(SessionId,WorldId,EntityId,EntityVersion) — Experimental proposal E на основе World.On(EntityDisposedMessage) #190347,B. IdentityValidation сравнивает supplied snapshot/current; disposed,invalid,другая сессия/world/id,version mismatch и stale revision возвращают разные решения. Не угадывается новый version по spatial absence. Owner не часть identity, его смена не создаёт новую сущность. Тесты моделируют creation/disposal/reuse и поздний snapshot.

LifecycleObservation содержит отдельные Death,Dead,RemoveUnit,FogRemove,Disposed,SpatialPresent. Accepted означает валидный handle/revision, а не способность захватывать территорию. Труп или удаляемая entity может иметь ещё валидный handle. Нет предположения о длительности трупа или полном scheduler.

Текущий Integration DTO имеет MatchId,Uid,LifecycleStamp,адаптерный UnitKey.Generation,SourceRevision и отдельный GameDeathState. Он не содержит достоверных WorldId/EntityId/EntityVersion; opaque LifecycleStamp не объявлен эквивалентом DefaultEcs version. GameUnitIdentityResolver не переписан. Будущий provider должен получать реальные значения и проверять live version перед публикацией согласованного snapshot.

P4-F06 внутренний LastKnownNavigationFragment возвращает только squared distances и booleans, не позиции/контакты. Gate: hasPath,min(actual²,last²)≤640000,pathTarget-to-chosen²>threshold; chosen=ChaseInvisible?actual:last; completion last²<625. XZ-only,800 проходит,800.001 нет,25 не завершает. Предварительные visible update/BuildingSegment guards не входят в helper. Он не доступен как FOW-export и не меняет публичную проекцию.
