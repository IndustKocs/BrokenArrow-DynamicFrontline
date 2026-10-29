# Пассивный источник обслуживания

Readonly Harmony observers не меняют аргументы/result, не пропускают оригинал и не подавляют native exception. Подключение опционально: PassiveServiceProbeEnabled.

- Repair: положительная разница HP строго внутри RepairSystem.ApplyRepairAndHealthNewValues; owner/NetID проверяются до и после.
- Ammo: положительная разница AmmunitionContainer.AmmoAvailable внутри SupplySystem.InternalUpdate, затем причинное сопоставление ammunition ID/recipient в NetworkWeaponsService.SetAmmoCount. Одна разница между несвязанными snapshot не считается снабжением.

Proof содержит sequence, owner, native recipient NetID/local entity version, native GameTime, amount, kind и evidence. Поставщик привязывается по собственному supplyEntity в контексте native вызова; source NetID/local generation переводятся в canonical identity только при наличии соответствующего полного owner sample. При отсутствии такой записи source остаётся Unknown, а не выдумывается. Repair/source lifetimes и полнота всех callbacks остаются отдельной runtime-проверкой.

Сейчас подтверждаются дискретные положительные операции. Граница определённости — только момент операции; промежуток до следующей операции Unknown. В canonical input они идут как PositiveServices, не как выдуманные ServiceIntervals. SupplyComplete=false: ни бесконечной защиты, ни вымышленного grace, ни автоматического штрафа при неизвестном снабжении.

Bounded lateness: в canonical history принимаются доказательства не старше1с относительно согласованного времени и не из будущего; local history до10с для повторной передачи. Sequence конфликт отвергается; history4096, callback queue256, переполнение явно делает источник недоступным. Exact duplicate не учитывается второй раз. Registry epoch/owner/local generation препятствуют переносу операции на новый юнит с тем же NetID. Resync переносит dedupe ledger.

Ядро уже поддерживает настоящие подтверждённые intervals и min(20s, 2*duration) grace. Runtime probe должен установить, какую положительную portion реально гарантируют native callbacks. Пока такого доказательства нет, превращать соседние дискретные операции в continuous service запрещено. Этот сценарий остаётся Unknown, а не успешно проверенным supply.
