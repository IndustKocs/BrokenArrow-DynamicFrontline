# full-debug6 — совместимость перехвата лобби

Наблюдение full-debug5: Il2CppInterop не смог создать native backend для NetworkLobbyService::.ctor и перешёл на managed patch handler; HUD отрисовался, но сообщение lobby hooks ready не доказывало обнаружение лобби.

Исправлено: перехват конструктора полностью удалён. Capture вызывается только postfix метода Connect. Dispose по-прежнему очищает сохранённый текущий экземпляр. Проверка native RVA подтверждает уникальность Connect (0x64F340) и Dispose (0x64FC10). Общий getter get_State и заглушка Initialize не патчатся. Посторонние игровые методы, античит и авторизация не менялись.

Регистрация обработчиков явно обозначена как observation pending. При отсутствии Capture HUD сообщает ожидание native Connect. При захваченном, но неподходящем лобби — ожидание private Scenario. Сообщение private Scenario lobby observed and validated появляется только после успешного NativeContext.Read и проверки сценария/приватности. Нет обхода gate или синтетической подстановки.

Проверки: Steam 95, RC2 54, RC1 44, Simulation023 72 — все 265 пройдены. IL-проверка собранного plugin запрещает GetConstructors, get_State и Initialize в Install; positive metadata audit двух оставшихся hook адресов пройден. Имеющийся interop повторно генерировать для этого исправления в разработке не требовалось: поменялся потребитель, не ABI. У тестеров стандартная подготовка/проверка локального interop сохраняется. Launcher updater и пути логов проверяются 23 тестами финального EXE.

Реальная установка не изменялась и игра не запускалась. Нет утверждения о проверенном двухклиентном подключении. Class::Init fallback warning не исправлялся и не скрывался. Следующая runtime-проверка: отсутствие constructor warning, HUD full-debug6, создание/вход в приватное Scenario 5454584, получение validated lobby, затем LoadReadProbe обоими тестерами. Если событие Connect не наблюдается, мод остаётся в ожидании; нужно собрать диагностику, а не обходить проверку.
