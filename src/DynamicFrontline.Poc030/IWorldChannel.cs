namespace DynamicFrontline.Poc030;
// Transport seam only; canonical world, observation lane and resync remain RC2.
public interface IWorldChannel : IDisposable
{
 string State {get;} bool Authenticated {get;} string ConnectionEpoch {get;}
 int PendingMessages {get;} double? LastExchangeMilliseconds {get;}
 event Action<WorldMessage>? Received;
 bool Send<T>(string kind,T payload);
 void PublishHealth(SourceHealth health);
 void PublishFailure(SourceState state,string reason);
}
