# Совместимость

Historical baseline284=36 Core +59 Frontline +89 Integration +58 Combat +42 Pass04. Воспроизведены до изменений и после. Старые ожидания и evidence не переписаны. Добавлены72 новые группы в отдельном executable тестов; суммарно356 групп. Это группы, не утверждение о356 независимых game-facing проверках.

F02–F06 и старые исходники Core022 не менялись. Legacy Simulation сохранён; новый Simulation023 выбирается явно конструктором и версией Zoc_023/1.2. Checkpoint несовместимых session/config/version/map geometry отклоняется. Runtime no-op/loaders сохраняют прежнее поведение, новая игра не запускается.

Совместно собираются Core, Frontline, GameAdapter, Integration, Explorer, Runner. Оба прежних MelonLoader проекта компилируются с прежними read-only dependencies, скопированными только в ignored tools новой ветки. Это не проверка hooks/runtime.

Первая попытка общего regression script выявила отсутствие скопированных config fixtures; script исправлен, повторный полный прогон успешен. Попытка сборки одновременно с benchmark упёрлась в Windows DLL lock; финальная сборка выполнена после завершения benchmark.
