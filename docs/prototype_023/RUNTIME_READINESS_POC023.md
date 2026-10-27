# Runtime readiness: запрещён к установке

| Источник/эффект | Текущий статус | Для следующего этапа |
|---|---|---|
| Roster/profile/S0/alive soldiers | Synthetic JSON | Авторитетный полный snapshot и provenance |
| Навигация, mobility, portals | Synthetic masks | Валидированный terrain provider и revision |
| Position/staleness | JSON fixture | Правило возраста снимка и authoritative movement |
| Ammo/repair/heal | Confirmed synthetic interval | Native event identity, конец/разрыв услуги |
| Raw combat/impact identity | In-memory | Один hook до native application, без дубля |
| Recovery cadence/gate | Synthetic1с | Реальный scheduler и безопасный interception |
| Native destruction feed | Separate synthetic absolute/delta | Источник/revision/reset/watermark |
| MatchEndIntent | In-memory only | Network authority, exactly-once endgame |
| Own HUD/radial ruler | DTO/interface | Реальный binding и visibility rules |
| Geometry | Partial raster spike | Clearance oracle и полная spike suite |
| Performance | Measured subset | Full pipeline dense/moving/GC budget |

Компиляция loaders не закрывает ни одну из этих строк. Нет calls к HP writes, SendEndGame, Steam или античиту. Не копировать DLL в папку игры; этот набор не является payload для ТЗ12.
