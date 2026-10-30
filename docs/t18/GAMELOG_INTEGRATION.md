# Gamelog / [DF]

Используется существующий `BrokenArrow.Client.Ecs.Utils.Log.LogManager.Instance`,
`GetLogger("DynamicFrontline")`, `ILogger.Info(string)`. События направляются в уже
настроенные игрой handlers. Не открывается GameLogicLogger binary stream и не
добавляется произвольный текст в бинарный лог игры.

Формат строки: `[DF] {"v":1,"utc":"...","kind":"Status","detail":"..."}`.
Kinds: Build, Consent, Lobby, Connect, Status, Roster, RosterAttestation, Match,
ReadUnavailable, Resync/Result, Reconnect, HudUnavailable, Stopped, Shutdown.
Нет записи игровых наблюдений целиком или секретов в каждый кадр.

Ограничения: до 4 строк/сек, одинаковый kind не чаще одного раза за 2 секунды;
входная очередь 128, резервная память 256 строк, detail до 320 символов,
резервный файл до 16 MiB. Переполнение учитывается счётчиком Dropped.

Резерв: `UserData/DynamicFrontlineSteam/logs/DF-<UTC>.jsonl`.
Native logger вызывается на основном потоке. При его недоступности сохраняется
резервная диагностика. HUD не утверждает, что строка уже оказалась в Gamelog:
`GameLoggerAccepted_UnverifiedFile` означает только успешный вызов интерфейса.
Маршрутизация именно в файл Gamelog должна быть подтверждена реальным тестом.

Сбор: `scripts/t18/collect-logs.ps1 -GameRoot <папка игры> -OutputDirectory <новая папка>`.
Он копирует четыре последних DF JSONL и фильтрованный Latest.log, без конфигов/ключей.
После матча найдите `[DF]` в актуальном штатном Gamelog и приложите соответствующий
фрагмент. Если строк нет — сообщите это вместе с fallback; не считать native sink проверенным.
