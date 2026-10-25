# Передача следующему этапу

Готовы к автономному использованию: immutable correction table и её cache, Polar RNG-order fragment, FastDimond, trajectory helper в подтверждённой геометрии, десятислотовый buff evaluator с supplied stress, single-sphere endpoints, yaw/pitch interpolation, A*priority. Для runtime каждый требует отдельной проверки caller/domain/provenance.

Подготовленные контракты: ICurveEvaluator/IInverseCurveFactory,IFloatRandom,IStressModifierProvider,CurveSource и version-aware snapshot proposal. Existing GameAdapter не подменён. Входная стрессовая стадия должна поступать от доказанного provider; final diagnostics не применяют изменения. CDF строить при конфигурации и инвалидировать по source/version/hash, не на каждый выстрел.

Перед подключением подтвердить active CDF/tangents,trig orientation,weapon/projectile RNG lineage,actual BuffConfig,полный stress producer,world/version/session и consistent read phase,nav masks/revision/consumer semantics,team-scoped intel API,network authority. Полные server code15,command permission и reconnect по-прежнему Unknown. Запуск/установка не входят в текущую работу.

Территориальные правила, FOW projection, профили, связи и DesertionController остаются прежними. Новые weapon math не могут автоматически менять capture influence. Line в одном consumer не повышает все маршруты до Reachable; spatial removal не death; LastKnown не безопасный contact cache. Любое последующее подключение требует отдельного решения и регрессии всех284 групп.
