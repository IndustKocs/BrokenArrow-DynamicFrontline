> Дополнение FullDebugView + Launcher: актуальные документы — [установка](INSTALL_GUIDE_LAUNCHER.md), [FullDebug](FULL_DEBUG_VIEW_REPORT.md), [испытания лаунчера](LAUNCHER_TEST_REPORT.md), [runtime](RUNTIME_CHECKLIST_FULLDEBUG.md). TeamSafe по умолчанию; полная геометрия разрешается только после двух согласий.

> Актуальная ревизия: preflight3 / ТЗ №18-В. См. [T18B_REPORT.md](T18B_REPORT.md). Unity Debug + read-only marker receipt; строгий session gate; PocketCells отключён; handshake после входа в доказанную комнату. Ниже сохранён предыдущий контекст, при расхождении действует отчёт ТЗ №18-В.

# Визуализация

`DisplayProjection` создаёт immutable DisplayGrid из существующего ObservationBridge /
Simulation023: собственные клетки territory checkpoint, Interior подтверждённых
EncirclementCertificate своей команды и forecast intents. Перерасчёта формул в renderer нет.

В режиме TeamSafe клиент B получает TeamDisplayT18 с Public023 и DisplayGrid. Session,
время, команда, размер, индексы и числовые значения проверяются. A строит аналогичную
свою проекцию. При неполном roster, stale peer, ошибке, resync или смене матча проекция
очищается; прошлый фронт не изображается как текущий.

Unity IMGUI рисует отдельную тактическую карту 300×300 внутри игры. Сетка визуализации
30×30 консервативно объединяет исходные клетки; это визуальное упрощение, не изменение
расчётной сетки 90×90. Верх — увеличение world Z.

- Синий: своя территория; голубая граница — её внешний контур.
- Серый: Unknown, не обещание нейтральной или вражеской территории.
- Оранжевый: области собственных котлов из геометрического сертификата.
- Зелёные окружности: ZOC выбранного в HUD своего юнита; переключатель показывает
  до 20 собственных ZOC. Скрытые вражеские позиции не рисуются.
- Белая метка: выбранный в HUD свой юнит.

HUD показывает Encircled / Pending / ReleasePending / DataUncertain и tau.
Фактический стресс читается из OwnedSample. Прогнозный Delta берётся только из
StressEffectIntent. При отсутствии доказанного интервала — UNKNOWN, а не ноль.
Никаких native effects renderer не применяет.

В первой версии нет world-space overlay на ландшафте, подмены штатной миникарты и
связи с нативным выделением. Сигнатуры GUI проверены по wrappers; внешний вид,
частота кадров и корректность Unity OnGUI ещё требуют запуска на обоих ПК.
