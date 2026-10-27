# Воспроизведение

Рабочая ветка: feature/prototype-023-zoc-encirclement. SDK8.0.426 согласно global.json; target .NET6. Путь не зашит в новый script. Нужны прежний NuGet local feed в tools/feed и установленный совместимый runtime. В локальном запуске использован прежний toolchain; автоматических загрузок нет.

```powershell
./build-prototype023.ps1 -DotnetPath 'PATH/TO/dotnet.exe' -Regression
./build-prototype023.ps1 -DotnetPath 'PATH/TO/dotnet.exe' -Benchmarks
& 'PATH/TO/dotnet.exe' src/DynamicFrontline.Prototype023.Runner/bin/Release/net6.0/DynamicFrontline.Prototype023.Runner.dll replay config/prototype023/ring-supply-release.json artifacts/prototype023/ring-supply-release
& 'PATH/TO/dotnet.exe' src/DynamicFrontline.Prototype023.Runner/bin/Release/net6.0/DynamicFrontline.Prototype023.Runner.dll benchmark artifacts/prototype023/benchmarks 10000 3
```

Не запускать сборку тех же DLL одновременно с benchmark на Windows: loaded assembly lock. Build error не проглатывается. Если прежние MelonLoader.dll/0Harmony.dll отсутствуют в tools/melonloader/MelonLoader/net6, loader-status сообщает blocker; остальные автономные проекты доступны.

HTML можно открыть как локальный файл без сервера. Для браузера во время работы использовался read-only localhost8767. Scenario schema описывает оболочку; вложенные профили/события дополнительно проверяются C# контрактами, это не полная JSONSchema валидация всех семантических инвариантов.

JSON changes/services на250мс границах; native events должны входить в свой интервал. Frame берётся из ядра; JS не вычисляет правила. Для повторения сохранённого продолжения runner автоматически сериализует checkpoint на половине сценария и сравнивает final. Результаты и hashes находятся в artifacts/prototype023; сохранённое evidence — в evidence/prototype023.
