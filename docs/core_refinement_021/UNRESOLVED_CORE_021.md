# Оставленные границы

| Механизм | Точный блокер / оставленная часть |
|---|---|
| CF19 ballistic solver | H1 и полный caller/guard contract не подтверждены в Pass01–03; G source не задаёт active value |
| Artillery dispersion CDF | Генераторы отклонений и consumers AnimationCurve не установлены полностью |
| РСЗО/кассетные | Залп, spawn дочерних ammo, полный collision/building path отсутствуют |
| SeekerSystem | Реализован только gate prefix; retarget/LOS/shooter guards/caps/miss-vector и число draws не полная state machine |
| CF18 | .NET System.Random — экспериментальная реконструкция; native generator equivalence, fallback, remote index и reconnect неизвестны |
| Критические эффекты | Численные transitions проверены; нормализация native table layout и полный reset/stress/apply pipeline не закрыты |
| FOW | Нет geometry raycast и контактов; подготовка terrain samples/terrain flags/sensor branch остаётся caller contract |
| Smoke | Не реализованы interval resolution, containment/opacity branches, endpoints и clipping; half chord не весь SmokeHelper |
| Fuel/helicopter | Setter permission, навигационный desired state и constraints не реализованы |
| Damage authority | code15, окончательное применение и sender rights неизвестны; pipeline не переписан |
| Config | Static assets не доказывают активные параметры Custom PvP; TryLoadScenario имеет ограничения |
| Special floats | Полная native NaN/Infinity/denormal/FMA equivalence не заявлена; guarded finite domain и float tolerance описаны отдельно |

Исследовательская ветка Pass04 не использовалась для неявного расширения утверждений Pass01–03 и не изменялась. Следующие уточнения должны пройти отдельный review доказательств и тестов. Эти блокеры не подменены физическими приближениями или игровыми hooks.
