using System.Text.Json;
namespace DynamicFrontline.Poc030;
public static class ConnectionProbe
{
    public static async Task<object> Run(ProbeConfig supplied)
    {
        var config=supplied with{Mode="NetworkProbe",RuntimeReadAuthorized=false,PrivateSessionConfirmed=false};
        var map=JsonSerializer.Deserialize<MapDescriptor>(File.ReadAllText(config.MapFile))!;var profiles=JsonSerializer.Deserialize<ProfileSet>(File.ReadAllText(config.ProfilesFile))!;
        var hello=new Hello("df030-world-2",config.SessionId,config.Role,profiles.BuildFingerprint,DevInstall.Hash(typeof(ConnectionProbe).Assembly.Location),new DynamicFrontline.Frontline.Rules023.Rules023Config().Hash,profiles.Hash,map.Hash,"NetworkOnly_NoNativeReader","NoGameNoWrites");
        using var channel=new DevChannel(config,hello,File.ReadAllBytes(config.SecretFile));var running=channel.RunWorld();var watch=System.Diagnostics.Stopwatch.StartNew();bool success=false;
        while(watch.Elapsed.TotalSeconds<15){if(channel.State=="AuthenticatedWorldTls"){success=true;await Task.Delay(500);break;}await Task.Delay(50);}
        string state=channel.State;channel.Dispose();await running;return new{Passed=success,Role=config.Role,State=state,GameExecuted=false,NativeReadersExecuted=false,Scope="Endpoint/pin/HMAC/context only; does not prove live source or core"};
    }
}
