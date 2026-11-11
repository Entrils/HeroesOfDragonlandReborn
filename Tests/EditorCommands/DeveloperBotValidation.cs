using HeroesOfDragonland.World.Editor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        DeveloperBotValidation.StartScenarios();
        result.Log("Started live bot scenarios; inspect DeveloperBotValidation.ScenarioResult for completion.");
    }
}
