# Критические эффекты

CF09/P02 (#24129, B): q=floor(multiplier/100),p=(multiplier-100*q)*hpLossRate+base,n=q+(p>=roll?1:0). Остаток повторно на100 не делится. Пример250,.1,.02→q2,p5.02,n3 для стандартного roll. p не ограничивается. Attempts получает конкретный double roll∈[0,1); caller должен взять его из отдельного критического RNG. Select получает ICombatRandom для выбора индексов; генератор ракеты сюда автоматически не передаётся.

Кандидаты — Optics0,Mobility1,Targeting2,Loading3 с level<2, в этом порядке. affected=4-count+число level1 среди candidates. При выборе level1 и affected<4 и threshold>affected выполняется reroll без уменьшения attempts. Успешный переход увеличивает level; level2 удаляет кандидата со сдвигом и сохранением порядка. Возвращается trace каждого выбора. Неподходящий индекс RNG отклоняется.

MaximumDraws — явно обозначенный предохранитель автономного harness против бесконечного повторения тестовой последовательности. Он не выдаётся за игровое правило: при исчерпании возвращаются BudgetExhausted=true и remaining attempts, результат неполный. Пустые candidates завершают выбор независимо от оставшихся attempts.

P3-F09 (#24109, B): CriticalTable содержит 12 явных записей type×levels0..2. Нормализованная запись Primary/Secondary — контракт библиотеки, не утверждение о layout native asset. Optics Primary умножает optics, Secondary — aimTime; Mobility Primary умножает movementSpeed; при level>0 rotateSpeed заменяется значением Config. Targeting Primary умножает dispersion; Loading Primary — magazineBurstReload. Неиспользуемые Secondary не распространяются на другие параметры. Caller передаёт накопители, функции возвращают новые значения.

Тестовые nonlinear rows специально синтетические и не объявляются активным BuffConfig. Извлечённый criticalValuesInDeclarationOrder не преобразован в таблицу автоматически: требуется подтверждённая привязка всех полей. Полный reset→stress→critical→apply порядок, physics damage и ECS effects не реализованы.
