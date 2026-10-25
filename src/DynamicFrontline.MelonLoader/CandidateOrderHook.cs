using DynamicFrontline.Core;
namespace DynamicFrontline.Loader;

// Not registered with Harmony. This signature is a candidate for IL2CPP wrapper validation.
// AddCommandsQuery's entity parameter is index 2; Unity nullable-vector/byref ABI needs runtime validation.
internal static class CandidateOrderHook
{
    internal static Func<object,UnitKey?>? ResolveOwnEntity { get; set; }
    internal static UserCommandGate? Gate { get; set; }
    internal static int LocalOwner { get; set; }
    internal static bool CoverageValidated { get; set; }
    internal static bool Prefix(object[] __args, ref bool __result)
    {
        if(!CoverageValidated || Gate == null || ResolveOwnEntity == null) return true;
        try
        {
            UnitKey? key=ResolveOwnEntity(__args[2]);
            if(key is not { } own || Gate.Allows(own,LocalOwner)) return true;
            __result=false; return false;
        }
        catch
        {
            // No ambiguous blanket suppression. Disable the candidate if mapping fails.
            CoverageValidated=false; return true;
        }
    }
}
