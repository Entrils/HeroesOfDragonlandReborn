internal class CommandScript : IRunCommand { public void Execute(ExecutionResult result) { CampDifficultyValidation.Validate(); result.Log("Camp validation passed"); } }
