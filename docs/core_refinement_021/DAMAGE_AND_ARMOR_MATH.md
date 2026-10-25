# Урон и броня

F10/P02 (#24006, B) вычисляет pk=Pow(P,k), ak=Pow(A,k), numerator=D*pk, denominator=pk+E*ak, damage=numerator/denominator. HeatDetails возвращает все эти величины. Никакого P/A approximation или дополнительного clamp. D≥0, P/A/E/k>0, конечные intermediates и ненулевой denominator — поддерживаемый домен библиотеки. Отрицательные bases, zero armor/penetration, native NaN/Inf equivalence не объявляются восстановленными; вызывающий получает domain error. При D10,P=A100,E1,k2 результат5.

F07 сохранён: P≥A →D; иначе x=D+D/(P*E)*(P-A); x≤0 →0, x>0 →max(x,D*minimum). F08 для non-HEAT использует damage>threshold, а не ≥; HEAT bypass. F09 использует actualRange, не actualRange-minimumRange. Проверены границы и сравнительные сценарии с HEAT. Функции не выбирают броневую проекцию, ammo branch или сетевого получателя автоматически.

CF03 (#24202, A для коэффициентов) считает surface=max(0,length(center-hit)-roughRadius). Caller передаёт корректный center с уже учтённым вертикальным смещением. Сначала d≥radius →0, затем NoFalloff →1, иначе clamp(1-d/r,0,1). Radius0 даёт0 даже при NoFalloff. HP и stress радиусы независимы. Ammunition4: d22.5, D12,S310,rH45,rS67.5 →fH.5,fS2/3,HP6,directStress206.6667.

Это урон до прочих modifiers, не финальный damage pipeline. Повреждения зданий, прохождение стен, фильтры целей, overpressure, network apply и code15 не моделируются. Извлечённые значения ammo — пример статической таблицы, не active PvP DB.
