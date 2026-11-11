using HeroesOfDragonland.Art.Editor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        HumanoidVisualValidation.Start();
        result.Log("Started humanoid Play Mode checks; inspect HumanoidVisualValidation.Status.");
    }
}
