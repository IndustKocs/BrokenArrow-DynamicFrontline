namespace DynamicFrontline.Poc030;

public static class ScenarioSources
{
    public static string Resolve5454584(string gameRoot)
    {
        var root=Path.GetFullPath(gameRoot);
        var local=Path.Combine(root,"Scenarios","5454584");
        // Steam keeps this app's subscriptions beside common, in the same library.
        var steamapps=Directory.GetParent(root)?.Parent?.FullName;
        var workshop=steamapps==null?null:Path.Combine(steamapps,"workshop","content","1604270","3830189668");
        var candidates=new[]{local,workshop}.Where(p=>p!=null&&Directory.Exists(p)).Cast<string>().ToArray();
        if(candidates.Length==0)throw new InvalidDataException("Scenario5454584Missing_SubscribeWorkshop3830189668");
        foreach(var p in candidates)
            if(!File.Exists(Path.Combine(p,"5454584.bascr"))||!File.Exists(Path.Combine(p,"meta.json")))
                throw new InvalidDataException("Scenario5454584Incomplete");
        if(candidates.Length>1)
            foreach(var name in new[]{"5454584.bascr","meta.json"})
                if(DevInstall.Hash(Path.Combine(candidates[0],name))!=DevInstall.Hash(Path.Combine(candidates[1],name)))
                    throw new InvalidDataException("Scenario5454584ConflictingCopies");
        return candidates[0];
    }
}
