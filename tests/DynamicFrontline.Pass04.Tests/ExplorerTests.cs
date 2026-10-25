using System.Text.Json;
using DynamicFrontline.Core.Diagnostics;
using DynamicFrontline.Core.VerifiedCombat;
static partial class Tests
{
    static partial void Explorer()
    {
        Test("Explorer new metadata intermediates bounded CDF output",()=>{var c=new ResearchProfileCatalog(File.ReadAllText("config/core021/research-profiles.json"));var requests=JsonSerializer.Deserialize<ExplorerRequest[]>(File.ReadAllText("config/core022/explorer-examples.json"))!;foreach(var request in requests){var result=FormulaExplorer.Evaluate(request,c);Check(result.SourceMethod!=null&&result.SupportedDomain!=null&&result.EquivalenceStatus!=null);string json=JsonSerializer.Serialize(result,FormulaExplorer.JsonOptions);Check(json.Length<10000&&json.Contains("Explicit synthetic"));}var before=JsonSerializer.Serialize(requests);foreach(var r in requests)FormulaExplorer.Evaluate(r,c);Check(JsonSerializer.Serialize(requests)==before);});
        Test("Explorer full table opt-in and stageful error",()=>{var c=new ResearchProfileCatalog(File.ReadAllText("config/core021/research-profiles.json"));var r=JsonSerializer.Deserialize<ExplorerRequest[]>(File.ReadAllText("config/core022/explorer-examples.json"))![0];r.Parameters["IncludeTable"]=JsonSerializer.SerializeToElement(true);var result=FormulaExplorer.Evaluate(r,c);var element=JsonSerializer.SerializeToElement(result.Calculation);Check(element.GetProperty("Values").GetArrayLength()==1000);r.Parameters["Indices"]=JsonSerializer.SerializeToElement(new[]{1000});try{FormulaExplorer.Evaluate(r,c);throw new Exception("Expected error");}catch(FormulaStageException e){Check(e.Stage=="Polar.TableLookup");}});
        Test("Explorer Polar FastDimond and identity support",()=>{var c=new ResearchProfileCatalog(File.ReadAllText("config/core021/research-profiles.json"));var q=new float[100];q[1]=.25f;q[2]=.25f;var p=new Dictionary<string,JsonElement>{{"Queue",JsonSerializer.SerializeToElement(q)},{"SourceId",JsonSerializer.SerializeToElement("test")},{"SourceVersion",JsonSerializer.SerializeToElement("1")},{"CDFKeys",JsonSerializer.SerializeToElement(new[]{new CurveKey(0,0),new CurveKey(1,1)})}};Check(FormulaExplorer.Evaluate(new("P4-Polar",null,p),c).EquivalenceStatus!.Contains("Experimental"));Check(FormulaExplorer.Evaluate(new("P4-F03",null,p),c).Calculation!=null);});
    }
}
