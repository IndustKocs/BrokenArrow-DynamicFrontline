# RNG и границы ракетной модели

CyclicRandom CF18/P4-F04 сохранён: seed-NetID (unchecked int в автономной модели),100 values,1-(float)NextDouble(),конструктор один раз,advance-before-read,99→0,min==max без продвижения,min>max ошибка. Добавлены IFloatRandom и ValuesConsumed, увеличиваемый только при реально принятом draw. Это instrumentation, не новые игровые индексы.

Проверены queue value1 и exact upper endpoint, последовательные обращения, различные initial indices, invalid range/nonfinite без продвижения, repeatability250 draws, Polar index1000 и FastDimond second equal-bound no draw. Bounds-call count и consumed value count различаются: FastDimond вызывает NextFloat дважды, но при x±1000 расходует один value.

System.Random .NET6 не объявляется bit-exact IL2CPP. Producer seed, projectile sharing, inlined init, fallback static RNG, network index synchronization и reconnect остаются незакрытыми. Counter не обеспечивает синхронизацию клиентов. CriticalMath по-прежнему использует другой интерфейс ICombatRandom.

SeekerFragments не расширен в полную state machine. Pass04 DropCurrentTarget очищает OriginalTarget/target и сохраняет LastTargetPosition, но не равен SetMissileDead. Miss vector, map-height ограничение, network lost-target, shooter support и timer reasons требуют отдельных контекстов. Не введены безусловный roll каждый tick или фиктивная missile simulation. Старые gates сохранены с прежними ограничениями.
