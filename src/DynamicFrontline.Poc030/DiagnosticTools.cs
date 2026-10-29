using System.Text.Json;
namespace DynamicFrontline.Poc030;
public static class DiagnosticTools
{
    public static object Metrics(string path)
    {
        string[] fields=["ReadMilliseconds","ReadBytes","HudLastMilliseconds","HudLastBytes","WorkerBeforeDiagnosticMilliseconds","WorkerBeforeDiagnosticBytes","SourceAgeMilliseconds","IncomingQueue","OutgoingQueue","LastWireExchangeMilliseconds","UnknownGeometry"];
        var samples=fields.ToDictionary(f=>f,_=>new List<double>());
        foreach(var row in Read(path))foreach(var f in fields)if(row.TryGetProperty(f,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetDouble(out var n)&&double.IsFinite(n))samples[f].Add(n);
        return new{Scope="Observed log samples only. HUD is last callback sampled at reader cadence; wire exchange includes wait. Missing metrics stay null.",Metrics=samples.ToDictionary(p=>p.Key,p=>{var a=p.Value.OrderBy(v=>v).ToArray();return a.Length==0?null:(object)new{N=a.Length,P50=a[(a.Length-1)/2],P95=a[(int)((a.Length-1)*.95)],P99=a[(int)((a.Length-1)*.99)],Max=a[^1],Mean=a.Average()};})};
    }
    static IEnumerable<JsonElement> Read(string path)
    {
        if(new FileInfo(path).Length>128L*1024*1024)throw new InvalidDataException("DiagnosticFileBudget");
        foreach(var line in File.ReadLines(path)){if(line.Length>24*1024*1024)throw new InvalidDataException("DiagnosticRecordBudget");if(string.IsNullOrWhiteSpace(line))continue;using var d=JsonDocument.Parse(line);yield return d.RootElement.Clone();}
    }
    public static object Compare(string a,string b)
    {
        Dictionary<long,StageDigests> Results(string path){var result=new Dictionary<long,StageDigests>();foreach(var r in Read(path).Where(r=>r.GetProperty("Kind").GetString()=="Result")){long id=r.GetProperty("InputId").GetInt64();var digest=r.GetProperty("Digests").Deserialize<StageDigests>()!;if(result.TryGetValue(id,out var old)&&old!=digest)throw new InvalidDataException("ConflictingLocalResult");result[id]=digest;}return result;}
        var x=Results(a);var y=Results(b);var shared=x.Keys.Intersect(y.Keys).OrderBy(i=>i).ToArray();var mismatches=shared.Where(i=>x[i]!=y[i]).ToArray();
        return new{Scope="Canonical result comparison; runtime status comes from input logs",Compared=shared.Length,Matches=shared.Length-mismatches.Length,Mismatches=mismatches,OnlyA=x.Keys.Except(y.Keys).OrderBy(i=>i).ToArray(),OnlyB=y.Keys.Except(x.Keys).OrderBy(i=>i).ToArray(),Passed=shared.Length>0&&mismatches.Length==0};
    }
    public static object Replay(string path)
    {
        ObservationBridge? bridge=null;var actual=new Dictionary<long,StageDigests>();var expected=new Dictionary<long,StageDigests>();int inputs=0;
        foreach(var r in Read(path))switch(r.GetProperty("Kind").GetString()){
            case "ReplayContext":bridge=new(r.GetProperty("Binding").Deserialize<MatchBinding>()!,r.GetProperty("Map").Deserialize<MapDescriptor>()!,r.GetProperty("Profiles").Deserialize<ProfileSet>()!,r.GetProperty("Fingerprint").GetString()!);break;
            case "ReplayCheckpoint":(bridge??throw new InvalidDataException("ReplayContextMissing")).Restore(r.GetProperty("State").Deserialize<ObservationCheckpoint>()!);break;
            case "ReplayInput":var input=r.GetProperty("Input").Deserialize<CanonicalInput>()!;actual[input.InputId]=(bridge??throw new InvalidDataException("ReplayContextMissing")).Step(input,0);inputs++;break;
            case "Result":expected[r.GetProperty("InputId").GetInt64()]=r.GetProperty("Digests").Deserialize<StageDigests>()!;break;
        }
        var mismatches=actual.Where(p=>!expected.TryGetValue(p.Key,out var e)||e!=p.Value).Select(p=>p.Key).ToArray();return new{Scope="Offline deterministic replay; does not validate native readers",Inputs=inputs,Mismatches=mismatches,Passed=inputs>0&&mismatches.Length==0};
    }
    public static object Collect(string directory,string output,bool sensitive=false)
    {
        string[] allowed=["Start","SourceMetric","Result","Resync","NativeBinding","ServiceBindingUnavailable","HudUnavailable"];
        var paths=Directory.GetFiles(directory,"*.jsonl");if(paths.Length>32)throw new InvalidDataException("CollectorFileBudget");Directory.CreateDirectory(output);var files=new List<object>();
        foreach(var p in paths){var dest=Path.Combine(output,Path.GetFileName(p));if(File.Exists(dest))throw new IOException("Collector refuses overwrite");using(var writer=new StreamWriter(dest))foreach(var row in Read(p)){var kind=row.GetProperty("Kind").GetString();if(allowed.Contains(kind)||sensitive&&kind is "ReplayContext" or "ReplayInput" or "ReplayCheckpoint" or "PositiveServiceOperation")writer.WriteLine(row.GetRawText());}files.Add(new{Name=Path.GetFileName(dest),Sha256=DevInstall.Hash(dest)});}
        var report=new{Schema=1,SensitiveReplayIncluded=sensitive,Files=files,Excluded="Configs, keys, certificates, game binaries, loader logs and unrecognized records"};File.WriteAllText(Path.Combine(output,"collection.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));return report;
    }
}
