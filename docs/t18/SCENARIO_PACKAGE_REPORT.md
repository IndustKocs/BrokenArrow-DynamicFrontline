# full-debug7 — поставка сценария 5454584

В пакет добавлены только авторские 5454584.bascr и meta.json из проверенного Workshop item 3830189668. steamid, Workshop cache и ресурсы Airbase не распространяются. Исходники включены в SHA256_INVENTORY и подготовку START.ps1; установка через launcher/controller. MapDescriptor и source hash не менялись.

В receipt добавлены категории ScenarioPayload (8) и BorrowedScenario (9), с allowlist ровно двух путей. BorrowedScenario запрещён во входном манифесте. Совпадающая предсуществующая полная пара помечается borrowed без записи; отличающаяся/неполная авторская копия блокирует Apply до WAL/замен. Borrowed проверяется при Verify, никогда не перезаписывается при обновлении и сохраняется при Remove независимо от последующих авторских правок.

Новые файлы принадлежат установщику. Они проходят обычный WAL/staging/SHA256/receipt процесс. Если один файл изменён пользователем, обновление блокируется и Remove сохраняет всю пару с конфликтом. Другие файлы папки и Workshop не затрагиваются. Откат незавершённой операции восстанавливает исходное состояние.

Автономные тесты: новые install/update/remove; identical author borrow/reinstall; author edit + removal; foreign/incomplete refusal before writes; partial-pair/receipt-only category rejection; edited owned pair preservation; injected interruption after first replacement on fresh install and upgrade with recovery of both files. Всего Steam 95, RC2 59, RC1 44, Simulation023 72 = 270. Дополнительно regression IL/interop и launcher updater. Полный пакет проверен отдельно в sandbox: см. evidence/t18-debug-launcher/scenario-package-validation.json.

Реальная установка игры не изменялась и игра не запускалась. Текущая авторская копия на ПК прочитана только для сравнения: оба хеша совпадают с пакетом, поэтому при последующей разрешённой установке она будет borrowed, а не присвоена установщиком. Реальный двухклиентный матч не объявляется проверенным.
