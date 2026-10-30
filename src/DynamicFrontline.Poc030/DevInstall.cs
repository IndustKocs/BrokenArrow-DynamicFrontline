using System.Security.Cryptography;
using System.Text.Json;
namespace DynamicFrontline.Poc030;
public enum FileCategory { ImmutablePayload, VerifiedGeneratedInterop, MutableUserConfig, SharedInteropCache, SharedLoaderConfig, InteropBootstrap, RetiredInteropCache, PinnedLoader }
public sealed record InstallFile(string Relative,string Source,string Sha256,FileCategory Category,string? ReplaceApprovedHash=null);
public sealed record InstalledFile(string Relative,string Sha256,FileCategory Category,string? OriginalHash=null);
public sealed record InstallReceipt(int Schema,string Target,InstalledFile[] Files);
public sealed record FilePlan(string Relative,string? Before,string? After,string Stage,string Backup,FileCategory Category);
public sealed record InstallJournal(int Schema,string Operation,string Target,bool Committed,InstallReceipt? BeforeReceipt,InstallReceipt AfterReceipt,FilePlan[] Files);
public sealed record InstallResult(string Target,string State,string[] Preserved,string[] Conflicts);
// Deliberately separate from Launcher. Every destructive operation is constrained to a validated receipt/plan/hash.
public sealed class DevInstall : IDisposable
{
    readonly string root,control; readonly FileStream gate; readonly Action<string>? fault;
    public DevInstall(string target,bool sandbox,bool consent,Action<string>? fault=null)
    {
        root=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);this.fault=fault;
        if(!consent)throw new InvalidDataException("Explicit consent required even for sandbox apply");
        Safe(root);
        if(!OperatingSystem.IsWindows()||new DriveInfo(Path.GetPathRoot(root)!).DriveFormat!="NTFS")throw new InvalidDataException("Only local Windows NTFS supported");
        if(root.StartsWith("\\")||root==Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar))throw new InvalidDataException("Local dedicated target required");
        if(sandbox && File.Exists(Path.Combine(root,"BrokenArrow.exe")))throw new InvalidDataException("Sandbox cannot target game");
        if(!sandbox && (!File.Exists(Path.Combine(root,"BrokenArrow.exe"))||System.Diagnostics.Process.GetProcessesByName("BrokenArrow").Length>0||System.Diagnostics.Process.GetProcessesByName("EACLauncher").Length>0))throw new InvalidDataException("Closed compatible game required");
        control=Inside("UserData/DynamicFrontline030/.rc2");Directory.CreateDirectory(control);
        gate=new FileStream(Path.Combine(control,"operation.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
    }
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    string ReceiptPath=>Inside("UserData/DynamicFrontline030/install-receipt-rc2.json");
    string JournalPath=>Path.Combine(control,"journal.json");
    public static string Hash(string path){using var f=File.OpenRead(path);using var sha=SHA256.Create();return Convert.ToHexString(sha.ComputeHash(f));}
    static void Safe(string path){for(var p=path;!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p)){if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Reparse point forbidden");}}
    string Inside(string relative)
    {
        if(Path.IsPathRooted(relative))throw new InvalidDataException("Absolute relative path");var path=Path.GetFullPath(Path.Combine(root,relative));
        if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Outside target");Safe(path);return path;
    }
    public static bool Allowed(string p,FileCategory category)=>category switch
    {
        FileCategory.PinnedLoader=>PinnedLoader.Files.ContainsKey(p),
        FileCategory.ImmutablePayload=>p=="Mods/DynamicFrontline.Poc030.MelonLoader.dll"||p=="Mods/DynamicFrontline.Steam.MelonLoader.dll"||p.StartsWith("UserLibs/DynamicFrontline.",StringComparison.Ordinal)&&p.EndsWith(".dll",StringComparison.Ordinal)&&p.Count(c=>c=='/')==1&&!p.Contains('\\')||p=="UserData/DynamicFrontline030/compatibility.json"||p=="UserData/DynamicFrontlineSteam/compatibility.json"||p=="UserData/DynamicFrontlineSteam/map.json"||p=="UserData/DynamicFrontlineSteam/profiles.json",
        FileCategory.MutableUserConfig=>p=="UserData/DynamicFrontline030/probe.json",
        FileCategory.VerifiedGeneratedInterop=>p=="UserData/DynamicFrontline030/interop-provenance.json"||p=="UserData/DynamicFrontlineSteam/interop-provenance.json", // shared loader cache has a separate ownership plan
        FileCategory.SharedInteropCache or FileCategory.RetiredInteropCache=>p.StartsWith("MelonLoader/Il2CppAssemblies/",StringComparison.Ordinal)&&p.Count(c=>c=='/')==2&&!p.Contains('\\')&&(p.EndsWith(".dll",StringComparison.Ordinal)||p.EndsWith(".db",StringComparison.Ordinal)),
        FileCategory.SharedLoaderConfig=>p=="MelonLoader/Dependencies/Il2CppAssemblyGenerator/Config.cfg"||p=="UserData/Loader.cfg",
        FileCategory.InteropBootstrap=>p=="MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/Cpp2IL.exe"||p=="MelonLoader/Dependencies/Il2CppAssemblyGenerator/Cpp2IL/Plugins/Cpp2IL.Plugin.StrippedCodeRegSupport.dll"||p.StartsWith("MelonLoader/Dependencies/Il2CppAssemblyGenerator/UnityDependencies/",StringComparison.Ordinal)&&p.Count(c=>c=='/')==4&&!p.Contains('\\')&&p.EndsWith(".dll",StringComparison.Ordinal),
        _=>false
    };
    InstallReceipt? Receipt()
    {
        if(!File.Exists(ReceiptPath)){
            var legacy=Inside("UserData/DynamicFrontline030/install-receipt.json");if(!File.Exists(legacy))return null;
            using var doc=JsonDocument.Parse(File.ReadAllText(legacy));if(doc.RootElement.GetProperty("Schema").GetInt32()!=1)throw new InvalidDataException("Unknown legacy receipt");
            var files=doc.RootElement.GetProperty("Files").EnumerateArray().Select(f=>{var path=f.GetProperty("Relative").GetString()!;var category=path=="UserData/DynamicFrontline030/probe.json"?FileCategory.MutableUserConfig:path=="UserData/DynamicFrontline030/interop-provenance.json"?FileCategory.VerifiedGeneratedInterop:FileCategory.ImmutablePayload;
                if(!Allowed(path,category))throw new InvalidDataException("Invalid legacy ownership");return new InstalledFile(path,f.GetProperty("Sha256").GetString()!,category);}).ToArray();
            if(files.Select(f=>f.Relative).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=files.Length)throw new InvalidDataException("Duplicate legacy ownership");
            return new(2,root,files); // Virtual migration; durable RC2 receipt is committed inside the next WAL transaction.
        }var r=JsonSerializer.Deserialize<InstallReceipt>(File.ReadAllText(ReceiptPath))??throw new InvalidDataException("Corrupt receipt");
        if(r.Schema!=2||r.Target!=root||r.Files.Select(f=>f.Relative).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=r.Files.Length||r.Files.Any(f=>!Allowed(f.Relative,f.Category)))throw new InvalidDataException("Invalid receipt");return r;
    }
    void Atomic<T>(string path,T value,string op)
    {
        Safe(path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temp=path+"."+op+".tmp";
        using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){var bytes=JsonSerializer.SerializeToUtf8Bytes(value,Json);file.Write(bytes);file.Flush(true);}
        File.Move(temp,path,true);
    }
    public InstallResult Verify()
    {
        var receipt=Receipt()??throw new InvalidDataException("Missing receipt");return new(root,"Verified",receipt.Files.Where(f=>f.Category==FileCategory.MutableUserConfig&&File.Exists(Inside(f.Relative))).Select(f=>f.Relative).ToArray(),receipt.Files.Where(f=>f.Category==FileCategory.RetiredInteropCache?File.Exists(Inside(f.Relative)):f.Category!=FileCategory.MutableUserConfig&&(!File.Exists(Inside(f.Relative))||Hash(Inside(f.Relative))!=f.Sha256)).Select(f=>f.Relative).ToArray());
    }
    public InstallResult Apply(InstallFile[] package)
    {
        if(File.Exists(JournalPath))throw new InvalidDataException("Recover pending operation first");
        if(package.Length==0||package.Select(f=>f.Relative).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=package.Length)throw new InvalidDataException("Invalid package");
        var receipt=Receipt();var next=new List<InstalledFile>();var plans=new List<FilePlan>();var op=Guid.NewGuid().ToString("N");var staging=Path.Combine(control,op);var preserved=new List<string>();
        foreach(var f in package)
        {
            if(f.Category==FileCategory.PinnedLoader&&PinnedLoader.Files.GetValueOrDefault(f.Relative)!=f.Sha256)throw new InvalidDataException("UnpinnedLoader");
            if(!Allowed(f.Relative,f.Category)||!File.Exists(f.Source)||Hash(f.Source)!=f.Sha256)throw new InvalidDataException("Package integrity/path mismatch");
            var dest=Inside(f.Relative);string? before=File.Exists(dest)?Hash(dest):null;var own=receipt?.Files.SingleOrDefault(x=>x.Relative==f.Relative);
            if(before!=null&&f.Category==FileCategory.MutableUserConfig){using var cfg=JsonDocument.Parse(File.ReadAllText(dest));if(!cfg.RootElement.TryGetProperty("Mode",out _))throw new InvalidDataException("Config migration required");preserved.Add(f.Relative);next.Add(new(f.Relative,before,f.Category));continue;}
            bool shared=f.Category is FileCategory.SharedInteropCache or FileCategory.SharedLoaderConfig or FileCategory.InteropBootstrap or FileCategory.RetiredInteropCache or FileCategory.PinnedLoader;
            if(before!=null&&(own==null||own.Sha256!=before)&&!(shared&&own==null&&f.ReplaceApprovedHash==before))throw new InvalidDataException("Foreign/changed file preserved: "+f.Relative);
            bool retire=f.Category==FileCategory.RetiredInteropCache;if(retire&&(before==null||f.ReplaceApprovedHash!=before))throw new InvalidDataException("Retirement requires exact current hash");
            next.Add(new(f.Relative,retire?"":f.Sha256,f.Category,own?.OriginalHash??(shared&&own==null?before:null)));plans.Add(new(f.Relative,before,retire?null:f.Sha256,Path.Combine(staging,plans.Count+".stage"),Path.Combine(staging,plans.Count+".backup"),f.Category));
        }
        if(receipt!=null)next.AddRange(receipt.Files.Where(f=>!next.Any(n=>n.Relative==f.Relative)));
        var journal=new InstallJournal(2,op,root,false,receipt,new(2,root,next.ToArray()),plans.ToArray());
        Atomic(JournalPath,journal,op);fault?.Invoke("prepared");Directory.CreateDirectory(staging);
        foreach(var plan in plans)
        {
            if(plan.After==null)continue;
            var source=package.Single(f=>f.Relative==plan.Relative).Source;
            using(var output=new FileStream(plan.Stage,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {fault?.Invoke("temp-created:"+plan.Relative);var bytes=File.ReadAllBytes(source);output.Write(bytes.AsSpan(0,bytes.Length/2));output.Flush(true);fault?.Invoke("partial-write:"+plan.Relative);output.Write(bytes.AsSpan(bytes.Length/2));output.Flush(true);}
            if(Hash(plan.Stage)!=plan.After)throw new InvalidDataException("Staging integrity");
        }
        foreach(var plan in plans)
        {
            var dest=Inside(plan.Relative);if((File.Exists(dest)?Hash(dest):null)!=plan.Before)throw new InvalidDataException("Target changed after plan");
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);if(plan.Before!=null)File.Copy(dest,plan.Backup,false);
            var original=next.Single(f=>f.Relative==plan.Relative).OriginalHash;
            if(original!=null&&original==plan.Before){var saved=Original(original);Directory.CreateDirectory(Path.GetDirectoryName(saved)!);if(!File.Exists(saved)){var temporary=saved+"."+op+".tmp";using(var from=File.OpenRead(dest))using(var to=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){from.CopyTo(to);to.Flush(true);}File.Move(temporary,saved);}if(Hash(saved)!=original)throw new InvalidDataException("Original cache backup integrity");}
            fault?.Invoke("before-replace:"+plan.Relative);if(plan.After==null)File.Delete(dest);else File.Move(plan.Stage,dest,true);fault?.Invoke("after-replace:"+plan.Relative);
        }
        fault?.Invoke("before-receipt");Atomic(ReceiptPath,journal.AfterReceipt,op);fault?.Invoke("after-receipt");
        Atomic(JournalPath,journal with{Committed=true},op);fault?.Invoke("committed");Cleanup(journal);return new(root,"Installed",preserved.ToArray(),[]);
    }
    public InstallResult Remove(bool preserveSettings=true)
    {
        if(File.Exists(JournalPath))throw new InvalidDataException("Recover pending operation first");var receipt=Receipt()??throw new InvalidDataException("Missing receipt; no guessed deletion");var op=Guid.NewGuid().ToString("N");var plans=new List<FilePlan>();var keep=new List<InstalledFile>();var conflicts=new List<string>();
        bool cacheConflict=receipt.Files.Any(f=>(f.Category is FileCategory.SharedInteropCache or FileCategory.SharedLoaderConfig or FileCategory.InteropBootstrap or FileCategory.RetiredInteropCache or FileCategory.PinnedLoader)&&File.Exists(Inside(f.Relative))&&Hash(Inside(f.Relative))!=f.Sha256);
        foreach(var f in receipt.Files)
        {
            var dest=Inside(f.Relative);if(f.Category==FileCategory.MutableUserConfig){if(preserveSettings){keep.Add(f);continue;}if(File.Exists(dest))plans.Add(new(f.Relative,Hash(dest),null,Path.Combine(control,op,plans.Count+".stage"),Path.Combine(control,op,plans.Count+".backup"),f.Category));continue;}
            if(cacheConflict&&(f.Category is FileCategory.SharedInteropCache or FileCategory.SharedLoaderConfig or FileCategory.InteropBootstrap or FileCategory.RetiredInteropCache or FileCategory.PinnedLoader)){keep.Add(f);conflicts.Add(f.Relative);continue;}
            bool retired=f.Category==FileCategory.RetiredInteropCache;
            if(retired&&f.OriginalHash==null)continue;
            if(!File.Exists(dest)&&!retired)continue;if(File.Exists(dest)&&(retired||Hash(dest)!=f.Sha256)){keep.Add(f);conflicts.Add(f.Relative);continue;}
            if(f.OriginalHash!=null&&(!File.Exists(Original(f.OriginalHash))||Hash(Original(f.OriginalHash))!=f.OriginalHash))throw new InvalidDataException("Original cache backup missing or changed");
            plans.Add(new(f.Relative,retired?null:f.Sha256,f.OriginalHash,Path.Combine(control,op,plans.Count+".stage"),Path.Combine(control,op,plans.Count+".backup"),f.Category));
        }
        var journal=new InstallJournal(2,op,root,false,receipt,new(2,root,keep.ToArray()),plans.ToArray());Atomic(JournalPath,journal,op);fault?.Invoke("prepared");Directory.CreateDirectory(Path.Combine(control,op));
        foreach(var p in plans){var dest=Inside(p.Relative);if((File.Exists(dest)?Hash(dest):null)!=p.Before)throw new InvalidDataException("Remove target changed");if(p.After!=null){using(var from=File.OpenRead(Original(p.After)))using(var stage=new FileStream(p.Stage,FileMode.CreateNew,FileAccess.Write,FileShare.None)){from.CopyTo(stage);stage.Flush(true);}if(p.Before!=null)File.Copy(dest,p.Backup);File.Move(p.Stage,dest,true);}else File.Move(dest,p.Backup);fault?.Invoke("after-remove:"+p.Relative);}
        Atomic(ReceiptPath,journal.AfterReceipt,op);fault?.Invoke("after-receipt");Atomic(JournalPath,journal with{Committed=true},op);Cleanup(journal);return new(root,"RemovedOwnedUnchangedFiles",keep.Where(f=>f.Category==FileCategory.MutableUserConfig).Select(f=>f.Relative).ToArray(),conflicts.ToArray());
    }
    public InstallResult Recover()
    {
        if(!File.Exists(JournalPath))return new(root,"NoPendingOperation",[],[]);
        var j=JsonSerializer.Deserialize<InstallJournal>(File.ReadAllText(JournalPath))??throw new InvalidDataException("Corrupt journal");
        if(j.Schema!=2||j.Target!=root||!Guid.TryParseExact(j.Operation,"N",out _)||j.Files.Where((f,i)=>!Allowed(f.Relative,f.Category)||f.Stage!=Path.Combine(control,j.Operation,i+".stage")||f.Backup!=Path.Combine(control,j.Operation,i+".backup")).Any())throw new InvalidDataException("Invalid journal");
        if(j.Committed){Cleanup(j);return new(root,"Committed",[],[]);}var conflicts=new List<string>();
        foreach(var p in j.Files.Reverse())
        {
            var dest=Inside(p.Relative);var now=File.Exists(dest)?Hash(dest):null;
            if(now==p.Before)continue;
            if(now!=p.After){conflicts.Add(p.Relative);continue;}
            if(p.Before==null){if(now!=null)File.Delete(dest);}
            else if(File.Exists(p.Backup)&&Hash(p.Backup)==p.Before){File.Move(p.Backup,dest,true);}
            else conflicts.Add(p.Relative);
        }
        if(conflicts.Count>0)return new(root,"RecoveryConflictPreserved",[],conflicts.ToArray());
        if(j.BeforeReceipt==null){if(File.Exists(ReceiptPath)&&File.ReadAllText(ReceiptPath)==JsonSerializer.Serialize(j.AfterReceipt,Json))File.Delete(ReceiptPath);}
        else Atomic(ReceiptPath,j.BeforeReceipt,j.Operation);
        Cleanup(j);return new(root,"RolledBack",[],[]);
    }
    void Cleanup(InstallJournal j)
    {
        foreach(var p in j.Files)foreach(var f in new[]{p.Stage,p.Backup}){Safe(f);if(File.Exists(f))File.Delete(f);}
        foreach(var f in new[]{ReceiptPath+"."+j.Operation+".tmp",JournalPath+"."+j.Operation+".tmp"})if(File.Exists(f))File.Delete(f);
        foreach(var f in j.AfterReceipt.Files.Where(f=>f.OriginalHash!=null)){var temp=Original(f.OriginalHash!)+"."+j.Operation+".tmp";if(File.Exists(temp))File.Delete(temp);}
        File.Delete(JournalPath); // no recursive directory deletion, logs/secrets/user settings survive
    }
    string Original(string hash){if(hash.Length!=64||!hash.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid original hash");return Inside("UserData/DynamicFrontline030/.rc2/originals/"+hash+".bin");}
    public void Dispose()=>gate.Dispose();
}

