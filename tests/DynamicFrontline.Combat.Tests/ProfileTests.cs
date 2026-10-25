using System.Text.Json;
using DynamicFrontline.Core.Diagnostics;
static partial class Tests
{
    static partial void Profiles()
    {
        Test("Research profile provenance preserved",()=>{var c=new ResearchProfileCatalog(File.ReadAllText("config/core021/research-profiles.json"));var p=c.Get("buff-52469");Check(!p.RuntimeConfirmed&&p.AssetId=="52469"&&p.SourceTableSha256.Length==64);Eq(p.Values["InfantryDmgModFloor"],.72);p.Values["InfantryDmgModFloor"]=99;Eq(c.Get("buff-52469").Values["InfantryDmgModFloor"],.72);Eq(c.Get("buff-52552").Values["StressDamageModThreshold"],36);});
        Test("Research rejects hash unknown and mislabeled runtime",()=>{string json=File.ReadAllText("config/core021/research-profiles.json");Reject(()=>new ResearchProfileCatalog(json,new string('0',64)));Reject(()=>new ResearchProfileCatalog(json.Replace("\"RuntimeConfirmed\": false","\"RuntimeConfirmed\": true")));Reject(()=>new ResearchProfileCatalog(json).Get("active-match"));});
        Test("Explorer compares profiles no implicit global config",()=>{var c=new ResearchProfileCatalog(File.ReadAllText("config/core021/research-profiles.json"));var cases=JsonSerializer.Deserialize<ExplorerRequest[]>(File.ReadAllText("config/core021/explorer-examples.json"))!;var a=FormulaExplorer.Evaluate(cases[0],c);var b=FormulaExplorer.Evaluate(cases[1],c);Eq((float)a.Calculation,.88);Eq((float)b.Calculation,.52);Check(a.Evidence=="A"&&a.Source.EndsWith("FORMULAS.md"));foreach(var r in cases)Check(FormulaExplorer.Evaluate(r,c).Calculation!=null);});
        Test("Explorer rejects incompatible profile and missing input",()=>{var c=new ResearchProfileCatalog(File.ReadAllText("config/core021/research-profiles.json"));Reject(()=>FormulaExplorer.Evaluate(new("F10","buff-52469",new()),c));Reject(()=>FormulaExplorer.Evaluate(new("F10",null,new()),c));});
    }
}
