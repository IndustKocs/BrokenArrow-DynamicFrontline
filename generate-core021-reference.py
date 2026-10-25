"""Independent algebraic reference, Python double. NOT comparison with game runtime."""
from pathlib import Path
import json,random,math
r=random.Random(21021);rows=[]
for i in range(1000):
    d,p,a,e,k=r.randint(0,30),r.randint(1,500),r.randint(1,500),r.choice([.5,1,2]),r.choice([1,2,3])
    rows.append(dict(formula='F10',inputs=[d,p,a,e,k],expected=d*p**k/(p**k+e*a**k)))
    resist,mod,n=r.choice([0,.25,.5,.75,1]),r.choice([0,.5,1,2]),r.randrange(6)
    rows.append(dict(formula='F11',inputs=[resist,mod,n],expected=1 if n==0 else ((1-resist)*mod)**n))
    factors=[r.choice([.25,.5,1,2]) for _ in range(6)]
    rows.append(dict(formula='F12',inputs=factors,expected=math.prod(factors)))
    hp,maxhp,initial,slots=r.randrange(-1,101),80,8,r.randrange(1,12)
    rows.append(dict(formula='CF12',inputs=[hp,maxhp,initial,slots],expected=max(0,min(slots,math.ceil(hp/(maxhp/initial))))))
    distance,radius=r.randrange(101),r.randrange(101)
    rows.append(dict(formula='CF03',inputs=[distance,radius],expected=0 if distance>=radius else max(0,min(1,1-distance/radius))))
target=Path(__file__).resolve().parent/'config/core021/reference-values.json'
target.write_text(json.dumps(rows,separators=(',',':'))+'\n',encoding='utf-8')
print(len(rows),'independent reference cases')
