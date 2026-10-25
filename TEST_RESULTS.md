# Результаты автономных проверок

25 октября 2026. Среда: .NET 6.0.36; Microsoft Windows NT 10.0.19045.0. Логических процессоров: 20.

Новые сценарии: **59/59**, ошибок 0. Прежние тесты Core/GameAdapter: **36/36**. Сборка MelonLoader, прежнего runner и нового runner: 0 ошибок, 0 предупреждений.

## Методика нагрузки

Seed=62026. Карты 128×128 и 256×256, ячейка 10 условных единиц. Численности 100/300/500/1000, периоды 100/250/1000 мс. На каждом шаге перемещается примерно 10% частей; четверть профилей — разведка. Из 70 шагов первые 10 прогревочные, 60 измеряемые. Показатели относятся к одному потоку C# и синтетической модели; это не FPS и не результат Unity/PvP.

p95/p99 — nearest-rank для 60 наблюдений. Первый прогрев использует JIT; серия короткая, системная нагрузка влияет на числа. Нет утверждения о стабильной верхней границе времени.

| Сетка | Частей | Тик, мс | Медиана, мс | p95, мс | p99, мс | Выделено КБ/тик | CPU мс/сек симуляции |
|---|---:|---:|---:|---:|---:|---:|---:|
| 128×128 | 100 | 100 | 0.160 | 1.155 | 1.363 | 176.8 | 2.67 |
| 128×128 | 100 | 250 | 0.116 | 1.013 | 1.025 | 185.3 | 0.99 |
| 128×128 | 100 | 1000 | 0.117 | 1.024 | 1.104 | 185.2 | 0.24 |
| 128×128 | 300 | 100 | 0.390 | 1.330 | 1.642 | 514.6 | 7.18 |
| 128×128 | 300 | 250 | 0.406 | 1.405 | 1.652 | 518.9 | 3.06 |
| 128×128 | 300 | 1000 | 0.400 | 1.420 | 1.607 | 522.9 | 0.77 |
| 128×128 | 500 | 100 | 0.729 | 1.983 | 2.416 | 803.9 | 11.28 |
| 128×128 | 500 | 250 | 0.727 | 1.858 | 2.036 | 808.3 | 4.36 |
| 128×128 | 500 | 1000 | 0.668 | 1.549 | 1.883 | 812.4 | 1.04 |
| 128×128 | 1000 | 100 | 2.196 | 2.715 | 3.527 | 1647.4 | 19.97 |
| 128×128 | 1000 | 250 | 2.042 | 3.591 | 5.288 | 1658.3 | 8.53 |
| 128×128 | 1000 | 1000 | 2.009 | 2.739 | 3.340 | 1658.2 | 1.92 |
| 256×256 | 100 | 100 | 0.080 | 0.107 | 2.825 | 167.7 | 1.80 |
| 256×256 | 100 | 250 | 0.080 | 0.284 | 2.856 | 176.1 | 0.88 |
| 256×256 | 100 | 1000 | 0.079 | 0.407 | 2.792 | 176.1 | 0.22 |
| 256×256 | 300 | 100 | 0.276 | 3.061 | 3.149 | 484.6 | 6.57 |
| 256×256 | 300 | 250 | 0.301 | 3.158 | 4.093 | 501.8 | 3.22 |
| 256×256 | 300 | 1000 | 0.284 | 3.056 | 3.583 | 501.6 | 0.78 |
| 256×256 | 500 | 100 | 0.670 | 4.104 | 5.687 | 808.9 | 12.46 |
| 256×256 | 500 | 250 | 0.499 | 3.261 | 4.843 | 826.2 | 4.56 |
| 256×256 | 500 | 1000 | 0.514 | 3.467 | 4.276 | 826.2 | 1.16 |
| 256×256 | 1000 | 100 | 1.501 | 3.928 | 4.340 | 1603.1 | 21.99 |
| 256×256 | 1000 | 250 | 1.165 | 4.196 | 6.363 | 1620.9 | 9.12 |
| 256×256 | 1000 | 1000 | 1.012 | 3.784 | 4.065 | 1621.3 | 2.10 |

## Отдельный принудительный пересчёт границы

Карта из чередующихся блоков 8×8. Кэш отключён самим способом измерения: Extract вызывается каждый раз. Пять прогревочных и 30 измеряемых вызовов. В обычной симуляции граница кэшируется по смене владельцев, поэтому её нулевое время в отдельных тиках не означает бесплатное построение.

| Сетка | Путей | Медиана, мс | p95, мс | p99, мс | КБ/построение |
|---|---:|---:|---:|---:|---:|
| 128×128 | 540 | 1.436 | 4.330 | 6.954 | 1465.5 |
| 256×256 | 2108 | 6.144 | 25.521 | 26.518 | 5674.2 |

В artifacts/benchmarks.json дополнительно сохранены отдельные времена влияния, захвата, графа, границы и прочих стадий; managed heap, working set и число сборок GC поколений 0/1/2. Байты измерены GC.GetAllocatedBytesForCurrentThread. Точное количество созданных объектов не измерялось; GC collection count не выдаётся за количество аллокаций.

После первого замера удалены iterator-аллокации из flood fill территории и переиспользована очередь. Сохранились выделения графа, снимков и геометрии: для дальнейшей Unity-интеграции полезны пулы и повторное использование буферов. Итоговая таблица относится к версии после этой правки.

## Воспроизводимые сценарии

- PASS — neutral capture and regular advance require rooted territory
- PASS — regular withdrawal retains last stable control
- PASS — hysteresis requires accumulated capture and decays after death
- PASS — capture after four equal ticks
- PASS — equal opposing influence contested without ownership flicker
- PASS — enemy capture changes stable owner only after threshold
- PASS — soldier count and shock affect influence independently of HP
- PASS — territory excludes dead
- PASS — territory excludes zero
- PASS — territory excludes panic
- PASS — territory excludes deserting
- PASS — territory excludes loaded
- PASS — noncapture role 3 never changes grid even in rear
- PASS — noncapture role 4 never changes grid even in rear
- PASS — noncapture role 5 never changes grid even in rear
- PASS — invalid capture role rejected and unknown IDs default deny
- PASS — local area moves without territorial trail
- PASS — separate autonomous groups and overlapping enclave union
- PASS — local radius tracks soldiers and last soldier removes enclave
- PASS — restoring territorial corridor absorbs local presence
- PASS — out of map and masked units cannot capture or relay
- PASS — only changed footprints are updated
- PASS — direct and intermediate operational support
- PASS — regular recon special regular chain supports without painting corridor
- PASS — multiple reconnaissance relays connect to root
- PASS — destroyed relay breaks chain and replacement restores it
- PASS — too distant and unrooted chain remain disconnected
- PASS — hop limit stops artificial long chains
- PASS — relay exclusion panic
- PASS — relay exclusion deserting
- PASS — relay exclusion loaded
- PASS — relay exclusion zero
- PASS — shock participation configured independently of capture
- PASS — opponent nodes never link across teams
- PASS — terrain barrier blocks links including diagonal corner cutting
- PASS — operational support does not imply logistics or territory
- PASS — logistics can exist without territorial or operational continuity
- PASS — distance and unknown retreat are not proof of encirclement
- PASS — safe retreat without support remains autonomous
- PASS — confirmed closed route waits then produces configured severity
- PASS — physical closure with operational support lowers severity but opens no route
- PASS — recovery before penalties and repeated isolation reset confirmation timer
- PASS — recovery after penalties holds reconnecting then settles
- PASS — reconnected territory and support wait before Connected
- PASS — boundary extracts stable simplified straight front
- PASS — boundary supports closed islands and concave contours
- PASS — masked terrain boundary remains distinct from enemy front
- PASS — input order does not change authoritative outputs
- PASS — JSON snapshots and ordered deltas reconstruct authority
- PASS — foreign duplicate reordered delta rejected
- PASS — presentation default denies territory and every enemy coordinate or key
- PASS — invalid tick duplicate UID match and nonfinite data fail before mutation
- PASS — configuration rejects invalid domains
- PASS — configuration file loads explicit synthetic profiles
- PASS — synthetic retreat routing detects enclosing terrain and neutral escape
- PASS — enemy territory retreat rule is independent configurable policy
- PASS — desertion adapter suppresses territory relay and local area until restored
- PASS — configurable panic coefficient and type multiplier apply
- PASS — vehicle relay opt in does not enable territorial capture

## Диагностика и сохранность

artifacts/frontline-diagnostic.html строится из 18 настоящих кадров ядра; исходные DTO сохранены в diagnostic-frames.json. В браузере проверены отображение карты, смена тика, потеря узла поддержки и независимое переключение слоёв. Это авторитетная синтетическая диагностика, не публичное представление для PvP.

artifacts/game-preservation.json: шесть контрольных хэшей совпали; новых, отсутствующих и изменивших размер/время файлов нет. Игра не запускалась.

Команда воспроизведения — build-frontline.ps1 из FRONTLINE_PROTOTYPE_REPORT.md. Отчёты JSON и журналы находятся в artifacts/.
