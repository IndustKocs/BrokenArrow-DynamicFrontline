using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Microsoft.Win32;
[assembly:AssemblyTitle("Dynamic Frontline Launcher")]
[assembly:AssemblyVersion("0.4.0.0")]
public sealed class LauncherForm:Form {
 TextBox path=new TextBox(),log=new TextBox(),token=new TextBox();Label state=new Label();FlowLayoutPanel actions=new FlowLayoutPanel();ProgressBar progress=new ProgressBar();CheckBox keep=new CheckBox();bool busy;string activeSecret="";string workspace,package,tools;JavaScriptSerializer json=new JavaScriptSerializer();
 static string Root=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
 public LauncherForm(bool preview){
  Text="Dynamic Frontline Launcher · 0.4.0";ClientSize=new Size(780,560);MinimumSize=new Size(796,600);Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(24,28,34);ForeColor=Color.WhiteSmoke;
  var title=new Label{Text="DYNAMIC FRONTLINE",Font=new Font("Segoe UI",20,FontStyle.Bold),Left=24,Top=18,Width=700,Height=42};Controls.Add(title);
  Controls.Add(new Label{Text="Приватный Scenario · установщик и обслуживание",Left=26,Top=64,Width=700,Height=28});
  path.SetBounds(26,106,610,30);Controls.Add(path);var browse=new Button{Text="Папка…",Left=644,Top=104,Width=108,Height=32};browse.Click+=(s,e)=>{using(var d=new FolderBrowserDialog()){if(d.ShowDialog()==DialogResult.OK)path.Text=d.SelectedPath;}};Controls.Add(browse);
  state.SetBounds(26,148,726,44);state.Text="Совместимость: проверяется перед установкой. Игра не запускается.";Controls.Add(state);
  actions.SetBounds(22,200,738,86);Controls.Add(actions);
  Add("Установить","Install");Add("Проверить файлы","Verify");Add("Проверить обновления","CheckUpdates");Add("Обновить","Update");Add("Удалить","Remove");Add("Восстановить после сбоя","Recover");
  keep.SetBounds(26,286,610,26);keep.Text="Сохранить копию настроек при удалении (вне игры)";keep.Checked=true;Controls.Add(keep);var collect=new Button{Text="Собрать логи",Left=642,Top=282,Width=110,Height=30};collect.Click+=async(s,e)=>await Run("Collect");Controls.Add(collect);
  Controls.Add(new Label{Text="GitHub token: private Releases, чтение (не сохраняется)",Left=26,Top=318,Width=410,Height=26});token.SetBounds(440,316,310,28);token.UseSystemPasswordChar=true;Controls.Add(token);
  progress.SetBounds(26,358,726,10);Controls.Add(progress);
  log.SetBounds(26,380,726,108);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BackColor=Color.FromArgb(15,18,23);log.ForeColor=Color.Gainsboro;Controls.Add(log);
  Controls.Add(new Label{Text="Используйте модификацию на собственный риск. Соблюдайте правила игры.\nЛаунчер не запускает Broken Arrow; запуск — самостоятельно через Steam.",Left=26,Top=500,Width=726,Height=45,ForeColor=Color.FromArgb(245,195,100)});
  FormClosing+=(s,e)=>{if(busy){e.Cancel=true;MessageBox.Show("Дождитесь завершения операции. При аварии используйте восстановление.");}};
  if(preview){path.Text=@"D:\SteamLibrary\steamapps\common\broken_arrow";state.Text="Локальная сборка · FullDebugView требует согласия обоих игроков";log.Text="Готов к проверке совместимости. Изменений игры не выполнено.";return;}
  workspace=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DynamicFrontlineLauncher");
  Safe(workspace);Directory.CreateDirectory(workspace);var version=HashResource("package.zip").Substring(0,16);tools=Path.Combine(workspace,"launcher-"+version);Directory.CreateDirectory(tools);Safe(tools);
  foreach(var name in new[]{"bootstrap.ps1","controller.ps1","release-channel.json"})Extract(name,Path.Combine(tools,name));
  package=Path.Combine(tools,"package-"+Guid.NewGuid().ToString("N"));if(!Directory.Exists(package)){string z=Path.Combine(tools,"package.zip");Extract("package.zip",z);ZipFile.ExtractToDirectory(z,package);}
  path.Text=FindGame();RefreshState();
 }
 static void Safe(string p){for(p=Path.GetFullPath(p);!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p))if((Directory.Exists(p)||File.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Reparse path refused");}
 static string HashResource(string n){using(var f=Assembly.GetExecutingAssembly().GetManifestResourceStream(n))using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");}
 static void Extract(string name,string to){Safe(to);using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))using(var output=new FileStream(to,FileMode.Create,FileAccess.Write,FileShare.None))input.CopyTo(output);}
 static string FindGame(){
  var roots=new List<string>();
  using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")){if(key!=null&&key.GetValue("SteamPath") is string)roots.Add((string)key.GetValue("SteamPath"));}
  foreach(string location in new[]{@"SOFTWARE\WOW6432Node\Valve\Steam",@"SOFTWARE\Valve\Steam"})using(var key=Registry.LocalMachine.OpenSubKey(location)){if(key!=null&&key.GetValue("InstallPath") is string)roots.Add((string)key.GetValue("InstallPath"));}
  roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
  var candidates=new List<string>(roots);
  foreach(string steam in roots.Distinct(StringComparer.OrdinalIgnoreCase)){string vdf=Path.Combine(steam,@"steamapps\libraryfolders.vdf");if(File.Exists(vdf))foreach(System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(vdf),"\"path\"\\s+\"([^\"]+)\""))candidates.Add(m.Groups[1].Value.Replace(@"\\",@"\"));}
  return candidates.Select(c=>Path.Combine(c,@"steamapps\common\broken_arrow")).FirstOrDefault(c=>File.Exists(Path.Combine(c,"BrokenArrow.exe")))??"";
 }

 void Add(string title,string command){var b=new Button{Text=title,Width=230,Height=34,FlatStyle=FlatStyle.Flat};b.Click+=async(s,e)=>await Run(command);actions.Controls.Add(b);}
 void Append(string text){if(IsDisposed)return;if(activeSecret.Length>0)text=text.Replace(activeSecret,"[REDACTED]");BeginInvoke((Action)(()=>{if(log.TextLength>24000)log.Clear();log.AppendText(text+Environment.NewLine);}));}
 void RefreshState(){string f=Path.Combine(workspace,"installed-launcher-state.json");if(File.Exists(f)){var d=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(f));state.Text="Версия: "+d["Version"]+" · "+d["State"];}else state.Text="Мод этим лаунчером не установлен. Совместимость проверяется перед изменениями.";}
 async Task Run(string action){if(busy)return;if(!Directory.Exists(path.Text)){MessageBox.Show("Выберите папку Broken Arrow.");return;}
  bool mutation=action=="Install"||action=="Update"||action=="Remove"||action=="Recover";
  if(mutation&&MessageBox.Show("Разрешить операцию «"+action+"» в папке:\n"+path.Text+"\n\nУстановка/обновление включает MelonLoader и необходимые зависимости. Игра должна быть закрыта. Игра не будет запущена.","Явное согласие",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
  busy=true;actions.Enabled=false;path.Enabled=false;token.Enabled=false;activeSecret=token.Text;progress.Style=ProgressBarStyle.Marquee;
  try{Safe(path.Text);string request=Path.Combine(tools,"request-"+Guid.NewGuid().ToString("N")+".json");File.WriteAllText(request,json.Serialize(new{Action=action,GameRoot=Path.GetFullPath(path.Text),Workspace=workspace,Package=package,Consent=mutation,KeepSettings=keep.Checked,Sandbox=false}),new UTF8Encoding(false));
   var start=new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),Arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(tools,"bootstrap.ps1")+"\" -Request \""+request+"\"",UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   if(!string.IsNullOrWhiteSpace(token.Text))start.EnvironmentVariables["DF_GITHUB_TOKEN"]=token.Text;
   int code=await Task.Run(()=>{using(var p=new Process{StartInfo=start}){p.OutputDataReceived+=(s,e)=>{if(e.Data!=null)Append(e.Data);};p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)Append(e.Data);};p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();p.WaitForExit();return p.ExitCode;}});
   MessageBox.Show(code==0?"Операция завершена. Игра не запускалась.":"Операция остановлена. Подробности в журнале. При прерванной установке нажмите «Восстановить после сбоя».");RefreshState();
  }catch(Exception e){MessageBox.Show(e.Message);}finally{busy=false;actions.Enabled=true;path.Enabled=true;token.Enabled=true;activeSecret="";progress.Style=ProgressBarStyle.Blocks;}
 }
 [STAThread]public static void Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{if(args.Length==2&&args[0]=="--self-check"){File.WriteAllText(args[1],new JavaScriptSerializer().Serialize(new{GameFound=!string.IsNullOrEmpty(FindGame()),EmbeddedPackageSha256=HashResource("package.zip"),GameStarted=false}));return;}bool preview=args.Length==2&&args[0]=="--preview";using(var f=new LauncherForm(preview)){if(preview){f.Show();Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.GetFullPath(args[1]));}return;}Application.Run(f);}}catch(Exception e){MessageBox.Show("Лаунчер остановлен: "+e.Message);}}
}
