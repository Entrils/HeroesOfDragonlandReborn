using HeroesOfDragonland.World.Editor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        HudLayoutValidation.StartChecks();
        result.Log("Started responsive HUD checks; inspect HudLayoutValidation.Status.");
    }
}
