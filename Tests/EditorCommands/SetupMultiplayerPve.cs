using HeroesOfDragonland.World.Editor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        MultiplayerPveSceneBuilder.Build();
        result.Log("Created and saved authoritative PvE test scene and enemy prefabs");
    }
}
