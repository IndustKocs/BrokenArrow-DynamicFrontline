# Транспорт и полномочия

Роль A означает координатора собственного dev TCP, а не владельца native боя. Разрешены только loopback/RFC1918 IP и явный порт1024–65535. Нет автоматического firewall, публичного endpoint, discovery, native opcode или миграции A→B.

Перед кадрами обе стороны обмениваются случайными 32-byte nonce; их общий digest включён в HMAC-подписанный Hello. Secret минимум32bytes хранится отдельно, в пакет/логи не входит. Session UUID должен быть новым для опыта. Hello сверяет protocol/session/role/full compatibility digest/payload/rules/profile/map/ABI/capabilities/connection nonce. В текущем runtime profile и map отмечены Unknown: это не допуск к расчёту мира. Auth проверяет разрешённого обладателя секрета; team из пакета не используется для полномочий.

Кадр максимум16KiB, до4Hz; sequence строго последовательный. Перескок, replay, чужая сессия/ABI/подпись → разрыв. Подключение60s, кадр5s. Один peer, одна latest-status запись, нет бесконечной очереди. На разрыв — Disabled/Disconnected, никакого повторного native write. Автоматического reconnect/resync в этой версии нет; новый диагностический сеанс требуется явно.

Status содержит sequence/revision/binding epoch/quality/own count/opaque observation hash. Координат, целей, hidden units, чужих контуров и service-деталей в этом DTO нет. Собственные снимки пишутся локально; collector удаляет позиции/имена/пути. Независимые кадры разных клиентов не сравниваются по hash. Проверка доставки статуса не равна независимому пересчёту одного canonical input.

НЕ РЕАЛИЗОВАНЫ: canonical snapshot/delta, подтверждённый owner-snapshot union, gap recovery/full resync, native MatchId binding, мир для одновременного shadow recompute. Приёмка этой части требует: authenticated sender↔player binding, полный roster двух участников без AI/unmodded omissions, неизменяемое canonical input ID/hash, два независимых пересчёта и одинаковые outputs, отрицательные stale/duplicate/reorder/drop/reconnect тесты. По status loopback это не засчитывается.

EffectJournal автономно резервирует ID+payload hash: identical confirmed→Duplicate, different→Conflict, uncertain completion→Ambiguous без retry, budget→fail closed. Он не native backend. Все игровые write capabilities отсутствуют и не могут включаться config-флагом.
