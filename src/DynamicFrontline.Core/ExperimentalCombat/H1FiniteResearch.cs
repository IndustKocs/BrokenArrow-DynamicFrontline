using DynamicFrontline.Core.VerifiedCombat;
namespace DynamicFrontline.Core.ExperimentalCombat;
public readonly record struct H1ResearchResult(float Result,double AbsoluteInput,double BaseAngle,double ReducedArgument,double Correction,bool LargeInputHandler);
public static class H1FiniteResearch
{
    // P4-F08 finite rational transcription; no native NaN/errno/FMA/ULP contract and no ballistic wiring.
    public static H1ResearchResult Evaluate(float x)
    {
        FormulaDomain.Finite(x,nameof(x));double a=Math.Abs((double)x),b,z;
        if(a>5.512405943901487e18)return new(MathF.CopySign(1.5707963705062866f,x),a,Math.PI/2,0,0,true);
        if(a<.4375){b=0;z=a;}else if(a<.6875){b=.4636476090008061;z=(2*a-1)/(a+2);}else if(a<1.1875){b=.7853981633974483;z=(a-1)/(a+1);}else if(a<2.4375){b=.982793723247329;z=(a-1.5)/(1.5*a+1);}else{b=1.5707963267948966;z=-1/a;}
        double q=z*z,r=z*z*z*((.004706779342861492*q+.19232454640210858)*q+.2965285988192392)/((.2993096999596597*q+1.1107249999539954)*q+.8895857968624323);
        return new(MathF.CopySign((float)(b+z-r),x),a,b,z,r,false);
    }
}
