# full-debug4 — native layout repair

Confirmed defect: locally generated full-debug3 UnityEngine.Rect had four float fields at offset 0, rather than 0/4/8/12. Color and Vector3 were affected too. OnGUI returned without an exception but this did not prove a visible draw. NativeContext.Read also logged a null target; additional null checks now preserve waiting/blocking behavior for incomplete room/lobby/auth data.

Root cause: our Cpp2IL command omitted attributeanalyzer and attributeinjector. Dummy output therefore lacked native FieldOffset/Token metadata. The corrected command uses both processors and runs from the pinned Cpp2IL directory so its pinned plugin is discovered. See https://github.com/SamboyCoding/Cpp2IL/blob/development/README.md for processor selection.

No manual native offsets are patched. Generation consumes native metadata, rejects missing critical source attributes, and verifies every matched generated value field against the injected offset. Fresh generation verified 13162 fields and 38 binding contracts. Rect/Color/Vector3 size and offset checks pass, old full-debug3 wrappers fail the same check. Debug/Object/GUI/Input and GUI state getters/setters JIT-compile without execution. The pointer-name guard remains; corrected generation needed zero pointer-call repairs.

Installer: only the exact owned generator Config.cfg schema can be canonicalized back to its ORIGINAL receipt SHA256. Formatting differences (whitespace/newlines/trailing list comma) are accepted for verify/update/remove; changed values, unknown fields, duplicate filenames and unknown files remain blocked. WAL plans still use the actual before-file hash. Tests cover formatting, changed values, verify, update, crash recovery preserving the original formatted bytes, and removal.

Validation: Steam 95 + RC2 53 + RC1 44 + Simulation023 72 = 264 passing existing/regression cases, plus pointer repair/identity tests and fresh generation checks. Real HUD visibility, native roster, Gamelog receipt and first private 1v1 remain unverified. No game launched or live game files changed during this fix.

Tester: close game, use full-debug2-or-newer public launcher Update, then Verify files. It regenerates interop locally; never copy wrappers between PCs. Same Steam parameters and Anti-Cheat Disabled. First check only main-menu HUD labelled full-debug4. Do not enter a match or confirm roster until startup is verified.
