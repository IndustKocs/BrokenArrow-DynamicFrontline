# Startup hotfix — 0.4.0-T18-full-debug2 (2026-10-31)

Two independent defects confirmed from the first real startup:
- MelonGame metadata used display names with spaces. Corrected to the runtime identity SteelBalalaikaStudio / BrokenArrow. Binary attribute regression passes.
- Pinned interop generator emits illegal CLR generic pointer arguments in IL2CPP.RenderTypeName<T*>. Old UnityEngine.Debug .cctor reproduces BadImageFormatException using RuntimeHelpers.PrepareMethod without invoking game code. New generator post-pass renders the element through the same renderer and appends pointer/byref suffixes. It preserves native lookup names and leaves pinned loader binaries unchanged. Unknown illegal generic forms fail preparation.

Fresh local generation repairs 1889 call sites. No illegal pointer generic calls remain. Debug/Object/GUI/Input static constructors pass JIT compilation without invocation. All 38 binding contracts remain present. Synthetic executed tests cover shared MethodSpec tokens, pointer/byref/multiple indirection/void/generic element names, rejection before repair, and idempotence. Existing Steam 87, RC2 52, RC1 44, Simulation023 72 checks pass (255 total).

Runtime validation remains pending: loader completion, native Unity calls, session gate, Relay, HUD, Gamelog and first LoadReadProbe. JIT preflight does not claim those tests passed. No game launch or game installation performed by this fix. User's game was already running during preparation; target files were not modified.

## Install on both PCs

Download the full-debug2 launcher from GitHub Releases. Close Broken Arrow. Leave GitHub token empty. If DF is already installed, use Update, then Verify files; if removed, use Install, then Verify files. Fresh interop and JIT checks run locally. Never copy interop between PCs. The new launcher uses the public release channel without a token. The old full-debug1 EXE still has the private-channel flag: download the new EXE once to migrate without credentials.

Then launch manually via Steam Anti-Cheat Disabled with:
--melonloader.agfoffline --melonloader.agfvdumper 2022.1.0-pre-release.21

First check: no incompatible game message, DF mod loads, no UnityEngine.Debug BadImageFormatException, HUD appears. Only then continue private Scenario 5454584/Airbase LoadReadProbe. No roster/FullDebug consent until both own rosters are checked. Native effects remain off.
