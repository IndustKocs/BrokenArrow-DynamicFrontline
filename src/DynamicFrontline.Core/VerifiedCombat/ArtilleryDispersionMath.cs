using System.Collections.ObjectModel;
using System.Numerics;
namespace DynamicFrontline.Core.VerifiedCombat;

public readonly record struct CurveKey(float Time,float Value);
public interface ICurveEvaluator { float Evaluate(float x); string Identity {get;} string Interpolation {get;} }
public interface IInverseCurveFactory { ICurveEvaluator Create(ReadOnlySpan<CurveKey> keys); string Identity {get;} }
public interface IFloatRandom { float NextFloat(float minimum,float maximum); long ValuesConsumed {get;} }
public readonly record struct CurveSource(string SourceId,string Version,string ContentHash);
public sealed class CorrectionTable
{
    private readonly float[] values;
    public CurveSource Source {get;}public string SourceEvaluator {get;}public string InverseInterpolation {get;}
    public ReadOnlyCollection<CurveKey> InverseKeys {get;}public int Count=>values.Length;
    internal CorrectionTable(CurveSource source,string evaluator,string interpolation,CurveKey[] keys,float[] table)
    {Source=source;SourceEvaluator=evaluator;InverseInterpolation=interpolation;InverseKeys=Array.AsReadOnly(keys);values=table;}
    public float this[int index]=>index>=0&&index<1000?values[index]:throw new FormulaStageException("Polar.TableLookup","Index outside 0..999; upper endpoint is not clamped");
    public float[] CopyValues()=>values.ToArray();
}
public sealed class FormulaStageException(string stage,string message):ArgumentException(message){public string Stage {get;}=stage;}
public readonly record struct PolarSample(float AngleDegrees,float IndexSample,int Index,float Radius,long ValuesConsumed);
public readonly record struct DiamondSample(float XRaw,int QuantizedAbsoluteX,float YMinimum,float YMaximum,float YRaw,Vector2 Result,long ValuesConsumed);
public readonly record struct TrajectoryDeviation(Vector3 Direction,float DistanceDelta,Vector3 Normal,float HorizontalScale,float VerticalScale,bool Applied);
public static class ArtilleryDispersionMath
{
    public const float MinimumDistanceDelta=1e-5f; // dependencies float:0x186796cc0, bits 0x3727C5AC
    // P4-F01 #21700 B. Supplied evaluators determine interpolation, never assume Unity equivalence.
    public static CorrectionTable Build(CurveSource source,ICurveEvaluator cdf,IInverseCurveFactory inverseFactory)
    {
        ArgumentNullException.ThrowIfNull(cdf);ArgumentNullException.ThrowIfNull(inverseFactory);
        if(string.IsNullOrWhiteSpace(source.SourceId)||string.IsNullOrWhiteSpace(source.Version)||source.ContentHash==null||source.ContentHash.Length!=64||!source.ContentHash.All(Uri.IsHexDigit))throw new FormulaStageException("CDF.Source","Explicit source/version/SHA256 required");
        var keys=new CurveKey[10001];float previous=-1;
        for(int j=0;j<=10000;j++){float x=j/10000f,y=cdf.Evaluate(x);if(!float.IsFinite(y)||y<=previous)throw new FormulaStageException("CDF.Validate","CDF must strictly increase from initial previous=-1; library rejects rather than emulating Debug.Break");keys[j]=new(y,x);previous=y;}
        var inverse=inverseFactory.Create(keys);var table=new float[1000];
        for(int i=0;i<1000;i++){table[i]=inverse.Evaluate(MathF.Sqrt(i/1000f));if(!float.IsFinite(table[i]))throw new FormulaStageException("CDF.SampleInverse","Nonfinite inverse result");}
        return new(source,cdf.Identity,inverse.Interpolation,keys,table);
    }
    public static PolarSample Polar(CorrectionTable table,IFloatRandom random)
    {
        ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(random);long before=random.ValuesConsumed;
        float angle=random.NextFloat(0,360),raw=random.NextFloat(0,1000);
        if(!float.IsFinite(angle)||angle<0||angle>360||!float.IsFinite(raw)||raw<0||raw>1000)throw new FormulaStageException("Polar.RNG","RNG value outside requested bounds");
        int index=(int)MathF.Truncate(raw);return new(angle,raw,index,table[index],random.ValuesConsumed-before);
    }
    // P4-F03 #24224 B: preserve truncation and equal-bound NextFloat semantics.
    public static DiamondSample FastDimond(IFloatRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);long before=random.ValuesConsumed;float x=random.NextFloat(-1000,1000);
        if(!float.IsFinite(x)||x< -1000||x>1000)throw new FormulaStageException("FastDimond.X","Invalid RNG output");
        int q=(int)MathF.Truncate(MathF.Abs(x));float min=q-1000,max=1000-q,y=random.NextFloat(min,max);
        if(!float.IsFinite(y)||y<min||y>max)throw new FormulaStageException("FastDimond.Y","Invalid RNG output");
        return new(x,q,min,max,y,new(x/1000,y/1000),random.ValuesConsumed-before);
    }
    public static TrajectoryDeviation Apply(Vector3 direction,float distanceDelta,float effectiveRange,float horizontalRadius,float verticalRadius,float u,float v)
    {
        VectorMath.Validate(direction);FormulaDomain.Finite(distanceDelta,nameof(distanceDelta));FormulaDomain.Finite(effectiveRange,nameof(effectiveRange));
        if(effectiveRange<=0)return new(direction,distanceDelta,default,0,1,false);
        FormulaDomain.Nonnegative(horizontalRadius,nameof(horizontalRadius));FormulaDomain.Nonnegative(verticalRadius,nameof(verticalRadius));FormulaDomain.Finite(u,nameof(u));FormulaDomain.Finite(v,nameof(v));
        var cross=Vector3.Cross(direction,Vector3.UnitY);float length=FormulaDomain.Result(cross.Length());
        if(length<=0)throw new FormulaStageException("Trajectory.Geometry","Degenerate cross outside supported domain");
        var normal=cross/length;float horizontal=FormulaDomain.Result(u*horizontalRadius/effectiveRange),vertical=FormulaDomain.Result(1+v*verticalRadius/effectiveRange);
        var result=direction+normal*horizontal;VectorMath.Validate(result);
        float delta=distanceDelta*vertical;if(float.IsNaN(delta))throw new FormulaStageException("Trajectory.DistanceDelta","Indeterminate value");
        return new(result,Math.Clamp(delta,MinimumDistanceDelta,float.MaxValue),normal,horizontal,vertical,true);
    }
    public static TrajectoryDeviation GenerateAndApply(Vector3 direction,float distanceDelta,float range,float rh,float rv,Func<Vector2> deviation)
    {
        FormulaDomain.Finite(range,nameof(range));if(range<=0)return Apply(direction,distanceDelta,range,rh,rv,0,0);
        ArgumentNullException.ThrowIfNull(deviation);var sample=deviation();return Apply(direction,distanceDelta,range,rh,rv,sample.X,sample.Y);
    }
}
public sealed class CorrectionTableCache
{
    private readonly Dictionary<(CurveSource,string,string),CorrectionTable> tables=new();private readonly int maximumEntries;
    public CorrectionTableCache(int maximumEntries=16){if(maximumEntries<=0)throw new ArgumentOutOfRangeException(nameof(maximumEntries));this.maximumEntries=maximumEntries;}
    // Source fingerprint and evaluator identities must identify immutable inputs; no global active-game cache.
    public CorrectionTable Get(CurveSource source,ICurveEvaluator cdf,IInverseCurveFactory inverse)
    {var key=(source,cdf.Identity,inverse.Identity);if(tables.TryGetValue(key,out var found))return found;var table=ArtilleryDispersionMath.Build(source,cdf,inverse);if(tables.Count==maximumEntries)tables.Clear();tables.Add(key,table);return table;}
}
