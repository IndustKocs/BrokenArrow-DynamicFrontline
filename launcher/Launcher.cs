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
[assembly:AssemblyVersion("0.4.4.0")]
public sealed class LauncherForm:Form {
 TextBox path=new TextBox(),log=new TextBox(),token=new TextBox();Label state=new Label();FlowLayoutPanel actions=new FlowLayoutPanel();ProgressBar progress=new ProgressBar();CheckBox keep=new CheckBox();bool busy;string activeSecret="";string workspace,package,tools;JavaScriptSerializer json=new JavaScriptSerializer();
 Panel home=new Panel(),settings=new Panel();Button primary;string primaryAction="Install";
 static readonly Color Gold=Color.FromArgb(231,166,48),Surface=Color.FromArgb(35,39,44),Muted=Color.FromArgb(157,168,181);
 static string Root=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
 public LauncherForm(bool preview,bool skipUpdate=false){
  Text="Dynamic Frontline Launcher · 0.4.4";ClientSize=new Size(480,746);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(27,30,33);ForeColor=Color.WhiteSmoke;
  home.AutoScroll=true;settings.AutoScroll=true;home.Dock=DockStyle.Fill;settings.Dock=DockStyle.Fill;Controls.Add(settings);Controls.Add(home);
  LabelAt(home,"BROKEN ARROW",104,26,330,22,9,Gold,true);
  LabelAt(home,"Dynamic Frontline",102,48,354,38,21,Color.WhiteSmoke,true);
  var logo=new Panel{Left=26,Top=28,Width=62,Height=62};logo.Paint+=(sender,e)=>{e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var brush=new SolidBrush(Gold)){e.Graphics.FillPolygon(brush,new[]{new Point(5,54),new Point(23,8),new Point(31,27),new Point(25,40)});e.Graphics.FillPolygon(brush,new[]{new Point(35,9),new Point(56,54),new Point(37,42),new Point(42,29)});}using(var pen=new Pen(Color.WhiteSmoke,2))e.Graphics.DrawLines(pen,new[]{new Point(32,10),new Point(28,25),new Point(35,36),new Point(30,55)});};home.Controls.Add(logo);
  var card=new Panel{Left=26,Top=114,Width=428,Height=124,BackColor=Surface};home.Controls.Add(card);
  LabelAt(card,"ТЕСТОВАЯ СБОРКА",18,14,390,20,9,Muted,true);
  LabelAt(card,"Dynamic Frontline 0.4",18,38,390,30,16,Color.WhiteSmoke,true);
  state.SetBounds(18,76,390,40);state.Font=new Font("Segoe UI",9);state.ForeColor=Muted;state.Text="Совместимость проверяется перед установкой.";card.Controls.Add(state);
  actions.SetBounds(26,258,428,58);actions.Margin=Padding.Empty;actions.Padding=Padding.Empty;home.Controls.Add(actions);
  primary=StyledButton("УСТАНОВИТЬ МОД",0,0,428,56,true);primary.Margin=Padding.Empty;primary.Click+=async(sender,e)=>await Run(primaryAction);actions.Controls.Add(primary);
  var updates=StyledButton("Проверить обновления",26,328,210,38,false);updates.Click+=async(sender,e)=>await Run("CheckUpdates");home.Controls.Add(updates);
  var verify=StyledButton("Проверить файлы",244,328,210,38,false);verify.Click+=async(sender,e)=>await Run("Verify");home.Controls.Add(verify);
  var gameLogs=StyledButton("Логи игры  ↗",26,380,210,38,false);gameLogs.Click+=(sender,e)=>OpenLogs(false);home.Controls.Add(gameLogs);
  var modLogs=StyledButton("Логи мода  ↗",244,380,210,38,false);modLogs.Click+=(sender,e)=>OpenLogs(true);home.Controls.Add(modLogs);
  var warning=new Panel{Left=26,Top=436,Width=428,Height=62,BackColor=Color.FromArgb(49,41,26)};home.Controls.Add(warning);
  LabelAt(warning,"ЗАПУСКАТЬ БЕЗ АНТИЧИТА",14,8,400,22,10,Gold,true);
  LabelAt(warning,"В Steam выберите Anti-Cheat Disabled.",14,32,400,22,9,Color.FromArgb(220,204,171),false);
  progress.SetBounds(26,514,428,4);home.Controls.Add(progress);
  log.SetBounds(26,530,428,110);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BorderStyle=BorderStyle.None;log.Font=new Font("Segoe UI",9);log.BackColor=Color.FromArgb(22,25,28);log.ForeColor=Muted;home.Controls.Add(log);
  var options=StyledButton("Настройки",26,658,152,36,false);options.Click+=(sender,e)=>{if(!busy){home.Hide();settings.Show();settings.BringToFront();}};home.Controls.Add(options);
  var collect=StyledButton("Экспорт диагностики",186,658,268,36,false);collect.Click+=async(sender,e)=>await Run("Collect");home.Controls.Add(collect);
  LabelAt(home,"Лаунчер не запускает игру · 0.4.4",26,708,428,22,9,Muted,false);
  LabelAt(settings,"Настройки",26,24,428,42,23,Color.WhiteSmoke,true);
  LabelAt(settings,"ПАПКА BROKEN ARROW",26,88,428,22,9,Muted,true);
  path.SetBounds(26,118,428,28);path.BackColor=Surface;path.ForeColor=Color.WhiteSmoke;path.BorderStyle=BorderStyle.FixedSingle;settings.Controls.Add(path);
  var browse=StyledButton("Обзор…",26,158,144,36,false);browse.Click+=(sender,e)=>{if(busy)return;using(var d=new FolderBrowserDialog()){if(d.ShowDialog()==DialogResult.OK)path.Text=d.SelectedPath;}};settings.Controls.Add(browse);
  var find=StyledButton("Найти автоматически",178,158,276,36,false);find.Click+=(sender,e)=>{if(!busy)path.Text=FindGame();};settings.Controls.Add(find);
  LabelAt(settings,"ОБСЛУЖИВАНИЕ МОДА",26,226,428,22,9,Muted,true);
  var recover=StyledButton("Восстановить после сбоя",26,260,428,38,false);recover.Click+=async(sender,e)=>await Run("Recover");settings.Controls.Add(recover);
  keep.SetBounds(26,310,428,46);keep.Text="Сохранить копию настроек при удалении\n(вне папки игры)";keep.Checked=true;keep.Font=new Font("Segoe UI",9);settings.Controls.Add(keep);
  var remove=StyledButton("Удалить мод…",26,368,428,40,false);remove.ForeColor=Color.FromArgb(239,125,113);remove.Click+=async(sender,e)=>await Run("Remove");settings.Controls.Add(remove);
  LabelAt(settings,"Удаление сохраняет посторонние файлы.\nЛаунчер остаётся на компьютере.",26,422,428,44,9,Muted,false);
  LabelAt(settings,"GITHUB TOKEN · ТОЛЬКО ДЛЯ PRIVATE RELEASES",26,502,428,22,8,Muted,true);
  token.SetBounds(26,534,428,28);token.UseSystemPasswordChar=true;token.BackColor=Surface;token.ForeColor=Color.WhiteSmoke;token.BorderStyle=BorderStyle.FixedSingle;settings.Controls.Add(token);
  LabelAt(settings,"Для публичного репозитория не нужен. Не сохраняется.",26,572,428,38,9,Muted,false);
  LabelAt(settings,"Запуск через Steam: Anti-Cheat Disabled",26,630,428,28,10,Gold,true);
  var back=StyledButton("Готово",26,680,428,40,true);back.Click+=(sender,e)=>{settings.Hide();home.Show();home.BringToFront();};settings.Controls.Add(back);settings.Hide();
  FormClosing+=(sender,e)=>{if(busy){e.Cancel=true;MessageBox.Show("Дождитесь завершения операции. При аварии используйте восстановление.");}};
  if(preview){path.Text=@"D:\SteamLibrary\steamapps\common\broken_arrow";state.Text="Установлена · full-debug7\nПриватный Scenario · LoadReadProbe";primary.Text="ОБНОВИТЬ МОД";log.Text="Готов к работе.\r\nПроверка файлов доступна перед тестом.";return;}
  workspace=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DynamicFrontlineLauncher");
  Safe(workspace);Directory.CreateDirectory(workspace);var version=HashResource("package.zip").Substring(0,16);tools=Path.Combine(workspace,"launcher-"+version);Directory.CreateDirectory(tools);Safe(tools);
  foreach(var name in new[]{"bootstrap.ps1","controller.ps1","release-channel.json"})Extract(name,Path.Combine(tools,name));
  package=Path.Combine(tools,"package-"+Guid.NewGuid().ToString("N"));if(!Directory.Exists(package)){string z=Path.Combine(tools,"package.zip");Extract("package.zip",z);ZipFile.ExtractToDirectory(z,package);}
  path.Text=FindGame();RefreshState();
  if(!skipUpdate)Shown+=async(s,e)=>await UpdateLauncher();
 }
 async Task UpdateLauncher(){
  busy=true;Enabled=false;state.Text="Проверка обновлений лаунчера…";progress.Style=ProgressBarStyle.Marquee;
  try{
   var successor=await Task.Run(()=>LauncherUpdate.Prepare(workspace,Assembly.GetExecutingAssembly().Location,File.ReadAllText(Path.Combine(tools,"release-channel.json"))));
   if(successor!=null){LauncherUpdate.Safe(successor);string hash=LauncherUpdate.Hash(successor);if(Path.GetFileName(Path.GetDirectoryName(successor))!=hash)throw new IOException("Update cache identity mismatch");
    Process.Start(new ProcessStartInfo{FileName=successor,Arguments="--verified-update "+hash,UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(successor)});
    busy=false;Close();return;
   }
   Append("Лаунчер актуален. Сборка мода обновляется кнопкой «Обновить».");
  }catch(Exception){Append("Автообновление лаунчера недоступно или не прошло проверку. Текущая версия сохранена; проверка файлов, удаление и восстановление доступны. Повторите запуск позже.");}
  finally{busy=false;if(!IsDisposed){Enabled=true;progress.Style=ProgressBarStyle.Blocks;RefreshState();}}
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

 static void LabelAt(Control parent,string text,int x,int y,int w,int h,float size,Color color,bool bold){parent.Controls.Add(new Label{Text=text,Left=x,Top=y,Width=w,Height=h,ForeColor=color,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular)});}
 static Button StyledButton(string text,int x,int y,int w,int h,bool accent){var b=new Button{Text=text,Left=x,Top=y,Width=w,Height=h,FlatStyle=FlatStyle.Flat,BackColor=accent?Gold:Surface,ForeColor=accent?Color.FromArgb(24,25,27):Color.WhiteSmoke,Font=new Font("Segoe UI",accent?12:9,accent?FontStyle.Bold:FontStyle.Regular),Cursor=Cursors.Hand,UseVisualStyleBackColor=false};b.FlatAppearance.BorderColor=accent?Gold:Color.FromArgb(55,60,67);b.FlatAppearance.BorderSize=1;return b;}
 void OpenLogs(bool mod){try{if(string.IsNullOrWhiteSpace(path.Text)){MessageBox.Show("Выберите папку игры в настройках.");return;}string folder=LauncherFolders.Logs(path.Text,mod);Safe(folder);if(!Directory.Exists(folder)){MessageBox.Show(mod?"Логи мода ещё не созданы. Они появятся после его запуска.":"Логи игры ещё не созданы. Проверьте папку игры в настройках.");return;}Process.Start(new ProcessStartInfo{FileName=folder,UseShellExecute=true});}catch(Exception e){MessageBox.Show("Не удалось открыть папку: "+e.Message);}}
 void Append(string text){if(IsDisposed)return;if(activeSecret.Length>0)text=text.Replace(activeSecret,"[REDACTED]");BeginInvoke((Action)(()=>{if(log.TextLength>24000)log.Clear();log.AppendText(text+Environment.NewLine);}));}
 void RefreshState(){string f=Path.Combine(workspace,"installed-launcher-state.json");primaryAction="Install";primary.Text="УСТАНОВИТЬ МОД";state.Text="Не установлена · совместимость будет проверена";if(File.Exists(f)){var d=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(f));var current=Convert.ToString(d["State"]);state.Text=Convert.ToString(d["Version"])+"\n"+(current=="Installed"?"Установлена · приватный Scenario":current=="Pending"?"Незавершённая операция":current=="Removed"?"Мод удалён":current);if(current=="Installed"){primaryAction="Update";primary.Text="ОБНОВИТЬ МОД";}else if(current=="Pending"){primaryAction="Recover";primary.Text="ВОССТАНОВИТЬ УСТАНОВКУ";}}}
 async Task Run(string action){if(busy)return;if(!Directory.Exists(path.Text)){home.Hide();settings.Show();settings.BringToFront();MessageBox.Show("Выберите папку Broken Arrow в настройках.");return;}
  bool mutation=action=="Install"||action=="Update"||action=="Remove"||action=="Recover";
  if(mutation&&MessageBox.Show("Разрешить операцию «"+action+"» в папке:\n"+path.Text+"\n\nУстановка/обновление включает MelonLoader и необходимые зависимости. Игра должна быть закрыта. Игра не будет запущена.","Явное согласие",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
  settings.Hide();home.Show();home.BringToFront();
  busy=true;actions.Enabled=false;path.Enabled=false;token.Enabled=false;activeSecret=token.Text;progress.Style=ProgressBarStyle.Marquee;
  try{Safe(path.Text);string request=Path.Combine(tools,"request-"+Guid.NewGuid().ToString("N")+".json");File.WriteAllText(request,json.Serialize(new{Action=action,GameRoot=Path.GetFullPath(path.Text),Workspace=workspace,Package=package,Consent=mutation,KeepSettings=keep.Checked,Sandbox=false}),new UTF8Encoding(false));
   var start=new ProcessStartInfo{FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe"),Arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+Path.Combine(tools,"bootstrap.ps1")+"\" -Request \""+request+"\"",UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   if(!string.IsNullOrWhiteSpace(token.Text))start.EnvironmentVariables["DF_GITHUB_TOKEN"]=token.Text;
   int code=await Task.Run(()=>{using(var p=new Process{StartInfo=start}){p.OutputDataReceived+=(s,e)=>{if(e.Data!=null)Append(e.Data);};p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)Append(e.Data);};p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();p.WaitForExit();return p.ExitCode;}});
   MessageBox.Show(code==0?"Операция завершена. Игра не запускалась.":"Операция остановлена. Подробности в журнале. При прерванной установке нажмите «Восстановить после сбоя».");RefreshState();
  }catch(Exception e){MessageBox.Show(e.Message);}finally{busy=false;actions.Enabled=true;path.Enabled=true;token.Enabled=true;activeSecret="";progress.Style=ProgressBarStyle.Blocks;}
 }
 [STAThread]public static void Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{if(args.Length==2&&args[0]=="--self-check"){File.WriteAllText(args[1],new JavaScriptSerializer().Serialize(new{GameFound=!string.IsNullOrEmpty(FindGame()),EmbeddedPackageSha256=HashResource("package.zip"),GameStarted=false}));return;}bool preview=args.Length==2&&(args[0]=="--preview"||args[0]=="--preview-settings");using(var f=new LauncherForm(preview,args.Length==2&&args[0]=="--verified-update"&&args[1]==LauncherUpdate.Hash(Assembly.GetExecutingAssembly().Location))){if(preview){if(args[0]=="--preview-settings"){f.home.Hide();f.settings.Show();f.settings.BringToFront();}f.Show();Application.DoEvents();using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));b.Save(Path.GetFullPath(args[1]));}return;}Application.Run(f);}}catch(Exception e){MessageBox.Show("Лаунчер остановлен: "+e.Message);}}
}
