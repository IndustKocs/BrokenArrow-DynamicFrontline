namespace DynamicFrontline.Integration;

public static class NativeNavigationStatusPolicy
{
    // Flags require a runtime-validated backend contract; enum values alone prove neither path success nor blockage.
    public static RouteState Path(byte nativeStatus,bool completeSemanticsVerified=false,bool impossibleSemanticsVerified=false)=>nativeStatus switch
    {1 when completeSemanticsVerified=>RouteState.Reachable,4 when impossibleSemanticsVerified=>RouteState.Blocked,_=>RouteState.Unknown};
    public static RouteState Passage(byte nativeStatus,bool directionalSemanticsVerified=false)=>!directionalSemanticsVerified?RouteState.Unknown:nativeStatus switch
    {1=>RouteState.Reachable,2=>RouteState.Blocked,_=>RouteState.Unknown};
}
