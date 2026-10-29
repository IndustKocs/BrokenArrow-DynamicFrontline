using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
namespace DynamicFrontline.Poc030;
public static class InteropGate
{
    public static void VerifyLoaded(string provenanceFile,GameCompatibilityDescriptor descriptor,string? cacheDirectory=null)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(provenanceFile));var root=doc.RootElement;
        if(root.GetProperty("GameAssembly").GetString()!=descriptor.Files["GameAssembly.dll"]||root.GetProperty("Metadata").GetString()!=descriptor.Files["BrokenArrow_Data/il2cpp_data/Metadata/global-metadata.dat"])throw new InvalidDataException("InteropPairMismatch");
        var wrappers=root.GetProperty("Wrappers");
        if(cacheDirectory!=null){foreach(var file in wrappers.EnumerateObject()){if(Path.GetFileName(file.Name)!=file.Name)throw new InvalidDataException("CachePath");var path=Path.Combine(cacheDirectory,file.Name);if(!File.Exists(path)||DevInstall.Hash(path)!=file.Value.GetString())throw new InvalidDataException("CacheDiskMismatch:"+file.Name);}
            foreach(var path in Directory.GetFiles(cacheDirectory,"*.dll"))if(!wrappers.TryGetProperty(Path.GetFileName(path),out _))throw new InvalidDataException("MixedCache");
            foreach(var name in new[]{"BrokenArrow","DefaultEcs","GalaxyCoreLib","NetworkCommon","UnityEngine.CoreModule","UnityEngine.IMGUIModule","Il2Cppmscorlib"}){var file=wrappers.TryGetProperty(name+".dll",out _)?name+".dll":"Il2Cpp"+name+".dll";if(!wrappers.TryGetProperty(file,out _))throw new InvalidDataException("RequiredWrapperMissing:"+name);Assembly.LoadFrom(Path.GetFullPath(Path.Combine(cacheDirectory,file)));}}
        var loaded=AppDomain.CurrentDomain.GetAssemblies();
        foreach(string required in new[]{"BrokenArrow","DefaultEcs","UnityEngine.CoreModule","UnityEngine.IMGUIModule","Il2Cppmscorlib"})
        {if(!wrappers.TryGetProperty(required+".dll",out _)&&!wrappers.TryGetProperty("Il2Cpp"+required+".dll",out _))throw new InvalidDataException("MissingWrapperProvenance:"+required);if(!loaded.Any(a=>a.GetName().Name==required||a.GetName().Name=="Il2Cpp"+required))throw new InvalidDataException("InteropNotLoaded:"+required);}
        foreach(var a in loaded)
        {
            var name=a.GetName().Name+".dll";
            if(!wrappers.TryGetProperty(name,out var expected))continue;
            if(string.IsNullOrEmpty(a.Location)||Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location)))!=expected.GetString())throw new InvalidDataException("InteropCacheMismatch:"+name);
        }
        foreach(var dep in root.GetProperty("Generator").EnumerateArray())
        {
            var name=dep.GetProperty("Name").GetString();if(name=="Il2CppInterop.Generator.dll")continue;
            var a=AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(a=>a.GetName().Name+".dll"==name);
            if(a==null){var loader=loaded.SingleOrDefault(a=>a.GetName().Name=="MelonLoader")??throw new InvalidDataException("MissingLoader");var path=Path.Combine(Path.GetDirectoryName(loader.Location)!,name!);if(!File.Exists(path)||DevInstall.Hash(path)!=dep.GetProperty("Sha256").GetString())throw new InvalidDataException("MissingOrChangedLoaderDependency");a=Assembly.LoadFrom(path);}
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location)))!=dep.GetProperty("Sha256").GetString())throw new InvalidDataException("LoaderDependencyMismatch");
        }
    }
}
