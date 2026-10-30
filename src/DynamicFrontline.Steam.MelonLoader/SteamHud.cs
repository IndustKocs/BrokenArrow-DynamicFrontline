using System.Reflection;
using DynamicFrontline.Poc030;
using DynamicFrontline.Frontline.Rules023;
namespace DynamicFrontline.Steam.MelonLoader;
// Standalone in-game tactical panel, so no undocumented native minimap mutation is required.
public sealed class SteamHud
{
 readonly Type rect=LiveReader.Resolve("UnityEngine.Rect"),gui=LiveReader.Resolve("UnityEngine.GUI"),color=LiveReader.Resolve("UnityEngine.Color");
 readonly MethodInfo box,button,draw; readonly PropertyInfo tint; readonly object texture;
 public bool MapVisible=true,AllOwnZoc;public int Selected;public int LastDrawCalls {get;private set;}
 public SteamHud(){box=gui.GetMethod("Box",[rect,typeof(string)])!;button=gui.GetMethod("Button",[rect,typeof(string)])!;var tex=LiveReader.Resolve("UnityEngine.Texture");draw=gui.GetMethod("DrawTexture",[rect,tex])!;tint=gui.GetProperty("color")!;texture=LiveReader.Resolve("UnityEngine.Texture2D").GetProperty("whiteTexture")!.GetValue(null)!;if(box==null||button==null||draw==null)throw new MissingMethodException("Unity GUI binding");}
 object Rect(float x,float y,float w,float h)=>Activator.CreateInstance(rect,[x,y,w,h])!;
 public void Box(float x,float y,float w,float h,string text)=>box.Invoke(null,[Rect(x,y,w,h),text]);
 public bool Button(float x,float y,float w,float h,string text)=>Convert.ToBoolean(button.Invoke(null,[Rect(x,y,w,h),text]));
 void Fill(float x,float y,float w,float h,float r,float g,float b,float a=1){tint.SetValue(null,Activator.CreateInstance(color,[r,g,b,a]));draw.Invoke(null,[Rect(x,y,w,h),texture]);LastDrawCalls++;}
 public void Render(RuntimeView? view,Observation? own)
 {
  LastDrawCalls=0;if(!MapVisible)return;
  var previous=tint.GetValue(null);try{
   Box(16,292,332,380,"DF tactical map — OWN / UNKNOWN");
   var grid=view?.Display;if(view?.Projection==null||grid==null){Box(26,330,312,200,"No verified paired world\nFront / ZOC / pockets unavailable\nComplete LoadReadProbe on BOTH PCs");return;}
   grid.Validate();const int cells=30;const float ox=32,oy=330,size=300,step=size/cells;var owned=new bool[cells*cells];var pockets=new bool[cells*cells];
   int Pixel(int i)=>Math.Min(cells-1,(i/grid.Width)*cells/grid.Height)*cells+Math.Min(cells-1,(i%grid.Width)*cells/grid.Width);
   foreach(int i in grid.OwnCells)owned[Pixel(i)]=true;foreach(int i in grid.PocketCells)pockets[Pixel(i)]=true;
   Fill(ox,oy,size,size,.12f,.14f,.17f);
   for(int y=0;y<cells;y++)for(int x=0;x<cells;x++){int i=y*cells+x;if(owned[i])Fill(ox+x*step,oy+(cells-1-y)*step,step,step,.10f,.38f,.68f);if(pockets[i])Fill(ox+x*step,oy+(cells-1-y)*step,step,step,.90f,.35f,.08f,.8f);
    if(owned[i]){if(x==0||!owned[i-1])Fill(ox+x*step,oy+(cells-1-y)*step,1,step,.4f,.85f,1);if(x==cells-1||!owned[i+1])Fill(ox+(x+1)*step-1,oy+(cells-1-y)*step,1,step,.4f,.85f,1);if(y==0||!owned[i-cells])Fill(ox+x*step,oy+(cells-y)*step-1,step,1,.4f,.85f,1);if(y==cells-1||!owned[i+cells])Fill(ox+x*step,oy+(cells-1-y)*step,step,1,.4f,.85f,1);}
   }
   var chosen=own?.Owned.Length>0?own.Owned[Selected%own.Owned.Length]:null;
   foreach(var z in view.Projection.Circles.Where(c=>!c.IntelligenceEstimate&&(AllOwnZoc||c.Key.Uid==chosen?.NetworkId)).Take(20)){
    float cx=ox+(float)((z.Position.X-grid.Origin.X)/(grid.Width*grid.CellSize))*size,cy=oy+size-(float)((z.Position.Y-grid.Origin.Y)/(grid.Height*grid.CellSize))*size;
    float rx=(float)(z.Radius/(grid.Width*grid.CellSize))*size,ry=(float)(z.Radius/(grid.Height*grid.CellSize))*size;
    for(int i=0;i<48;i++){double a=i*Math.PI/24;float x=cx+rx*(float)Math.Cos(a),y=cy+ry*(float)Math.Sin(a);if(x>=ox&&x<ox+size-2&&y>=oy&&y<oy+size-2)Fill(x,y,2,2,.25f,1,.4f);}
   }
   if(chosen!=null){float x=ox+(float)((chosen.X-grid.Origin.X)/(grid.Width*grid.CellSize))*size,y=oy+size-(float)((chosen.Z-grid.Origin.Y)/(grid.Height*grid.CellSize))*size;if(x>=ox&&x<ox+size-4&&y>=oy&&y<oy+size-4)Fill(x,y,4,4,1,1,1);}
   tint.SetValue(null,previous);Box(26,635,312,28,"Blue own | orange pocket | green ZOC");
  }finally{tint.SetValue(null,previous);}
 }
}
