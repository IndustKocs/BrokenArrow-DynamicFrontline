using System.Reflection;
using DynamicFrontline.Poc030;
using DynamicFrontline.Frontline.Rules023;
namespace DynamicFrontline.Steam.MelonLoader;
// Standalone in-game tactical panel, so no undocumented native minimap mutation is required.
public sealed class SteamHud
{
 readonly Type rect=LiveReader.Resolve("UnityEngine.Rect"),gui=LiveReader.Resolve("UnityEngine.GUI"),color=LiveReader.Resolve("UnityEngine.Color");
 readonly MethodInfo box,button,draw; readonly PropertyInfo tint; readonly PropertyInfo whiteTexture; object? texture;
 public bool MapVisible=true,AllOwnZoc,FullDebug;public int Selected;public int LastDrawCalls {get;private set;}
 public SteamHud(){box=gui.GetMethod("Box",[rect,typeof(string)])??throw new MissingMethodException("GUI.Box");button=gui.GetMethod("Button",[rect,typeof(string)])??throw new MissingMethodException("GUI.Button");var tex=LiveReader.Resolve("UnityEngine.Texture");draw=gui.GetMethod("DrawTexture",[rect,tex])??throw new MissingMethodException("GUI.DrawTexture");tint=gui.GetProperty("color")??throw new MissingMemberException("GUI.color");whiteTexture=LiveReader.Resolve("UnityEngine.Texture2D").GetProperty("whiteTexture")??throw new MissingMemberException("Texture2D.whiteTexture");}
 public IDisposable BeginFrame()=>new GuiState(gui,color);
 sealed class GuiState:IDisposable
 {
  readonly List<(PropertyInfo Property,object? Value)> saved=new();
  public GuiState(Type gui,Type color){try{foreach(var name in new[]{"color","backgroundColor","contentColor","enabled","depth"}){var property=gui.GetProperty(name)??throw new MissingMemberException("GUI."+name);saved.Add((property,property.GetValue(null)));property.SetValue(null,name=="enabled"?true:name=="depth"?-10000:Activator.CreateInstance(color,[1f,1f,1f,1f]));}}catch{Dispose();throw;}}
  public void Dispose(){foreach(var entry in saved.AsEnumerable().Reverse())entry.Property.SetValue(null,entry.Value);saved.Clear();}
 }
 object Rect(float x,float y,float w,float h)=>Activator.CreateInstance(rect,[x,y,w,h])!;
 public void Box(float x,float y,float w,float h,string text)=>box.Invoke(null,[Rect(x,y,w,h),text]);
 public bool Button(float x,float y,float w,float h,string text)=>Convert.ToBoolean(button.Invoke(null,[Rect(x,y,w,h),text]));
 void Fill(float x,float y,float w,float h,float r,float g,float b,float a=1){texture??=whiteTexture.GetValue(null)??throw new InvalidOperationException("WhiteTextureNotReady");tint.SetValue(null,Activator.CreateInstance(color,[r,g,b,a]));draw.Invoke(null,[Rect(x,y,w,h),texture]);LastDrawCalls++;}
 public void Render(RuntimeView? view,Observation? own)
 {
  LastDrawCalls=0;if(!MapVisible)return;
  var previous=tint.GetValue(null);try{
   Box(16,292,332,380,FullDebug?"FULL DEBUG — BOTH TEAMS / HIDDEN DATA":"DF tactical map — OWN / UNKNOWN");
   var grid=view?.Display;if(view?.Projection==null||grid==null){Box(26,330,312,200,"No verified paired world\nFront / ZOC / pockets unavailable\nComplete LoadReadProbe on BOTH PCs");return;}
   grid.Validate(FullDebug);int cells=FullDebug?60:30;const float ox=32,oy=330,size=300;float step=size/cells;var owned=new bool[cells*cells];var pockets=new bool[cells*cells];var other=new bool[cells*cells];
   int Pixel(int i)=>Math.Min(cells-1,(i/grid.Width)*cells/grid.Height)*cells+Math.Min(cells-1,(i%grid.Width)*cells/grid.Width);
   foreach(int i in grid.OwnCells)owned[Pixel(i)]=true;foreach(int i in grid.PocketCells)pockets[Pixel(i)]=true;
   foreach(int i in grid.OtherCells??[])other[Pixel(i)]=true;
   Fill(ox,oy,size,size,.12f,.14f,.17f);
   // Coalesce equal raster spans: a filled region costs one draw per row, not one per cell.
   int Paint(int i)=>pockets[i]?3:owned[i]?1:other[i]?2:0;
   for(int y=0;y<cells;y++)for(int x=0;x<cells;){int start=x,paint=Paint(y*cells+x);while(x<cells&&Paint(y*cells+x)==paint)x++;if(paint!=0)Fill(ox+start*step,oy+(cells-1-y)*step,(x-start)*step,step,paint==1?.10f:paint==2?.65f:.90f,paint==1?.38f:paint==2?.12f:.35f,paint==1?.68f:paint==2?.15f:.08f);}
   for(int y=0;y<cells;y++)for(int x=0;x<cells;x++){int i=y*cells+x;
    if(owned[i]){if(x==0||!owned[i-1])Fill(ox+x*step,oy+(cells-1-y)*step,1,step,.4f,.85f,1);if(x==cells-1||!owned[i+1])Fill(ox+(x+1)*step-1,oy+(cells-1-y)*step,1,step,.4f,.85f,1);if(y==0||!owned[i-cells])Fill(ox+x*step,oy+(cells-y)*step-1,step,1,.4f,.85f,1);if(y==cells-1||!owned[i+cells])Fill(ox+x*step,oy+(cells-1-y)*step,step,1,.4f,.85f,1);}
   }
   var chosen=own?.Owned.Length>0?own.Owned[Selected%own.Owned.Length]:null;
   foreach(var z in view.Projection.Circles.Where(c=>FullDebug||!c.IntelligenceEstimate&&(AllOwnZoc||c.Key.Uid==chosen?.NetworkId)).Take(FullDebug?100:20)){
    float cx=ox+(float)((z.Position.X-grid.Origin.X)/(grid.Width*grid.CellSize))*size,cy=oy+size-(float)((z.Position.Y-grid.Origin.Y)/(grid.Height*grid.CellSize))*size;
    float rx=(float)(z.Radius/(grid.Width*grid.CellSize))*size,ry=(float)(z.Radius/(grid.Height*grid.CellSize))*size;
    for(int i=0;i<48;i++){double a=i*Math.PI/24;float x=cx+rx*(float)Math.Cos(a),y=cy+ry*(float)Math.Sin(a);if(x>=ox&&x<ox+size-2&&y>=oy&&y<oy+size-2)Fill(x,y,2,2,z.Team==grid.Team?.25f:1,z.Team==grid.Team?1:.3f,.4f);}
   }
   foreach(var area in grid.LocalAreas??[]){for(int i=0;i<24;i++){double a=i*Math.PI/12;float x=ox+(float)((area.Center.X+area.Radius*Math.Cos(a)-grid.Origin.X)/(grid.Width*grid.CellSize))*size,y=oy+size-(float)((area.Center.Y+area.Radius*Math.Sin(a)-grid.Origin.Y)/(grid.Height*grid.CellSize))*size;if(x>=ox&&x<ox+size-2&&y>=oy&&y<oy+size-2)Fill(x,y,2,2,1,1,.2f);}}
   if(FullDebug){var states=view.Projection.Own.Skip((Selected/12)*12%Math.Max(1,view.Projection.Own.Length)).Take(12);Box(365,330,475,300,"FULL DEBUG: BOTH SIDES / NOT GAME EFFECTS\n"+string.Join("\n",states.Select(u=>$"T{grid.Forecast.FirstOrDefault(f=>f.Unit==u.Key.Uid)?.Team} #{u.Key.Uid}: {u.State}, tau {u.Tau:F1}s, predicted delta {grid.Forecast.FirstOrDefault(f=>f.Unit==u.Key.Uid)?.Delta?.ToString("F2")??"Unknown"}")));}
   if(chosen!=null){float x=ox+(float)((chosen.X-grid.Origin.X)/(grid.Width*grid.CellSize))*size,y=oy+size-(float)((chosen.Z-grid.Origin.Y)/(grid.Height*grid.CellSize))*size;if(x>=ox&&x<ox+size-4&&y>=oy&&y<oy+size-4)Fill(x,y,4,4,1,1,1);}
   tint.SetValue(null,previous);Box(26,635,312,28,FullDebug?"Blue/red territory | orange pockets | yellow local":"Blue own | orange pocket | green ZOC");
  }finally{tint.SetValue(null,previous);}
 }
}
