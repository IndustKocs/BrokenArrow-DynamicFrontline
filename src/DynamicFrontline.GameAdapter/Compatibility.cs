using System.Security.Cryptography;
using System.Text.Json;
namespace DynamicFrontline.GameAdapter;

public sealed record CompatibilityManifest(string UnityVersion, Dictionary<string,string> Hashes);
public static class Compatibility
{
    public static CompatibilityManifest Load(Stream input) => JsonSerializer.Deserialize<CompatibilityManifest>(input)
        ?? throw new InvalidDataException("Missing compatibility manifest");
    public static bool Verify(string root,string unity,CompatibilityManifest manifest,out string reason)
    {
        if (unity != manifest.UnityVersion) { reason="unity-version-mismatch"; return false; }
        if (manifest.Hashes.Count == 0) { reason="empty-manifest"; return false; }
        foreach(var pair in manifest.Hashes)
        {
            string path=Path.GetFullPath(Path.Combine(root,pair.Key));
            string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) { reason="invalid-manifest-path"; return false; }
            if(!File.Exists(path)) { reason="missing-build-file"; return false; }
            using var input=File.OpenRead(path);
            using var sha=SHA256.Create();
            if(!Convert.ToHexString(sha.ComputeHash(input)).Equals(pair.Value,StringComparison.OrdinalIgnoreCase))
            { reason="build-hash-mismatch"; return false; }
        }
        reason="build-fingerprint-matched"; return true;
    }
}
