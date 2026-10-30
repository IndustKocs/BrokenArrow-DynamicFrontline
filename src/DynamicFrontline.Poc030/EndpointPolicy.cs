using System.Net;
namespace DynamicFrontline.Poc030;
public static class EndpointPolicy
{
    public const string RadminA="26.137.77.181", RadminB="26.204.104.7";
    public static IPAddress Validate(ProbeConfig config)
    {
        if(config.Role is not ("A" or "B")||config.Port is <1024 or >65535)throw new InvalidDataException("InvalidEndpointRoleOrPort");
        var address=IPAddress.Parse(config.Host);var b=address.GetAddressBytes();
        if(config.RadminPair){if(config.Host!=RadminA)throw new InvalidDataException("PinnedRadminCoordinatorRequired");return address;}
        if(!(IPAddress.IsLoopback(address)||b.Length==4&&(b[0]==10||b[0]==172&&b[1] is >=16 and <=31||b[0]==192&&b[1]==168)))throw new InvalidDataException("ExplicitPrivateEndpointRequired");
        return address;
    }
    public static void VerifyRadminPeer(string role,IPAddress remote)
    {
        string expected=role switch{"A"=>RadminB,"B"=>RadminA,_=>throw new InvalidDataException("InvalidRole")};
        if(!remote.Equals(IPAddress.Parse(expected)))throw new InvalidDataException("PinnedRadminPeerRequired");
    }
}
