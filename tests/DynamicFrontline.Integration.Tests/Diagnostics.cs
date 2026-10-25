using System.Text.Json;
using DynamicFrontline.Frontline;
using DynamicFrontline.Integration;

static class Diagnostics
{
    public static void Build()
    {
        using var f=new Fixture();var all=new List<object>();var safeFrames=new List<object>();var projections=new TeamSafeProjection();
        var units=new[]{f.Unit(x:25),f.Unit(2,3,x:55),f.Unit(3,2,x:85),f.Unit(4,x:115),f.Unit(5,team:1,x:295),f.Unit(6,3,1,265),f.Unit(7,2,1,235),f.Unit(8,team:1,x:205)};
        var developer=new FrontlineDiagnosticsService(new(true,true,true));
        for(int i=0;i<18;i++)
        {
            if(i==6)units[2]=units[2] with{Alive=false,Hp=0};if(i==12)units[2]=units[2] with{Alive=true,Hp=100,LifecycleStamp="respawn-3"};
            if(!f.Step(units))throw new Exception("Diagnostic fixture failed");var frame=f.Bridge.LastGood!;
            all.Add(new{frame=frame.Result,grid=frame.Territory,integration=new{Source=frame.Input.Context.Origin.ToString(),frame.Input.Context.MatchId,
                RulesVersion=frame.Result.Header.RulesVersion,ProfileVersion=f.Profiles.Document.Version,Processed=frame.Input.Units.Length,frame.Input.UnknownProfiles,
                frame.PendingRoutes,frame.CachedRoutes,frame.Health.RejectedSnapshots,frame.SnapshotMilliseconds,frame.CoreMilliseconds,
                Exclusions=frame.Input.Units.Select(u=>new{u.Key.Uid,Reason=u.CaptureReason.ToString()})}});
            File.WriteAllText($"artifacts/poc02/frame-{i:D2}.json",developer.ExportDeveloper(frame));
            var safe=projections.Project(frame.Input,0,i);File.WriteAllText($"artifacts/poc02/team-safe-{i:D2}.json",new FrontlineDiagnosticsService(new()).ExportTeam(safe,0,f.Match));
            safeFrames.Add(new{frame=new{Header=new{Tick=i},Delta=new{Cells=Array.Empty<object>()},Frontline=new{Paths=Array.Empty<object>()},LocalAreas=Array.Empty<object>(),
                Operational=new{Nodes=Array.Empty<object>(),Edges=Array.Empty<object>()},Isolation=new{Units=Array.Empty<object>()},
                Units=safe.FriendlyUnits.Select(u=>new{Key=new{Uid=u.LocalHandle,Generation=0},UnitId=u.DefinitionId,Team=0,u.Position,u.Alive})},
                grid=new{Width=32,Height=32,CellSize=10,Cells=Array.Empty<object>()},integration=new{Source="Team Safe Diagnostic",PublicTick=i}});
        }
        // Reuse the actual PoC01 renderer and controls; replace only data and add integration metadata panel.
        string template=File.ReadAllText("artifacts/poc02/legacy/artifacts/frontline-diagnostic.html");
        string Build(object frames,bool teamSafe)
        {
            int start=template.IndexOf("const frames=",StringComparison.Ordinal)+"const frames=".Length;
            int end=template.IndexOf(";const canvas",start,StringComparison.Ordinal);
            string html=template[..start]+JsonSerializer.Serialize(frames)+template[end..];
            html=html.Replace("Динамический фронт и оперативная связь","Dynamic Frontline · PoC 0.2");
            html=html.Replace("OFFLINE / AUTHORITATIVE DEBUG / SYNTHETIC DATA",teamSafe?"TEAM SAFE DIAGNOSTIC / OWN DATA ONLY":"DEVELOPER DEBUG / SYNTHETIC ADAPTER DATA");
            html=html.Replace("6: потеря спецназа-посредника · 9: открыт путь отхода из котла · 12: восстановление посредника","6: потеря посредника · 12: новая сущность с новой generation · обе команды симметричны");
            html=html.Replace("<pre id=\"stats\"></pre>","<pre id=\"metadata\"></pre><pre id=\"stats\"></pre>");
            string extension=teamSafe?
                "const baseDraw=draw;draw=function(){baseDraw();document.getElementById('metadata').textContent='Team Safe Diagnostic: только свои подразделения. Территория, граф, изоляция и внутренние счётчики скрыты.';document.getElementById('stats').textContent='';};":
                "const baseDraw=draw;draw=function(){baseDraw();const m=frames[+document.getElementById('time').value].integration;document.getElementById('metadata').textContent=JSON.stringify(m,null,2);};";
            // Existing listeners captured the old function; replace their registration with a delegating closure.
            html=html.Replace("e.addEventListener('input',draw)","e.addEventListener('input',()=>draw())");
            return html.Replace("</script>",extension+"draw();</script>");
        }
        File.WriteAllText("artifacts/poc02/frontline-diagnostic-poc02.html",Build(all,false));
        File.WriteAllText("artifacts/poc02/team-safe-diagnostic.html",Build(safeFrames,true));
        File.WriteAllText("artifacts/poc02/diagnostic-frames.json",JsonSerializer.Serialize(all));
    }
}
