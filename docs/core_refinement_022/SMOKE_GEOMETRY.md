# Конечный интервал одной сферы

P4-F09 #20415,A. D=T-O,L=length(D),a=length(C-O),b=length(C-T),rho²=length²(cross(C-O,D))/length²(D). Reject при rho>R или max(a,b)>L+R. h=sqrt(R²-rho²). Inside строго a<R,b<R.

Start=insideO?0:|Project(C-O,D)|-h. End=insideT?L:(!insideO?start+2h:L-(|Project(C-T,D)|-h)). Project вычисляется как D*(dot(V,D)/length²(D)), затем magnitude. Сохранена именно magnitude, не signed clipping. Возвращаются endpoints и intermediates, не только длина.

Для O0,T(10,0,0): C5,R2→3..7; C0,R2→0..2; C10,R2→8..10; C5,R20→0..10; C(5,2,0),R2→5..5; C20,R2→нет интервала. Endpoint surface не считается inside. Нулевой radius на линии даёт point interval. При C(-2,0,0),R2 восстановленные guards допускают0..4: это намеренно не стандартное пересечение с отрезком.

Zero/underflowed ray, nonfinite intermediates, negative radius и отрицательный rounded radicand отклоняются. Native special float/near-zero equivalence не заявляется. No interval содержит HasInterval=false и endpoints−1,−1; не путать с valid tangent of zero length.

P3-F07 Smoke accumulator не изменён. Он принимает уже подготовленные sorted non-overlapping intervals, не автоматически результаты нескольких сфер. Containment/overlap opacity resolver остаётся Research Only; стандартный union не добавлен. Новые расчёты не создают FOW contacts и не меняют TeamSafeProjection.
