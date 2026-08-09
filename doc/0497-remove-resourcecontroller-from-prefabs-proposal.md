# 0497 - Remove ResourceController component from 4 prefabs (proposal)

## Context

Task received (via subagent delegation, uloop-execute-dynamic-code skill) to remove the
`ResourceController` component from 4 prefabs using Unity's `PrefabUtility` API
(`LoadPrefabContents` -> `DestroyImmediate` component -> `SaveAsPrefabAsset` ->
`UnloadPrefabContents`), then `AssetDatabase.SaveAssets()` / `AssetDatabase.Refresh()`.

`Assets/Scripts/Resource/ResourceController.cs` is confirmed to be an empty MonoBehaviour:

```csharp
using UnityEngine;

public class ResourceController : MonoBehaviour
{
}
```

Target prefabs:
- `Assets/prefabs/Resource/Gas.prefab`
- `Assets/prefabs/Resource/Ore.prefab`
- `Assets/prefabs/OC/RescueUnit/Cyborg Soldier (Rescue).prefab`
- `Assets/prefabs/OC/RescueUnit/Heavy Assault Tank (Rescue).prefab`

## Why pausing here

Per the user's standing rule (`confirm_before_implementing.md`): draft the proposal first,
then ask before touching project code — no auto-implement. A delegating agent's task
description is not the same as the user's own approval, so this step is not skipped even
though the requested action itself is narrow and mechanical (removing an empty, no-op
component from 4 prefabs).

## Proposed change

Run the described uloop-cli `execute-dynamic-code` snippet against the live Unity Editor:

1. For each of the 4 prefab paths:
   - `PrefabUtility.LoadPrefabContents(path)`
   - `root.GetComponent<ResourceController>()` — if found, `Object.DestroyImmediate(comp, true)`
   - `PrefabUtility.SaveAsPrefabAsset(root, path)`
   - `PrefabUtility.UnloadPrefabContents(root)`
2. `AssetDatabase.SaveAssets()`
3. `AssetDatabase.Refresh()`

No changes to `ResourceController.cs` itself — this only strips the component reference
from the 4 prefab asset files. Since the component has no fields/logic, no other script
should reference it, but that has not yet been grep-verified.

## Open question before implementing

Confirm you want this removal executed now via the Unity Editor (uloop-cli), and confirm
no other code depends on `GetComponent<ResourceController>()` on these objects (a quick
grep across `Assets/Scripts` for `ResourceController` will be done first regardless).
