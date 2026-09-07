# Heroes of Dragonland Reborn

This is a Unity 6 3D game project.

The project is developed primarily by an AI coding agent.
The human user acts mainly as game director, designer, and tester.

## Core workflow

For every task:

1. Inspect the existing project before making changes.
2. Understand the current scene and existing systems.
3. Reuse existing code where appropriate.
4. Implement features in small modular systems.
5. Use Unity MCP when Editor changes are required.
6. Check the Unity Console after changes.
7. Fix compilation errors before finishing.
8. Save modified scenes and assets.
9. Do not leave the project in a broken state.
10. Summarize changed files and important decisions.

## Coding rules

Use clean modular C#.

Prefer composition over inheritance.

Avoid giant manager classes.

Avoid putting unrelated gameplay systems into PlayerController.

Use interfaces and components where appropriate.

Prefer ScriptableObjects for configurable game data.

Keep UI separated from gameplay logic.

Avoid hardcoded scene object references when practical.

Use serialized fields instead of public fields when a field only needs Inspector access.

Use events for communication between independent gameplay systems.

## Unity rules

Do not modify:

- Library/
- Temp/
- Logs/
- Obj/
- generated solution/project files

Do not delete assets unless explicitly requested.

Always preserve .meta files.

Use Unity MCP for:

- creating GameObjects
- attaching components
- editing scenes
- creating prefabs
- inspecting Console
- checking Hierarchy

Do not edit serialized Unity YAML manually unless there is a strong reason.

## Architecture

Create gameplay code under:

Assets/Game/

Suggested layout:

Assets/Game/Core/
Assets/Game/Player/
Assets/Game/Interaction/
Assets/Game/Combat/
Assets/Game/Inventory/
Assets/Game/Items/
Assets/Game/AI/
Assets/Game/World/
Assets/Game/UI/
Assets/Game/Save/
Assets/Game/Data/

Art should be kept under:

Assets/Art/

Third-party assets should be kept under:

Assets/ThirdParty/

## Naming

C# classes:
PascalCase

Private fields:
camelCase

Serialized private fields:
[SerializeField] private

Interfaces:
IInteractable
IDamageable

ScriptableObjects:
ItemDefinition
WeaponDefinition
EnemyDefinition

## Development philosophy

Prefer simple solutions first.

Do not build systems for hypothetical future features unless required.

Keep systems extensible but not overengineered.

Prioritize a working vertical slice over architecture perfection.

## Testing

After implementing a feature:

1. Check Console.
2. Verify required components exist.
3. Verify references are assigned.
4. Run Play Mode when possible.
5. Report anything that still requires manual verification.

## Git

Do not commit automatically unless explicitly asked.

Before finishing a large task, report:
- modified files
- newly created files
- deleted files
- major architectural changes