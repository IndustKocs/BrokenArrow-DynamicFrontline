# Проверки PoC030

| Уровень | Факт | Вывод |
|---|---|---|
| Исходный Core/Frontline/Integration/Combat/Pass04 |36+59+89+58+42=284, все passed| Регрессии проверенного конечного домена |
| Исходный PoC023 |72 группы passed; четыре replay, включая2700s PendingPolicy| Итого356, без двойного подсчёта284 |
| Новые offline guards |44/44| HMAC/handshake/size/replay/gap, closed writes, roles, changed pair, streaming service, uncertain apply, binding epoch |
| Новые interop outputs |168DLL;14 контрактов found| Только generated metadata, не native invocation |
| Два OS-процесса |A/B AuthenticatedStatusOnly| Реальный localhost TCP; не две игры |
| Полный автономный Step |6 наборов×32 шага| 100/500/1000; одинаковые3200×3200m, cell50/100; не2700s load soak |
| Установка/удаление |См. artifacts/poc030/sandbox-results.json| Только искусственный каталог; никакого installApproved |
| Настоящий IL2CPP reader/HUD |NotRun| Игра не запускалась |
| Два игрока в закрытом матче |NotRun| Нет отдельного разрешения/доступа ко второй машине |
| EffectsTest/FullModeTest |NotRun/Blocked| Нет native writer и полномочий |

Evidence: artifacts/prototype023/tests.log, regression/*.log, четыре replay outputs; artifacts/poc030/tests.log, loopback/A.json+B.json, interop/*.log, evidence/poc030/generated-binding-check.json, performance/pipeline.json. Хеши финальных evidence сохраняются в evidence/poc030/results-manifest.json.

Первый loopback harness проверял B после штатного закрытия A, получив Disconnected. Исправлен момент измерения: обе стороны фиксируют аутентифицированный обмен до завершения опыта, затем корректно закрывают сокеты. Это исправление стенда, не объявление сетевой ошибки игры.

Не проверены реальными вызовами: generic ref Get<T>, чтение struct/enum/interop enumeration, доступность всех типов при Melon OnInitialize, GUI.Box unstripping, фактический loader cache, приватность источника вне owned gate, native pause/phase, server authority. Не подменять эти пункты 44 synthetic проверкой.
