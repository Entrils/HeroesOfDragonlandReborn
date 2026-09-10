using HeroesOfDragonland.World.Editor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        FortressTestSceneBuilder.Build();
        result.Log("Saved " + FortressTestSceneBuilder.ScenePath);
    }
}
