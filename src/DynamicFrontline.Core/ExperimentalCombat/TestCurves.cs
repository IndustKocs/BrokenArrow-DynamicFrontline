using System.Numerics;
using DynamicFrontline.Core.VerifiedCombat;
namespace DynamicFrontline.Core.ExperimentalCombat;
// Explicit deterministic approximation, not Unity AnimationCurve tangents/wrapping.
public sealed class LinearTestCurve:ICurveEvaluator
{
    private readonly CurveKey[] keys;public string Identity {get;}public string Interpolation=>"Experimental piecewise linear; reject out-of-domain";
    public LinearTestCurve(ReadOnlySpan<CurveKey> input)
    {
        if(input.Length<2)throw new ArgumentException("Need two keys");keys=input.ToArray();
        for(int i=0;i<keys.Length;i++){FormulaDomain.Finite(keys[i].Time,nameof(input));FormulaDomain.Finite(keys[i].Value,nameof(input));if(i>0&&keys[i].Time<=keys[i-1].Time)throw new ArgumentException("Keys must have increasing time");}
        Identity=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join(";",keys.Select(k=>$"{BitConverter.SingleToInt32Bits(k.Time):X8}:{BitConverter.SingleToInt32Bits(k.Value):X8}")))));
    }
    public float Evaluate(float x)
    {
        FormulaDomain.Finite(x,nameof(x));if(x<keys[0].Time||x>keys[^1].Time)throw new FormulaStageException("Curve.Evaluate","Outside supplied key domain; Unity wrapping unknown");
        int low=0,high=keys.Length-1;while(high-low>1){int mid=(low+high)/2;if(keys[mid].Time<=x)low=mid;else high=mid;}
        var a=keys[low];var b=keys[high];return FormulaDomain.Result(a.Value+(b.Value-a.Value)*((x-a.Time)/(b.Time-a.Time)));
    }
}
public sealed class LinearInverseFactory:IInverseCurveFactory
{public string Identity=>"Experimental-linear-v1";public ICurveEvaluator Create(ReadOnlySpan<CurveKey> keys)=>new LinearTestCurve(keys);}
public static class ExperimentalPolarOrientation
{
    public static Vector2 CosSin(PolarSample sample)
    {float radians=sample.AngleDegrees*0.01745329238474369f;return new(sample.Radius*MathF.Cos(radians),sample.Radius*MathF.Sin(radians));}
}
