using System;
using System.IO;
public static class LauncherFolders {
 public static string Logs(string gameRoot,bool mod){
  if(string.IsNullOrWhiteSpace(gameRoot))throw new ArgumentException("Game folder required");
  return Path.Combine(Path.GetFullPath(gameRoot),mod?Path.Combine("UserData","DynamicFrontlineSteam","logs"):"GameLogs");
 }
}
