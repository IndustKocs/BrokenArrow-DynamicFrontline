# Первый опыт: два ПК, Airport / Airport_network

## 0. Что уже разрешено и что ещё нет

Подготовка файлов и автономные проверки разрешены. **Установка в настоящую Broken Arrow, запуск загрузчика/игры и матч требуют отдельного согласия.** Этот документ готовит последующие действия, не выполняет их. Native effects отсутствуют.

Первый игровой опыт проводится сразу на двух клиентах: A и B, закрытый PvP, два человека на противоположных командах, без bots/spectators. Не начинать одноклиентную игровую пробу вместо выбранного сценария.

## 1. Одинаковая поставка и локальные зависимости

1. На обоих ПК распаковать один приватный RC2 ZIP в постоянную папку, проверить внешний SHA256 и внутренний package inventory.
2. Проверить Steam app1604270 build26273833 и четыре игровых файла через preflight-runtime.ps1. Любое несовпадение — остановка, без пропуска guard.
3. Выполнить INTEROP_BOOTSTRAP_A_B.md на каждом ПК вне игры. Не копировать чужие generated wrappers вместо локальной генерации.
4. Использовать одинаковый config/poc030-rc2/maps/Airport-100.json для первого опыта. 50м — отдельно измеренный сравнительный вариант, не автоматическая смена точности.
5. Сверить доступные колоды с MAP_AND_PROFILE_SUPPORT.md. Для первого опыта разрешены только перечисленные ID, максимум100 суммарных юнитов. Неизвестный ID должен остановить достоверный полный расчёт, а не исчезнуть из него.

## 2. Ручное приватное соединение без игры

Выбрать A как listener на его LAN/private VPN IPv4; B подключается к этому же адресу. По умолчанию порт47617. Не менять глобальные настройки firewall и не открывать публичный listener автоматически. Если потребуется правило firewall, применить отдельно согласованное правило только для выбранного private endpoint.

На A создать новую пару:

```powershell
./scripts/poc030-rc2/new-pair.ps1 -OutputDirectory 'C:/DF-private/pair-01' -HostAddress '192.168.1.10' -Port 47617
```

Передать только папку B второму владельцу приватным способом. A/server.pfx остаётся на A. Session key, PFX и подробный replay не отправлять в обычный диагностический архив. Срок сертификата7 дней; новая сессия/пара не должна случайно объединять разные матчи.

На каждом ПК подготовить свой config, пока без runtime authorization:

```powershell
./scripts/poc030-rc2/prepare-client.ps1 -Role A -PairDirectory 'C:/DF-private/pair-01/A' -MapFile './config/poc030-rc2/maps/Airport-100.json' -InteropManifest 'C:/DF-local/interop-A/local-interop-manifest.json' -OutputDirectory 'C:/DF-private/client-A' -Mode LoadReadProbe -TrustedReplicaConsent
```

На B заменить Role, PairDirectory, InteropManifest и OutputDirectory своими путями. Адрес в pair.json остаётся адресом A. TrustedReplicaConsent выбирается только при согласии обоих владельцев на передачу полного скрытого мира в приватный диагностический процесс. Без него используется TeamSafe и независимый пересчёт на B не ожидается.

Для проверки соединения запустить одновременно на обоих ПК:

```powershell
./scripts/poc030-rc2/run-dev-tools.ps1 -DotnetPath $dotnet network-probe 'C:/DF-private/client-A/probe.json' 'C:/DF-private/connection-A.json'
```

B использует свой config/output. Этот CLI не запускает игру и не создаёт native reader. PASS проверяет только endpoint, pin, HMAC и context. Он не подтверждает игровой источник или расчёт. При несовпадении pin/context проверить одинаковые пакет/map/profile/pair; не отключать проверки.

## 3. После отдельного согласия: LoadReadProbe на обоих

1. При закрытой игре установить отдельно pinned MelonLoader0.7.3 без первого запуска. Подробности и pins — INTEROP_BOOTSTRAP_A_B.md.
2. Повторить prepare-client в **новом** OutputDirectory с `-AuthorizeRuntimeRead`, Mode LoadReadProbe, одинаковым consent mode и при необходимости `-PassiveServiceProbe`. RosterValidated пока не указывать.
3. Построить plan, проверить список файлов, применить install с explicit-install-consent, затем verify. См. INSTALL_RECOVERY_AND_CONFIG.md. Если ранее существовал probe.json, installer его сохранит: отдельно сравнить сохранённый config с подготовленным. Не считать reinstall переключением режима.
4. Включить требуемые loader launch options, затем после согласованного допуска запустить обе игры. Создать один закрытый Airport_network матч, противоположные стороны, два человека.
5. Оба HUD должны показать loaded reader/source metrics и связку участников. LivePartial до подтверждения roster ожидаем; это не ошибка, разрешающая подменить полный источник.
6. На каждой стороне вызвать по одному проверенному infantry/APC/IFV/tank/supply. Сверить собственный count, UnitID/owner/team, actual HP/stress, движения, загрузку/выгрузку, смерть и исчезновение. Native bindings должны читаться без exceptions, spikes и budget stops.
7. Проверить Pause/Resume, возврат в меню/сессию: NoSession/ReadUnavailable/Stale должны доходить peer; heartbeat не должен делать старую позицию свежей.
8. Проверить ScenarioMeta.Name=Airport_network, NavigationName=Airport/23м, фактические bounds малой зоны и соответствие native teams1/4 roots. Несовпадение не обходить редактированием descriptor под желаемый ответ.
9. Сохранить обычные логи обоих клиентов. Любое необъяснимое roster расхождение или отсутствие обязательного getter оставляет этап не пройденным. Без этого не включать RosterValidated.

## 4. ObserveTwoClient

После успешного двухстороннего LoadReadProbe закрыть обе игры, согласовать новый этап и подготовить новую пару/config для нового матча. Mode ObserveTwoClient, AuthorizeRuntimeRead, RosterValidated и одинаковый TrustedReplicaConsent. Максимум100 суммарных поддержанных юнитов. Сохранённый mutable config обновить явно только при закрытой игре; сделать его копию прежде, чем заменить подготовленным.

Последовательность опытов:

1. Проверить одинаковый native binding и hash context. A показывает CanonicalObserved, B — IndependentCanonicalResult; сравнить Result по InputId, а не только зелёный TLS status.
2. Пехота и БТР/БМП по обе стороны100м:400↔600; отдельно погрузка, высадка, гибель партнёра и tank как отрицательный партнёр. Не должно быть stacking или relay цепочек бонуса.
3. Перемещения Line/Assault: постоянный захват и движущаяся local area отдельно. Recon/Special role32 не захватывает. ZOC не перекрашивает землю.
4. Одиночный враг/односторонний заслон не дают котёл. Затем кольцо с непокрытым и заполненным центром, два отдельных котла, разрыв/слияние, выход через реальный проход. Недостоверный участок остаётся Unknown. Сравнить с геометрией игры, не только между одинаковыми копиями алгоритма.
5. Обычный обстрел/восстановление: actual HP/stress меняются штатной игрой. FORECAST отображается отдельно и ничего не записывает в игру.
6. Реальный ремонт/пополнение: положительные causal proofs должны относиться к собственному recipient и правильному поколению. Не считать proximity или animation доказательством. Пока interval coverage Unknown, supply/grace test записать как Unknown, не PASS.
7. Пауза, прекращение публикации одним клиентом, разрыв соединения, reconnect. Прогноз должен стать недоступным, затем продолжиться после checkpoint на свежем input. Не терять identity/timers и не начислять пропущенный интервал повторно. Перезапуск координатора с потерей истории требует новой session, не rollback canonical revision.
8. Проверить HUD/F8/F9. Скрытые вражеские позиции/круги/точный контур не должны появляться в публичной проекции. Текущая версия не рисует даже видимого противника без visibility binding.
9. Снять source read p95/p99, worker age, allocation/GC, queue/Unknown/deadline данные. Offline timings не заменяют этот пункт. Live45-minute soak требует отдельного согласованного выполнения.

## 5. Сбор, сравнение, replay и stop conditions

```powershell
./scripts/poc030-rc2/run-dev-tools.ps1 -DotnetPath $dotnet collect "$game/UserData/DynamicFrontline030/logs" 'C:/DF-results/A'
./scripts/poc030-rc2/run-dev-tools.ps1 -DotnetPath $dotnet compare 'C:/DF-results/A/RC2-A-....jsonl' 'C:/DF-results/B/RC2-B-....jsonl'
```

Выбирать логи одного этапа/сессии. Compared должен быть>0; missing InputIds перечисляются отдельно. Идентичный transport status не считается сравнением core.

Подробный replay включается заранее флагом DetailedReplayConsent только для короткого диагностического окна, на обоих ПК с согласия владельцев. Лог ограничен32MiB; при переполнении нельзя продолжать считать запись полной. Export такого лога: collect с `--include-sensitive-replay`. Затем:

```powershell
./scripts/poc030-rc2/run-dev-tools.ps1 -DotnetPath $dotnet replay 'C:/DF-results/private/RC2-A-....jsonl'
./scripts/poc030-rc2/run-dev-tools.ps1 -DotnetPath $dotnet metrics 'C:/DF-results/A/RC2-A-....jsonl'
```

Обычный collector оставляет только разрешённые метрики/results и не переносит configs, keys, PFX, game DLL или loader logs. Detailed replay содержит скрытый мир: хранить и передавать отдельно.

Stop: build/cache mismatch, reader exception, incomplete mandatory roster/profile/map, превышение budget, расходящиеся canonical digests, неизвестное поколение/owner или неожиданное изменение native состояния. Закрыть игру, собрать логи, при необходимости recover/remove по инструкции. Не переключать эффекты и не отключать guards ради продолжения опыта.
