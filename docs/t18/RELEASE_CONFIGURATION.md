# Публикация и обновления

Репозиторий: https://github.com/IndustKocs/BrokenArrow-DynamicFrontline (Private).
Конфигурация: launcher/release-channel.json. Канал принимает опубликованные prerelease, но не drafts, и только пакет с Protocol=df18-world-4. Несовместимый пакет останавливает установку.

Артефакты: DynamicFrontlineLauncher.exe, DynamicFrontline-T18-Steam-Candidate.zip, SHA256.txt, RELEASE_NOTES.md. EXE содержит ту же ZIP-сборку для установки без GitHub credentials. Bootstrap portable PowerShell закреплён SHA256; зависимости мода закреплены исходными scripts/t18/dependencies.json и system-runtime.json. Пакет имеет SHA256_INVENTORY.json.

Для воспроизводимой сборки разработчик запускает scripts/t18/package.py, затем launcher/build.ps1 с путём к ZIP. EXE строится штатным csc .NET Framework, не включает игровые DLL и interop.

Издатель авторизуется локально через GITHUB_TOKEN с Contents: write и запускает launcher/publish-release.ps1 -Directory <поставка> -Tag <новый-tag>. Скрипт сначала проверяет private=true и наличие всех четырёх артефактов. Создаётся DRAFT prerelease; перед публикацией нужно проверить содержимое и вручную опубликовать draft. Этот шаг в текущей сессии не выполнен из-за отсутствия авторизованного доступа.

Тестеры должны иметь собственный доступ к private repo. Для обновлений поле токена использует Contents: read; передавать общий publisher token запрещено. Releases API и asset API вызываются только для заданного репозитория, по HTTPS. Загруженный ZIP проверяется по digest GitHub asset и внутреннему inventory; имена с выходом из каталога, дубликаты и чрезмерный размер отклоняются. Это доверие к учётной записи издателя/GitHub, не отдельная криптографическая подпись релиза.

EXE пока unsigned. Смена приватности, публикация токенов и запуск игры не входят в скрипты. Не добавляйте в GitHub артефакты preparation/sandbox/download caches либо исследовательские файлы игры.
