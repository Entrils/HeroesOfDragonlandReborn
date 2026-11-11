using HeroesOfDragonland.Art.Editor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        ArtPipelinePlayValidation.Start();
        result.Log("Started crate validation; inspect ArtPipelinePlayValidation.Status for the physics result.");
    }
}
