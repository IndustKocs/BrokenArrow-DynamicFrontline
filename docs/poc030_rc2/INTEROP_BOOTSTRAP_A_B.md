# Локальный interop на каждом ПК

## Pinned inputs

Игра: Steam app1604270, build26273833, Unity6000.3.24f1, metadata39. Четыре игровых SHA256 находятся в config/poc030/compatibility.json. На ПК A Steam manifest при подготовке также показал build26273833; это не проверка ПК B.

Нужны локальные .NET runtime6.0 (стенд6.0.36; SDK8.0.426 для сборки исходников), PowerShell7 и следующие отдельно полученные инструменты. В приватном RC2 ZIP сторонние/game binaries отсутствуют.

| Файл | SHA256 |
|---|---|
| MelonLoader.x64.zip v0.7.3 | 5B2B2F3D1CD42B59EC886C5BDC2663EDAE87A0097A4F4A8F58C0965A99DDA416 |
| Cpp2IL.exe 2022.1.0-pre-release.21 | 663FB432433B4371FD1EE0EBC321A8FFF2A9AAC5AC4230C843F9E03DDEE4E04C |
| Managed.zip Unity6000.3.24 | 1142D6C9AA501DA2DF09AA82B5CFD18B584C0A7E1DDE0E059DE665A147278821 |
| Cpp2IL.Plugin.StrippedCodeRegSupport.dll той же версии | 2CC4F8C66541F18B4DAED5410149EE7859C3190454D30BDA5CCD0895D2989EE6 |

Официальные источники: https://github.com/LavaGang/MelonLoader/releases/tag/v0.7.3 ; https://github.com/SamboyCoding/Cpp2IL/releases/tag/2022.1.0-pre-release.21 ; https://github.com/LavaGang/MelonLoader.UnityDependencies/releases/download/6000.3.24/Managed.zip . Проверить SHA256, не подменять latest. Generator/Runtime/Harmony dependencies дополнительно pinned в config/poc030-rc2/toolchain.json.

## Подготовка вне игры

Распаковать loader в отдельную tool-directory; это не установка в игру. Проверить dotnet --list-runtimes. Из корня RC2:

```powershell
./scripts/poc030/preflight-runtime.ps1 -GameRoot $game
./scripts/poc030-rc2/prepare-interop.ps1 -DotnetPath $dotnet -Cpp2IlPath $cpp2il -LoaderDirectory $loaderNet6 -GameRoot $game -UnityZip $managedZip -StrippedPlugin $strippedPlugin -OutputDirectory 'C:/DF-local/interop-A'
```

OutputDirectory обязан быть новым и находиться вне игры. В поставке есть собственный interop-generator driver по ожидаемому scripts/poc030/interop-generator/bin/Release/net6.0 пути; его сторонние зависимости читаются из LoaderDirectory. Скрипт читает настоящие GameAssembly/metadata, создаёт новые dummy, затем wrappers с точными Unity base libraries и xref DB; выполняет metadata binding inspector и повторяет preflight. Никакой игровой код не запускается.

Результат на стенде:198 DLL и2 DB. Префикс game wrappers — Il2Cpp (например Il2CppBrokenArrow.dll); UnityEngine.* остаются без префикса. Локальные generated DLL/Unity files не передавать вместе с общим RC2 ZIP; каждый владелец генерирует их на своём ПК.

Config.cfg и bootstrap manifest подготавливаются для обычного MelonLoader generator. Его dependency setup выполняется раньше решения о regeneration, поэтому одного готового cache недостаточно: нужны pinned Cpp2IL/SCRS/Unity dependency files и SHA512 GameAssembly в config. Offline flag сам по себе не отключает regeneration.

После отдельного разрешения установить pinned MelonLoader0.7.3 в закрытую совместимую игру по официальной процедуре, **без запуска**, сохранив его отдельный inventory и исходные совпавшие файлы. Затем применить RC2 plan. RC2 installer проверяет установленный MelonLoader.dll и не принимает force_regeneration=true. Не использовать альтернативный base directory: текущий plugin ожидает стандартные UserData/Mods/UserLibs пути относительно BrokenArrow.exe.

Для отдельно разрешённого запуска нужны параметры:

```text
--melonloader.agfoffline --melonloader.agfvdumper 2022.1.0-pre-release.21
```

На загрузке InteropGate сверяет всю локальную cache/provenance, отсутствие лишних DLL, pinned loader dependencies и фактически загруженные wrapper assemblies. Только затем разрешается guarded reader. Metadata verification не подтверждает native invocation: первый такой вызов остаётся LoadReadProbe на обоих ПК.

Исходный порядок MelonLoader проверен по исходникам v0.7.3: Dependencies/Il2CppAssemblyGenerator/Core.cs, Config.cs, Packages/Cpp2IL.cs, Packages/UnityDependencies.cs и MelonLoader/Utils/MelonEnvironment.cs в https://github.com/LavaGang/MelonLoader/tree/v0.7.3 .
