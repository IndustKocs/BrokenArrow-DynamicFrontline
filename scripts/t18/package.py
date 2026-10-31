"""Allowlisted T18 candidate: no game/interop/loader binaries or private RC2 pairs."""
from pathlib import Path
import sys,json,hashlib,shutil,zipfile,subprocess
root=Path(__file__).resolve().parents[2]
out=root/(sys.argv[1] if len(sys.argv)>1 else 'artifacts/t18-release')
stage=out/'DynamicFrontline-T18-Steam-Candidate'
if stage.exists():raise SystemExit('Fresh output required')
stage.mkdir(parents=True)
def copy(src,dst=None):
 s=root/src;t=stage/(dst or src);t.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(s,t)
def tree(src,ext=None):
 for p in (root/src).rglob('*'):
  if p.is_file() and not any(x in ('bin','obj','__pycache__') for x in p.relative_to(root/src).parts) and (ext is None or p.suffix in ext):copy(p.relative_to(root))
for name in ('Core','Frontline','GameAdapter','Integration','Poc030','Steam','Steam.MelonLoader'):
 copy(f'src/DynamicFrontline.Steam.MelonLoader/bin/Release/net6.0/DynamicFrontline.{name}.dll',f'payload/DynamicFrontline.{name}.dll')
 tree('src/DynamicFrontline.'+name,{'.cs','.csproj','.json'})
for p in (root/'scripts/poc030-rc2/dev-tools/bin/Release/net6.0').glob('*'):
 if p.is_file() and p.suffix in ('.dll','.json') and p.name.startswith(('DynamicFrontline.','dev-tools.')):copy(p.relative_to(root),'bin/dev-tools/'+p.name)
for name in ('START.ps1','Directory.Build.props','global.json','NuGet.Config','config/poc030/compatibility.json','config/poc030-rc2/profiles.json','config/poc030-rc2/toolchain.json','config/poc030-rc2/maps/Airbase-5454584-100.json'):copy(name)
tree('launcher',{'.cs','.ps1','.json'})
for name in ('docs/t18','evidence/t18','evidence/t18-hardening','evidence/t18b','evidence/t18-debug-launcher'):tree(name)
for name in ('docs/poc030_rc2/INTEROP_BOOTSTRAP_A_B.md','docs/poc030_airbase/MAP_DESCRIPTOR_AIRBASE_5454584.md'):
 if (root/name).exists():copy(name)
for name in ('scripts/t18','scripts/poc030/interop-generator','scripts/poc030-rc2/dev-tools'):tree(name,{'.cs','.csproj','.ps1','.py','.json'})
for name in ('scripts/poc030-rc2/prepare-interop.ps1','scripts/poc030/generate-interop.ps1','scripts/poc030/preflight-runtime.ps1','scripts/poc030/build-poc030.ps1'):copy(name)
for suffix in ('.dll','.deps.json','.runtimeconfig.json'):copy('scripts/poc030/interop-generator/bin/Release/net6.0/interop-generator'+suffix)
for name in ('DynamicFrontline.Steam.Tests','DynamicFrontline.Rc2.Tests','DynamicFrontline.Poc030.Tests','DynamicFrontline.Prototype023.Tests','DynamicFrontline.InteropRepair.Tests'):tree('tests/'+name,{'.cs','.csproj','.json'})
tree('scripts/poc030/interop-jit-probe',{'.cs','.csproj'})
for suffix in ('.dll','.deps.json','.runtimeconfig.json'):copy('scripts/poc030/interop-jit-probe/bin/Release/net6.0/interop-jit-probe'+suffix)
commit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip()
(stage/'BUILD.json').write_text(json.dumps({'Version':'0.4.0-T18-full-debug6','Protocol':'df18-world-4','Commit':commit,'RuntimeValidated':False,'NativeEffects':False,'Transport':'SteamNetworkingMessages + ephemeral ECDH/HMAC','Map':'Airbase/5454584','FirstRun':'LoadReadProbe','Interop':'Generate locally; not distributed'},indent=2),encoding='utf-8')
(stage/'START_HERE.txt').write_text('Read docs/t18/INSTALL_GUIDE_LAUNCHER.md. Candidate: real Steam/game test not run.\nSame archive for both Steam friends. No manual IP, role, certificate or session key.\nNo automatic installation or launch. No native gameplay effects.\n',encoding='utf-8')
entries=[{'Path':str(p.relative_to(stage)).replace('\\','/'),'SHA256':hashlib.sha256(p.read_bytes()).hexdigest().upper()} for p in sorted(stage.rglob('*')) if p.is_file()]
(stage/'SHA256_INVENTORY.json').write_text(json.dumps(entries,indent=2),encoding='utf-8')
zip_path=out/(stage.name+'.zip')
with zipfile.ZipFile(zip_path,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for p in sorted(stage.rglob('*')):
  if p.is_file():z.write(p,p.relative_to(stage))
hash_value=hashlib.sha256(zip_path.read_bytes()).hexdigest().upper()
(out/'SHA256.txt').write_text(hash_value+'  '+zip_path.name+'\n',encoding='utf-8')
print(json.dumps({'Zip':str(zip_path),'SHA256':hash_value,'Files':len(entries),'RuntimeValidated':False}))
