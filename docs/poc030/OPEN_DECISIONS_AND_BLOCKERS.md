# Открытые решения и блокеры

| Код | Конкретное основание | Следующий шаг / критерий |
|---|---|---|
|B01 interop deployment|168сборок сгенерированы, но нет установленного/проверенного loader cache; Unity base libs не использованы|Сверить loader generation/options/cache path, подготовить отдельный файловый plan; после согласия подтвердить loaded hashes и generic/ref invocation на двух клиентах|
|B02 roster/identity|World.GetEnumerator/Entity.Get<T> есть в новых wrappers; видимость не полнота; NetID не содержит проверенной spawn generation|Проверить owner/team, full own units, loaded/loading/death, visibility loss, reuse/reconnect/transfer; добавить net spawn provenance, без reset tau при rebind|
|B03 active DB/Buff|UnitData.Id/Role/MaxStress читаются из real object, но источник активного DB/Buff не привязан|Связать DataBaseService LoadCompiled/JSON/TryLoadScenario и активные Buff references; сверить active sample с candidate профилем|
|B04 map/navigation|MapSettings.MapSize и MapMetaData существуют, но не дают сами по себе roots/scoring mask/directed clearance|Выбрать реальный сценарий, извлечь его mask/roots/navigation routes/revision; добавить provider и exact geometry oracle. Synthetic rectangle запрещён|
|B05 service|SupplySystem debit/ResupplyAbilities callsites из UA01 не доказывают positive recipient operation|Пассивно проверить credit на получателе для ammo/repair/heal; источник должен выдавать start/positive portion/continue/end. Until then Unknown|
|B06 combat/recovery|F02–06 bounded arithmetic сохранена, raw hit source/replace-once/recovery scheduler authority не подтверждены|Получить raw decomposition доF04 и конкретного исполнителя; доказать оба владельца, ambiguous ACK no retry, затем отдельное разрешение. Никакого stacked132 поверх120|
|B07 canonical network|Текущий TCP только статусы; owner-player binding и full/delta/resync отсутствуют|Реализовать peer-owned union и canonical input protocol с immutable IDs/gaps, затем независимый recompute одинакового input. Не сравнивать независимые local frames|
|B08 score/end|NativeScore Ingest ещё не проверяет конфликт payload под старым ID; native feed и match-end authority нет|Адаптер immutable IDs+payload hash, один режимDelta/Absolute; полномочия end отдельно; сохранить PendingPolicy|
|B09 geometry/UI|Старый raster certificate общ дляteam/mobility; нет полного independent clearance suite/two-pocket contour и реального ZOC UI|Concave/diagonal/bridges/two-roots/two-pockets на одинаковом physical scene; собственные уведомления безhidden coordinates; visible-only круги, toggle off|
|B10 perf|Короткие32tick full-Step замеры не включают IL2CPP/HUD/TCP; long1000unit soak не выполнен|Полный end-to-end измеритель, повторные100/500/1000dense/all600/deaths/two-pockets и2700s, bounded queues/caches|
|B11 политики|K, ширина/ось/75или80%, valuation/air, conflict/final policies в исходном HEAD experimental/Pending|Не придумывать решение. Согласовать конкретные параметры перед FullModeTest; Observe может собирать факты без них|

Отдельное разрешение на игру закрывает только возможность провести опыт, а не технические B02–B10. Не просить разрешение как замену недоделанной адаптации. Текущий архив позволяет ревью кода, автономные проверки и дальнейшую подготовку; полная приёмка ТЗ16 остаётся незавершённой.
