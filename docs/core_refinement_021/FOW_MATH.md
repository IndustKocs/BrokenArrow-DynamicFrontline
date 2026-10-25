# Численные фрагменты FOW

CF14 (#20385,B) получает уже разрешённые входы обоих FOW components. f=max(flash,penalty); cover=terrainCover*(insideSmoke?smokeCover:1). a=clamp(f,1,floatMax)*AntiVisible/clamp(cover,1,floatMax). При Sneak&&!Airdrop&&flash≤1: a=min(a,clamp(AntiVisible/clamp(cover*CoverBonus,1,floatMax),SneakMin,MaxAntiVisible)). Затем clamp(a,float.Epsilon,MaxAntiVisible), DistanceGround*a, при применимом optics ещё множитель. float.Epsilon сохранён, не заменён на0.001. Ground result включает intermediates.

CF15 принимает уже выбранную ground/low/high дальность, applicable radar multiplier, flash/penalty, antiVisible и nullable optics. Здесь нет ground minimum1 и cover division. Sensor selection, высота, UnitType и radar policy не угадываются.

CF16 работает с подготовленными упорядоченными TerrainSample. Caller строит inclusive i=0..trunc(length) с шагом1 и разрешает terrain/height. Eligible учитывает IgnoreTerrain, height≤terrain.Height и IgnoreForestOnly/type2. Счётчик каждого type сохраняется на всём span. Cost=max(1,Opacity+k*Multiplier), forcedNear и rawFlags&2 дают1; это не проверка type==forest. Ineligible стоит1. SmokeExtra не добавляется при forcedNear. FirstBudgetExceededSample использует строгое > и обозначает только terrain sample, не smoke-only превышение. Raycast, projectile branch и native terrainCounts[15] побочные эффекты отсутствуют.

P3-F05 (#20576,B) вызывается на событие таймера: clamp(flash-reduction,visible?max(1,penalty):1,floatMax). Пример3,.5,true,4→4. Функция не вызывается автоматически на render frame; period неизвестен.

P3-F08 (#20415,B) возвращает расстояние от бесконечной прямой до центра сферы и half chord. Нулевой луч отклоняется. Tangent даёт halfChord0; отсутствие пересечения — false. Это не конечный ray/segment intersection: endpoint-inside, reject по длине, signed projection и clipping намеренно не реализованы.

P3-F07 (#20414,B) принимает уже разрешённые sorted non-overlapping intervals. При length>0 N=floor(length)+1; cost суммируется в исходном порядке, k общий с1 через все интервалы. Zero length пропускается. Примеры l2,opacity3,mult.5→12; два disjoint l1→17. Overlap/unsorted/reversed входы отклоняются как неподготовленные; это не новая политика разрешения перекрытий. MaximumSamples — защитный бюджет harness, проверяемый до накопления.

Все результаты — расчётные величины. Они не разрешают экспорт врагов, не создают командные контакты и не меняют граф оперативной связи.
