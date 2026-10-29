# Результат двухклиентной подготовки

| Проверка | Результат |
|---|---|
| Два отдельных процесса, canonical input/TLS/core digest | Offline PASS |
| Owner observations → auto native-like binding → canonical → independent core → own projection | Offline fixture PASS |
| Forced disconnect → checkpoint → следующий согласованный input | Offline fixture PASS |
| Checkpoint с активным tau + повтор input | Offline PASS |
| Два клиента Broken Arrow, LoadReadProbe | **NotRun** |
| Два клиента Broken Arrow, ObserveTwoClient | **NotRun** |
| Native source completeness / readonly hooks / HUD / live resync | **NotRun** |
| Native reader performance / live soak | **NotRun** |

Фактические логи автономных процессов включены в evidence. Они не доказывают runtime поведение игры. Новые реальные результаты необходимо записать в отдельный каталог с сохранением исходной offline записи, а не переписать эти строки на PASS вручную без трасс.
