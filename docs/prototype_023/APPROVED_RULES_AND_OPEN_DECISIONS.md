# Правила и временные решения

| Утверждено ТЗ | Реализовано |
|---|---|
| 400 / 600 / 100 м, симметричная пара пехота+БТР/БМП | Resolver, без суммирования и цепочек |
| Пехота aliveSoldiers×K, техника HP-множитель, falloff | ZocStrengthCalculator |
| 10 с подтверждения, 5 с выхода | per-unit FSM |
| .003→.01 за 60 активных секунд | аналитический интеграл H |
| Услуга + min(20, 2×длительность) | объединение интервалов, grace не сокращается |
| Классы m/q и F02–F06 | Core helpers + отдельные источники |
| 900/900/900, веса1/2/3, 120/420, 4:1 и 2000 | VictorySimulation |

Experimental: minimumStrength=2, dominance=1.25, corridorWidth=100 м, release=PausePenalty; raster50/100 м; K=1; breakthroughDepth=.75 (тест .80 тоже есть), width200 м, X-axis; NominalActiveValue; StableAliveRoster; нейтральные конфликтные перекрытия. Условия не выдаются за native баланс.

FinalPolicy по умолчанию null → PendingPolicy; SyntheticSum доступна только как явный тест. Одновременные противоположные основания → PendingPolicy. Полный список незакрытых решений: OPEN_DECISIONS.md.
