namespace DynamicFrontline.Core.VerifiedCombat;
public readonly record struct AstarPriorityResult(float AbsoluteX,float AbsoluteY,float SingleSum,double Heuristic,double Priority);
public static class NavigationMath
{
    // P4-F05 #26017 A: add absolute deltas in single precision BEFORE promotion.
    public static AstarPriorityResult AstarPriority(float dx,float dy,double g)
    {
        FormulaDomain.Finite(dx,nameof(dx));FormulaDomain.Finite(dy,nameof(dy));if(!double.IsFinite(g))throw new ArgumentOutOfRangeException(nameof(g));
        float x=Math.Abs(dx),y=Math.Abs(dy),sum=FormulaDomain.Result(x+y);
        double h=(double)sum-0.5857864376269051*Math.Min(x,y),priority=g+1.25*h;
        if(!double.IsFinite(priority))throw new ArithmeticException("Priority overflow");return new(x,y,sum,h,priority);
    }
    // Consumer-specific #12295; intentionally does not change NativeNavigationStatusPolicy.
    public static bool SubGraphConsumerAccepts(byte status)=>status is 1 or 2;
}
