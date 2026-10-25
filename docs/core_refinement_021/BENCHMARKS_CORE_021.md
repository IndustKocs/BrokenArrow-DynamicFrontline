# Производительность 0.2.1

.NET 6.0.36, Windows, локальный автономный прогон. Micro: 1000 warmup, 15 batches по 10000 операций (critical selection — 1000), Stopwatch.GetTimestamp, GC.GetAllocatedBytesForCurrentThread. Результат сохраняется в sink. Median — медиана batch ns/call; p95 здесь максимум из 15 batch, не p95 одиночной операции. JIT, tiering и фоновая нагрузка ограничивают интерпретацию.

| Helper | Release ns/call | Debug ns/call | Release bytes/call | Debug bytes/call |
|---|---:|---:|---:|---:|
| HEAT | 47.8 | 70.1 | 0.000 | 0.000 |
| Kinetic | 27.6 | 32.8 | 0.000 | 0.000 |
| AoE | 9.9 | 13.7 | 0.000 | 0.000 |
| Infantry count | 21.9 | 25.9 | 0.000 | 0.000 |
| Countermeasures | 13.1 | 17.9 | 0.000 | 0.000 |
| Missile composition | 25.5 | 33.0 | 0.000 | 0.000 |
| Ammunition | 25.4 | 85.0 | 0.000 | 0.000 |
| Critical attempts | 21.4 | 29.3 | 0.000 | 0.000 |
| CF18 next | 15.2 | 18.6 | 0.000 | 0.000 |
| Helicopter | 55.1 | 71.2 | 0.000 | 0.000 |
| Fuel requested | 17.9 | 23.0 | 0.000 | 0.000 |
| Air range | 30.0 | 32.3 | 0.000 | 0.000 |
| Smoke intervals | 91.6 | 193.9 | 0.000 | 0.000 |
| Critical selection with trace | 136.3 | 272.8 | 192.000 | 192.000 |

Scalar helpers и span loops не создают коллекций. Critical selection выделяет immutable-facing trace; это диагностический выбор, не hot-path территории. Профиль загружается явно, не на каждом тике. Оптимизации старого ядра не менялись.

## Контроль интеграции

Сетка 256², 1000 юнитов, 250 ms, seed 82026, 10 warmup +60 samples. Исторический baseline сохранён в evidence/poc02/benchmarks.json. Проверены старый и новый Release DLL одним неизменённым integration runner с полным набором 24 сценариев, оба вывода записаны только в новое worktree.

| Запуск | median ms | p99 ms | bytes/tick |
|---|---:|---:|---:|
| Исторический PoC02 | 2.276 | 20.866 | 3275923 |
| PoC02 повтор сейчас | 2.301 | 20.753 | 3276082 |
| 0.2.1 тот же runner | 2.246 | 20.621 | 3275910 |
| Отдельный micro runner Release | 3.447 | 7.074 | 3276141 |
| Отдельный micro runner Debug | 4.697 | 7.587 | 3290579 |

Отдельный короткий runner показал более высокую медиану: этот результат сохранён, не отброшен. Повтор одинаковым integration runner не выявил регрессии; небольшой выигрыш не объявляется ускорением. Различия в десятках/сотнях bytes/tick связаны с измерением и runtime, а не подключением новых формул: интеграционные исходники неизменны. Замеры не предсказывают FPS и стоимость реальной навигации.
