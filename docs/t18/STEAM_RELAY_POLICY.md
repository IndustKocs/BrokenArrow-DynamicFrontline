# Steam Relay-only

Steam Networking Messages сам по себе не гарантирует SDR: Steam допускает direct P2P
в зависимости от настроек и доступных маршрутов. Поэтому прежнее предположение о
безусловном relay было неверным.

Выбрана политика RelayOnly_ICE0. В preflight3 после подтверждения разрешённой подключённой native комнаты устанавливается global
P2P_Transport_ICE_Enable (104) = 0 через NetworkingUtils v004, затем проверяется результат
GetConfigValue. При нуле ICE-кандидаты не используются. Не применяются эвристические
ICE/SDR penalty: они лишь предпочитают маршрут, но не запрещают fallback.

Перед каждой отправкой/чтением проверяется неизменность политики. Для Connected-сессии
GetSessionConnectionInfo должен показывать Relayed, отсутствие Unauthenticated и
Unencrypted и ненулевой relay POP. Если проверка не проходит, обмен DF прекращается.
При недоступном SDR соединение не переходит на direct; HUD показывает ошибку.

Настройка process-global, поскольку Steam Messages не принимает connection config при
создании сессии. Она сохраняется до выхода из игры, не меняет Steam preferences,
firewall или VPN. Другие новые Steam Networking соединения процесса тоже могут быть
ограничены. Возврат к direct не выполняется при Stop DF: это могло бы изменить политику
ещё живых соединений. Для восстановления исходных defaults нужен выход из игры.
Существующие посторонние соединения не объявляются защищёнными этой политикой.

Проверки policy на fake API и наличие native exports — автономные. Реальный SDR путь,
ABI, backend Broken Arrow и отсутствие конфликтов с его lifecycle ещё НЕ проверены.
При игре фиксировать [DF]/HUD с policy и успешное соединение обоих клиентов;
никаких утверждений о runtime validation по unit-тестам.

Источники:
- [Valve: IP sharing / Steam Networking](https://help.steampowered.com/en/faqs/view/1433-AD20-F11D-B71E)
- [Valve SDK: ICE config, scopes, route flags](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/include/steam/steamnetworkingtypes.h)
- [Steam Networking Messages API](https://partner.steamgames.com/doc/api/ISteamNetworkingMessages)

Preflight3: до проверки Connected + relay flags/POP передаётся только постоянный route probe. Ошибка политики/маршрута останавливает DF. Steam discovery сохранён в лобби, handshake/data channel начинается в доказанной комнате. См. T18B_REPORT.md.
