# ТЗ17-RT — подготовительный отчёт

Дата: 30.10.2026. Пакет RC2: commit `007f943fb079c1862af9fe128b22d8d4d08e23da`. **Prepared / Runtime NotRun**. Настоящая игра, MelonLoader и сетевой матч не запускались; игровые файлы не изменялись.

Это отчёт о выполненной разрешённой подготовке, не о состоявшемся игровом испытании. Ответ на вопрос «работает ли RC2 в настоящем двухклиентном PvP» пока **не установлен**. Продолжение требует отдельного согласия обоих владельцев и подтверждения допустимого режима; ТЗ не объявляется полностью принятым.

|Этап|Статус|Основание|
|---|---|---|
|Аудит локального комплекта|PASS|Внешний SHA256; точный inventory 303 файлов; собственные DLL, scripts, CLI, configs; зависимости совпали|
|Локальный preflight|PASS|Все четыре pinned файла и Steam build26273833 совпали; только чтение|
|Sandbox install/verify/remove/recovery|PASS|Архив распакован заново; два искусственных каталога, полный локальный interop, возврат прежнего кэша; 47/47 tests, включая 12 fail-fast процессов|
|R0 допустимость Scenario|PARTIAL|Официально документированный кандидат; текущая server policy, штатный launch option и допуск loader не проверены|
|R1 штатный Airport 1v1 без модов|NOT_RUN|Нет отдельного согласия/двух доступных физических клиентов|
|R2 подготовка игровых A/B|NOT_RUN|R0–R1 не пройдены; роли/второй ПК не предоставлены|
|R3 LoadReadProbe|NOT_RUN|Запуск/установка не разрешены|
|R4 ObserveTwoClient|NOT_RUN|Нет результатов R3 и согласия TrustedDiagnosticReplica|
|R5 восстановление в игре|NOT_RUN|Нет игрового сеанса|
|Live performance и soak|NOT_RUN|Синтетические цифры сюда не перенесены|

Исходные материалы: финальные docs/poc030_rc2/* и архив; MODDED_PVP_FEASIBILITY.md и NETWORK_ARCHITECTURE_AUDIT.md из research-tz18-lobby-gamelog/research/tz18a; UA01 UPDATE_AUDIT_REPORT.md. Исследовательские документы ссылаются на checkpoint11199c2 и используются только для native contracts/Scenario policy, не как описание финального RC2. Их файлы не изменялись.

Повторно проверены pinned MelonLoader0.7.3, Il2CppInterop1.5.1, Harmony, Cpp2IL, Unity dependencies и stripped plugin; packaged driver нашёл 30 metadata contracts. Это не loaded IL2CPP invocation. Канонические хеши вычислены средствами DLL поставки; CoreHash ниже означает SHA256 DynamicFrontline.Frontline.dll, а не ResultHash.

- **PackageHash**: `550E92DE5ED8F48A2945522859596792CFC17250741BA878CB3932624AEF61ED`
- **MapHash**: `92D7643DEA34B28C073556D639874ACBBBAF943292A6C5A0CE560C60BA33C95A`
- **ProfilesHash**: `87983EA813E3EB8DAA86A878D25D58AEAE366833E534F9706BA230ACB870056B`
- **RulesHash**: `2642826ED57BDD9699B9144A3C7F9E09FBDF1390EC0F10EF13FC92C048120FA9`
- **CoreHash**: `0732B505658AA24E9DE92906E51AB2F6F97F8D833D7317AE11CBF11CA2FF20C8`
- **ABI**: `ua01-member-v1`

Проверка отсутствия эффектов: compiled Gates.CanWrite вернул false для всех четырёх enum modes; ProbeMod допускает только LoadReadProbe/ObserveTwoClient; ObservationBridge создаёт SimulationLane.Observe. Пассивные Harmony callbacks читают значения до/после штатной операции и не заменяют args/result, не пропускают оригинал. Это bounded source/compiled-gate аудит, не runtime-доказательство отсутствия любых побочных эффектов loader.

Evidence: ../../evidence/poc030_rt/preparation. Результаты A/B sandbox относятся к двум каталогам **одного ПК**, не к двум машинам. Пакет RC2 не пересобирался и не менялся. Рабочая версия тестового runner дополнительно содержит event benchmark; её 47 проверок не заменяют проверку самой поставки, которая выполнена отдельным распакованным CLI.

Следующее действие: согласовать владельцев/роли двух ПК и разрешение только на R0–R1 — штатный запуск и baseline без DF/MelonLoader. Установку RC2 согласовать отдельно после PASS R0–R1. Детали и стоп-условия: SCENARIO_SESSION_VALIDATION.md и RUNTIME_BLOCKERS.md.
