using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
namespace DynamicFrontline.Poc030;
public static class InteropGate
{
    public static void VerifyLoaded(string provenanceFile,GameCompatibilityDescriptor descriptor)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(provenanceFile));var root=doc.RootElement;
        if(root.GetProperty("GameAssembly").GetString()!=descriptor.Files["GameAssembly.dll"]||root.GetProperty("Metadata").GetString()!=descriptor.Files["BrokenArrow_Data/il2cpp_data/Metadata/global-metadata.dat"])throw new InvalidDataException("InteropPairMismatch");
        var loaded=AppDomain.CurrentDomain.GetAssemblies();var wrappers=root.GetProperty("Wrappers");
        foreach(string required in new[]{"BrokenArrow","DefaultEcs","UnityEngine.CoreModule","UnityEngine.IMGUIModule","Il2Cppmscorlib"})
        {if(!loaded.Any(a=>a.GetName().Name==required))throw new InvalidDataException("InteropNotLoaded:"+required);}
        foreach(var a in loaded)
        {
            var name=a.GetName().Name+".dll";
            if(!wrappers.TryGetProperty(name,out var expected))continue;
            if(string.IsNullOrEmpty(a.Location)||Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location)))!=expected.GetString())throw new InvalidDataException("InteropCacheMismatch:"+name);
        }
        foreach(var dep in root.GetProperty("Generator").EnumerateArray())
        {
            var name=dep.GetProperty("Name").GetString();if(name=="Il2CppInterop.Generator.dll")continue;
            var a=loaded.SingleOrDefault(a=>a.GetName().Name+".dll"==name)??throw new InvalidDataException("MissingLoaderDependency");
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location)))!=dep.GetProperty("Sha256").GetString())throw new InvalidDataException("LoaderDependencyMismatch");
        }
    }
}
