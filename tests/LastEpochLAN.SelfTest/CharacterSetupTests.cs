using LastEpochLAN.Mod.OfflineTools;

internal static class CharacterSetupTests
{
    internal static void Run(Action<bool,string> check)
    {
        var gate=new CharacterSetupGate();
        gate.Arm(" New hero ",1,new[]{"existing"},0);
        check(gate.Pending(0) && !gate.ConsumeCreation("existing","New hero",1,1),
            "creation intent excludes existing save IDs even with matching name and class");
        check(!gate.ConsumeCreation("new","Other hero",1,1) && !gate.ConsumeCreation("new","New hero",2,1),
            "creation intent cannot boost a different character or class");
        check(gate.ConsumeCreation("new","New hero",1,2) && !gate.ConsumeCreation("new","New hero",1,3),
            "creation intent is consumed once without requeueing the same native creation");
        gate.Arm("Cancelled",1,Array.Empty<string>(),0);
        check(!gate.Pending(600) && !gate.ConsumeCreation("new","Cancelled",1,600),
            "expired creation intent does not change a later character");
        check(!gate.Ready("hero|scene1",false,0) && !gate.Ready("hero|scene1",false,100),
            "loading or unspawned character never starts campaign setup");
        check(!gate.Ready("hero|scene1",true,101) && !gate.Ready("hero|scene1",true,102.9) && gate.Ready("hero|scene1",true,103),
            "campaign setup waits for two seconds of stable playable state");
        check(!gate.Ready("hero|scene1",false,104) && !gate.Ready("hero|scene1",true,105) && gate.Ready("hero|scene1",true,107),
            "a loading screen or active transition resets readiness before further quest work");
        check(!gate.Ready("hero|scene2",true,108) && !gate.Ready("other|scene2",true,110),
            "scene and character changes each require a fresh stable readiness interval");
        gate.ResetReadiness();
        check(!gate.Ready("other|scene2",true,115),"scene callback invalidates readiness even for a reused scene name");
    }
}
