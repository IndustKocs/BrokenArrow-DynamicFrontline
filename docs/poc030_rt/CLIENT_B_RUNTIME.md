# Клиент B — runtime

Дата: 30.10.2026. Пакет RC2: commit `007f943fb079c1862af9fe128b22d8d4d08e23da`. **Prepared / Runtime NotRun**. Настоящая игра, MelonLoader и сетевой матч не запускались; игровые файлы не изменялись.

Статус: NOT_RUN. Физический ПК/владелец роли B ещё не согласован. Локальный аудит не присваивает автоматически этому компьютеру роль A.

До R2 заполнить: владелец и согласие; ОС/NTFS; game root; build и четыре hashes; PackageHash/CoreHash/RulesHash/ABI/MapHash/ProfilesHash; отдельная локальная interop provenance; режим конфиденциальности; согласованный приватный endpoint; результат network-probe на двух разных ПК. Сертификат/PFX/общий ключ в отчёт не включать. PFX остаётся у A.

До R3: PASS R0/R1, согласие установки и запуска, LoadReadProbe; RosterValidated=false. Подтвердить loaded wrappers, generic/ref-return, FightId/scenario/world generation, owner/team, собственный полный roster, UnitID/NetID/local version, HP/stress/MaxStress/soldiers/position/loading/death/removal, фазу/возраст чтения. Сверить реальные bounds/roots/native nav с Airport-100. Записать по действиям, не просто итог «зелёный».

До R4: PASS R3 обоих, закрыть игры, новый session/config; explicit consent TrustedDiagnosticReplica с двух сторон для независимого полного пересчёта. Без него TeamSafe и соответствующий предел приёмки.

После каждого этапа сохранить отдельный каталог Session/Build/Role: очищенный collect, контекст без секретов, действия оператора, ошибки и метрики. Runtime поля сейчас UNKNOWN, не нулевые значения. Двухмашинный network-probe: NOT_RUN.
