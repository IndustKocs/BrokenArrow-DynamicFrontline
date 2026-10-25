"""Copy selected research examples and their provenance, never active game settings."""
from pathlib import Path
import json,hashlib
root=Path(__file__).resolve().parent
research=Path(r'C:\Users\pl-ni\Documents\ChatGPT\ба\research')
def load(relative):return json.loads((research/relative).read_text(encoding='utf-8-sig'))
def hashfile(relative):return hashlib.sha256((research/relative).read_bytes()).hexdigest()
profiles=[]
def add(id,source,asset,profile,formulas,values,asset_hash=None):
    profiles.append(dict(Id=id,Source=str(research/source),AssetId=asset,Profile=profile,ResearchVersion='Combat Systems Audit Pass01-03',SourceTableSha256=hashfile(source),AssetSha256=asset_hash,RuntimeConfirmed=False,FormulaIds=formulas,Values=values))
source='full_code_audit/evidence/buff_values.json'
for row in load(source):
    if row['pathID'] not in (52469,52552):continue
    vals=dict(row['profiles']['Calm']);vals.update(row['parameters'])
    add('buff-'+str(row['pathID']),source,str(row['pathID']),'Extracted BuffConfig / Calm (active profile unknown)',['F01','F02','F03','F05','CF01'],vals,row['sha256'])
source='combat_systems_audit/evidence/ammunition_examples.json'
row=next(x for x in load(source) if x['Id']==4)
values={k:v for k,v in row.items() if k in ('Damage','StressDamage','HealthAOERadius','StressAOERadius')}
add('ammo-4',source,'Ammunitions.Id=4','Extracted table; active DB unknown',['CF03'],values)
source='combat_systems_audit/evidence/profiles_decoded.json'
row=next(x for x in load(source) if x['pathID']==52558)
terrain=next(v for v in row.values() if isinstance(v,list) and v and isinstance(v[0],dict) and 'opacity' in v[0])
forest=next(x for x in terrain if x['type']==2)
add('fow-52558-forest',source,'52558 / terrain type 2','Extracted FOW; active profile unknown',['CF14','CF16','P3-F05'],dict(ForestOpacity=forest['opacity'],ForestMultiplier=forest['multiplier'],TerrainCover=forest['cover'],TerrainHeight=forest['height'],FlashReduction=row['FlashReduction']),hashfile('combat_systems_audit/evidence/profile_assets/52558.bin'))
target=root/'config/core021';target.mkdir(parents=True,exist_ok=True)
(target/'research-profiles.json').write_text(json.dumps(profiles,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print([(p['Id'],p['Values']) for p in profiles])
