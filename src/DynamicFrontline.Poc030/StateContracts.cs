namespace DynamicFrontline.Poc030;
public enum ReceiptStage { Begin, PositiveRecipientReceipt, Continue, End }
public sealed record ServiceReceipt(string Unit,string Operation,long Sequence,ReceiptStage Stage,double Time,double Amount,string Provenance);
public sealed class StreamingRelief
{
    sealed class StreamState { public long Sequence; public double Start,Last; public bool Positive,Ended; public ServiceReceipt? Previous; }
    readonly Dictionary<(string,string),StreamState> streams=new();
    public int Count=>streams.Count;
    // Open-ended source events; no assumed future end, no retroactive native application.
    public void Accept(ServiceReceipt receipt,double observationTime)
    {
        if(!double.IsFinite(receipt.Time)||!double.IsFinite(observationTime)||receipt.Time<0||receipt.Time>observationTime||string.IsNullOrWhiteSpace(receipt.Provenance)||string.IsNullOrWhiteSpace(receipt.Unit)||string.IsNullOrWhiteSpace(receipt.Operation)||!double.IsFinite(receipt.Amount)||receipt.Amount<0)throw new InvalidDataException("InvalidReceipt");
        var key=(receipt.Unit,receipt.Operation);
        if(!streams.TryGetValue(key,out var state))
        {
            if(receipt.Stage!=ReceiptStage.Begin||receipt.Sequence!=0||streams.Count>=4096)throw new InvalidDataException("MissingStartOrBudget");
            streams.Add(key,new(){Start=receipt.Time,Last=receipt.Time,Sequence=0,Previous=receipt});return;
        }
        if(receipt.Sequence==state.Sequence){if(receipt==state.Previous)return;throw new InvalidDataException("ConflictingReceipt");}
        if(receipt.Sequence!=state.Sequence+1||receipt.Time<state.Last||state.Ended||receipt.Stage==ReceiptStage.Begin)throw new InvalidDataException("ReceiptGapOrLifecycle");
        if(receipt.Stage==ReceiptStage.PositiveRecipientReceipt)
        {
            if(receipt.Amount<=0)throw new InvalidDataException("NoPositiveRecipientOperation");
            if(!state.Positive)state.Start=receipt.Time;
            state.Positive=true;
        }
        if(receipt.Stage==ReceiptStage.End)
        {
            state.Ended=true;

        }
        state.Last=receipt.Time;state.Sequence=receipt.Sequence;state.Previous=receipt;
    }
    public string At(string unit,double time)
    {
        if(!double.IsFinite(time)||time<0)throw new InvalidDataException("InvalidTime");
        var active=streams.Where(x=>x.Key.Item1==unit&&x.Value.Positive&&!x.Value.Ended).Select(x=>x.Value).ToArray();
        if(active.Any(s=>time>=s.Last&&time-s.Last<=1))return "BeingSupplied";
        if(active.Length>0)return "Unknown_ServiceStreamStale";
        var merged=new List<(double Start,double End)>();
        foreach(var s in streams.Where(x=>x.Key.Item1==unit&&x.Value.Positive&&x.Value.Ended).Select(x=>x.Value).OrderBy(s=>s.Start))
        {if(merged.Count==0||s.Start>merged[^1].End)merged.Add((s.Start,s.Last));else merged[^1]=(merged[^1].Start,Math.Max(merged[^1].End,s.Last));}
        return merged.Any(s=>time>=s.End&&time<s.End+Math.Min(20,2*(s.End-s.Start)))?"PostSupplyGrace":"NoRelief";
    }
}
// Canonical generation must come from verified spawn evidence. Local handles are never substituted.
public sealed record CanonicalIdentity(string Match,int NetworkId,long SpawnGeneration);
public sealed record Binding(LocalIdentity Local,long Epoch,int Owner,CanonicalIdentity Canonical);
public sealed class IdentityBindings
{
    readonly Dictionary<CanonicalIdentity,Binding> bindings=new();
    public void Bind(Binding next,bool continuityProven)
    {
        if(next.Canonical.SpawnGeneration<0||next.Epoch<0||bindings.Count>=10000&&!bindings.ContainsKey(next.Canonical))throw new InvalidDataException("IdentityBudgetOrUnknownGeneration");
        if(bindings.TryGetValue(next.Canonical,out var previous)&&previous!=next)
        {if(!continuityProven||next.Epoch<=previous.Epoch)throw new InvalidDataException("ResyncRequired_NoMemoryReset");}
        bindings[next.Canonical]=next;
    }
    public Binding? Find(CanonicalIdentity identity)=>bindings.GetValueOrDefault(identity);
    public void InvalidateBindings(){bindings.Clear();} // Stress/tau/dedup live separately and are not touched.
}
public enum ApplyDisposition { New, Duplicate, Conflict, Ambiguous, BudgetExceeded }
public sealed class EffectJournal
{
    readonly Dictionary<string,(string Hash,bool Confirmed)> entries=new(); readonly int limit;
    public EffectJournal(int limit=4096){this.limit=limit;}
    public ApplyDisposition Reserve(string id,string payloadHash)
    {
        if(entries.TryGetValue(id,out var old))return old.Hash!=payloadHash?ApplyDisposition.Conflict:old.Confirmed?ApplyDisposition.Duplicate:ApplyDisposition.Ambiguous;
        if(entries.Count>=limit)return ApplyDisposition.BudgetExceeded;
        entries.Add(id,(payloadHash,false));return ApplyDisposition.New;
    }
    public void Confirm(string id,string payloadHash){if(!entries.TryGetValue(id,out var value)||value.Hash!=payloadHash)throw new InvalidDataException("UnreservedEffect");entries[id]=(payloadHash,true);}
}
