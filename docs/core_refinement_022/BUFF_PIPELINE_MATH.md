# Buff evaluation pipeline

Источники #24106/#24109/#24110, CRITICAL_AND_BUFFS.md; имена ten fields подтверждены сигнатурой BuffDebuffSystem offsets0x60..0x84: AimTime, TimeBetweenBurstsStress, MagazineReloadStress, MagazineBurstReloadCritical, Dispersion, DamageReceived, MovementSpeed, RotateSpeed, MissileAccuracy, Optics.

Evaluate создаёт Reset со всеми1, затем ApplyStress, затем ApplyCritical, затем Final. Каждая стадия возвращает отдельный readonly record; входы не изменяются. Result содержит Reset/AfterStress/AfterCritical/Final. IStressModifierProvider принимает reset и возвращает уже подтверждённые caller values с provenance. SuppliedStressModifiers — контейнер входных данных, не восстановленная универсальная stress formula.

ApplyCritical повторно использует CriticalMath.Apply из0.2.1. Проверенный ISIL #24109 lines1403–1520 показывает: optics/aim,dispersion,magazineBurstReload и movement умножаются; rotate присваивается из config при mobility level>0. Формулировка handoff о возможной перезаписи speed не переносится на movement multiplier без native основания. Для stress movement.5,rotate.6 и mobility factor.4,rotateConfig.2: final movement.2,rotate.2, а не.12. Нулевые критические строки в synthetic identity table не добавляют эффекта; это свойство таблицы, не скрытый skip всех level0.

CF09 attempts/selection, отдельный critical RNG, уровни0..2, trace и draw budget сохранены. Никаких RNG draws в numeric buff pipeline. Синтетические rows не active BuffConfig.

Final publication не выполняет ECS writes. PotentialMovementCommandReview=true при movement≤float.Epsilon отражает изученный gate; не утверждается отмена всех команд. WouldDisableBuffUpdate — диагностическое намерение исходного порядка, не фактический Disable. Entity filters Airdrop/Death/Dead и наличие BuffUpdate должны быть проверены caller; автономная функция не обходит их и не моделирует scheduling.
