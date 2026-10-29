# Передача установщику

Launcher не изменялся. Zip — частный диагностический candidate, не production allowlist. Нельзя присваивать runtimeValidated/installApproved по его существованию.

Новый dev installer работает только с пятью точными файлами: Mods/DynamicFrontline.Poc030.MelonLoader.dll, UserLibs/DynamicFrontline.Poc030.dll и probe.json/compatibility.json/interop-provenance.json в UserData/DynamicFrontline030. Manifest проверяется перед записью; любой существующий файл или другой DynamicFrontline mod — конфликт без overwrite. Поэтому backups не нужны: receipt явно фиксирует NoExistingFilesOverwritten. Создание новых файлов — CreateNew; после каждого — receipt. В случае неполной установки удалить только подтверждённые по receipt файлы.

prepare-dev-install без Apply или с Plan ничего не пишет. apply требует отдельный ExplicitInstallConsent и закрытую игру. Реальный preflight только читает. Sandbox запрещён при наличии BrokenArrow.exe; он применялся лишь к искусственной директории. Junction/reparse destinations отклоняются.

remove-dev-install -Plan читает receipt; -Apply требует ExplicitRemovalConsent, closed game и совпадение каждого hash. Изменённый config или DLL сохраняется с ошибкой; автоматически не удалять пользовательские изменения. Нет recursive Mods delete. Логи, secrets, чужие DLL и пустые каталоги остаются.

**Не выполняется:** установка/обновление самого loader, перенос168генерированных interop DLL, настройка runtime cache, Steam options, firewall, запуск/остановка процессов. Эти операции требуют отдельного reviewable плана. Нынешний пакет сам по себе не закрывает этот этап и не должен подаваться Launcher как готовый runnable profile.

На второй машине использовать собственную законную установку. Dependencies должны совпадать с evidence/dependencies.json. SDK/Cpp2IL/loader скачивание и лицензии не включены в zip; существующий pinned toolchain.lock задаёт исходные версии/хеши. Генерация локальная по согласованной паре; игровые бинарники и ресурсы не пересылаются.
