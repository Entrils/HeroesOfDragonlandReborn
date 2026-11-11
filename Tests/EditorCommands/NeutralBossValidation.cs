using HeroesOfDragonland.World.Editor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        NeutralBossValidation.Validate();
        result.Log("PASS neutral boss combat validation");
    }
}
