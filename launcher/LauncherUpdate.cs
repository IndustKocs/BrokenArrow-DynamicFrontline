using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

// Stable entry point: verified successors live outside the game, never overwrite a running EXE.
public static class LauncherUpdate {
 public sealed class Asset {public string Url,Hash;public long Size;}
 public static string Hash(string file){using(var f=File.OpenRead(file))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
 public static void Safe(string path){for(var p=Path.GetFullPath(path);!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p))if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Launcher update reparse path refused");}
 public static Asset Select(string text,string repository,bool prereleases){
  if(!System.Text.RegularExpressions.Regex.IsMatch(repository,"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))throw new InvalidDataException("Invalid release repository");
  var parser=new JavaScriptSerializer{MaxJsonLength=4*1024*1024};
  var releases=parser.Deserialize<List<Dictionary<string,object>>>(text);
  var release=releases.Where(r=>!(bool)r["draft"]&&(prereleases||!(bool)r["prerelease"]))
   .OrderByDescending(r=>DateTimeOffset.Parse((string)r["published_at"],System.Globalization.CultureInfo.InvariantCulture)).FirstOrDefault();
  if(release==null)throw new InvalidDataException("No published launcher release");
  var assets=((System.Collections.IEnumerable)release["assets"]).Cast<Dictionary<string,object>>().Where(a=>(string)a["name"]=="DynamicFrontlineLauncher.exe").ToArray();
  if(assets.Length!=1)throw new InvalidDataException("Release must contain one launcher");
  var selected=assets[0];var digest=selected.ContainsKey("digest")?selected["digest"] as string:null;
  if(digest==null||!System.Text.RegularExpressions.Regex.IsMatch(digest,"^sha256:[a-fA-F0-9]{64}$"))throw new InvalidDataException("Launcher SHA256 missing");
  var url=new Uri((string)selected["browser_download_url"]);long size=Convert.ToInt64(selected["size"]);
  if(url.Scheme!="https"||url.Host!="github.com"||!url.AbsolutePath.StartsWith("/"+repository+"/releases/download/",StringComparison.Ordinal)||size<=0||size>64*1024*1024)throw new InvalidDataException("Invalid launcher origin or size");
  return new Asset{Url=url.AbsoluteUri,Hash=digest.Substring(7).ToLowerInvariant(),Size=size};
 }
 static byte[] Download(string url,int limit){
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  var request=(HttpWebRequest)WebRequest.Create(url);request.UserAgent="DynamicFrontlineLauncher";request.Timeout=15000;request.ReadWriteTimeout=15000;
  using(var response=(HttpWebResponse)request.GetResponse()){
   if(response.ResponseUri.Scheme!="https"||response.ContentLength>limit)throw new InvalidDataException("Unsafe update response");
   using(var input=response.GetResponseStream())using(var output=new MemoryStream()){
    var buffer=new byte[65536];int n;var deadline=DateTime.UtcNow.AddSeconds(30);
    while((n=input.Read(buffer,0,buffer.Length))>0){if(output.Length+n>limit||DateTime.UtcNow>deadline)throw new InvalidDataException("Update transfer limit");output.Write(buffer,0,n);}return output.ToArray();
   }
  }
 }
 public static string Cache(string workspace,Asset asset,byte[] bytes){
  if(asset.Hash==null||!System.Text.RegularExpressions.Regex.IsMatch(asset.Hash,"^[a-f0-9]{64}$")||asset.Size<=0||asset.Size>64*1024*1024)throw new InvalidDataException("Invalid cache identity");
  if(bytes.LongLength!=asset.Size)throw new InvalidDataException("Launcher size mismatch");
  using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant()!=asset.Hash)throw new InvalidDataException("Launcher checksum mismatch");
  string directory=Path.Combine(workspace,"launcher-updates",asset.Hash);Safe(directory);Directory.CreateDirectory(directory);
  string dest=Path.Combine(directory,"DynamicFrontlineLauncher.exe");Safe(dest);
  if(File.Exists(dest)){if(Hash(dest)!=asset.Hash)throw new InvalidDataException("Cached launcher modified");return dest;}
  string temporary=Path.Combine(directory,Guid.NewGuid().ToString("N")+".part");
  try{using(var f=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}File.Move(temporary,dest);}finally{if(File.Exists(temporary))File.Delete(temporary);}
  if(Hash(dest)!=asset.Hash)throw new InvalidDataException("Cached launcher verification failed");return dest;
 }
 public static string Prepare(string workspace,string executable,string channel){
  var cfg=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(channel);
  if((bool)cfg["Private"])throw new InvalidOperationException("Automatic launcher updates require the public release channel");
  string repository=(string)cfg["Repository"];
  if(!System.Text.RegularExpressions.Regex.IsMatch(repository,"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))throw new InvalidDataException("Invalid repository");
  var asset=Select(Encoding.UTF8.GetString(Download("https://api.github.com/repos/"+repository+"/releases?per_page=100",4*1024*1024)),repository,(bool)cfg["IncludePrerelease"]);
  if(Hash(executable)==asset.Hash)return null;
  return Cache(workspace,asset,Download(asset.Url,64*1024*1024));
 }
}
