# Formula Explorer 0.2.2

Используется прежний DynamicFrontline.Combat.Explorer. Вход — JSON array запросов с Formula,Profile,Parameters. Новые Pass04 запросы требуют Profile=null и явные supplied/synthetic inputs; активный runtime profile не выбирается. Старые Core021 research profiles продолжают работать для прежних формул.

| Formula | Основные Parameters |
|---|---|
| P4-F01 | SourceId,SourceVersion,CDFKeys:[{Time,Value}], optional Indices/IncludeTable/IncludeKeys |
| P4-Polar | то же + Queue[100],PreviousIndex,optional ExperimentalCosSin |
| P4-F03 | Queue[100],optional PreviousIndex |
| P4-F02 | Direction[3],DistanceDelta,EffectiveRange,HorizontalRadius,VerticalRadius,U,V |
| BuffPipeline | optional Stress(10 fields),StressProvenance; optional Levels,Rows[12],MobilityRotateSpeed |
| P4-F09 | Origin[3],Target[3],Center[3],Radius |
| P4-F07 | Speed,Speeds:{MinSpeed,MaxSpeed,AfterburnSpeed},Yaw/Pitch:{MinRate,MaxRate,AfterburnRate} |
| P4-F05 | Dx,Dy,G |
| P4-F04 | Queue[100],PreviousIndex,Ranges:[[min,max],...] |
| VersionAwareIdentity | Snapshot/Current с Identity:{SessionId,WorldId,EntityId,EntityVersion},Revision,OwnerId и lifecycle booleans |
| P4-F08 | X; Experimental finite rational |

CDF default выводит keyCount10001,tableLength1000,metadata и indices0,1,250,500,999. Full values/keys включаются только отдельными flags. Queue и исходные параметры видны в EffectiveInputs; Polar/FastDimond сообщают consumed values. Optional CosSin помечен Experimental, без него Polar выдаёт только подтверждённую angle/index/radius часть.

Выход дополнен SourceMethod,Evidence,SupportedDomain,EquivalenceStatus,ParameterProfile,intermediates и Calculation. Для Buff/identity точные источники методов раскрыты также в тематических документах; общий Pass04 formula catalog связывает пакет доказательств. Большие результирующие таблицы не выводятся по умолчанию.

При domain/index/input ошибке stderr содержит Error,Stage,Complete=false и exit code2. Для FormulaStageException стадия точная, для остальных validation errors обобщена Input/validation. CLI сначала вычисляет весь request array; при ошибке не печатает частичный success JSON. Никакого исправления index1000, невалидной CDF или unordered preset.

Примеры: `config/core022/explorer-examples.json`; результат `evidence/core022/explorer-results.json`. Table source hash вычисляется по точным float key bits, не по данным игрового asset. CDF/input profile synthetic, не live. P4-F06 не экспортируется через CLI как средство извлечения разведданных.
