# R1–R8: закрытие замечаний RC1

| ID | Реализация | Доказательство | Runtime предел |
|---|---|---|---|
| R1 | DevInstall: MutableUserConfig, virtual RC1 receipt migration, hash-owned DLL removal | RC2 tests configure/remove/reinstall и RC1 migration; два полных sandbox manifests | На старой установке сохранённый конфиг нужно проверить вручную перед сменой режима |
| R2 | Предварительный WAL, flush staging, atomic receipt, rollback, shared-cache original backups/retirement | 12 fail-fast child processes; upgrade/remove/partial-write/recovery/cache interruption tests; A/B sandbox install/verify/remove | Реальное отключение питания не проверено; NTFS only |
| R3 | SourceHealthTracker и atomic snapshot/health publication, age независимо от heartbeat | stale/failure/pause tests; race найден и исправлен в reconnect integration | Native read timing NotRun |
| R4 | prepare-interop + generate-interop + transaction plan + InteropGate | Свежая генерация 198 DLL +2 DB; 30 metadata contracts; полный sandbox cache | Loaded IL2CPP invocation NotRun |
| R5 | LiveReader → RuntimeObserver → CanonicalWorld → ObservationBridge → Simulation023.Observe → Public023/HUD | 80→20→80 actual-stress test; full worker two-process trace; no autonomous backend writes | Настоящий HUD и callback NotRun |
| R6 | Native binding, full/delta, generations, point-proof dedupe, authenticated checkpoint, ACK/digests | Два TLS процесса и forced reconnect; active tau checkpoint; conflicting/late/duplicate inputs | Между игровыми ПК NotRun |
| R7 | Stage timing/raw CSV, full codec/core/projection runs; struct neighbor iteration, immutable hash caching, no full checkpoint merely for digest; obsolete generation pruning | PERFORMANCE_RC2 и actual raw files | Native reader/HUD/live soak не измерены; allocation target не объявлен достигнутым |
| R8 | Allowlisted actual evidence files, collection/compare/replay CLI | Build/test/transcript files присутствуют в ZIP, а не только их хеши | Реальные игровые логи появятся после отдельного согласия |

Код и тесты находятся в `src/DynamicFrontline.Poc030*`, `src/DynamicFrontline.Frontline/Rules023`, `tests/DynamicFrontline.Rc2.Tests`, `scripts/poc030-rc2`. Точная исходная ревизия поставки записана в build record. Закрытие offline части не равно runtime acceptance.
