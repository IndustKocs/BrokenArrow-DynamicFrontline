> Дополнение FullDebugView + Launcher: актуальные документы — [установка](INSTALL_GUIDE_LAUNCHER.md), [FullDebug](FULL_DEBUG_VIEW_REPORT.md), [испытания лаунчера](LAUNCHER_TEST_REPORT.md), [runtime](RUNTIME_CHECKLIST_FULLDEBUG.md). TeamSafe по умолчанию; полная геометрия разрешается только после двух согласий.

# ТЗ №18-В — 0.4.0-T18-preflight3

Основа: preflight2, bf128c4. Ветка codex/t18b-final-preflight. Только подготовка к первому приватному Scenario PvP 1v1; игровая приёмка НЕ выполнена.

| Замечание | Статус | Результат / граница доказательства |
|---|---|---|
| Gamelog / DummyLogger | Исправлено; требуется runtime | NativeContext больше не получает DummyLogger. UnityGamelog вызывает UnityEngine.Debug.Log на Unity thread через существующий interop. Штатный FileLog остаётся единственным writer. |
| Подтверждение записи | Подготовлено; runtime NotRun | После FileLog._isInitialized читается фактический _logFilePath, запоминается конец файла, через Unity отправляется уникальный [DF] marker. Только чтение его точного совпадения после исходного offset даёт GamelogMarkerConfirmed. Лимит 64 KiB/сек, ожидание 15 сек; смена файла, усечение и timeout не считаются успехом. |
| Защищённая сессия | Исправлено статически; native binding требует runtime | IsActive исключён из решения. Используются действующий ProtectedLobbyTypes, IsLogged, ActiveLobbyTypes, EOS _roomService и IsIntegrityViolated; подключённая native room, отсутствие replay, Scenario 5454584, FightId, RoomStatus и привязка к ранее увиденному приватному lobby через WaitingRoomId/HostId. Нет чтения токенов, вызовов Begin/EndSession, изменений античита или авторизации. |
| Неизвестное состояние | Исправлено | Нет политики/члена interop/подключения/сопоставления комнаты — запрет. Загрузка/завершение матча не разрешают чтение; 1/10/20 разрешены только при остальных доказательствах. Lobby discovery не читает игровой мир; канал данных запускается после native room gate. |
| PocketCells | Ограничено намеренно | Передача пустая на обеих сторонах, входящий непустой массив отвергается. Core продолжает считать котлы, собственные предупреждения/прогнозы остаются. Цветная геометрия котлов временно недоступна: отсутствие утечки полного hidden-derived контура не доказано. |
| Steam Relay | Усилено; требуется runtime | ICE0 устанавливается после разрешённой подключённой комнаты, а не при discovery. До подтверждённого Relayed/authenticated/encrypted/POP отправляется только постоянный однобайтовый route probe, без handshake/world. Direct/изменение политики/потеря маршрута останавливают DF; fallback нет. |
| Совместимость native сети | Ограничено | Global ICE0 может затрагивать другие новые Steam Networking sessions процесса; existing-соединения не считаются покрытыми. Отсутствие конфликтов нельзя доказать без игры. Выход из процесса восстанавливает defaults; Stop DF их не меняет. |

## Проверки

- Steam/diagnostics/session/privacy/receipt: 61/61 автономных проверок.
- RC2: 51/51. RC1: 44/44. Simulation023: 72/72.
- Generated metadata: 38/38 контрактов, включая новые поля room/auth/FileLog и Unity Debug. Это не вызов native методов.
- Сценарии gate: неизвестная политика, защищённый Scenario, активная EOS session, integrity violation, logout, запрещённый Scenario, другая/неприватная комната, disconnect, replay, неверный сценарий/тип/FightId, loading/end/fail.
- Privacy: одинаковые видимые/собственные данные при изменённых скрытых юнитах/кольце дают одинаковую отображаемую геометрию котлов; два реальных автономных запуска ядра с различными скрытыми юнитами передают пустой PocketCells. Это НЕ доказательство неразличимости всех метаданных или всего публичного фронта.
- Receipt: принятие вызова без файла не успех; точный свежий marker подтверждается, timeout/rotation/truncation остаются unverified. Проверки выполнялись на искусственном файле, не Gamelog игры.
- Старые проверки длинных структурированных Result, safe export, сравнения, overflow, reconnect сохранены.

Simulation023, формулы, расчёты фронта/ZOC/окружений, renderer панели, START.ps1 и транзакционный installer не переписаны. Протокол df18-world-3 исключает смешанную пару preflight2/preflight3. Публичная own-territory карта остаётся существующим контрактом; own предупреждения о влиянии скрытого противника разрешены, его координаты/контуры не выдаются. Coordinator по-прежнему доверенная сторона; Result receipt не независимый пересчёт скрытого мира.

## Что обязательно проверить в игре

1. На обоих ПК marker действительно появляется в текущем штатном Gamelog; HUD показывает GamelogMarkerConfirmed. Сам marker подтверждает один путь/момент, не полноту всего журнала.
2. Реальные значения server policy и EOS bookkeeping, WaitingRoomId↔LobbyId, переходы loading/deployment/match/end и disconnect. При отказе не ослаблять gate, собрать безопасную диагностику.
3. Оба участника входят в приватный Scenario 5454584/Airbase; Steam companion найден до матча, защищённый data channel — после доказанной комнаты.
4. SDR маршрут и отсутствие ухудшения native соединения после ICE0, при reconnect и выходе хоста. Ошибка — Stop DF, завершение опыта, выход из игры; не переключать на direct.
5. LoadReadProbe на обоих ПК: собственный состав, ID, посадка/выход, смерть/spawn, реальные HP/стресс. Только затем обе roster-confirmations и ObserveTwoClient.
6. Фронт, ZOC, own encirclement warnings и отдельный прогноз стресса; геометрия котлов в этой сборке отключена. Нет native эффектов и смены победных условий.
7. Экспорт/сравнение двух логов после матча; никогда не отправлять сырой Gamelog или папку local-preparation.

Отрицательный protected-mode контроль сначала выполнен автономно. Не запускать мод в защищённом игровом режиме ради проверки.

## Источники исследования

Локальные read-only материалы: research-full-code-audit-018b/research/full_code_audit_018b/CRITICAL_WARNING.md, SCENARIO_PVP_CONTRACT.md; research-tz18-lobby-gamelog/research/tz18a/GAMELOG_PIPELINE_AUDIT.md; signatures и ISIL EosAntiCheatService, RoomState, AuthorizationService, FileLog. Материалы не изменялись и в пакет не копируются.

[Valve Messages](https://partner.steamgames.com/doc/api/ISteamNetworkingMessages) и [SDK types/config](https://github.com/ValveSoftware/GameNetworkingSockets/blob/master/include/steam/steamnetworkingtypes.h) подтверждают параметры/модель API, но не runtime-совместимость конкретной игры.
