# full-debug3 — startup and HUD diagnostics (2026-10-31)

Observed on full-debug2: MelonLoader loads 1 mod, the old BadImageFormatException is absent, then DF reports NullReferenceException with no stack and the tester sees no HUD. This log does not establish the exact throwing call.

Changes:
- A controller with no network room/state is treated as outside a match; lobby observation continues. Actual match state still must pass every existing Scenario/server/anti-cheat gate. No synthetic session or permissive fallback.
- HUD binding and drawing are independent of native lobby-hook success after verified interop. Texture access is deferred to rendering. Missing binding names are explicit.
- GUI color/background/content/enabled/depth are scoped and restored, so inherited invisible/disabled state does not silently hide the panel.
- Bootstrap JSONL is written before native logging. Status is persisted before binding FileLog. Fault reports include fixed phase, exception type and DF method names, never exception messages, paths, arguments or local values. Export keeps only bounded allowlisted codes.
- First successful panel draw and missing OnGUI callback have distinct diagnostics. OnGUI exceptions are logged with rate limiting rather than silently queued.

Validation: Steam 95 + RC2 52 + RC1 44 + Simulation023 72 = 263 passing checks; interop pointer repair regression passes. Debug/Object/GUI/Input constructors and the five GUI state property getter/setters JIT-compile without invocation. No game started or real game files changed by this work.

Limits: null room is a verified unsafe code path, not a proven location of the tester's exception. HUD visibility and exact native failure still require the next real startup. No successful 1v1, Steam Relay, native roster or Gamelog receipt is claimed.

Tester: close game, use the full-debug2 public-channel launcher Update, then Verify files on both PCs. Same Steam launch arguments and Anti-Cheat Disabled. First stop at main menu and check HUD with full-debug3 label. Do not start a match or confirm roster yet. If absent, keep the new startup log for local diagnosis; share only safe exports.
