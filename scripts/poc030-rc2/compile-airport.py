"""Compile a conservative connectivity-preserving quotient of the real Airport nav graph.
Requires locally decoded scenario JSON. Does not execute game code or modify game files.
"""
import argparse,hashlib,json,math,pathlib,struct,collections
from importlib.machinery import SourceFileLoader
wire=SourceFileLoader('airport_wire',str(pathlib.Path(__file__).with_name('inspect-airport.py'))).load_module()

def compile_map(game,scenario,compatibility,output,cell):
    base=pathlib.Path(game)/'BrokenArrow_Data/StreamingAssets'
    graph=(base/'Navigation/Airport.ng').read_bytes()
    archive=(base/'SharedMissions/Airport_network/Airport_network.bascr').read_bytes()
    if hashlib.sha256(graph).hexdigest()!='3c61de14f71f3b8ce8ae861cbc1208ad5d71949e504e45d444b1b5d12f10a68d':raise ValueError('Airport graph changed')
    if hashlib.sha256(archive).hexdigest()!='5aa1e3d132bbb55880686129c07446fdadb4929c67b6183fd78a9628a55331f1':raise ValueError('Airport scenario changed')
    head=json.loads((pathlib.Path(scenario)/'head.bam.json').read_text(encoding='utf-8-sig'));objects=json.loads((pathlib.Path(scenario)/'objects.bam.json').read_text(encoding='utf-8-sig'))
    if head['name']!='Airport_network' or head['map']!='Airport':raise ValueError('Scenario')
    zones={o['_uid']:o for o in objects if o['_type']==7};z=zones[55]
    if z['rotate']!={'X':0.0,'Y':0.0,'Z':0.0,'W':1.0}:raise ValueError('Rotated zone unsupported')
    ox=z['pos']['X']-z['scale']['X']/2;oy=z['pos']['Z']-z['scale']['Z']/2
    width=round(z['scale']['X']/cell);height=round(z['scale']['Z']/cell);count=width*height
    def index(x,y):
        a=math.floor((x-ox)/cell);b=math.floor((y-oy)/cell)
        return b*width+a if 0<=a<width and 0<=b<height else -1
    nodes={};directed=set()
    for tag,_,data in wire.fields(graph):
        if tag!=7:continue
        f=list(wire.fields(data));v={t:d for t,_,d in f if t!=3};key=(v.get(1,0),v.get(2,0));x=struct.unpack('<f',v.get(4,b'\0'*4))[0];y=struct.unpack('<f',v.get(5,b'\0'*4))[0]
        nodes[key]=index(x,y)
        for t,_,b in f:
            if t!=3:continue
            link={n:d for n,_,d in wire.fields(b)};mask=link.get(3,0)
            # Native NavMeshAreas: Walkable=1, Road=8, Forest=16; reject water, jumps and buildings.
            if mask&(1|8|16) and not mask&~(1|8|16):directed.add((key,(link.get(1,0),link.get(2,0))))
    links=[(a,b) for a,b in directed if a<b and (b,a) in directed and a in nodes and b in nodes]
    parent={n:n for pair in links for n in pair if nodes[n]>=0}
    def find(n):
        while parent[n]!=n:parent[n]=parent[parent[n]];n=parent[n]
        return n
    for a,b in links:
        if nodes[a]>=0 and nodes[a]==nodes[b]:parent[find(b)]=find(a)
    components=collections.defaultdict(set)
    for n in parent:components[nodes[n]].add(find(n))
    allowed=[len(components[i])==1 for i in range(count)]
    edges=set()
    for a,b in links:
        ca,cb=nodes[a],nodes[b]
        if ca>=0 and cb>=0 and ca!=cb and allowed[ca] and allowed[cb] and abs(ca%width-cb%width)+abs(ca//width-cb//width)==1:edges.add(min(ca,cb)*count+max(ca,cb))
    roots=[]
    for uid,team in [(11,0),(12,1)]:
        o=next(o for o in objects if o['_uid']==uid);position={'X':o['pos']['X'],'Y':o['pos']['Z']}
        if not allowed[index(position['X'],position['Y'])]:raise ValueError('Root not in proven passable cell')
        roots.append({'Team':team,'Position':position})
    descriptor=json.loads(pathlib.Path(compatibility).read_text(encoding='utf-8-sig'));descriptor['Files']=dict(sorted(descriptor['Files'].items()))
    fingerprint=hashlib.sha256(json.dumps(descriptor,separators=(',',':')).encode()).hexdigest().upper()
    result={'Map':'Airport','GameFingerprint':fingerprint,'Evidence':'UA01 Airport_network zone55; native mutual land links, connected cell components, orthogonal quotient; conservative raster; runtime active zone/nav confirmation required','SourceSha256':hashlib.sha256(graph+archive).hexdigest().upper(),'Revision':1,'Width':width,'Height':height,'CellSize':cell,'Origin':{'X':ox,'Y':oy},'Scoring':allowed,'Infantry':allowed,'Ground':allowed,'Roots':roots,'Portals':[],'GeometryValidated':True,'InfantryEdges':sorted(edges),'GroundEdges':sorted(edges),'Scenario':'Airport_network','ActiveZoneId':55}
    out=pathlib.Path(output);out.write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
    report={'NativeNodes':len(nodes),'MutualLandLinks':len(links),'PhysicalZone':[z['scale']['X'],z['scale']['Z']],'CellSize':cell,'Cells':count,'SupportedCells':sum(allowed),'AmbiguousCellsExcluded':sum(len(c)>1 for c in components.values()),'Edges':len(edges),'ConnectivityProof':'Every retained cell is internally connected in the native mutual graph; every retained edge has a native witness','RuntimeValidated':False,'ActiveZoneRuntimeValidated':False,'DynamicNavigationValidated':False,'MapFileSha256':hashlib.sha256(out.read_bytes()).hexdigest()}
    out.with_suffix('.evidence.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('game');p.add_argument('scenario');p.add_argument('compatibility');p.add_argument('output');p.add_argument('--cell',type=int,choices=[50,100],default=50);a=p.parse_args();compile_map(a.game,a.scenario,a.compatibility,a.output,a.cell)
