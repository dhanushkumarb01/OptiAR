# Contributing to OptiAR

## Who works on what

**3D optics assets (collaborator):**

1. 3D models of the four optical devices: concave mirror, convex mirror, convex lens, concave lens
2. Object and image markers (e.g. the arrow/candle object and its image)
3. Visual ray elements (incident and reflected/refracted rays)
4. Reusable Unity prefabs for each experiment
5. Materials, textures and any other supporting assets

Put new files here (create the folders if they don't exist):

| Type | Folder |
| --- | --- |
| Models (`.fbx`, `.obj`, `.blend`) | `Assets/Models/Optics/` |
| Prefabs | `Assets/Prefabs/Optics/` |
| Materials | `Assets/Materials/Optics/` |
| Textures | `Assets/Textures/Optics/` |

## Please don't change (unless we agree first)

- UI screens and layouts in `Assets/Scenes/00_Boot.unity`
- Navigation and screen scripts (`Assets/Scripts/App/AppManager.cs`, `Assets/Scripts/Screens/`)
- The optics calculation (`Assets/Scripts/App/Optics.cs`) and topic data (`Assets/Data/`)
- Vuforia / AR integration (to be added later)
- `ProjectSettings/` and `Packages/manifest.json`. Ask before adding a package.

If you need to change one of these, message first so we don't both edit the same file.
Unity scene files are hard to merge.

## Rules

- Use Unity **2022.3.62f3** only.
- **Pull before you start, push when you finish.**
- Always commit the `.meta` file together with its asset. Don't delete or recreate `.meta` files.
- Move or rename assets **inside the Unity Project window**, not in File Explorer. That keeps references working.
- Avoid opening and saving `00_Boot.unity` unless your change needs it. Build and test your work as prefabs instead.
- Keep each file under 50 MB. GitHub rejects files over 100 MB.
- Use clear names, e.g. `ConvexLens.fbx`, `ConvexLens.prefab`, `M_Glass.mat`.
- Write short, clear commit messages, e.g. `Add concave mirror prefab`.
