# Подготовка Airbase / 5454584 на двух ПК

Сейчас разрешена только автономная подготовка. Ни один шаг ниже с изменением игры/запуском не выполнен автоматически. Сначала завершить допустимость R0 и согласовать установку с обоими владельцами.

1. Использовать одинаковый новый ZIP RC2-Airbase-5454584 и сверить SHA256/inventory. Не смешивать DLL с007f943.
2. На каждом ПК проверить build26273833 и четыре игровых файла. Локально подготовить interop по историческому INTEROP_BOOTSTRAP_A_B; не копировать чужие wrappers.
3. Подтвердить точный сценарий5454584. Поддерживаемый путь: Scenarios/5454584/5454584.bascr и meta.json в корне игры. Оба файла должны совпадать с config/poc030-rc2/airbase-source-pins.json; auto-renamed Workshop paths сейчас не поддерживаются. Любое изменение editor/meta требует нового аудита, не переписывания hashes. Сам bascr в ZIP не включён.
4. Создать новую приватную pair существующим new-pair.ps1 (согласованные LAN/private VPN endpoint/порт47617). Старый сеанс не использовать. Секреты/PFX не отправлять с логами.
5. prepare-client.ps1: RoleA/B, собственные PairDirectory/InteropManifest, свежий OutputDirectory, MapFile=<package>/config/poc030-rc2/maps/Airbase-5454584-100.json, ModeLoadReadProbe. AuthorizeRuntimeRead только после согласия на запуск, RosterValidated не ставить. TrustedReplicaConsent только с согласия обоих.
6. После R0–R1 и отдельного разрешения: закрытая игра, plan→проверка→install→verify, затем network-probe на двух реальных ПК. Installer сохраняет существующий mutable probe.json: его переключение проверить отдельно. Не выбирать Airport descriptor по старому примеру runbook.
7. LoadReadProbe: подтвердить mapAirbase/scenario5454584,nav30,MinX/Z0,SizeX/Z9000,две native teams1/7,root positions,полный owned roster и реальные profiles. При несовпадении остановиться; не редактировать bounds под желаемый результат.
8. Только после успешного R3 обоих: закрыть игры, новый session, ModeObserveTwoClient+RosterValidated. Повторить territory/ZOC/encirclement/read-stress tests прежнего runbook в этой карте. По умолчанию100units/100м. Dynamic geometry/service Unknown не превращать вPASS.
9. collect/compare/replay/metrics прежним CLI; хранить PackageHash/Session/MapHash с каждым набором. Не сравнивать input IDs разных sessions/packages. Detailed replay только отдельным согласием и коротким окном. Удаление/recovery по прежнему runbook, loader ownership отдельно.
