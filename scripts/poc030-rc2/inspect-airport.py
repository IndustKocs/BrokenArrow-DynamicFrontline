"""Read-only UA01 protobuf inventory, not a validated gameplay map provider."""
import argparse, collections, hashlib, json, pathlib, struct, zipfile

def varint(data, pos):
    n=0
    for shift in range(0,70,7):
        b=data[pos];pos+=1;n|=(b&127)<<shift
        if b<128:return n,pos
    raise ValueError('Invalid varint')

def fields(data):
    pos=0
    while pos<len(data):
        tag,pos=varint(data,pos);number,wire=tag>>3,tag&7
        if wire==0:value,pos=varint(data,pos)
        elif wire==2:
            size,pos=varint(data,pos);value=data[pos:pos+size];pos+=size
            if len(value)!=size:raise ValueError('Truncated protobuf')
        elif wire in (1,5):
            size=8 if wire==1 else 4;value=data[pos:pos+size];pos+=size
            if len(value)!=size:raise ValueError('Truncated fixed value')
        else:raise ValueError(f'Unsupported wire {wire}')
        yield number,wire,value

def graph(path):
    data=path.read_bytes();header={};bounds=[float('inf'),float('inf'),float('-inf'),float('-inf')];count=0;links=0;masks=collections.Counter()
    for tag,wire,value in fields(data):
        if tag==7:
            node=list(fields(value));attrs={t:v for t,w,v in node if t!=3};x=struct.unpack('<f',attrs.get(4,b'\0'*4))[0];y=struct.unpack('<f',attrs.get(5,b'\0'*4))[0]
            bounds=[min(bounds[0],x),min(bounds[1],y),max(bounds[2],x),max(bounds[3],y)];count+=1
            for t,w,v in node:
                if t==3:
                    link={k:v for k,_,v in fields(v)};masks[str(link.get(3,0))]+=1;links+=1
        elif tag in (1,2,3,5,6):header[str(tag)]=value.decode() if wire==2 else value
    return {'file':path.name,'sha256':hashlib.sha256(data).hexdigest(),'protobufFields':header,'nodes':count,'links':links,'linkMasks':dict(masks),'nodeCoordinateExtents':bounds,'extentsArePlayableBounds':False}

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('game');p.add_argument('output');a=p.parse_args()
    root=pathlib.Path(a.game)/'BrokenArrow_Data/StreamingAssets';scenario=root/'SharedMissions/Airport_network';meta=json.loads((scenario/'meta.json').read_text(encoding='utf-8-sig'))
    with zipfile.ZipFile(scenario/'Airport_network.bascr') as archive:entries=[{'name':i.filename,'bytes':i.file_size} for i in archive.infolist()]
    result={'selectedMap':meta['sceneName'],'selectedScenario':meta['scenario'],'nativeEffects':False,'runtimeValidated':False,'geometryValidated':False,'graphs':[graph(root/'Navigation'/n) for n in ['Airport.ng','Airport_roads.ng']],'scenarioSha256':hashlib.sha256((scenario/'Airport_network.bascr').read_bytes()).hexdigest(),'scenarioEntries':entries,'blockers':['Playable polygon and team roots not decoded','Static graph is not dynamic navigation revision','Layer mask semantics need validation']}
    pathlib.Path(a.output).write_text(json.dumps(result,indent=2),encoding='utf-8')
