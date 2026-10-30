# RC2 Airbase / 5454584 — отдельная диагностическая сборка

Ветка codex/rc2-airbase-5454584, база007f943 с последующим статическим аудитом8689420. Игра и сценарий не изменялись, мод не устанавливался. RuntimeValidated=false. Точная ревизия и SHA256 DLL — BUILD_RECORD и inventory архива. Старый ZIP Airport сохранён.

## Что установлено и изменено

MapSettings Airbase содержит MapSize9000×9000. PlayableZone..ctor берёт MapSize, умножает на0.5 для center/extents, копирует full-map bounds в начальную playable zone. Константа0.5 прочитана из pinned GameAssembly, VA18677FC00. Значит статическая полная область X/Z=[0,9000]. В5454584 нет объектов PlayableZone и script nodes b/c/d пусты. Это основание default descriptor, а не утверждение об уже проверенном матче. Native PlayZoneBounds всё равно сверяются на каждом полном наблюдении; другое значение блокирует расчёт.

Descriptor config/poc030-rc2/maps/Airbase-5454584-100.json:90×90,100м,Origin(0,0),8100cells,7746supported,354 неоднозначных исключены,14665native witness edges. Граф Airbase.ng:90000узлов,338770 взаимных land links. Native nav30м не равен расчётной сетке100м. Новые root candidates: spawn28/team1 в(56.7489548,3778.56787),spawn29/team7 в(8930.753,4060.57471). Обе ячейки доказанно поддержаны статическим графом. Game participants должны подтвердить native teams1/7; internal DF teams0/1 соответствуют их сортировке.

CanonicalWorld.MapDescriptor поддерживает только две явные пары: Airport/Airport_network и Airbase/5454584. Runtime source validation новой пары хеширует в порядке: Airbase.ng, пользовательский bascr, MapSettingsAirbase.bundle, meta.json. Имена/path фиксированы, произвольные пути не принимаются. CheckObserved требует scenario5454584,navNameAirbase,cell30,bounds9000 иteams1/7. prepare-client поддерживает новую пару. Старые Airport guards сохранены. Все вычисления идут в прежний Simulation023 Observe; writers не добавлены.

compile-airport.py --airbase создаёт descriptor из реального графа и локально decoded snapshot. Требует pinned bascr/meta/settings/nav; пустую script logic и отсутствие explicit zone. Исходные decoded JSON остаются local-only; не входят в архив. Полный sourcehash меняется при правке сценария/metadata. Не исправлять его вручную для обхода проверки.

## Ограничения

Это подготовка к LoadReadProbe→ObserveTwoClient, не подтверждённый игровой мод. R0/Scenario admission и согласие установки остаются отдельными. Неизменённый allowlist18profiles,100 суммарных units,4Гц. Неизвестные UnitID не отбрасываются из якобы полного мира. Dynamic navigation,actual root mapping,loaded generic getters/HUD/services требуют игрового подтверждения. Positive service proofs не доказывают непрерывные grace intervals; native effects выключены. HUD-предупреждение StaticMapGeometry_DynamicChangesUnvalidated теперь не содержит ошибочного названия Airport.

## Проверки

Фактические новые логи — evidence/airbase. Исторические docs/poc030_rc2 и остальные evidence относятся к прежней поставке; не считать их новым игровым прогоном. Новые тесты охватывают реальные размеры/roots, неверные scenario/nav cell/bounds/teams и изменение каждого source file. Сетевой тест с этой картой использует два локальных процесса и синтетические owned observations, не две игры. Длительный offline benchmark использует реальную статическую геометрию и синтетические100units. Native reads/HUD/TLS в его время не включены. Allocation target0.5MiB остаётся отдельным ограничением.

Установка/удаление — прежний transaction CLI, с отдельно разрешёнными действиями. Локальный interop остаётся для той же игровой сборки26273833, четыре pinned hashes не менялись. Нет автоматической установки или публикации сценария.

Фактически:49/49 RC2 tests и44/44 RC1 checks PASS; два TLS процесса с Airbase descriptor и reconnect PASS. MapDescriptor воспроизведён побайтно, runtime source-check выполнен автономно по реальным файлам, четыре игровых хеша/Steam build совпали.

Нагрузочный прогон:100units×10800steps, warmup128, p95=12.674мс,p99=13.911мс,max52.192мс,deadline>250мс=0,1.725MiB/шаг. RawCSV.GZ приложен. После этого изменён только текст предупреждения StaticAirportGeometry→StaticMapGeometry; measured-assemblies фиксирует именно измеренные бинарники, Frontline math не менялась. Это45мин модельного времени, wall123.81с, не live soak. Прежняя цель0.5MiB/шаг не достигнута; ограниченный диагностический режим100units сохраняется.

Распакованный архив-кандидат прошёл полный sandbox install/verify/remove на A/B с локальным interop и возвратом прежнего кэша; packaged replay PASS. Финальная упаковка меняет только evidence/документы/revision record, DLL остаются проверенными.
