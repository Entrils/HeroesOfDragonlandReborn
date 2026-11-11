using HeroesOfDragonland.Art.Editor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        CharacterAnimationValidation.Start();
        result.Log("Started production animation checks; inspect CharacterAnimationValidation.Status.");
    }
}
