# Gamelog — preflight3 / ТЗ №18-В

Отправка выполняется через UnityEngine.Debug.Log на Unity thread. Штатная цепочка:
Unity logger → FilteredLogHandler → Application.logMessageReceivedThreaded → очередь
FileLog → штатный writer. DF не вызывает FileLog.Init/WriteMessage, не заменяет handler
и не открывает Gamelog на запись. LogManager/DummyLogger и LogRelay не используются.

UnityGamelog читает текущие _isInitialized, _fileMaxSizeReached и _logFilePath.
GamelogProbe запоминает EOF и отправляет уникальный [DF] marker. Только обнаружение
точной записи после этого EOF даёт GamelogMarkerConfirmed. Чтение ограничено 64 KiB
раз в секунду; timeout 15 секунд. Rotation/truncation остаются unverified. Успешный
вызов Debug.Log не доказывает запись. Если FileLog не готов, probe остаётся NotSent.
Подтверждение одной записи не доказывает полноту или устойчивость Gamelog к сбою.

Структурированный JSONL preflight2 остаётся основным полным журналом Result: шесть
полных контрольных сумм, SessionHash/InputId/ResultSource. Native sink получает
редкие полные Result; при overflow фиксируется DiagnosticLoss и DF останавливается.
Безопасный экспорт START.ps1 -Action Collect сохраняет только разрешённую схему,
без сырого Gamelog, nonce marker, путей, SteamID, токенов и скрытого игрового мира.
Он сохраняет фиксированный код GamelogMarkerConfirmed либо код неуспешной проверки.

Сравнение двух экспортов — существующий dev-tools compare. CoordinatorReceipt не
выдаётся за независимый пересчёт. Полнота реального матча не подтверждается только
совпадением контрольных сумм.

Автономные проверки marker выполнены на искусственном файле. Настоящий Gamelog,
Unity/IL2CPP вызов и native drain ещё требуют разрешённого runtime-теста на обоих ПК.
См. T18B_REPORT.md и TESTER_QUICKSTART.md.
