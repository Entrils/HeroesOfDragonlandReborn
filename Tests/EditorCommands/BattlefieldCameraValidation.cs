using HeroesOfDragonland.World.Editor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        BattlefieldCameraValidation.StartChecks();
        result.Log("Started third-person camera validation; inspect BattlefieldCameraValidation.Status.");
    }
}
