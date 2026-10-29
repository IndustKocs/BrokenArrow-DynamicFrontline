# Сборка диагностического кандидата

Требуются PowerShell7 и SDK8.0.426/net6 reference packs; пути передаются параметрами. Никаких auto-download/update.

Из корня worktree:

```powershell
./scripts/poc030/build-poc030.ps1 -DotnetPath '<dotnet.exe>' -LoaderDirectory '<MelonLoader/net6>' -FeedDirectory '<local nupkg feed>'
./scripts/poc030/test-loopback.ps1 -DotnetPath '<dotnet.exe>'
./scripts/poc030/preflight-runtime.ps1 -GameRoot '<game>' -SteamManifest '<appmanifest_1604270.acf>'
./scripts/poc030/generate-interop.ps1 -DotnetPath '<dotnet.exe>' -Cpp2IlPath '<pinned Cpp2IL.exe>' -LoaderDirectory '<MelonLoader/net6>' -GameRoot '<game>' -OutputDirectory '<fresh directory outside game>'
```

Пакет содержит собственный исходный код для rebuild диагностических DLL/тестов/interop driver и используемые Core/Frontline sources. Исторический полный regression runner остаётся в исходном worktree; его результаты сохранены отдельно. Generated game wrappers не распространяются. Наличие offline wrappers не разрешает их копирование в установленный loader.

Plan установки: `scripts/poc030/prepare-dev-install.ps1 -PackageRoot '<unpacked>' -TargetRoot '<game>' -Plan`. Этот скрипт не пишет. Apply запускается только после отдельного согласия и закрытой игры. Сначала прочитать ADAPTATION_REPORT/OPEN_DECISIONS_AND_BLOCKERS.
