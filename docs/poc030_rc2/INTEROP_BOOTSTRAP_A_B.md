# Interop: preparation status

Both machines must independently pass `scripts/poc030/preflight-runtime.ps1`. Current local file preflight passed; this does not approve installation. Generation remains isolated outside the game. `generate-interop.ps1` validates pinned generator dependencies and output bindings. Generated wrappers are local proprietary derivatives and must not enter a distributable archive.

RC1 generation uses NoXrefCache and omits UnityBaseLibsDir. It is not equivalent to the normal loader generation path and is not yet approved for bootstrapping. Missing exact Unity dependencies must not be replaced by guessed versions.

Verified upstream 0.7.3 contracts:

- [MelonEnvironment](https://raw.githubusercontent.com/LavaGang/MelonLoader/v0.7.3/MelonLoader/Utils/MelonEnvironment.cs): cache is under the configured base directory, `MelonLoader/Il2CppAssemblies`.
- [Generator Core](https://raw.githubusercontent.com/LavaGang/MelonLoader/v0.7.3/Dependencies/Il2CppAssemblyGenerator/Core.cs): generation decisions and old-file cleanup depend on generator configuration; copying wrappers alone is insufficient.
- [Generator options](https://raw.githubusercontent.com/LavaGang/MelonLoader/v0.7.3/Dependencies/Il2CppAssemblyGenerator/Packages/Il2CppInterop.cs): normal generation includes Unity base libraries.
- [LoaderConfig](https://raw.githubusercontent.com/LavaGang/MelonLoader/v0.7.3/MelonLoader/LoaderConfig.cs): offline generation disables remote API contact; it does not disable generation itself.

Outstanding: isolated complete generation, atomic cache/config ownership plan and rollback with foreign cache preservation, fingerprint matching, and separate authorized two-client loaded verification. Do not fabricate a generation-success receipt to suppress loader work. The plugin now waits up to 45 seconds for verified loaded wrappers, and refuses missing provenance/hash mismatches before creating its reader.
