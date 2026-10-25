using System.Text.Json;
using DynamicFrontline.Frontline;

static class Diagnostic
{
    public static void WriteDemo()
    {
        var f=new Fixture(Fixture.DefaultRules with{CaptureThreshold=24000,LinkDistance=45});
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)if(x<10||x>22||y>=28)f.Grid.Seed(y*32+x,y>=28?(x<16?0:1):(x<10?0:1));
        var units=new[]{f.Unit(1,1,95,105),f.Unit(2,1,105,105),f.Unit(3,4,135,105),f.Unit(4,3,175,105),f.Unit(5,1,205,105),
            f.Unit(6,1,215,115),f.Unit(7,1,155,235),f.Unit(8,1,225,115,1),f.Unit(9,4,235,55,1),
            f.Unit(10,4,15,25),f.Unit(11,4,45,45),f.Unit(12,4,65,75),f.Unit(13,1,155,285),f.Unit(14,1,165,285,1)};
        f.Routes.Routes[units[6].Key]=RouteAccess.Blocked;
        var frames=new List<object>();for(int i=0;i<18;i++)
        {
            if(i==6)units[3]=units[3] with{Alive=false};if(i==12)units[3]=units[3] with{Alive=true};
            if(i==9)f.Routes.Routes[units[6].Key]=RouteAccess.Available;
            units[4]=units[4] with{Position=new(205,105+i)};var frame=f.Step(units);
            frames.Add(new{frame,grid=f.Sim.Snapshot()});
        }
        string data=JsonSerializer.Serialize(frames);File.WriteAllText("artifacts/diagnostic-frames.json",data);
        string html="""
<!doctype html><html lang="ru"><meta charset="utf-8"><title>Dynamic Frontline • автономная диагностика</title>
<style>body{margin:0;background:#101a27;color:#e7edf5;font:15px system-ui}main{max-width:1200px;margin:30px auto;padding:0 24px}h1{font-size:26px;margin-bottom:8px}.muted{color:#a9bacb}section{display:grid;grid-template-columns:minmax(400px,760px) 1fr;gap:24px;margin-top:20px}canvas{width:100%;background:#1c2939;border:1px solid #425065}aside{background:#192536;padding:20px;border-radius:10px}label{display:block;margin:12px 0}input{accent-color:#62bdf2}input[type=range]{width:100%}.legend{line-height:1.9}pre{white-space:pre-wrap;font:13px system-ui}.tag{font:12px monospace;color:#ffd29a}@media(max-width:800px){section{grid-template-columns:1fr}}</style>
<main><span class="tag">OFFLINE / AUTHORITATIVE DEBUG / SYNTHETIC DATA</span><h1>Динамический фронт и оперативная связь</h1>
<div class="muted">Реальные результаты C#-ядра. Это диагностический обзор обеих команд, не разрешённое игроку сетевое представление.</div>
<section><div><canvas id="map" width="800" height="800"></canvas><label>Тик <b id="tick"></b><input id="time" type="range" min="0" max="17" value="0"></label>
<div class="muted">6: потеря спецназа-посредника · 9: открыт путь отхода из котла · 12: восстановление посредника</div></div><aside>
<b>Слои</b><label><input id="territory" type="checkbox" checked> Territorial Control</label><label><input id="front" type="checkbox" checked> Линия фронта и границы</label>
<label><input id="local" type="checkbox" checked> Локальные области пехоты</label><label><input id="graph" type="checkbox" checked> Operational Connectivity</label>
<label><input id="units" type="checkbox" checked> Подразделения и изоляция</label><hr><div class="legend">🟦 Команда 0 · 🟥 Команда 1<br>Серый — нейтральная территория<br>Жёлтый — оспариваемая ячейка<br>Зелёные связи — есть путь к корню<br>Пунктир — компонент без корня<br>Пунктирный круг — локальная область<br>Квадрат — линейная пехота<br>Круг — разведка / спецназ<br>Оранжевый ободок — Encircled</div><hr><pre id="stats"></pre><div class="muted">Снабжение не выводится из оперативной связи. Локальные области не входят в территориальные очки.</div></aside></section></main>
<script>const frames=__DATA__;const canvas=document.getElementById('map'),ctx=canvas.getContext('2d');const on=id=>document.getElementById(id).checked;
function draw(){const n=+document.getElementById('time').value,{frame:f,grid:g}=frames[n],s=800/(g.Width*g.CellSize);ctx.clearRect(0,0,800,800);document.getElementById('tick').textContent=f.Header.Tick;
if(on('territory'))for(const c of g.Cells){ctx.fillStyle=!c.Available?'#080e18':c.Contested?'#b99b39':c.Owner===0?'#234e70':c.Owner===1?'#713b4a':'#27313d';ctx.fillRect(c.Index%g.Width*g.CellSize*s,Math.floor(c.Index/g.Width)*g.CellSize*s,g.CellSize*s-.4,g.CellSize*s-.4)}
if(on('front'))for(const p of f.Frontline.Paths){ctx.strokeStyle=p.Neighbor>=0?'#ffe1a1':'#8c9cad';ctx.lineWidth=p.Neighbor>=0?3:1;ctx.beginPath();p.Points.forEach((v,i)=>i?ctx.lineTo(v.X*s,v.Y*s):ctx.moveTo(v.X*s,v.Y*s));ctx.stroke()}
if(on('local'))for(const a of f.LocalAreas){ctx.strokeStyle=a.Team===0?'#69c9ff':'#ff91a6';ctx.fillStyle=a.Team===0?'#69c9ff22':'#ff91a622';ctx.setLineDash([5,4]);ctx.beginPath();ctx.arc(a.Center.X*s,a.Center.Y*s,a.Radius*s,0,Math.PI*2);ctx.fill();ctx.stroke();ctx.setLineDash([])}
const key=k=>k.Uid+':'+k.Generation,byKey=new Map(f.Units.map(u=>[key(u.Key),u])),nodes=new Map(f.Operational.Nodes.map(u=>[key(u.Unit),u]));
if(on('graph'))for(const e of f.Operational.Edges){const a=byKey.get(key(e.A)),b=byKey.get(key(e.B)),connected=nodes.get(key(e.A)).Connected;ctx.strokeStyle=connected?'#8df2ba':'#a9b3c5';ctx.lineWidth=2;ctx.setLineDash(connected?[]:[4,5]);ctx.beginPath();ctx.moveTo(a.Position.X*s,a.Position.Y*s);ctx.lineTo(b.Position.X*s,b.Position.Y*s);ctx.stroke();ctx.setLineDash([])}
if(on('units'))for(const u of f.Units){if(!u.Alive)continue;const x=u.Position.X*s,y=u.Position.Y*s,state=f.Isolation.Units.find(v=>key(v.Unit)===key(u.Key));ctx.fillStyle=u.Team===0?'#a4ddff':'#ffb8c6';ctx.beginPath();if(u.UnitId<=2)ctx.rect(x-5,y-5,10,10);else ctx.arc(x,y,5,0,7);ctx.fill();if(state?.State===2){ctx.strokeStyle='#ffab55';ctx.lineWidth=3;ctx.beginPath();ctx.arc(x,y,10,0,7);ctx.stroke()}ctx.fillStyle='#fff';ctx.font='12px system-ui';ctx.fillText(u.Key.Uid+' '+(['','line','reserve','SF','recon'][u.UnitId]||''),x+9,y-7)}
document.getElementById('stats').textContent='Изменённых ячеек: '+f.Delta.Cells.length+'\nЛокальных областей: '+f.LocalAreas.length+'\nСвязей графа: '+f.Operational.Edges.length+'\n\n'+f.Isolation.Units.map(u=>'UID '+u.Unit.Uid+': '+['Connected','Autonomous','Encircled','Reconnecting'][u.State]+' | T '+Number(u.TerritorialConnectivity)+' O '+Number(u.OperationalConnectivity)+' L '+Number(u.LogisticsConnectivity)).join('\n')}
document.querySelectorAll('input').forEach(e=>e.addEventListener('input',draw));draw();</script></html>
""";
        File.WriteAllText("artifacts/frontline-diagnostic.html",html.Replace("__DATA__",data));
    }
}
