# Классовый стресс и источники

m/q: Reserve .8/1.1; Regular1/1; Special1.2/.9; NativePassthrough1/1. Maximum=m×S0. Combat=q×F03(hpDamage,maxHP,S0,directStress). F02 использует новый максимум и исходные ratios; F06 сохраняет panic precedence. F05 вызывается неизменённым helper, без воспроизведения пропущенных вызовов после снятия gate. HP не изменяется.

| Источник | Единственный путь применения | Защита |
|---|---|---|
| RawCombat | Combat → q×Core.F03 → in-memory clamp | OriginalImpactId, только Stage=Raw |
| NativeTarget F04 | NativeTarget → Core.F04, без q | target/eventId |
| Encirclement | FSM analytic intent → Apply | consecutive interval watermark + session/unit/version |
| Unknown/composed | отклонение | Apply принимает только Encirclement, Combat только Raw |

Старый native combat и новый modified combat нельзя вызывать на одно воздействие; game adapter должен выбрать один путь. Текущий backend полностью автономный и такого native hook не имеет. Редоставка эффекта с уже пройденным end не применяется. Per-tick IDs не копятся в памяти; сохраняются только идентификаторы отдельных боевых/target событий. Checkpoint восстанавливает их и watermark.

Проверены800/132,1000/120,1200/108; F04target200 остаётся200; F05 без catch-up. Profile migration с изменением максимума отклоняется. Game-facing неизвестный S0 не заменяется1000; fixtures явно Synthetic.
