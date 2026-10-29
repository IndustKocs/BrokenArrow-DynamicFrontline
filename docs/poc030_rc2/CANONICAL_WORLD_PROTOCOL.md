# Канонический протокол RC2

A — координатор, B — второй владелец. Config SessionId идентифицирует приватную модовую сессию; он не заменяет native FightId. A выводит binding из двух наблюдений: равные FightId/Seed/Map/два Participant, различные owners и team UID, bots=false. B проверяет binding по собственному native снимку. Изменение binding требует новой сессии.

Pinned TLS защищает канал; HMAC с приватным случайным ключом подтверждает участников. Handshake включает payload, rules, profiles, map, build и ABI. Каждое соединение получает nonce-derived epoch; sequence replay/foreign epoch отвергаются. Разрешены только явные private/LAN/loopback IP и порт>=1024; автоматического discovery/NAT/firewall изменения нет. На wire: максимум16MiB на сообщение, chunks48KiB, очереди32.

Каждый владелец публикует full, затем delta от предыдущей успешной ревизии; раз в20 ревизий — full. Snapshot и source-health берутся атомарно. Неполный источник не удаляет юнитов из полного мира. Рассинхрон времён>125мс означает ожидание новой пары, не экстраполяцию и не сброс истории. Возраст и ревизия проверяются отдельно от sequence heartbeat.

Canonical identity = ModSession + native NetID + coordinator generation. Owner/definition/local entity-version transitions и возвращение после отсутствия в полном roster увеличивают поколение. Отсутствие — NoLongerObserved, не утверждение native death. Непрошедшая проверку запись не меняет registry. Reused ID не получает service proof от старой local identity.

В TrustedDiagnosticReplica A отправляет одинаковый normalized CanonicalInput. B независимо вызывает тот же Simulation023 и отвечает InputAck/ResultDigest. Сверяются input, territory, ZOC, encirclement, service state и projection digests по InputId. Exact repeats идемпотентны; конфликтующий повтор останавливает прогноз. Recent ACK/result ledger ограничен64.

После disconnect старые outbound frames выбрасываются; source snapshots запрашиваются заново. A передаёт полный core/world checkpoint: territory, timers/tau, service intervals, native identities/generations, source revisions, proof/delta dedupe history и следующий InputId. Вычислительные кэши переинициализируются одинаково. Checkpoint не является очередным обычным тиком и не начисляет штраф. Откат NextInput отвергается: при потере истории координатора нужна новая модовая сессия, а не скрытый reset tau.

В TeamSafe B→A передаёт свой roster, A→B — только Public023 своей команды. Независимого hidden-world replay на B в этом режиме нет. Trusted replica и подробная запись требуют явных согласованных config flags обоих владельцев.
