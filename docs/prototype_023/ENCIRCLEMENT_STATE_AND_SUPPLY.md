# Окружение, обслуживание, время

UnitKey=(session,uid,generation). Состояния NotEncircled, Pending, Encircled, ReleasePending, DataUncertain. Pending10 и release5 считаются по активным секундам. Team change сбрасывает историю; loaded приостанавливает её; dead/air/ineligible сбрасывают. Подтверждённое contested сохраняет котёл, unknown приостанавливает активный возраст и штраф, не снимая confirmed history.

H(t)=a×min(t,T)+(b−a)×min(t,T)^2/(2T)+b×max(0,t−T), a=.003,b=.01,T60. Добавка=maxStress×(H(tau+dt)−H(tau)); q сюда не входит. Возраст продолжает расти после достижения шкалы; backend ограничивает стресс максимумом. Recovery gate действует только на активном штрафе. PausePenalty и KeepPenaltyUntilConfirmed тестируются отдельно.

Supply принимает подтверждённый ненулевой Ammo/Repair/Heal с recipient, serviceId/eventId, start/end, provenance. Повторное событие идемпотентно, конфликт отклоняется. Перекрывающиеся/соприкасающиеся интервалы объединяются. Grace=end+min20,2duration; новая короткая услуга не отменяет более длинный действующий grace. Текущий стресс не обнуляется; F05 вне блокировки работает.

Сервисные события должны приходить до обслуживаемого интервала. Late/truncated native service stream и реальные callback-сигнатуры не реализованы. Simulation host фиксирован250мс; сам FSM/интеграл проверен для .1/.25/1 и большого шага со всеми границами. Recovery scheduler — явно synthetic 1с, native частота не установлена.
