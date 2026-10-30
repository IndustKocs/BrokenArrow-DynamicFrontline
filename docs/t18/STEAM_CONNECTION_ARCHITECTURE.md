> Актуальная ревизия: preflight3 / ТЗ №18-В. См. [T18B_REPORT.md](T18B_REPORT.md). Unity Debug + read-only marker receipt; строгий session gate; PocketCells отключён; handshake после входа в доказанную комнату. Ниже сохранён предыдущий контекст, при расхождении действует отчёт ТЗ №18-В.

# Актуализация preflight2

Протокол обновлён до df18-world-2: добавлены ResultWitness/CoordinatorReceipt.
Steam Messages, native companion lobby и ядро сохранены. Политика транспорта теперь
[RelayOnly_ICE0](STEAM_RELAY_POLICY.md); прежний default мог выбирать direct.

# Подключение через Steam

## Пользовательский путь

Оба тестера — Steam-друзья. После установки и разрешённого запуска игры DF автоматически
ожидает приватный Scenario. Первый создаёт обычное приватное
Scenario-лобби 5454584, второй входит через штатный список/приглашение игры.
DF сам определяет native host/client. Отдельное ручное Steam/IP-лобби создавать не нужно.

У игры есть собственный NetworkLobbyService с GalaxyConnection: это не доказательство,
что штатное лобби имеет Steam lobby ID. Поэтому дополнительное лобби Steam используется
только для обнаружения пары мода и обмена. Игровой сервер и мир не заменяются.

## Привязки

Read-only postfix на `NetworkLobbyService.get_State` сохраняет ссылку на сервис;
Dispose очищает её. На основном потоке читаются LobbyId, IsHost, State.Options.IsPrivate,
LobbyType и ScenarioMeta.Name. Ключ обнаружения — digest native lobby ID + host ID + scenario.
Состояние матча проверяется через GameController / NetworkRoomService. При активном
EosAntiCheatService.IsActive чтение юнитов и работа мода останавливаются; методы античита
не патчатся и не вызываются для управления его состоянием.

Steam flat ABI берётся только из уже загруженного в игру steam_api64.dll.
Нет SteamAPI_Init/Shutdown/RunCallbacks и подмены AppID. Используются Matchmaking v009,
Utils v010, User v023, Friends v017, NetworkingMessages v002. Результаты собственных
асинхронных запросов опрашиваются через GetAPICallResult; очередь callbacks игры не забирается.
Все native-вызовы происходят в основном потоке.

## Совместимость и защита

Metadata: protocol, native binding digest, session GUID, compatibility digest.
Metadata не содержит секрета. Клиент не выбирает неоднозначный результат поиска.
Другой участник обязан быть Steam-другом, состоять в той же companion-паре; входящие
сообщения принимаются только от его Steam identity в отдельном канале 18230.
Это кооперативный тестовый режим, не защита от модифицированного недобросовестного клиента.
Связь Steam account с native player дополнительно проверяется общим MatchBinding наблюдений;
нативного криптографического доказательства соответствия двух систем ID пока нет.

Hello включает digest пакета собственных DLL, сборки игры, карты, профилей, правил,
ABI, session и режим. Отдельный P-256 ECDH обменивается публичными ключами внутри
Steam-защищённого канала. HMAC ключ выводится из shared secret и упорядоченного transcript.
Ready подтверждает ключ; world-envelope подписывается HMAC-SHA256. Strict sequence и
connection epoch исключают повторное применение пакетов. Максимум frame 16 MiB,
кусок 48 KiB, очередь 32 managed messages, не более 8 отправок и 32 приёмов на pump.
Ручных сертификатов нет; шифрование транспорта обеспечивает Steam.

Обрыв инициирует новый handshake/epoch и существующий RC2 ResyncRequest. Экземпляр
RuntimeObserver/ядра остаётся: новая сессия TCP-подобного транспорта не обнуляет tau.
TeamSafe-клиент получает только свою проекцию, а не скрытый канонический мир.
Смена матча/режима создаёт отдельный scope. Host migration отвергается.

## Источники и пределы проверки

Официальные API:
- [Steam Networking Messages](https://partner.steamgames.com/doc/api/ISteamNetworkingMessages)
- [Steam networking](https://partner.steamgames.com/doc/features/multiplayer/networking)
- [Steam Matchmaking](https://partner.steamgames.com/doc/api/ISteamMatchmaking)
- [Steamworks.NET ABI declarations](https://github.com/rlabrecque/Steamworks.NET/blob/master/com.rlabrecque.steamworks.net/Runtime/autogen/NativeMethods.cs)

Экспорты сверены с локальной библиотекой, managed-привязки — с локальными generated wrappers.
Steam/Scenario runtime интеграция ещё не выполнялась. Наличие экспорта само по себе не
доказывает доступность backend в этом AppID или корректность runtime lifecycle.
