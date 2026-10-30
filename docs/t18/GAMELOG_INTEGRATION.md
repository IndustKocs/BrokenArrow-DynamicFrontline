# [DF] диагностика — preflight2

Native sink: существующий LogManager.GetLogger("DynamicFrontline").Info(string).
Успешный вызов ещё не доказывает попадание в Gamelog-файл; runtime-проверки нет.

Обычные сообщения сохраняют лимит 4/сек и подавление повторов kind (2 секунды).
Result имеет отдельную очередь: полный структурированный PascalCase JSON с Kind=Result,
InputId, SessionHash, ResultSource, шестью Digests и разрешёнными числовыми метриками.
Нет усечения до 320 символов, JSON-в-строке или отбрасывания Result по частотному лимиту.
Gamelog получает выборочные полные Result до 4/сек; резервный JSONL — все.

Резерв: UserData/DynamicFrontlineSteam/logs/DF-*.jsonl. Ротация 16 MiB.
Очередь Result 512 записей; переполнение = DiagnosticLoss и остановка расчёта.
Ошибка записи также останавливает DF. Raw fallback может содержать приватный текст
обычных сообщений и НЕ предназначен для пересылки.

Экспорт через START.ps1 -Action Collect собирает новые записи по типизированной схеме,
а не редактирует строки blacklist-заменами. Имена файлов заменены; свободный текст,
пути, UTC, SteamID/Owner/participants, world/replay и loader logs исключены.
Сохраняются digest-поля и псевдоним матча SessionHash для сравнения. Неизвестные kind/поля
отбрасываются, повреждённые Result дают Complete=false/DiagnosticLoss. Sensitive replay
флаг больше не открывает путь обхода безопасного экспорта.

Compare понимает [DF]-строки, plain JSONL, файлы и каталоги экспортов, проверяет
session+InputId, локальные конфликты, пропуски и потерю диагностики. Passed означает
совпадение имеющихся наборов checksums, не успешный игровой опыт.
CoordinatorReceipt на B проверяет передачу проекции и полученные хеши; независимый
пересчёт возможен только при действительных Coordinator/Independent записях.
CompleteSessionVerified всегда false: завершённый игровой сценарий подтверждается отдельно.
