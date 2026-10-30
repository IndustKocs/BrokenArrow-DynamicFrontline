namespace DynamicFrontline.Steam;
public static class DebugViewConsent
{
 public static string Epoch(string local,string peer,bool host)=>DynamicFrontline.Poc030.CanonicalCodec.Digest(new{A=host?local:peer,B=host?peer:local});
 public static bool Enabled(bool local,bool peer,bool localRoster,bool peerRoster)=>local&&peer&&localRoster&&peerRoster;
}
