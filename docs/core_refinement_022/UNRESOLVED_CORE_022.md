# Незакрытые механики

| Область | Точная граница |
|---|---|
| CDF | Active asset keys и Unity interpolation/tangents/wrap не установлены. LinearTestCurve Experimental |
| Polar | Соответствие двух trig helpers компонентам и ориентация не доказаны. CosSin условный |
| H1 | Finite rational transcription допустима; NaN payload/error flags/errno/FMA/rounding/ULP contract и полная CF19 equivalence отсутствуют |
| RNG | seed producer,projectile sharing,native System.Random,remote/fallback/reconnect не закрыты |
| Buff | reset/order/critical/apply fragment подтверждены; actual stress source и все dirty producers/repair/command predicates не восстановлены |
| Smoke | Односферный finite interval реализован; overlap/containment/opacity resolver и special floats остаются открыты |
| Aircraft | Yaw/pitch preset interpolation не полная аэродинамика; H2 tilt/remote hover остаются исследовательскими |
| Identity | World disposal/version подтверждён, но полного live roster/provider/Death→Remove scheduler/network reorder нет |
| Navigation | A*priority и consumer statuses не заменяют graph contract/masks/units/revisions/thread ownership |
| FOW/LastKnown | Направленные collections и второй actual writer не дают безопасного team API |
| Network | server damage code15 apply/authorization и per-unit command permissions не определены |

Эти пункты не заменены учебными алгоритмами, gameplay hooks или неподтверждёнными PvP правилами. Исследовательские Pass01–04 сохранены; Core021 docs не переписаны. SHA256 использованных источников фиксирует версию доказательной базы, но не является доказательством runtime-совместимости.
