# Манёвренность самолёта

P4-F07 #11974,B. CalculateYawRate и CalculatePitchRate используют один CalculateRate с отдельными rate presets. При speed≤MaxSpeed берётся Min→Max, иначе Max→Afterburn. t=clamp01((speed-startSpeed)/denominator), но denominator≤0.0010000000474974513f даёт0. Rate=start+(end-start)*t.

MaxSpeed equality относится к первой ветке. CornerSpeed отсутствует в интерфейсе, поскольку в данном методе это не breakpoint. Пример speeds100/200/300,rates10/20/15:150→15,250→17.5. Выход за диапазон speed даёт крайний rate через исходный clamp. Equal preset speeds поддерживаются с t0; unordered presets и nonfinite отклоняются библиотекой как неподдерживаемый домен, не выдаются за native validation.

Результат содержит branch,denominator,t,rate. Yaw/pitch независимы, входные records неизменяемы. Отрицательный конечный speed можно диагностировать через нижний clamp; rates и preset speeds требуют неотрицательной конечной области.

Прежние AircraftMath.ShiftFinite/ShiftTime/Fuel сохранены; имена в фактическом Core отличаются от ориентировочных HelicopterMath/FuelMath в ТЗ. Никаких дубликатов. Helicopter tilt H2, remote hover, stall, g-load, desired height и физическая интерпретация остаются исследовательскими. Игровое движение не подключено.
