# Разрешение ролей и активных профилей

GameUnitProfileResolver использует явный Unit Definition ID и сверяет ожидаемый игровой Role. Role не выдаёт права автоматически. Из статического enum известны 30 LineInfantry, 31 AssaultInfantry, 32 ReconInfantry, 33 Snipers, 34 AAInfantry, 35 ATGMInfantry, 36 SpecialForces. ID типа/loadout не является UID экземпляра.

Профиль задаёт CanCaptureTerritory, CanCreateLocalArea, CanRelayConnectivity, CaptureTypeMultiplier, InfluenceRadius, ConnectivityRange, ConnectivityChannel, MovementType, NavigationMask, StressPolicy, AllowLoading и AllowNonInfantryRelay. Только явно разрешённые 30/31 могут захватывать или создавать круги. Разведка/спецназ всегда лишены этих прав; relay разрешается отдельно. Снайперы, AA/ATGM и техника по умолчанию ничего не получают. CanCreateLocalArea=false может отключить круг, сохранив право захвата у связанной пехоты. UnknownUnitPolicy задаёт Exclude либо RejectSnapshot; default example — Exclude.

StressPolicy: Standard использует существующие коэффициенты Core; ExcludePanicked и ExcludeShockedAndPanicked дополнительно обнуляют участие. Собственные Loaded/Loading/Deserting/Death проверки выполняются независимо. Урон и бронепробитие не используются как территориальные коэффициенты.

DatabaseIdentity хранит ScenarioId, ProfileId, ProfileVersion, GameRulesVersion, Source, nullable Sha256 и ActiveVerified. Профиль сопоставляется с **фактически подтверждённой** активной базой, а не с установленной версией exe. По Pass 02 scenarioFolder/db.zip имеет приоритет перед базой active/default profile; static extracted database не доказывает текущий runtime выбор. При неизвестной базе обработка останавливается с UnknownDatabase; несовпадающей — ProfileMismatch. Nullable hash допустим только когда источник не может его достоверно получить; он не заменяется выдуманным runtime хэшем.

Load(json, expectedConfigSha256) отдельно проверяет SHA256 точных UTF-8 байтов конфигурации. Этот digest не равен хэшу игровой db.zip. Неверный digest/JSON отклоняются. Каталог создаётся один раз для контекста и использует словарь для O(1) lookup. Смена сценария/базы/версии профиля очищает симуляцию; разные базы не смешиваются в одном кадре.

Диапазоны: CaptureTypeMultiplier 0…1000, StatusMultiplier 0…100 и AliveSoldierCount ≤10000 — собственные защитные границы PoC, не утверждение о лимитах игры. InfluenceRadius/ConnectivityRange конечные >0; роли/каналы/policies должны быть допустимыми enum. Каталог передаётся ядру как новый ProfileCatalog, математическая логика не переписывается.

Пример JSON использует синтетические Unit IDs; не использовать их как реальные записи Broken Arrow. Создание проверенного runtime-каталога и его привязка к actual DB остаются отдельной задачей.

Пример: [config/poc02.profile.synthetic.json](config/poc02.profile.synthetic.json). ActiveVerified=true означает только подтверждённую искусственную базу примера. Loader не загружает этот файл автоматически. [config/poc02.settings.json](config/poc02.settings.json) отражает четыре выключенных настройки; фактические runtime preferences создаёт MelonLoader.
