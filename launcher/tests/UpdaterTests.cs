using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Web.Script.Serialization;
class UpdaterTests {
 static int count;
 static void Check(bool b){if(!b)throw new Exception("Assertion failed at "+count);count++;}
 static void Reject(Action a){bool rejected=false;try{a();}catch{rejected=true;}Check(rejected);}
 static string Json(string hash,string url,long size,bool draft=false,bool prerelease=true){return new JavaScriptSerializer().Serialize(new[]{new{draft=draft,prerelease=prerelease,published_at="2026-10-31T13:41:53Z",assets=new[]{new{name="DynamicFrontlineLauncher.exe",digest="sha256:"+hash,browser_download_url=url,size=size}}}});}
 static int Main(string[] args){try{
  string repo="IndustKocs/BrokenArrow-DynamicFrontline",url="https://github.com/"+repo+"/releases/download/test/DynamicFrontlineLauncher.exe";
  string executable=Path.GetFullPath(args[0]),root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);
  Check(LauncherFolders.Logs(root,false)==Path.Combine(root,"GameLogs"));
  Check(LauncherFolders.Logs(root,true)==Path.Combine(root,"UserData","DynamicFrontlineSteam","logs"));
  Reject(()=>LauncherFolders.Logs("",true));
  Check(!Directory.Exists(Path.Combine(root,"GameLogs"))&&!Directory.Exists(Path.Combine(root,"UserData")));
  byte[] bytes=File.ReadAllBytes(executable);string hash=LauncherUpdate.Hash(executable);
  var asset=LauncherUpdate.Select(Json(hash,url,bytes.Length),repo,true);Check(asset.Hash==hash);
  string older=Json(hash,url,bytes.Length),newer=Json(hash,url.Replace("/test/","/newer/"),bytes.Length).Replace("13:41:53Z","13:51:10Z");
  string shuffled="["+older.Substring(1,older.Length-2)+","+newer.Substring(1,newer.Length-2)+"]";
  Check(LauncherUpdate.Select(shuffled,repo,true).Url.Contains("/newer/"));

  Reject(()=>LauncherUpdate.Select(Json(hash,url,bytes.Length,true),repo,true));
  Reject(()=>LauncherUpdate.Select(Json(hash,url,bytes.Length),repo,false));
  Check(LauncherUpdate.Select(Json(hash,url,bytes.Length,false,false),repo,false).Hash==hash);
  Reject(()=>LauncherUpdate.Select(Json("invalid",url,bytes.Length),repo,true));
  Reject(()=>LauncherUpdate.Select(Json(hash,url.Replace("https:","http:"),bytes.Length),repo,true));
  Reject(()=>LauncherUpdate.Select(Json(hash,url.Replace(repo,"other/repository"),bytes.Length),repo,true));
  Reject(()=>LauncherUpdate.Select(Json(hash,url,65*1024*1024),repo,true));
  Reject(()=>LauncherUpdate.Select(Json(hash,url,0),repo,true));
  Reject(()=>LauncherUpdate.Select("[]",repo,true));
  Reject(()=>LauncherUpdate.Select(Json(hash,url,bytes.Length),"../invalid",true));
  string cached=LauncherUpdate.Cache(root,asset,bytes);Check(LauncherUpdate.Hash(cached)==hash);
  Check(LauncherUpdate.Cache(root,asset,bytes)==cached);
  var tampered=(byte[])bytes.Clone();tampered[0]^=1;Reject(()=>LauncherUpdate.Cache(root,asset,tampered));
  Reject(()=>LauncherUpdate.Cache(root,asset,new byte[1]));
  string receipt=Path.Combine(root,"successor-self-check.json");
  using(var p=Process.Start(new ProcessStartInfo{FileName=cached,Arguments="--self-check \""+receipt+"\"",UseShellExecute=false,CreateNoWindow=true})){if(!p.WaitForExit(15000))throw new Exception("Self-check timeout");Check(p.ExitCode==0);}
  var result=new JavaScriptSerializer().DeserializeObject(File.ReadAllText(receipt)) as System.Collections.Generic.Dictionary<string,object>;Check(!(bool)result["GameStarted"]);
  File.WriteAllText(cached,"tampered local cache");Reject(()=>LauncherUpdate.Cache(root,asset,bytes));
  Check(LauncherUpdate.Hash(executable)==hash);
  Console.WriteLine("PASS "+count+" updater checks; manifest policy, cache integrity, verified EXE self-check; no game launch/install.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}}
}
