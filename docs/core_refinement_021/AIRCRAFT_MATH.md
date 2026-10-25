# Авиационная математика

P3-F02 (#12239,A): d=T-C,s=.25*Agility*dt. Target возвращается при d²=0 либо s≥0 и s²≥d². Иначе C+d/sqrt(d²)*s. Порядок и знак шага сохранены; negative finite dt/agility допускаются ShiftFinite для исследовательского сравнения. Отдельная ValidateNormalUsage отклоняет отрицательные параметры обычного сценария, не меняя формулу. Zero dt или agility при ненулевом расстоянии оставляет C; zero distance возвращает T. Overflow и nonfinite вне поддерживаемой области.

P3-F03 (#12238,B): length(T-C)/(.25*Agility), только Agility>0. Для0 не возвращается выдуманное finite время. Пример (0,0,0)→(10,0,0), agility8,dt1 даёт (2,0,0), время5; dt5 достигает target.

P3-F04 (#10761/#9147,B): requested=old-dt*afterburnerMultiplier. Fuel возвращает Old,Requested,Applied=false. Requested может быть отрицательным — clamp не добавлен. Getter/setter permissions и фактическая запись не моделируются. Пример100,.5,2→99.

Не реализованы аэродинамика, desired velocity/height, collision/terrain clearance, landing, stall, range, crash и маршруты самолёта. Источник G установлен в Pass03, но CF19/H1 остаётся research only; 9.81 и textbook trajectory не подставляются.
