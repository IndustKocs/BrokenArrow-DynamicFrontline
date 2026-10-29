# RC2 — результаты проверки

356 прежних regression groups, 44/44 RC1, 47 групп RC2 проходят. Фактические журналы: evidence/regression-final.log, rc1-package-build.log, tests-package.log, tests-package/results.json. Двенадцать дочерних процессов намеренно завершаются fail-fast; recovery проверяется родительским тестом. Проверены retirement старого interop, rollback, mutable config, lifecycle, service dedup, checkpoint с активным tau, независимый геометрический oracle, replay/collector/compare.

Финальный TLS/resync: evidence/two-process — два независимых процесса, synthetic owner observations → TLS → canonical world → ядро → own projection. Input hashes после разрыва одинаковы. network-probe-A/B.json проверяют endpoint/pin/HMAC/context, без игры.

Свежий interop: 198 DLL + 2 базы; 30 групп metadata bindings, не вызов IL2CPP. preflight-final.log — чтение pinned игровых файлов. Полные manifests A/B прошли sandbox install/verify/remove с восстановлением прежнего кэша.

SDK8.0.426, target net6.0, pinned MelonLoader; ноль warnings/errors. Команды: build-prototype023.ps1; scripts/poc030/build-poc030.ps1; dotnet build scripts/poc030-rc2/dev-tools/dev-tools.csproj -c Release; dotnet run --project tests/DynamicFrontline.Rc2.Tests -c Release -- <output>; scripts/poc030-rc2/test-two-process.ps1 -DotnetPath <dotnet> -Reconnect.

Промежуточный tests-retirement.log содержит единичный UnauthorizedAccessException; повтор и финальный suite прошли. Прежние source-health race и clock-skew resync исправлены, финальный двухпроцессный тест прошёл. Физическое отключение питания не тестировалось.

NotRun: игровые A/B, loaded generic bindings, actual HUD, native service coverage, dynamic map, live timing/soak. Offline performance-пределы перечислены отдельно. Диагностическая поставка не означает полную игровую приёмку ТЗ17.
