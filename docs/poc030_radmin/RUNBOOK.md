# Два ПК: Radmin LoadReadProbe

A:26.137.77.181. B:26.204.104.7. Port47617. Все команды выполнять в PowerShell7 из корня нового распакованного ZIP, например C:/DF-RC2-Radmin. Не перемещать его после создания probe.json. Подготовительные команды ниже не устанавливают мод. Установочные команды применять только после отдельного разрешения и при закрытых играх.

## 1. Оба: проверить пакет, игру и сценарий

Сверить внешний SHA256 из .zip.sha256; inventory SHA256_INVENTORY.json должен совпасть по каждому файлу. Не смешивать DLL старых пакетов. Проверить четыре игровых файла/build26273833 существующим scripts/poc030/preflight-runtime.ps1. Файлы Scenarios/5454584/5454584.bascr и meta.json должны совпасть с config/poc030-rc2/airbase-source-pins.json; map=config/poc030-rc2/maps/Airbase-5454584-100.json.

На B игровые hashes ранее независимо не проверены — перед модифицированным запуском это требуется. Radmin должен быть включён, адрес каждогоПК должен совпадать сролью. Изменение адреса — остановка, не ручное отключение guard.

## 2. Оба: локальный interop

По docs/poc030_rc2/INTEROP_BOOTSTRAP_A_B.md подготовить .NET6 runtime, PowerShell7, pinned MelonLoader0.7.3,Cpp2IL и Unity dependencies. Генерировать interop отдельно на каждомПК вне игры. LoaderDirectory при генерации — распакованный каталог инструментов, не автоматическая установка. Получить собственный local-interop-manifest.json. Не передавать generated wrappers вместо генерации. Прежняя проверенная локальная генерация допустима только при повторной сверке всех source/provenance hashes.

## 3. Только A: создать новую приватную пару

```powershell
./scripts/poc030-rc2/new-pair.ps1 -OutputDirectory 'C:/DF-private/radmin-pair-01' -HostAddress '26.137.77.181' -Port 47617 -RadminPair
```

Передать другу только подкаталог B приватно. Друг размещает его как C:/DF-private/radmin-pair-01/B. A/server.pfx не передавать. Не отправлять ключи/PFX вместе с диагностикой. Сертификат действует 7 дней; новую пару создавать после истечения/смены сеанса.

## 4. Оба: подготовить свой client

Задать свой путь dotnet и interop. На A:

```powershell
$dotnet = (Get-Command dotnet).Source
$role = 'A'
$interop = 'C:/DF-local/interop-A/local-interop-manifest.json'
$client = 'C:/DF-private/radmin-client-A'
./scripts/poc030-rc2/prepare-client.ps1 -Role $role -PairDirectory "C:/DF-private/radmin-pair-01/$role" -MapFile './config/poc030-rc2/maps/Airbase-5454584-100.json' -InteropManifest $interop -OutputDirectory $client -Mode LoadReadProbe
```

На B заменить role='B',interop-B и client-B. OutputDirectory должен быть новым. В обоих probe.json должны быть Host26.137.77.181,Port47617,RadminPair=true,ReplicationTeamSafe. RuntimeReadAuthorized пока false. Не включать RosterValidated/TrustedReplicaConsent/DetailedReplayConsent/PassiveServiceProbe для начального теста связи.

## 5. Network-probe до установки и запуска игры

На A и B с разницей не более нескольких секунд выполнить:

```powershell
./scripts/poc030-rc2/run-dev-tools.ps1 -DotnetPath $dotnet network-probe "$client/probe.json" "$client/network-probe.json"
```

Оба Passed=true обязательны. Операция не запускает игру/native reader. При timeout можно повторить после проверки Radmin/адресов. Не менять pin/HMAC и не использовать 26.204.104.7 как Host на B. Если firewall блокирует, остановиться и согласовать адресное правило: только нужная программа/порт 47617 и peer26.204.104.7 на A; глобальную защиту не отключать. Никакое правило пока не создавалось. Успехпробы не подтверждает внутри­игровые данные.

## 6. Установка только после отдельного разрешения

У обоих закрыть BrokenArrow/EACLauncher. Установить pinned MelonLoader по официальной процедуре с отдельным inventory/backup, без первого запуска. Затем повторить prepare-client из пункта 4 с новым OutputDirectory и -AuthorizeRuntimeRead; обновить$client. Role/Map/Pair прежние, ModeLoadReadProbe. Проверить план:

```powershell
$game = 'D:/steam/steamapps/common/broken_arrow'
./scripts/poc030-rc2/run-dev-tools.ps1 -DotnetPath $dotnet plan $game "$client/client-manifest.json" "$client/install-plan.json"
```

После проверкиплана и разрешения:

```powershell
./scripts/poc030-rc2/run-dev-tools.ps1 -DotnetPath $dotnet install $game "$client/install-plan.json" --explicit-install-consent
./scripts/poc030-rc2/run-dev-tools.ps1 -DotnetPath $dotnet verify $game --explicit-install-consent
```

Существующий mutable probe.json сохраняется: отдельно проверить его соответствие подготовленному. Не считать reinstall переключением режима. Подробный откат: docs/poc030_rc2/INSTALL_RECOVERY_AND_CONFIG.md. Recover/removeRC2 не удаляют отдельно установленный MelonLoader.

## 7. Первый совместный запуск

Только при подтверждённом штатном допуске Scenario+loader и согласии обоих использовать официальный Anti-Cheat Disabled. Loader параметры: --melonloader.agfoffline --melonloader.agfvdumper 2022.1.0-pre-release.21. Никаких модификаций античита/авторизации. Приватный Scenario5454584,два человека,противоположные стороны,без ботов/наблюдателей.

Проверить загрузку и native sources сначала на небольшом наборе поддержанных units. MapAirbase,scenario5454584,nav30,bounds0..9000,teams1/7; actualHP/stress отдельно отпрогноза. Неполный roster до проверки —Partial, не ставить RosterValidated заранее. До 100units. При reader/interop/hash ошибки, утечке скрытых данных или превышении budget остановить этап. ObserveTwoClient не включать в этом сеансе.

## 8. Диагностика

Собрать существующим collect обычные JSONL накаждомПК. Для первой связи достаточно прислать network-probe.json обоих — без pair/probe configs,keys/PFX. Для game логов использовать отдельные папки A/B и Session+PackageHash. Не публиковать сырые Gamelog. Подробный replay требует отдельного согласия. Никакие прежние loopback результаты не считать реальным RadminPASS.
