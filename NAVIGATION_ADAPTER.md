# Навигация и оперативная поддержка

GameNavigationAdapter — работающий кэш/планировщик над IGameRouteBackend. Он не вызывает неподтверждённый NavigationGraph автоматически. Backend вызывается синхронно при Submit на потоке bridge; ответы передаются отдельными RouteAnswer через DrainAnswers. Асинхронность — свойство протокола адаптера, а не утверждение о потокобезопасности игровых API. По Pass 03 CalculatePath вызывает solver синхронно; вызовы Unity/Nav на worker запрещены до проверки.

Ключ RouteQuery включает MatchId, идентичность обоих endpoints (UnitKey с generation либо статичный root), позиции, команды, канал, тип движения, маску, NavigationRevision и максимальную стоимость. Направления A→B и B→A разные. RouteRequest дополнительно содержит собственный монотонный RequestId и SourceSnapshotSequence. Навигационная ревизия supplied adapter/provider, штатный engine revision не найден.

Reachable и Blocked принимаются только как полный валидный ответ backend. Pending означает ожидающий запрос/отложенный из-за бюджета кандидат. Unknown — отсутствие сведений, partial/error/timeout либо маршрут дороже разрешённого budget. Stale — устаревшие endpoints/сессия/ревизия либо истекший TTL перед повторным запросом. Слишком дорогой маршрут не считается доказательством физической блокировки.

Настройки NavigationSettings: MaximumQueriesPerTick=32, MaximumPending=256, CacheTicks=20, TimeoutTicks=8, MaximumCost=10000 по умолчанию; все положительные, cost конечный. Тесты/fixture используют свои явные значения. Бюджет исчерпан — кандидат получает Pending, но не создаёт бесконечную внутреннюю очередь. Повторный проход может его запросить позднее; справедливость обслуживания плотного постоянно меняющегося графа ещё не гарантирована.

BeginFrame удаляет ключи умерших/исчезнувших/переместившихся/сменивших команду endpoints. Смена карты/навигационной ревизии/матча очищает кэш; запоздалый ответ с прежним ID отбрасывается. Timeout становится Unknown, а не Blocked. Cache hit не вызывает backend. При смене только NavigationRevision территория и logical tick сохраняются; меняется маршрутный кэш.

Существующий OperationalConnectivityGraph остаётся ненаправленным. Новый IUnitLinkGeometry передаёт реальные идентичности вместо одних координат. Для ребра необходимы совместимые channel/movement/mask, расстояние ≤ min(rangeA,rangeB) и **Reachable в обоих направлениях**. То же консервативное правило действует для связи с корнем. Это проектная политика, устраняющая ложную обратную связь по направленному IsPassagePossible.

GroundPassage, OperationalRelay и LogisticsRoute — независимые ключи. Возврат к корню проверяется отдельным GroundPassage: хотя бы один Reachable даёт Available; Blocked возвращается только когда все корни подтверждённо Blocked; остальные случаи Unknown. Реальная логистика не реализована: NavigationConnectivityAdapter.IsSupplied всегда false. Даже успешный LogisticsRoute не создаёт запасы/боеприпасы.

NativeNavigationStatusPolicy по умолчанию ничего не объявляет достижимым. Статические PathStatus 1 Complete и 4 Impossible принимаются только при отдельно подтверждённой семантике backend. NotFull, Line, TooClose, NotFoundStart/End остаются Unknown до выбранной проверенной политики. ZonePassage EntryPossible/ExitPossible не превращаются в bool true. Ненулевой NavPath сам по себе ничего не доказывает.
