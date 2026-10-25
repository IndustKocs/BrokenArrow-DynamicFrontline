# Артиллерийская математика

P4-F01 #21700: j0..10000, x=j/10000f, key=(CDF(x),x), previous=-1 и строго y>previous. Библиотека отклоняет некорректную CDF; игровой LogError/Debug.Break не доказан как abort. Затем1000 значений inverse(sqrt(i/1000f)), i0..999. Индекс1000 не добавляется. Для identity CDF T250=.5; nonlinear piecewise test также покрыт.

ICurveEvaluator задаёт Evaluate,Identity,Interpolation. IInverseCurveFactory получает readonly keys. LinearTestCurve — детерминированная piecewise-linear аппроксимация без Unity tangent/wrap claim, отклоняет extrapolation. Поэтому монотонная CDF, чей обратный domain не покрывает запросы sqrt, тоже отклоняется. Исходный asset не угадывается.

CorrectionTable сохраняет источник/версию/SHA256, evaluator identity, interpolation и readonly keys; CopyValues возвращает копию. CorrectionTableCache ограничен16 entries; ключ включает весь CurveSource и оба evaluator IDs. Caller обязан обеспечивать immutable inputs и достоверный fingerprint; прямой cache предназначен одному owner. Explorer защищает свой cache lock и вычисляет fingerprint synthetic key data. Это не глобальный выбор active game profile. Build — подготовительная операция, не один вызов на выстрел.

Polar #24225: NextFloat(0,360) → NextFloat(0,1000) → trunc → table lookup. Результат содержит angle,indexSample,index,radius,число потреблённых values. RNG upper endpoint1000 даёт FormulaStageException, не clamp999. ExperimentalPolarOrientation.CosSin отдельно задаёт условную ориентацию с rad factor0.01745329238474369f; она не объявлена найденной ориентацией native helpers.

FastDimond #24224: x(-1000,1000),q=trunc(abs(x)),y(q-1000,1000-q),return(x/1000,y/1000). При x±1000 второй вызов имеет equal bounds и не расходует случайное значение. Fractional x500.9,q500 допускает xNormalized+yNormalized>1 на малую величину — это квантование исходного алгоритма, не ошибка, которую нужно исправлять. Распределение не uniform площадь ромба.

Trajectory #24223: normalize(cross(d,up)), direction+=n*(u*Rh/L), DistanceDelta=clamp(old*(1+v*Rv/L),1e-5f,floatMax). Нижняя граница из dependencies address0x186796cc0: 9.999999747378752e-06, bits0x3727C5AC; не float.Epsilon. При L≤0 GenerateAndApply возвращает входы до вызова генератора. Zero/overflowed cross отклоняется. Direction не world impact point; v меняет DistanceDelta, не координатуY. Никакой полной баллистики или автонормализации конечного direction.
