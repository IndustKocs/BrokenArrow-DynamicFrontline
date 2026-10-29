# Установка, удаление и восстановление RC2

## Граница разрешения

В этой разработке разрешены только sandbox-операции. Настоящая установка/запуск ещё не разрешены. CLI дополнительно требует явный consent argument, но наличие такого аргумента в инструкции само по себе не является согласием пользователя.

Поддержан локальный Windows NTFS, закрытая игра и закрытый EACLauncher. Junction/reparse paths, корень диска, UNC и sandbox с BrokenArrow.exe отвергаются. Не применять старые RC1 install scripts к этой поставке.

## Категории файлов

- Immutable: собственный plugin в Mods, пять project libraries в UserLibs, compatibility descriptor.
- Mutable: только UserData/DynamicFrontline030/probe.json; настроенный JSON сохраняется при reinstall/remove.
- Local generated provenance: свой interop-provenance.json.
- Shared cache: MelonLoader/Il2CppAssemblies/*.dll/*.db, generator Config.cfg и локальные bootstrap dependencies. Точный read-only plan записывает текущие хеши заменяемых файлов.
- Retired cache: старые wrappers, отсутствующие в новой генерации, удаляются из активного cache только по отдельным записям этого же плана. Исходные байты сохраняются и возвращаются при remove. Смешанный unprefixed/prefixed cache не оставляется активным.

Ключи/PFX/logs не являются install payload и не удаляются. Владелец — receipt+hash, а не совпадение имени каталога. Если cache изменился после установки, remove сохраняет всю взаимозависимую cache-группу как конфликт; неизменённые собственные модовые DLL можно удалить. Для ручного разрешения конфликта сначала сохранить отчёт и исходные файлы; не применять рекурсивное удаление.

## Команды

Из корня распакованного пакета, PowerShell7:

```powershell
$dotnet = 'C:/path/to/dotnet.exe'
$game = 'D:/steam/steamapps/common/broken_arrow'
$client = 'C:/DF-private/client-A'
$cli = './scripts/poc030-rc2/run-dev-tools.ps1'
& $cli -DotnetPath $dotnet plan $game "$client/client-manifest.json" "$client/install-plan.json"
```

Проверить весь install-plan до изменения игры. Он включает точные source/target hashes и старые cache-файлы. Plan хранить вне папки игры. После отдельно полученного согласия:

```powershell
& $cli -DotnetPath $dotnet install $game "$client/install-plan.json" --explicit-install-consent
& $cli -DotnetPath $dotnet verify $game --explicit-install-consent
```

Если операция прервана, при закрытой игре:

```powershell
& $cli -DotnetPath $dotnet recover $game --explicit-install-consent
& $cli -DotnetPath $dotnet verify $game --explicit-install-consent
```

Verify открывает служебный lock, поэтому в этой версии использует тот же закрытый-game gate. Ошибка доступа не даёт разрешения что-либо удалить: устранить блокировку и повторить recover. Он идемпотентен.

Удаление после отдельного разрешения:

```powershell
& $cli -DotnetPath $dotnet remove $game --explicit-removal-consent
```

Remove восстанавливает исходный shared cache из content-addressed originals, удаляет только неизменённые принадлежащие RC2 DLL и сохраняет mutable config. MelonLoader установлен отдельно и не удаляется RC2 CLI. Его откат выполняется отдельно по его собственному inventory, без удаления чужих модов.

## WAL и пределы проверки

До payload writes создаётся journal с before/after hashes, stage/backup путями и before/after receipt. Частичная staging-запись не становится активным DLL. Staging flush(true), atomic rename, receipt и commit marker разделены. При незавершённой операции recover проверяет текущий hash и откатывает только известные изменения; чужие изменения сохраняются.

Проверены частичные writes, исключения на границах, fail-fast отдельных процессов, upgrade/remove, повторное recover, старый общий cache и полные A/B manifests в sandbox. Один локальный запуск встретил временный Windows access-denied при rollback; неизменённый journal сохранился, повторный чистый тест прошёл. Это не имитация физического power-loss и не гарантия контроллера хранения.

Для собственных репетиций заменить команды на sandbox-install/sandbox-verify/sandbox-recover/sandbox-remove и выбрать отдельную пустую директорию без BrokenArrow.exe. Никогда не называть настоящую игру sandbox.
