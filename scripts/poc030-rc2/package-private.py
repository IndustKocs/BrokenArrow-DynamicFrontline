"""Explicit allowlist: own binaries/source, derived descriptors, sanitized offline evidence."""
import hashlib, json, pathlib, shutil, subprocess, zipfile, sys
root = pathlib.Path(__file__).resolve().parents[2]
out = root / (sys.argv[1] if len(sys.argv)>1 else 'artifacts/poc030_rc2/delivery')
radmin=len(sys.argv)>2 and sys.argv[2]=='radmin'
airbase=len(sys.argv)>2 and sys.argv[2] in ('airbase','radmin')
stage = out / ('DynamicFrontline-030-RC2-Airbase-Radmin' if radmin else 'DynamicFrontline-030-RC2-Airbase-5454584' if airbase else 'DynamicFrontline-030-RC2-private')
if stage.exists():
    raise SystemExit('Fresh delivery directory required')
stage.mkdir(parents=True)
def copy(src, dst=None):
    source = root / src
    target = stage / (dst or src)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
def tree(src, dst=None, suffixes=None):
    for p in (root / src).rglob('*'):
        if not p.is_file() or any(x in ('bin','obj','__pycache__') for x in p.relative_to(root/src).parts): continue
        if suffixes and p.suffix not in suffixes: continue
        copy(p.relative_to(root), pathlib.Path(dst or src)/p.relative_to(root/src))
for name in ('Core','Frontline','GameAdapter','Integration','Poc030','Poc030.MelonLoader'):
    copy(f'src/DynamicFrontline.Poc030.MelonLoader/bin/Release/net6.0/DynamicFrontline.{name}.dll',f'payload/DynamicFrontline.{name}.dll')
for p in (root/'scripts/poc030-rc2/dev-tools/bin/Release/net6.0').iterdir():
    if p.name.startswith(('DynamicFrontline.','dev-tools.')) and p.suffix in ('.dll','.json'):
        copy(p.relative_to(root),'bin/dev-tools/'+p.name)
for suffix in ('.dll','.deps.json','.runtimeconfig.json'):
    p='scripts/poc030/interop-generator/bin/Release/net6.0/interop-generator'+suffix
    copy(p)
tree('config/poc030-rc2'); copy('config/poc030/compatibility.json')
if radmin:
    tree('docs/poc030_radmin')
    tree('evidence/poc030_radmin',dst='evidence/radmin')
tree('docs/poc030_rc2');
if airbase:
    tree('docs/poc030_airbase')
    tree('evidence/poc030_airbase',dst='evidence/airbase')
tree('scripts/poc030-rc2',suffixes={'.ps1','.py','.cs','.csproj'})
for name in ('preflight-runtime.ps1','generate-interop.ps1','build-poc030.ps1'): copy('scripts/poc030/'+name)
tree('scripts/poc030/interop-generator',suffixes={'.cs','.csproj'})
for family in ('Core','Frontline','GameAdapter','Integration','Poc030','Poc030.MelonLoader'):
    tree('src/DynamicFrontline.'+family,suffixes={'.cs','.csproj'})
for p in (root/'tests').iterdir():
    if p.is_dir() and 'Launcher' not in p.name: tree(str(p.relative_to(root)),suffixes={'.cs','.csproj'})
for name in ('Directory.Build.props','global.json','NuGet.Config'): copy(name)
final='artifacts/poc030_rc2/final'
for p in (root/final).glob('*.log'): copy(p.relative_to(root),'evidence/'+p.name)
for name in ('bindings-expanded.json','network-probe-A.json','network-probe-B.json','airport-50.evidence.json','airport-100.evidence.json'):
    copy(final+'/'+name,'evidence/'+name)
for name in ('packaged-bindings.json',):
    if (root/final/name).exists(): copy(final+'/'+name,'evidence/'+name)
if (root/final/'package-sandbox').exists():
    for p in (root/final/'package-sandbox').iterdir():
        if p.is_file() and p.suffix in ('.log','.json'): copy(p.relative_to(root),'evidence/package-sandbox/'+p.name)
for p in (root/final/'tests-package').iterdir():
    if p.is_file(): copy(p.relative_to(root),'evidence/tests-package/'+p.name)
for p in (root/final/'performance').iterdir():
    if p.is_file(): copy(p.relative_to(root),'evidence/performance/'+p.name)
if (root/final/'events').exists():
    for p in (root/final/'events').iterdir():
        if p.is_file(): copy(p.relative_to(root),'evidence/events/'+p.name)
for p in (root/'artifacts/poc030_rc2/two-process-a3511f10475148aa8103eebed919e3b1').iterdir():
    if p.suffix in ('.json','.jsonl'): copy(p.relative_to(root),'evidence/two-process/'+p.name)
# Existing offline logs may contain local source paths, never session credentials.
for p in (stage/'evidence').rglob('*'):
    if p.is_file() and p.suffix in ('.log','.json','.jsonl'):
        text=p.read_text(encoding='utf-8-sig')
        for path in (str(root),str(root).replace('\\','/'),str(root).replace('\\','\\\\')):
            text=text.replace(path,'<WORKSPACE>')
        p.write_text(text,encoding='utf-8')
(stage/'SOURCE_BUILD.md').write_text('''# Rebuild own code

SDK 8.0.426, .NET 6 targeting/runtime packs. NuGet.Config intentionally uses
tools/feed; provide the local Microsoft.NETCore.App.Ref 6.0.36 and
Microsoft.AspNetCore.App.Ref 6.0.36 packages, or configure your own approved feed.
Pinned loader dependencies: config/poc030-rc2/toolchain.json.

dotnet build src/DynamicFrontline.Poc030.MelonLoader -c Release -p:LoaderDirectory=<absolute loader/net6>
dotnet build scripts/poc030/interop-generator -c Release -p:LoaderDirectory=<absolute loader/net6>
dotnet build scripts/poc030-rc2/dev-tools -c Release
dotnet run --project tests/DynamicFrontline.Rc2.Tests -c Release -- <fresh evidence directory>

For source history and baseline regression build scripts use the RC2 repository
revision in BUILD_RECORD.json. The archive includes the relevant source snapshot,
not unrelated Launcher/research tools or historical generated fixture files.
Benchmark commands: tests runner benchmark-full <map> <out> <count> <steps> <seed>
and benchmark-events <map> <out> 100 10800 <seed>. Events uses an explicitly
synthetic map; it cannot be selected by the game client preparation helper.
''',encoding='utf-8')
(stage/'README.md').write_text('''# Dynamic Frontline 0.3 RC2 — private diagnostic package

Start with docs/poc030_rc2/RC2_FINAL_REPORT.md and TWO_CLIENT_RUNBOOK_RC2.md.
Airport / Airport_network; both clients LoadReadProbe, then ObserveTwoClient.
No game installation or launch is authorized by this archive. RuntimeValidated=false.
Own binaries only. Install .NET 6 runtime / SDK 8 for builds separately. Pinned loader,
Cpp2IL, Unity dependencies and generated interop are prepared locally on each PC.
No native effects. Keep payload/config and generated local preparation directories
at stable paths. Never copy the entire archive into the game directory.
CLI: scripts/poc030-rc2/run-dev-tools.ps1. Install/remove only through reviewed plans.
Evidence contains synthetic observations, not a recorded game match.
''',encoding='utf-8')
if airbase:
    (stage/'README.md').write_text('''# RC2 Airbase / 5454584 — separate diagnostic build

Start with docs/poc030_airbase/RELEASE.md and RUNBOOK.md.
Use config/poc030-rc2/maps/Airbase-5454584-100.json, never rename the Airport map.
RuntimeValidated=false. No game installation or launch authorized.
Baseline docs/poc030_rc2 and evidence outside evidence/airbase are historical
Airport evidence, not validation of this variant. New build evidence: evidence/airbase.
Scenario/game binaries and secrets are not included. Both PCs require identical
original scenario bascr AND meta.json, pinned game and local interop.
''',encoding='utf-8')
if radmin:
    (stage/'README.md').write_text('''# RC2 Airbase / 5454584 / pinned Radmin pair

Start with docs/poc030_radmin/RUNBOOK.md and RELEASE.md.
A=26.137.77.181, B=26.204.104.7. Explicit -RadminPair preparation required.
TLS certificate pinning and HMAC remain mandatory. No installation has been performed.
Do not reuse DLLs or configs from previous ZIPs. Actual two-PC Radmin probe NotRun.
Current evidence: evidence/radmin. Airbase/base evidence is historical.
No scenario/game/loader binaries, PFX or keys included. RuntimeValidated=false.
''',encoding='utf-8')
record={'SourceRevision':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),
        'SourceStatus':subprocess.check_output(['git','status','--porcelain'],cwd=root,text=True).splitlines(),
        'RuntimeValidated':False,'GameInstalled':False,'GameLaunched':False,
        'SDK':'8.0.426','Target':'net6.0','ArchiveKind':'private diagnostic, not runtime acceptance'}
(stage/'BUILD_RECORD.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
inventory=[]
for p in sorted(stage.rglob('*')):
    if not p.is_file(): continue
    if p.suffix.lower() in ('.pfx','.key','.db','.exe') or p.name in ('GameAssembly.dll','global-metadata.dat'):
        raise RuntimeError('Forbidden packaged artifact: '+str(p))
    if p.suffix=='.dll' and not p.name.startswith(('DynamicFrontline.','dev-tools.','interop-generator.')):
        raise RuntimeError('Third-party binary: '+str(p))
    inventory.append({'Path':p.relative_to(stage).as_posix(),'Length':p.stat().st_size,'SHA256':hashlib.sha256(p.read_bytes()).hexdigest().upper()})
(stage/'SHA256_INVENTORY.json').write_text(json.dumps(inventory,indent=2),encoding='utf-8')
archive=out/(stage.name+'.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in sorted(stage.rglob('*')):
        if p.is_file(): z.write(p,p.relative_to(stage))
archive.with_suffix('.zip.sha256').write_text(hashlib.sha256(archive.read_bytes()).hexdigest().upper()+'  '+archive.name+'\n',encoding='ascii')
print(str(archive));print(len(inventory),'files; bytes',archive.stat().st_size)
