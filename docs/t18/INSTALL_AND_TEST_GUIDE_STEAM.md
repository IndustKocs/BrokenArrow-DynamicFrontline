# Установка и тест Steam — preflight2

Актуальная пошаговая инструкция: [TESTER_QUICKSTART.md](TESTER_QUICKSTART.md).
Автоматическая подготовка — START.ps1; ручная подготовка через prepare-steam.ps1 остаётся
для разработчика. Она не устанавливает loader; обычным тестировщикам следует использовать START.

Пока выполнены только автономные проверки и sandbox. Настоящая установка и запуск требуют
отдельного разрешения. Для первого опыта сохраняется LoadReadProbe → подтверждение roster
обоими игроками → ObserveTwoClient. IP/порт/сертификаты/роли руками не вводятся.

[Отчёт исправлений](PREFLIGHT_HARDENING_REPORT.md),
[Relay-only](STEAM_RELAY_POLICY.md), [HUD](HUD_AND_CONTROLS.md).
