using AsmResolver.DotNet;
using System.Security.Cryptography;
using System.Text.Json;
static class BindingInspector
{
    public static void Run(string directory,string output)
    {
        var requirements=new Dictionary<string,string[]>{
            ["BrokenArrow.Client.Ecs.Controllers.GameController"]=["get_Instance","get_IsInstanceAlive","get_GameContext","get_GameSession","get_GameTime","get_LoadStatus","get_CurrentMapName"],
            ["BrokenArrow.Client.Ecs.Configs.GameSessionContext"]=["get_CurrentPlayer","get_IsPaused","get_PlayersCount"],
            ["DefaultEcs.World"]=["GetEnumerator"],
            ["GalaxyCoreLib.NetEntity.ClientNetEntity"]=["get_NetID"],
            ["DefaultEcs.Entity"]=["get_IsAliveVersion","Has","Get","Version","WorldId","EntityId"],
            ["DefaultEcs.World+Enumerator"]=["MoveNext","get_Current"],
            ["BrokenArrow.Client.Ecs.Utils.PlayerInfo"]=["get_UID","get_TeamUid"],
            ["BrokenArrow.Shared.Ecs.Components.UnitComponent"]=["Owner","UnitData"],
            ["BrokenArrow.Shared.Ecs.Components.TransformComponent"]=["get_Position"],
            ["BrokenArrow.Client.Ecs.BattleSystem.Components.HealthComponent"]=["get_CurrentHealth","MaxHealth"],
            ["BrokenArrow.Client.Ecs.BattleSystem.Components.StressComponent"]=["CurrentStressValue","MaxStress"],
            ["BrokenArrow.Client.Ecs.Infantry.Components.InfantryUnitComponent"]=["AliveSoldierCount"],
            ["BrokenArrow.Client.Ecs.GNetwork.Components.NetworkEntityComponent"]=["get_NetEntity"],
            ["BrokenArrow.DataBase.Models.Units"]=["get_Id","get_Role","get_MaxStress"]
        };
        var modules=new[]{"BrokenArrow.dll","DefaultEcs.dll","GalaxyCoreLib.dll"}.Select(n=>ModuleDefinition.FromFile(Path.Combine(directory,n))).ToArray();
        var types=modules.SelectMany(m=>m.GetAllTypes()).ToArray();var results=new List<object>();bool pass=true;
        foreach(var (name,members) in requirements)
        {
            var type=types.SingleOrDefault(t=>t.FullName==name||t.FullName=="Il2Cpp"+name);
            var actual=type==null?Array.Empty<string>():type.Methods.Select(m=>m.Name!.ToString()).Concat(type.Fields.Select(f=>f.Name!.ToString())).Concat(type.Properties.Select(p=>p.Name!.ToString())).ToArray();
            var missing=members.Where(n=>!actual.Contains(n)).ToArray();pass&=type!=null&&missing.Length==0;
            results.Add(new {Type=name,ActualType=type?.FullName,Missing=missing,Methods=type?.Methods.Where(m=>members.Contains(m.Name!.ToString())).Select(m=>m.FullName).ToArray()});
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new{Scope="GeneratedAssemblyMetadata_NotRuntimeInvocation",Passed=pass,Results=results},new JsonSerializerOptions{WriteIndented=true}));
        if(!pass)throw new InvalidDataException("Generated binding members missing; inspect report");
        Console.WriteLine("All "+requirements.Count+" generated type contracts found (not runtime invocation)");
    }
}
