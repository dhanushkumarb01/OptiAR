# OptiAR — Interactive Ray Optics Lab

A Unity mobile app (portrait) that teaches ray optics. Students pick a mirror or lens,
read the concept, then move the object and watch the ray diagram and image change.
There is also a Test mode with challenges.

> **Current status:** the experiment uses a **2D ray-diagram prototype** drawn on the UI.
> Real AR (Vuforia) is **not implemented yet**. The AR scan screen only simulates
> finding a surface.

## Topics

1. Concave Mirror
2. Convex Mirror
3. Convex Lens
4. Concave Lens

## Requirements

- **Unity 2022.3.62f3** (install this exact version through Unity Hub)
- Android Build Support module (only needed later, for building to a phone)
- Git or [GitHub Desktop](https://desktop.github.com/)

## Getting started

1. Clone the repo:
   - **GitHub Desktop:** File → Clone repository → URL → `https://github.com/dhanushkumarb01/OptiAR.git`
   - **Command line:** `git clone https://github.com/dhanushkumarb01/OptiAR.git`
2. Open **Unity Hub** → **Add** → **Add project from disk** → pick the cloned `OptiAR` folder.
3. Open it with **2022.3.62f3**. The first import takes a few minutes because Unity rebuilds the `Library/` folder.
4. Open `Assets/Scenes/00_Boot.unity` and press **Play**.

## Project layout

| Folder | What's inside |
| --- | --- |
| `Assets/Scenes/` | `00_Boot.unity`, the only scene. All screens live here. |
| `Assets/Scripts/App/` | `AppManager.cs` (navigation), `Optics.cs` (mirror/lens formula), `TopicData.cs` (topic data type) |
| `Assets/Scripts/Screens/` | One script per screen: Splash, Home, Learn Topics, Concept, AR Scan, AR Experiment, Test |
| `Assets/Scripts/UI/` | UI helpers, including `RayDiagram.cs` (the 2D ray diagram) |
| `Assets/Data/` | One `TopicData` asset per topic (text, focal lengths, colours) |
| `Assets/UI/` | Sprites and icons used by the UI |
| `Assets/Fonts/` | Inter and JetBrains Mono (TextMesh Pro font assets) |
| `Assets/Prefabs/` | Prefabs. 3D optics prefabs go in `Assets/Prefabs/Optics/` |
| `Assets/Settings/` | URP render pipeline settings |

## Important

- The **UI and navigation are approved. Do not redesign them.**
- See [CONTRIBUTING.md](CONTRIBUTING.md) for who works on what.

## Saving and sharing changes (GitHub Desktop)

1. **Before you start:** click **Fetch origin**, then **Pull origin** to get the latest changes.
2. Work in Unity. Save the scene and the project (**File → Save Project**).
3. In GitHub Desktop, check the changed files. Every new asset must come with its `.meta` file.
4. Write a short summary (e.g. `Add convex lens model`) and click **Commit to main**.
5. Click **Push origin**.

Never commit `Library/`, `Temp/`, `Logs/`, `UserSettings/` or build files. The `.gitignore` already excludes them.
