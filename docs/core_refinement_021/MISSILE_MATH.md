# Ракетные helpers и RNG

F11 (#23970, B): n0→1, иначе Pow((1-r)*d,n). Поддерживается конечный r∈[0,1],d≥0,n≥0; эта domain policy не объявляется native clamp. Результат может превышать1. Пример .2,.5,2→.16.

F12 (#23969, B): accuracy*ECM*CM*stress*veterancy*critical, порядок слева направо, без clamp результата. .8*.9*.16→.1152;2*2→4 сохраняется. Отдельный CompareProbability допускает только [0,1] и использует >=. Это политика автономного вероятностного теста, не изменение F12.

CyclicRandom — Experimental реконструкция CF18 (#24442/#24443). Constructor(seed,NetID) создаёт System.Random(unchecked(seed-NetID)), queue[i]=1-(float)NextDouble(),100 элементов. NextFloat увеличивает index до чтения, оборачивает99→0; min==max возвращает min без draw; min>max и nonfinite отклоняются без продвижения. Другой constructor принимает копию точной очереди и предыдущий index для воспроизводимых fixtures. Нет глобального seed, remote индекса, статического fallback RNG или обещания совпадения System.Random с IL2CPP реализацией. Повторная Init не моделируется: объект инициализируется единожды.

Тип не реализует интерфейс критического RNG, чтобы их не спутать. Криты получают отдельный ICombatRandom. Tests проверяют period100, advance-before-read, wrap, seed replay, границы, отсутствие draw при константе.

SeekerFragments.Gate — только изученный префикс #24357: Seeker200/300 skip, отсутствующий Random skip, no target reset, state0 или рост CM при state1 устанавливает reroll; прочие установленные состояния без force сохраняются. EligibleForFurtherChecks не означает hit: shooter guards для50/100, CM caps, miss-vector draws, retarget, LOS и полный автомат не реализованы. Не выполняется roll каждый tick.
