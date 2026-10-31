# Launcher 0.4.1 / auto1

Постоянный EXE проверяет публичный GitHub Releases при открытии. Допускаются заданный owner/repo, HTTPS GitHub release URL, единственный launcher asset и опубликованный релиз; prerelease следует политике канала. SHA256 и размер проверяются по HTTPS API. Это целостность относительно доверенного GitHub-канала, не Authenticode. Лимиты: manifest 4 MiB, EXE 64 MiB, таймауты сети. Токен updater не использует.

Запись через временный файл и atomic rename в отдельный кэш вне игры. Перед запуском повторно проверяется SHA256 относительно имени каталога. Reparse paths запрещены. Исходный EXE и receipts сохраняются. Handoff marker связан с SHA256 исполняемого EXE и предотвращает цикл. Установка мода или запуск игры автоматически не выполняются.

При сетевой ошибке или невалидном release текущий UI сохраняется. Private EXE updater пока не поддерживается. Старым 0.4.0 нужен один ручной переход на 0.4.1.

19 автономных проверок: release selection, prerelease/draft, origin/size/hash rejection, повторный кэш, подмена/усечение payload, подмена кэша, сохранность исходного EXE, запуск проверенной копии только в --self-check (GameStarted=false). Финальный ZIP inventory и embedded ZIP проверяются при выпуске. Полная интерактивная смена окна через GitHub ещё не проверялась; игру не запускали. Мод остаётся full-debug5, ядро/interop/HUD/Steam не менялись.
