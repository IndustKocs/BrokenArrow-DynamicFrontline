namespace DynamicFrontline.Core.VerifiedCombat;

// Supported finite-domain policy, not reconstructed native guards.
public static class FormulaDomain
{
    public static void Finite(float x,string name){if(!float.IsFinite(x))throw new ArgumentOutOfRangeException(name);}
    public static void Nonnegative(float x,string name){Finite(x,name);if(x<0)throw new ArgumentOutOfRangeException(name);}
    public static void Positive(float x,string name){Finite(x,name);if(x<=0)throw new ArgumentOutOfRangeException(name);}
    public static void Count(int x,string name){if(x<0)throw new ArgumentOutOfRangeException(name);}
    public static float Result(float x){if(!float.IsFinite(x))throw new ArithmeticException("Outside supported finite result domain");return x;}
    public static void Probability(float x,string name){Nonnegative(x,name);if(x>1)throw new ArgumentOutOfRangeException(name);}
}
