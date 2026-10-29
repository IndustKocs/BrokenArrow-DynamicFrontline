# Исходная точка и трассировка

Изолированный worktree создан от чистого HEAD a5f1745fa26cc6171150db37805fe8731b2261bc. Исходная feature/prototype-023-zoc-encirclement не переключалась. Набор UA01 доступен локально; хеши всех верхнеуровневых отчётов/JSON/CSV сохранены в evidence/poc030/ua01-source-hashes.json. Хеш ТЗ сохранён отдельно. Инструменты: SDK8.0.426, net6.0, MelonLoader0.7.3.0, Interop1.5.1.0; точные SHA в dependencies.json. Исторические 284 входят в 356, тесты Launcher не прибавляются.

| Требование/архивное замечание | Перепроверка текущего кода | Статус |
|---|---|---|
| Штурмовая пехота не захватывает | GameUnitProfileResolver уже допускает Role30/31 с явным профилем; регрессии Integration прошли | Не воспроизведено; поведение сохранено |
| Внешний AttachReadOnlySession вместо bootstrap | ReadOnlyMod исходного HEAD требует внешнего вызова; новый ProbeMod получает GameController.Instance | Воспроизведено; новый reader реализован, runtime не проверен |
| CommandChangedEvent удалён | Новый reader не читает команды и не подписывается; старый читает CurrentCommand по имени | Не является текущей подпиской; адаптация не требует выдумывать replacement event |
| Smoke/Settings offsets | В новом reader нет сырых смещений/дыма; Core geometry не изменялся | Функция не используется; не объявлять smoke runtime совместимым |
| UID=canonical identity | Core key generation недостаточен для доказательства межклиентного spawn; новый LocalIdentity отделён, SpawnGeneration=null | Воспроизведён контрактный пробел; запись/прогноз заблокированы |
| Скрытый enemy исчезает = смерть | Новый reader не претендует на enemy roster, Complete всегда false; 72-тестовый набор сохраняет unknown | Runtime полнота неизвестна; отсутствие не превращается в уничтожение |
| Supply заранее знает End | SupplyReliefEvaluator принимает завершённый ServiceInterval | Воспроизведено; новая StreamingRelief принимает begin/receipt/continue/end, native источник отсутствует |
| Native score дубликат с иным payload | Victory.Ingest пропускает уже виденный ID без сравнения payload | Воспроизведено; native feed не подключён; EffectJournal не считается исправлением старого ledger |
| Geometry grid independence | Старые ring/open-gap тесты не доказывают произвольные clearance/concave cases | Не закрыто; добавлен один физически одинаковый ring/gap на двух сетках |
| Сумма p95 разных стадий | Старый benchmark не включает полный Step и cold geometry в общий pipeline | Ограничение воспроизведено; новый короткий Step+serialization benchmark, не runtime |
| Повторная revision, checkpoint, pause, final cutoff | Текущие 72 группы проходят заново | В проверенном домене не воспроизведено; не объявлять новые дефекты по старому отчёту |
| Полноценный PvP / native authority | Нет runtime доказательств | Неизвестно, не разрешено |

ТЗ16 A: выполнены изоляция, fingerprints, регрессии, адресная проверка перечисленных замечаний. B: реальный reader и новые wrappers, остальные провайдеры заблокированы/не реализованы (матрица отдельно). C: реальный status transport + offline guards; world snapshot/delta/resync отсутствуют. D: бинарный архив и sandbox installer; общий readiness blocked. E–G: NotRun. H: передача частичного результата с точными ограничениями, без игровых A/B логов.
