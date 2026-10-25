using DynamicFrontline.Core.VerifiedCombat;
static partial class Tests
{
    static partial void Aircraft()
    {
        Test("Aircraft yaw two segments and max equality",()=>{var s=new AircraftSpeedPreset(100,200,300);var r=new AircraftRatePreset(10,20,15);Eq(AircraftMath.CalculateYawRate(150,s,r).Rate,15);Eq(AircraftMath.CalculateYawRate(250,s,r).Rate,17.5);Check(!AircraftMath.CalculateYawRate(200,s,r).AfterburnBranch);Eq(AircraftMath.CalculateYawRate(400,s,r).Rate,15);Eq(AircraftMath.CalculateYawRate(0,s,r).Rate,10);});
        Test("Aircraft pitch independent rates",()=>{Eq(AircraftMath.CalculatePitchRate(150,new(100,200,300),new(2,4,1)).Rate,3);Eq(AircraftMath.CalculatePitchRate(250,new(100,200,300),new(2,4,1)).Rate,2.5);});
        Test("Aircraft denominator threshold zero equal presets",()=>{Eq(AircraftMath.CalculateRate(.0005f,new(0,.001f,.002f),new(10,20,15)).Rate,10);Eq(AircraftMath.CalculateRate(200,new(100,100,100),new(10,20,15)).Rate,20);Reject(()=>AircraftMath.CalculateRate(150,new(200,100,300),new(10,20,15)));Reject(()=>AircraftMath.CalculateRate(float.NaN,new(100,200,300),new(10,20,15)));});
    }
}
