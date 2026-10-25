"""Independent Python reference based on Pass04 formula cards; never executes native/C#."""
from pathlib import Path
import math,random,json,struct
r=random.Random(22004);rows=[]
f32=lambda x:struct.unpack('<f',struct.pack('<f',x))[0]
for i in range(500):
    x,y,g=r.randint(-10000,10000),r.randint(-10000,10000),r.randrange(100)
    rows.append(dict(formula='astar',inputs=[x,y,g],expected=g+1.25*(f32(abs(x)+abs(y))-.5857864376269051*min(abs(x),abs(y)))))
    speed=r.randrange(400);t=max(0,min(1,(speed-(100 if speed<=200 else 200))/100));rate=10+10*t if speed<=200 else 20-5*t
    rows.append(dict(formula='aircraft',inputs=[speed],expected=rate))
    x=r.randrange(-9999,10000)/10;q=math.trunc(abs(x));y=1000-q
    rows.append(dict(formula='diamond',inputs=[x,y],expected=[x/1000,y/1000]))
    c=r.randint(-10,20);radius=r.randrange(11);a=abs(c);b=abs(c-10)
    if max(a,b)>10+radius:expected=None
    else:
        start=0 if a<radius else abs(c)-radius
        end=10 if b<radius else start+2*radius if not a<radius else 10-(abs(c-10)-radius)
        expected=[start,end]
    rows.append(dict(formula='smoke',inputs=[c,radius],expected=expected))
    u=r.choice([-.75,-.5,0,.25,.5]);v=r.choice([-.75,-.5,0,.25,.5]);rows.append(dict(formula='trajectory',inputs=[u,v],expected=[-u*20/1000,10*(1+v*40/1000)]))
    x=f32(r.uniform(-10,10));a=abs(x)
    if a<.4375:b,z=0,a
    elif a<.6875:b,z=.4636476090008061,(2*a-1)/(a+2)
    elif a<1.1875:b,z=.7853981633974483,(a-1)/(a+1)
    elif a<2.4375:b,z=.982793723247329,(a-1.5)/(1.5*a+1)
    else:b,z=1.5707963267948966,-1/a
    q=z*z;correction=z**3*((.004706779342861492*q+.19232454640210858)*q+.2965285988192392)/((.2993096999596597*q+1.1107249999539954)*q+.8895857968624323)
    rows.append(dict(formula='h1',inputs=[x],expected=f32(math.copysign(b+z-correction,x))))
path=Path(__file__).resolve().parent/'config/core022/reference.json';path.write_text(json.dumps(rows,separators=(',',':'))+'\n',encoding='utf-8');print(len(rows),'reference cases')
