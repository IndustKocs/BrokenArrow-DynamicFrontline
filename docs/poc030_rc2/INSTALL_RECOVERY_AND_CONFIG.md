# Development installer

DevInstall is independent from Launcher. It validates paths, rejects reparse ancestors, requires local Windows NTFS, locks operations, verifies source hashes, stages files, flushes them and writes a journal before target replacement. Recovery compares known before/after hashes and preserves conflicts. Mutable probe.json survives removal and reinstall. Changed DLLs remain as explicit conflicts while unchanged owned files can be removed.

The `scripts/poc030-rc2/dev-tools` console exposes plan and sandbox-install/verify/recover/remove. It deliberately does not expose game installation: Shared loader cache/config rollback is not complete. No automatic game/loader launch exists in this CLI.

Twelve child-process fail-fast points recover successfully in fresh installer instances; exception tests cover the same apply flow. This is not proof against every disk/controller/power-loss failure. Upgrade/removal exception recovery passes; fail-fast upgrade/removal, disk-full and permission-loss matrices remain pending. Never remove a directory recursively or guess ownership from a filename.
