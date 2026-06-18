# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

Freshly-bootstrapped Unity project derived from the **URP Empty Template** (`Assets/Readme.asset` title = "URP Empty Template"). At time of writing there is **no game code yet** — the only C# in the project is the template's Readme tooling under `Assets/TutorialInfo/Scripts/`. New gameplay code should be added under `Assets/` (create a `Scripts/` folder).

The directory is not a git repository.

## Engine and pipeline

- Unity 6.x (URP `com.unity.render-pipelines.universal` 17.3.0). Open the project with a Unity 6 editor.
- **Universal Render Pipeline with two pre-configured asset pairs:** `Assets/Settings/PC_RPAsset.asset` + `PC_Renderer.asset`, and `Mobile_RPAsset.asset` + `Mobile_Renderer.asset`. URP quality levels select between them — when adding renderer features, update both renderers (or document which platform you intentionally diverged on).
- **New Input System** (`com.unity.inputsystem` 1.16.0) is enabled; legacy `InputManager.asset` is still present but the canonical action map is `Assets/InputSystem_Actions.inputactions` (Player + UI maps).
- Other notable packages: `com.unity.ai.navigation` 2.0.9 (component-based NavMesh — `NavMeshSurface`, not the legacy Window > Navigation flow), `com.unity.visualscripting` 1.9.9, `com.unity.test-framework` 1.6.0, `com.unity.timeline` 1.8.9.

## Working in the project

There is no CLI build/test pipeline configured. All builds, Play Mode runs, and test runs go through the Unity Editor. If you need headless invocation:

- Run Edit Mode + Play Mode tests: `Unity.exe -batchmode -projectPath <repo> -runTests -testPlatform <EditMode|PlayMode> -testResults <path.xml> -logFile -`
- Headless build: `Unity.exe -batchmode -quit -projectPath <repo> -executeMethod <YourBuildClass.Build> -logFile -` (no such build class exists yet — would need to be added under an `Editor/` folder).

When asked to "run the project" without the Editor available, say so explicitly rather than guessing — Unity won't compile from this workspace without the IDE.

## Template tooling to know about

`Assets/TutorialInfo/Scripts/Editor/ReadmeEditor.cs` runs on `InitializeOnLoad`. On the **first editor session** it force-selects the `Readme` asset and calls `WindowLayout.LoadWindowLayout` against `Assets/TutorialInfo/Layout.wlt`, overriding the user's editor layout. This is template behavior, not a bug. The whole `TutorialInfo/` folder can be removed via the **Remove Readme Assets** button on the Readme inspector once real content lands.

## Scenes

Only scene: `Assets/Scenes/SampleScene.unity`. It is the active scene in `EditorBuildSettings.asset`.
