# Native score и единственный финал

NativeScoreEvent: id/team/time/value/revision/mode/provenance. Feed mode выбирается при создании VictorySimulation: Delta или Absolute. Delta дедуплицируется по ID; Absolute принимает только новую revision. Режимы не смешиваются. Старый уже виденный ID можно повторно доставить; новый late event вне текущего интервала отклоняется. Недостоверный режим валидируется до изменения ledger. Конфликтующие payload под одним уже виденным ID не сверяются по хешу: producer обязан иметь неизменяемую identity; это дополнительный runtime contract blocker.

Очки уничтожения не выводятся из цены потери/HP/FOW/дезертирства и не умножаются на фазовые веса. NativeComplete влияет на synthetic финал; неизвестный native stream не придумывает значения. Текущий Simulation023 использует Delta, Absolute проверяется автономным VictorySimulation.

MatchEndIntent несёт session/version/time/winner/reasons/status. Первый по времени результат обрезает эффекты и ledger; поздние события не начисляются. Симуляция не вызывает игровое завершение. Контракт реального SendEndGame/network authority и native reset не подтверждён.

Полный checkpoint включает U/native/таймеры/IDs/mode/phase time, config hash и сериализованную victory policy. Возобновление на чужой карте/session/version отклоняется. Pause продвигает revision, но не active time; разрыв ревизий отклоняется, неизвестная area не начисляет выдуманную площадь.
