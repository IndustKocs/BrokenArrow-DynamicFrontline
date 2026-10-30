# Launcher — отчёт автономных испытаний

Реализован отдельный DynamicFrontlineLauncher.exe (WinForms, системный .NET Framework), без кнопки запуска игры. EXE содержит пакет и backend. Сам EXE не устанавливает игровой мир: backend вызывает START.ps1 и существующий DevInstall/WAL/receipt. Добавлен только явный параметр удаления собственных настроек; исходный default сохраняет их.

## Проверено на текущем Windows-стенде

- EXE собирается; окно отрисовано в bitmap и просмотрено. Предупреждение постоянно видно; кнопки «Играть» нет.
- Поиск Steam по HKCU/HKLM + libraryfolders.vdf обнаружил локальную игру. Первоначальный пропуск HKLM исправлен и проверен повторно.
- Bootstrap Windows PowerShell 5 → проверенный portable PowerShell 7.6.9 → controller → START Verify выполнен в sandbox. Исправлена UTF-8 обработка путей с кириллицей.
- Официальный portable архив скачан, SHA256 CE7484804C0F0AD4DDA10C958E71ACDAF7EEBFB22505C1B480F89E0E9F1DA94D совпал с GitHub asset digest.
- Controller Install / Verify / повторный Update / Remove: 455 файлов, подготовка вне игры, конфликтов нет.
- Намеренно повреждённая собственная DLL обнаружена Verify; её изменённые байты не были молча приняты.
- RC2 regression: 52/52, включая прерывания update/removal в нескольких точках, восстановление receipt/байтов, чужие файлы, сохранение mutable config, новое явное удаление собственной изменённой настройки с сохранением постороннего файла.
- FullDebug/Steam/diagnostics: 87/87; RC1: 44/44; Simulation023: 72/72 (см. приложенные результаты).

Controller сохраняет точный package/work path до начала транзакции; Recover использует ту же подготовку. После восстановления состояние определяется по фактическому плану, а не только по номеру версии. Обновление использует Apply поверх действующего receipt, без отдельного удаления старой версии. Изменённые неизвестные DLL/loader сохраняются и блокируют запись. Для исправления произвольной порчи нет force-перезаписи.

## GitHub Releases

Настроен private IndustKocs/BrokenArrow-DynamicFrontline. Встроенная установка не требует GitHub token; private updates требуют персонального read-only доступа. Токен передаётся только через окружение дочернего процесса, не включается в JSON request/state и маскируется в журнале. Репозиторий не переводится в Public.

Публикация НЕ выполнена: доступ из сессии не авторизован. Скрипт publish-release.ps1 проверяет private=true, создаёт draft prerelease и загружает EXE/ZIP/SHA256/notes. При отсутствии credentials останавливается. Не публикуйте ключи, исходные игровые файлы, interop, raw logs или каталог local-preparation.

## Ограничения / NotRun

Чистая Windows VM отсутствует; полный GUI-click путь установки на чистом ПК и установка отсутствующего системного Desktop Runtime не объявляются проверенными. Bootstrap проверен отдельно с официальным архивом; Cpp2IL/loader/Unity зависимости в sandbox использовали проверенный cache и локальный interop. Генерация interop из настоящих файлов уже была выполнена read-only в preflight2, новые bindings — проверены в preflight3.

Private Releases API с реальными credentials, загрузка опубликованного обновления, GitHub permissions двух тестеров и Authenticode signing — NotRun/не настроено. Локальный Update проверяет backend и транзакцию, а не GitHub authentication.

Реальная установка мода и запуск Broken Arrow не выполнялись. Контрольные суммы двух клиентов, Gamelog, native Scenario gate и Steam Relay требуют отдельного разрешённого игрового испытания. Общий runtime и не принадлежащие receipt логи не удаляются. Portable инструменты и архивы остаются вне игры в LocalAppData/DynamicFrontlineLauncher и занимают место; их автоматическая уборка не включена.

Каждая смена согласия обновляет согласованный consent epoch и session binding; повторное включение FullDebug не повторяет прежний SessionHash/InputId диагностического журнала.
