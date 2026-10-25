# Матрица реализации Pass01–03

A/B — достоверность конкретного статического фрагмента, не runtime-проверка. Already implemented означает сохранённую реализацию PoC02. Частичные функции не объявляются целым игровым методом. Источники: `research/full_code_audit/FORMULAS.md`, `research/combat_systems_audit/COMBAT_FORMULAS.md`, `pass_02/COMBAT_FORMULAS_PASS02.md`, `pass_03/COMBAT_FORMULAS_PASS03.md`.

| ID | Статус | Реализация / граница |
|---|---|---|
| F01 | Already implemented | InfantryReceivedDamageMultiplier, A; получаемый урон |
| F02 | Already implemented | Thresholds, A |
| F03 | Already implemented | StressDamage, A; без применения |
| F04 | Already implemented | StressToTarget, A |
| F05 | Already implemented | RecoverStressTick, A; один timer tick |
| F06 | Already implemented | Level, A; panic precedence |
| F07 | Already implemented | KineticDamage, A; аудит и новые тесты |
| F08 | Already implemented | DamageWithThreshold, A; строгое > |
| F09 | Already implemented | Penetration, A; actual range |
| F10 | Implemented in this pass | HeatDetails/HeatDamage, B; положительные конечные bases |
| F11 | Implemented in this pass | Countermeasures, B; степень, без clamp результата |
| F12 | Implemented in this pass | MissileHitChance, B; прямое произведение |
| F13 | Already implemented | AimTimes, A |
| CF01 | Not implemented | Полная условная cover/building композиция не подключена; F01 не заменяет CF01 |
| CF02 | Partially implemented | F03 и AoE fragments; полный порядок HP/armor/stress pipeline не собран |
| CF03 | Implemented in this pass | ExplosionMath, A; без buildings/collision filters |
| CF04 | Implemented in this pass | Общее count выражение через CF12, без дубликата |
| CF05 | Not implemented | Выбор брони по проекции остаётся вызывающей стороне |
| CF06 | Not implemented | Выбор HEAT-ветки по armorType/ArmorValue не автоматизирован |
| CF07 | Research only | Локальные углы и forceTop не восстанавливаются по догадке |
| CF08 | Research only | Оценка accuracy не равна полному dispersion CDF |
| CF09/P02 | Implemented in this pass | Attempts/Select, B; явный RNG и диагностический draw budget |
| CF10 | Research only | Shootability не источник разрешённых контактов |
| CF11 | Research only | Табличная баллистическая интерполяция, не solver |
| CF12 | Implemented in this pass | InfantryMath.Count, B; Hper/ceil/slots |
| CF13 | Partially implemented | Выбор death/heal B реализован; formation/ECS effects и transport guards остаются caller contract |
| CF14 | Partially implemented | Ground numeric helper B; оба FOW components и caller branch должны быть заранее проверены |
| CF15 | Partially implemented | Air composition B; sensor/radar selection передаётся явно |
| CF16 | Partially implemented | Terrain sample accumulator B; geometry/raycast/projectile branch не реализованы |
| CF17 | Research only | Не воспроизводится Unity scan scheduler |
| CF18 | Partially implemented | Experimental CyclicRandom; очередь100, seed-NetID, advance-before-read; native PRNG equivalence и remote index отсутствуют |
| CF19 | Research only | H1, guards и полный solver не подтверждены |
| CF20 (упоминание в авиационном handoff) | Research only | Фрагмент поворота самолёта не включён; не путать с P3 helicopter helpers |
| P3-F01 | Implemented in this pass | Selected ammunition container, B |
| P3-F02 | Implemented in this pass | ShiftFinite, A; точное условие достижения |
| P3-F03 | Implemented in this pass | ShiftTime, B; agility>0 |
| P3-F04 | Implemented in this pass | RequestedFuel, B; setter не применяется |
| P3-F05 | Implemented in this pass | WeaponFlashTimer, B; только событие таймера |
| P3-F06 | Research only | Spatial-entry update не часть новых чистых формул |
| P3-F07 | Partially implemented | Общий k и accumulation B; interval resolution предоставляется извне |
| P3-F08 | Partially implemented | Line distance/half chord B; endpoint clipping/reject branches не реализованы |
| P3-F09 | Partially implemented | Typed level table application B; полный reset→stress→apply порядок неизвестен |

Experimental CombatReadiness — собственная диагностическая композиция с observed state и предположением отсутствия нового урона. Dynamic Frontline Rules не изменялись. Статус Research only/Not implemented не маскируется приближением.
