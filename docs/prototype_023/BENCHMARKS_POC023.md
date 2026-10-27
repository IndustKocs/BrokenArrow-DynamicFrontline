# Нагрузочные измерения PoC023

48 независимых прогонов: 4 состава × 2 сетки × 2 физических размера клетки × 3 seed/repeat. По10000 обновлений; всего480000. Runtime {'runtime': '6.0.36', 'os': 'Microsoft Windows NT 10.0.19045.0', 'cpu': 20}. Сырые данные samples.csv.gz, summary.json и отдельные128sample geometry/serialization JSON в evidence/prototype023/benchmarks.

Измеренный исходный commit d49e46f; последующая замена per-tick stress IDs на watermark не меняет измеряемые field/area/victory stages. Финальный72-test suite отдельно проверяет10000stress-интервалов. Benchmark сборка .NET6 Release SDK8.0.426, тот же host. Во время части замеров запускались сборки/проверки; это не изолированный лабораторный профиль.

| Юнитов | p95 суммарного измеренного hot пути, диапазон мс | p95 холодной геометрии, мс | Выделено за10000 шагов, МБ |
|---|---:|---:|---:|
| 100 | 0.192–0.650 | 1.817–7.103 | 766.7–1947.9 |
| 300 | 0.618–2.088 | 1.569–7.229 | 2078.8–5606.3 |
| 500 | 1.029–3.764 | 1.620–7.495 | 3647.7–9527.9 |
| 1000 | 2.172–7.472 | 2.020–7.648 | 7376.4–19082.2 |

Клетки50/100м и сетки128²/256² — четыре разных размера карты, а не одна карта с одинаковой геометрией. В первой половине каждого прогона движется10% источников, во второй100%. Host4Гц, новые resolved positions каждую секунду (каждые4тика), field проверяет кэш каждый тик. Состав random spatial tanks, fixed valid profiles; baseline warm-up вне времён.

Семь stage distributions: resolver, incremental field, area, army, victory, withoutVictory, withVictory. Последние две получены из одного шага до/после victory; это парное измерение добавочной стоимости, не отдельный второй движок. Cold geometry128 и serialization128 измеряются отдельно и не входят в hot total. geometryUnknown включает Contested, а не только исчерпание бюджета.

Каждый итоговый field сравнен с независимым full reference; максимум ошибки 5.3290705182e-11. Сырой CSV имеет480000 строк + заголовок. p50/p95/p99/max и GC0/1/2 доступны для каждого прогона. allocatedBytes — накопленные аллокации (не resident memory), включают форматирование raw CSV и engine, но не final full reference/cold geometry. Это не zero-allocation результат.

Ограничения: area benchmark здесь пустой/стабильный, не worst case moving-local-area; dense bunches, массовый400↔600, смерть/portal invalidation проверены функционально лишь в малых fixtures, без длительной полной нагрузочной матрицы. Full Simulation023 не использует benchmark1Гц scheduler: он актуализирует при каждом250мс входе. Поэтому показатели не доказывают end-to-end SLA. Фактический shared geometry cache и высокие аллокации требуют следующей оптимизации/длительного полного прогона. Производительность всего ТЗ отмечена Partial.
