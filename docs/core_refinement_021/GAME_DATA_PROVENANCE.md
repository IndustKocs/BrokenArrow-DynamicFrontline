# Происхождение примеров

Каталог `config/core021/research-profiles.json` содержит Source, AssetId, Profile, ResearchVersion, SourceTableSha256, optional AssetSha256, RuntimeConfirmed=false, FormulaIds и Values. Hash таблицы вычислен по точным сохранённым байтам; asset hash отдельно взят из evidence либо вычислен по сохранённому binary asset. Это разные hashes, они не выдаются за hash активной db.zip.

| ID | Источник | Значения / назначение |
|---|---|---|
| buff-52469 | full_code_audit/evidence/buff_values.json, pathID52469 | Calm floor≈.72,perSoldier≈.02,threshold9999; F01/F02/F03/F05/CF01 data |
| buff-52552 | тот же файл, pathID52552 | Calm floor≈.36,perSoldier≈.02,threshold36 |
| ammo-4 | combat_systems_audit/evidence/ammunition_examples.json, Ammunitions.Id4 | Damage12,Stress310,rH45,rS67.5 |
| fow-52558-forest | combat_systems_audit/evidence/profiles_decoded.json; profile_assets/52558.bin | type2 opacity−15,mult.75,cover2,height15; asset FlashReduction≈.15 |

Числа JSON сохраняют точность извлечённого float, а не только округлённые значения таблицы задания. Пример flash reduction .5 в тесте — условное численное упражнение, не значение asset52558. Нелинейные critical rows, HEAT examples, RNG seed и геометрические точки — синтетические fixtures, не извлечённые игровые параметры.

ResearchProfileCatalog отклоняет неверный hash конфигурации, отсутствующую provenance, duplicate ID, nonfinite values и RuntimeConfirmed=true. Get возвращает копии, изменение которых не меняет каталог. Explorer разрешает профиль только для перечисленного formula ID; явные параметры запроса могут переопределить значения, а EffectiveInputs делает это видимым. Профиль не выбирается глобально и не передаётся GameUnitProfileResolver.

В Pass03 установлено: стандартный TryLoadScenario возвращает false в исследованных Rating/Custom ветках. Поэтому приоритет scenario DB из Pass02 нельзя применять безусловно к Custom PvP. Ни один каталог этого этапа не объявлен фактически загруженной базой матча.
