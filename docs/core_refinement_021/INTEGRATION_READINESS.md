# Готовность к последующей интеграции

Можно вызывать чистые helpers с явно валидированными входами для диагностики: damage/HEAT/AoE, count consistency, selected ammo calculation, critical numerical states, helicopter shift/time, requested fuel, visibility budget fragments. Все требуют сохранения provenance и области применения. Успех unit tests подтверждает библиотеку, не семантику игровых callbacks.

TerritorialInfluenceCalculator, EncirclementEvaluator, DesertionController, FrontlineSimulation, GameUnitProfileResolver и snapshot contracts не изменены. Для фронта по-прежнему важны наблюдаемые alive count/stress/desertion и разрешённый профиль. Armor, damage, missile chance, fuel и artillery accuracy не становятся территориальным multiplier. Operational connectivity не равна supply; unknown route не доказывает encirclement. Recon/SF остаются relay без захвата.

Перед runtime отдельно установить canonical lifecycle, complete census, поток/фазу чтения, активную DB/asset profile, direction/mask nav, контакты команды, sender permissions и damage authority. Привязка CF18 к реальному seed/order/remote index, critical RNG, weapons/containers и BuffDebuff apply требует доказательств. Ammo helper не даёт права менять запас; RequestedFuel не даёт права выполнить setter.

Core DLL и Explorer автономны и не зависят от игрового loader. Существующие loader исходники не изменены; в этом этапе не требовалась их пересборка или установка. Проверены зависимости математической сборки и 184 прежних сценария. PoC03 и gameplay hooks не выполнялись.
