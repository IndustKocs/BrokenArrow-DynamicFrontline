# Стресс, снабжение, сохранённые правила

Core/Rules023 не изменены. S0 оригинальный; m/q Reserve .8/1.1, Regular1/1, Special1.2/.9; native1/1. ClassMax=m*S0. Combat=q*(HPDamage/MaxHP*S0+Direct); реконструированный raw не прибавляется к уже начисленному F04. HP не изменяется. LiveReader отдельно выводит UnitData.MaxStress и текущий StressComponent.MaxStress; совпадение/различие не доказывает активный Buff или raw event source.

Окружение: подтверждение10s, выход5s, .003→.01 за60 активных секунд; аналитический интеграл39% ClassMax за60s. q к penalty не применяется. Legacy recovery/formulas сохранены. Плагин не инициализирует MaxStress повторно, не блокирует native recovery и не делает addback, fake hit или catch-up.

StreamingRelief принимает Begin, положительный recipient receipt, Continue, End. Begin/proximity/provider debit не дают relief. Нужны amount>0 и provenance. Нет заранее известного End; stale open stream даёт Unknown. Завершённые пересекающиеся интервалы объединяются, grace=min(20,2duration), короткое обслуживание не сокращает более длинную grace. Это автономный механизм; native подтверждение получения ремонта/боеприпасов пока не подключено. Ни поздний receipt, ни reconnect не инициируют ретроактивную native запись.

LocalIdentity(WorldId,EntityId,Version) отделена от CanonicalIdentity(match,NetId,spawnGeneration) и BindingEpoch. Неизвестная continuity требует resync, не сброса stress/tau/dedup. В реальном reader SpawnGeneration=null — нельзя угадывать её из local Version. Binding lifecycle ещё не превращён в полный runtime state restoration.

Утверждённые механики остаются в существующей модели: Line+Assault по explicit allowlist; persistent territory; ZOC400/600 при infantry+APC/IFV≤100m без stacking и Tank; один боец; full pocket interior; 3×900 с весами1/2/3; breakthrough глубина75–80%+ширина120s; collapse4:1+strict lead+active2000/420s. Неутверждённые K/width/valuation/air/final policies не выбирались заново. Candidate IDs57/59/60/108 имеют provenance извлечённой таблицы; это ещё не активный профиль.710/711/713 denied, MaxStress0 у711 не заменяется1000, Cost0 у497 не трактуется как доказанная army value.
