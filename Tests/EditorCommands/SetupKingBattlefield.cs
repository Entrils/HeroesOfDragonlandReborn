using HeroesOfDragonland.World.Editor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        KingBattlefieldBuilder.Build();
        result.Log("Saved " + KingBattlefieldBuilder.ScenePath);
    }
}
