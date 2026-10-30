using System.Text.Json;
namespace DynamicFrontline.Poc030;
public static class InstallPlanning
{
    public static InstallFile[] Plan(string target,InstallFile[] files)
    {
        target=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        if(files.Any(f=>f.Relative=="Mods/DynamicFrontline.Steam.MelonLoader.dll")&&File.Exists(Path.Combine(target,"Mods","DynamicFrontline.Poc030.MelonLoader.dll")))throw new InvalidDataException("Remove RC2 through its receipt before planning T18; do not load both plugins");
        if(files.Any(f=>f.Relative=="Mods/DynamicFrontline.Poc030.MelonLoader.dll")&&File.Exists(Path.Combine(target,"Mods","DynamicFrontline.Steam.MelonLoader.dll")))throw new InvalidDataException("Remove T18 through its receipt before planning RC2");
        if(files.Length==0||files.Select(f=>f.Relative).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=files.Length)throw new InvalidDataException("Duplicate or empty plan");
        var cacheNames=files.Where(f=>f.Category==FileCategory.SharedInteropCache).Select(f=>Path.GetFileName(f.Relative)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cache=Path.Combine(target,"MelonLoader","Il2CppAssemblies");
        if(cacheNames.Count>0&&Directory.Exists(cache))files=files.Concat(Directory.GetFiles(cache).Where(p=>(Path.GetExtension(p) is ".dll" or ".db")&&!cacheNames.Contains(Path.GetFileName(p))).Select(p=>new InstallFile("MelonLoader/Il2CppAssemblies/"+Path.GetFileName(p),p,DevInstall.Hash(p),FileCategory.RetiredInteropCache))).ToArray();
        return files.Select(f=>{
            if(!DevInstall.Allowed(f.Relative,f.Category)||DevInstall.Hash(f.Source)!=f.Sha256)throw new InvalidDataException("Invalid plan payload");
            var path=Path.GetFullPath(Path.Combine(target,f.Relative));if(!path.StartsWith(target+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Plan path");
            for(var p=path;!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p))if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Plan reparse point");
            bool shared=f.Category is FileCategory.SharedInteropCache or FileCategory.SharedLoaderConfig or FileCategory.InteropBootstrap or FileCategory.RetiredInteropCache;
            return f with{ReplaceApprovedHash=shared&&File.Exists(path)?DevInstall.Hash(path):null};
        }).ToArray();
    }
    public static void VerifyGame(string root,InstallFile[] files)
    {
        var descriptor=JsonSerializer.Deserialize<GameCompatibilityDescriptor>(File.ReadAllText(files.Single(f=>f.Relative is "UserData/DynamicFrontline030/compatibility.json" or "UserData/DynamicFrontlineSteam/compatibility.json").Source))!;
        string[] expected=["12E7088369539B029A7BF0064062CE030A53B5189E7067420903F47B4281FDDF","A4EDE1B29664378A2FB9F952F65330A73DBFA115B205065D1831F420540AFE01","79A54AF1C83B5B3BFA34936BE18899DE49FA88EEA6A33BA7351C2163F95F6613","A0A2D82854AF8FB1718D70EC0BE0F4028A1332B13BAE89051EC8B368187F34B4"];
        if(descriptor.BuildId!="26273833"||descriptor.MetadataVersion!=39||descriptor.UnityVersion!="6000.3.24f1"||descriptor.Files.Count!=4||!descriptor.Files.Values.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expected))throw new InvalidDataException("Unreviewed build descriptor");
        descriptor.Verify(root);
        if(!files.Any(f=>f.Category==FileCategory.SharedInteropCache)||!files.Any(f=>f.Category==FileCategory.SharedLoaderConfig)||!files.Any(f=>f.Category==FileCategory.InteropBootstrap))throw new InvalidDataException("Complete local interop plan required");
        var loader=Path.Combine(root,"MelonLoader","net6","MelonLoader.dll");if(!File.Exists(loader)||DevInstall.Hash(loader)!="BDD43DC0F3893C208C95389B863AB61C967AE14204DA0DD8051647E754C8709B")throw new InvalidDataException("Install the pinned MelonLoader 0.7.3 separately before applying RC2");
        var cfg=Path.Combine(root,"UserData","Loader.cfg");if(File.Exists(cfg)&&System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(cfg),@"(?im)^\s*force_regeneration\s*=\s*true"))throw new InvalidDataException("Loader force_regeneration must be disabled before planning");
    }
}
