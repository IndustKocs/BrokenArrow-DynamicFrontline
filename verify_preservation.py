"""Read-only verification against the existing research baseline. Writes only artifacts/."""
from pathlib import Path
import csv, hashlib, json
from datetime import datetime, timezone

root=Path(__file__).resolve().parent
baseline=root.parent/'research/evidence'
game=Path(r'D:\steam\steamapps\common\broken_arrow')
with (baseline/'inventory.csv').open(encoding='utf-8-sig') as f:
    old={x['path']:(int(x['bytes']),int(x['mtime_ns'])) for x in csv.DictReader(f)}
now={}
for path in game.rglob('*'):
    if path.is_file():
        stat=path.stat(); now[str(path.relative_to(game))]=(stat.st_size,stat.st_mtime_ns)
expected=json.loads((baseline/'sha256_before.json').read_text())
actual={}
for name in expected:
    with (game/name).open('rb') as source: actual[name]=hashlib.file_digest(source,'sha256').hexdigest()
result=dict(checkedAtUTC=datetime.now(timezone.utc).isoformat(),criticalHashesEqual=expected==actual,
    newFiles=sorted(now.keys()-old.keys()),missingFiles=sorted(old.keys()-now.keys()),
    changedSizeOrMtime=sorted(p for p in now.keys()&old.keys() if now[p]!=old[p]),
    scope='All game files: path/size/mtime; SHA256 of six research baseline files only.')
(root/'artifacts').mkdir(exist_ok=True)
(root/'artifacts/game-preservation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
raise SystemExit(0 if result['criticalHashesEqual'] and not any(result[k] for k in ['newFiles','missingFiles','changedSizeOrMtime']) else 1)
