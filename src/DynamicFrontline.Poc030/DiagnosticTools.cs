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
        if(Directory.Exists(path)){var files=Directory.GetFiles(path,"*.jsonl");if(files.Length>32)throw new InvalidDataException("DiagnosticFileBudget");foreach(var file in files.OrderBy(f=>f,StringComparer.Ordinal))foreach(var row in Read(file))yield return row;yield break;}
        if(new FileInfo(path).Length>128L*1024*1024)throw new InvalidDataException("DiagnosticFileBudget");
        foreach(var line in File.ReadLines(path)){if(line.Length>24*1024*1024)throw new InvalidDataException("DiagnosticRecordBudget");if(string.IsNullOrWhiteSpace(line))continue;var text=line.TrimStart('\uFEFF');if(text.StartsWith("[DF] ",StringComparison.Ordinal))text=text[5..];using var d=JsonDocument.Parse(text);yield return d.RootElement.Clone();}
    }
    public static object Compare(string a,string b)
    {
        bool loss=false,unscoped=false;var sources=new HashSet<string>();
        Dictionary<string,(StageDigests Digests,string Source)> Results(string path){var result=new Dictionary<string,(StageDigests Digests,string Source)>();foreach(var row in Read(path)){
            if(SafeDiagnostics.Kind(row)=="DiagnosticLoss"){loss=true;continue;}if(SafeDiagnostics.Kind(row)!="Result")continue;
            JsonElement r;try{r=SafeDiagnostics.Sanitize(row)!.Value;}catch(InvalidDataException){loss=true;continue;}
            long id=r.GetProperty("InputId").GetInt64();string context=r.TryGetProperty("SessionHash",out var c)?c.GetString()!:"legacy";unscoped|=context=="legacy";
            string resultSource=r.GetProperty("ResultSource").GetString()!;sources.Add(resultSource);string key=context+"/"+id;
            var digest=r.GetProperty("Digests").Deserialize<StageDigests>()!;if(result.TryGetValue(key,out var old)&&old!=(digest,resultSource))throw new InvalidDataException("ConflictingLocalResult");result[key]=(digest,resultSource);
        }return result;}
        var x=Results(a);var y=Results(b);var shared=x.Keys.Intersect(y.Keys).OrderBy(i=>i).ToArray();var mismatches=shared.Where(i=>x[i].Digests!=y[i].Digests).ToArray();
        return new{Scope="Checksums by session and input. CoordinatorReceipt is receipt verification, NOT independent recomputation.",Compared=shared.Length,Matches=shared.Length-mismatches.Length,Mismatches=mismatches,OnlyA=x.Keys.Except(y.Keys).OrderBy(i=>i).ToArray(),OnlyB=y.Keys.Except(x.Keys).OrderBy(i=>i).ToArray(),DiagnosticLoss=loss,LegacyUnscoped=unscoped,CompleteSessionVerified=false,IndependentRecomputationVerified=!unscoped&&!loss&&shared.Length>0&&mismatches.Length==0&&shared.All(i=>x[i].Source=="Coordinator"&&y[i].Source=="Independent"||x[i].Source=="Independent"&&y[i].Source=="Coordinator"),Passed=shared.Length>0&&mismatches.Length==0&&!loss&&x.Count==shared.Length&&y.Count==shared.Length};
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
        if(sensitive)throw new InvalidDataException("SafeExportOnly_NoReplayOrRawRecords");
        directory=Path.GetFullPath(directory);output=Path.GetFullPath(output);SafeDiagnostics.NoReparse(directory);SafeDiagnostics.NoReparse(output);
        if(Directory.Exists(output)||File.Exists(output)||output.StartsWith(directory+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("FreshExternalExportRequired");
        var paths=Directory.GetFiles(directory,"*.jsonl").OrderBy(p=>p,StringComparer.Ordinal).ToArray();if(paths.Length>32)throw new InvalidDataException("CollectorFileBudget");
        foreach(var p in paths)SafeDiagnostics.NoReparse(p);Directory.CreateDirectory(output);var files=new List<object>();int bad=0;
        for(int index=0;index<paths.Length;index++){
            string name=$"client-log-{index+1:D3}.jsonl",dest=Path.Combine(output,name);int written=0,invalid=0;
            using(var writer=new StreamWriter(dest)){
                try{foreach(var row in Read(paths[index])){try{var clean=SafeDiagnostics.Sanitize(row);if(clean!=null){writer.WriteLine(clean.Value.GetRawText());written++;}}catch(Exception e)when(e is InvalidDataException or InvalidOperationException or FormatException){invalid++;}}}
                catch(Exception e)when(e is JsonException or InvalidDataException){invalid++;}
                if(invalid>0)writer.WriteLine("{\"Kind\":\"DiagnosticLoss\",\"Count\":1}");
            }bad+=invalid;files.Add(new{Name=name,Sha256=DevInstall.Hash(dest),Records=written,Invalid=invalid});
        }
        var report=new{Schema=2,SensitiveReplayIncluded=false,Complete=bad==0,Invalid=bad,Files=files,Policy="Typed allowlist only; no identities, paths, timestamps, arbitrary text, world/replay, configs, credentials or loader logs"};
        File.WriteAllText(Path.Combine(output,"collection.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));return report;
    }
}
