using System.Text;
using System.Text.Json;
using DynamicFrontline.Frontline.Rules023;
public static class Preview
{
    public static void Write(string output,Scenario scenario,List<DisplayFrame> frames,List<Public023> publics)
    {
        var html="""
<!doctype html><meta charset="utf-8"><title>Dynamic Frontline 0.2.3 Х Offline</title>
<style>body{background:#101822;color:#e0e8ef;font:16px system-ui;margin:24px;max-width:1200px}h1{font-size:24px}main{display:flex;gap:24px}svg{background:#182632;width:65%;border:1px solid #506070}pre{white-space:pre-wrap}input[type=range]{width:70%}label{display:block;margin:12px 0}.note{color:#b5c6d7}aside{flex:1}</style>
<h1>Dynamic Frontline 0.2.3 Ч автономна€ диагностика</h1><p class="note">Synthetic / Experimental. ќтладочный файл содержит полный состав. —лой ZOC выключен по умолчанию. ƒанные кадров рассчитаны C# €дром; браузер только отображает их.</p>
<input id="time" type="range" min="0" value="0"><span id="stamp"></span><label><input id="circles" type="checkbox"> ѕоказать ZOC (только отладка)</label>
<main><svg id="map"></svg><aside><pre id="info"></pre></aside></main>
<script>
const frames=__FRAMES__,size=__SIZE__,cell=__CELL__;const slider=document.getElementById('time');slider.max=frames.length-1;
const map=document.getElementById('map');map.setAttribute('viewBox',`0 0 ${size*cell} ${size*cell}`);
function draw(){const f=frames[+slider.value];document.getElementById('stamp').textContent=f.Time+' с';let shapes='';
f.Owners.forEach((o,i)=>{if(o>=0)shapes+=`<rect x="${i%size*cell}" y="${Math.floor(i/size)*cell}" width="${cell}" height="${cell}" fill="${o?'#8b3d47':'#245d8c'}"/>`});
f.Units.forEach(u=>{if(document.getElementById('circles').checked&&u.Power>0)shapes+=`<circle cx="${u.X}" cy="${u.Y}" r="${u.Radius}" fill="none" stroke="${u.Team?'#ed7b7b':'#7bc3ed'}" stroke-width="6" opacity=".4"/>`;shapes+=`<circle cx="${u.X}" cy="${u.Y}" r="25" fill="${u.Team?'#ed7b7b':'#7bc3ed'}"/>`});map.innerHTML=shapes;
document.getElementById('info').textContent=JSON.stringify({time:f.Time,own:f.Own,fractions:f.Fractions,phaseIntegrals:f.U,native:f.Native,geometry:f.Geometry,notices:f.Notices,end:f.End},null,2)}slider.oninput=draw;document.getElementById('circles').onchange=draw;draw();</script>
""";
        html=html.Replace("__FRAMES__",JsonSerializer.Serialize(frames)).Replace("__SIZE__",scenario.Size.ToString()).Replace("__CELL__",scenario.CellSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(Path.Combine(output,"preview-debug.html"),html);
        // Separate export: no hidden IDs/positions/certificates are embedded in this document.
        File.WriteAllText(Path.Combine(output,"preview-player.html"),"<!doctype html><meta charset='utf-8'><title>Own state Х Offline</title><h1>—осто€ние своих подразделений</h1><p>Synthetic. »грова€ прив€зка отсутствует.</p><pre>"+System.Net.WebUtility.HtmlEncode(JsonSerializer.Serialize(publics,Formats.Json))+"</pre>");
        foreach(var f in frames.Where(f=>f.Time is .25 or 10 or 60 or 70 or 90 or 110 or 115 or 120 or 140 or 420 or 900 or 1800 or 2700))
        {
            var svg=new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {scenario.Size*scenario.CellSize} {scenario.Size*scenario.CellSize+200}'><rect width='100%' height='100%' fill='#182632'/>");
            for(int i=0;i<f.Owners.Length;i++)if(f.Owners[i]>=0)svg.Append($"<rect x='{i%scenario.Size*scenario.CellSize}' y='{i/scenario.Size*scenario.CellSize}' width='{scenario.CellSize}' height='{scenario.CellSize}' fill='{(f.Owners[i]==0?"#245d8c":"#8b3d47")}'/>");
            foreach(var u in f.Units)svg.Append($"<circle cx='{u.X}' cy='{u.Y}' r='25' fill='{(u.Team==0?"#7bc3ed":"#ed7b7b")}'/>");
            var own=f.Own.FirstOrDefault();svg.Append($"<text x='30' y='{scenario.Size*scenario.CellSize+90}' fill='white' font-size='55'>t={f.Time} / {own?.State} / {own?.Relief} / tau={own?.Tau:F2}</text></svg>");
            File.WriteAllText(Path.Combine(output,$"frame-{f.Time:0000.00}.svg"),svg.ToString());
        }
    }
}
