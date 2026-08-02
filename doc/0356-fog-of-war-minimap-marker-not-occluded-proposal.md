# 0356 — 미니맵 유닛/건물 마커(y40/50 스프라이트)가 Fog of War에 안 가려짐

**날짜:** 2026-08-02

## 질문

"미니맵으로 보면 아군유닛은 초록색 원, 아군건물은 초록색 사각형, 적유닛은 빨간색 원, 적건물은 빨간색
사각형으로 보이도록 유닛 머리위에 y40정도에 2D 스프라이트를 배치하고 색깔을 지정했는데, 적 유닛의 경우
fog of war 아래에 있는 유닛의 메쉬 자체는 가려지는데 y40에 있는 스프라이트는 가려지지 않는다."

작업 중인 변경사항 확인: `Worker Drone.prefab`에 `Circle` 자식(레이어 Unit, y=40, 초록),
`Brute Mech.prefab`에 `Circle` 자식(레이어 Enemy, y=50, 빨강)이 `SpriteRenderer`로 추가돼 있음
(각각 `git diff`로 확인).

## 원인

이 프로젝트의 Fog of War(`Assets/AssetFolder/AOSFogWar/csFogWar.cs`)는 셰이더 마스크나 카메라
컬링이 아니라 **실제 3D Plane 오브젝트**다.

```csharp
// csFogWar.cs:382-391
fogPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
...
fogPlane.transform.position = new Vector3(
    ...,
    levelMidPoint.position.y + fogPlaneHeight,
    ...);
```

씬 설정(`Assets/prefabs/Game/GameManager.prefab:1442`)에서 `fogPlaneHeight = 1`. 즉 안개는
**지면 바로 위(Y≈1)에 있는 실제 지오메트리**이고, 미니맵 카메라(오소그래픽, `orthographic size: 99.79`,
`m_CullingMask.m_Bits: 4294967295` = Everything, `GameManager.prefab:7465-7470`)가 이 평면을
그대로 렌더링하면서 표준 깊이 테스트로 그 아래 물체를 가린다.

탑다운 카메라 기준 "카메라에 더 가까움 = 월드 Y가 더 큼"이므로:
- 유닛 메쉬 몸체(Y≈0~수 미터)는 안개 평면(Y≈1)보다 낮아 → 가려짐 (사용자가 관찰한 정상 동작)
- 새로 추가한 마커 스프라이트(Y=40/50)는 안개 평면보다 훨씬 높아 → 애초에 안개 평면보다 카메라에
  가까우므로 깊이 테스트를 항상 통과 → **절대 가려지지 않음**

스프라이트/메쉬라는 렌더러 종류 차이가 아니라, 순전히 마커의 Y가 안개 평면보다 높다는 배치 문제다.

### 왜 "마커 Y를 낮추기"로는 해결이 안 되는가

마커 Y를 안개 평면 높이(≈1) 근처로 낮추면 안개 아래에서는 정확히 가려지겠지만, 이 프로젝트의 안개가
"평면보다 낮으면 무조건 가림"이라는 순수 깊이 테스트이기 때문에, 이번엔 건물이나 언덕처럼 안개와
무관하게 실제로 더 높이 솟아있는 씬 지오메트리에도 마커가 가려버린다 — "안개가 걷혔는데도 마커가
건물/지형 뒤에 숨어 안 보임"이라는 새 버그가 생긴다. 즉 이 프로젝트 안개 구현의 특성상 **높이 배치만으로는
"안개일 때만 정확히 가려짐"을 만족시킬 수 없다.**

## 제안 수정

물리적 가림 대신, 안개 상태를 직접 물어봐서 마커의 `SpriteRenderer.enabled`를 켜고 끄는 방식으로
전환한다. `UserControl.cs`가 이미 같은 목적(클릭/호버 대상에서 안개 속 대상 제외)으로 쓰고 있는
`csFogWar` 공개 API 패턴을 그대로 재사용한다 (`doc/0173`이 정리한 "에셋은 안 건드리고 public API만
사용" 관례와 동일):

```csharp
// UserControl.cs:1053 - 참고용, 그대로 재사용할 로직
Vector2Int center = fogWar.WorldToLevel(worldPosition);
...
Shadowcaster.LevelColumn.ETileVisibility visibility = fogWar.shadowcaster.fogField[cell.x][cell.y];
if (visibility == Revealed || visibility == PreviouslyRevealed)
    return true; // 보임
```

`Revealed`뿐 아니라 `PreviouslyRevealed`도 "보임"으로 인정하는 이유: 지금 안개 평면도 `PreviouslyRevealed`
타일은 완전히 안 가리고 반투명하게만 보여주므로(둘 다 인정해야 기존 메쉬 가림 동작과 마커 가림 동작이
일치함).

### 신규 파일: `Assets/Scripts/FogOfWar/MinimapMarkerFogVisibility.cs`

적 유닛/건물의 미니맵 마커(`Circle`/`Square` 자식)에만 붙이는 작은 컴포넌트. (참고: 안 쓰이고 있는
예제 에셋 `AOSFogWar/Examples/csFogVisibilityAgent.cs`도 정확히 같은 패턴이지만
`MeshRenderer`/`SkinnedMeshRenderer`만 토글하고 `SpriteRenderer`는 다루지 않아서 그대로는 못 씀.)

```csharp
using UnityEngine;
using FischlWorks_FogWar;

// 미니맵 마커(SpriteRenderer)를 안개 상태에 맞춰 켜고 끈다. 이 프로젝트의 Fog of War는 물리적
// Plane(csFogWar.fogPlane, Y≈1)으로 구현돼 있어 Y가 높은 오브젝트(미니맵 마커 등)는 깊이 테스트로
// 가려지지 않는다(doc/0356) - 그래서 안개 상태를 직접 조회해서 렌더러를 토글한다.
public class MinimapMarkerFogVisibility : MonoBehaviour
{
    [SerializeField] private SpriteRenderer icon;
    [SerializeField] private int visibilityMargin = 1; // UserControl.fogVisibilityMargin과 동일한 목적

    private csFogWar fogWar;

    private void Start()
    {
        fogWar = FindFirstObjectByType<csFogWar>();
    }

    private void Update()
    {
        if (fogWar == null || icon == null)
            return;

        icon.enabled = IsRevealedByFog();
    }

    // UserControl.IsRevealedByFog()와 동일한 로직 (doc/0173의 "public API만 사용" 관례상 중앙화하지 않고
    // 각 소비처에서 짧게 반복 - TerritoryFogReveal.cs도 동일).
    private bool IsRevealedByFog()
    {
        Vector2Int center = fogWar.WorldToLevel(transform.position);

        for (int x = -visibilityMargin; x <= visibilityMargin; x++)
        {
            for (int y = -visibilityMargin; y <= visibilityMargin; y++)
            {
                Vector2Int cell = new Vector2Int(center.x + x, center.y + y);

                if (!fogWar.CheckLevelGridRange(cell))
                    continue;

                var visibility = fogWar.shadowcaster.fogField[cell.x][cell.y];

                if (visibility == Shadowcaster.LevelColumn.ETileVisibility.Revealed ||
                    visibility == Shadowcaster.LevelColumn.ETileVisibility.PreviouslyRevealed)
                    return true;
            }
        }

        return false;
    }
}
```

### 프리팹 적용

- `Brute Mech.prefab`의 `Circle`(적 유닛 마커)에 위 컴포넌트 추가, `icon`에 같은 오브젝트의
  `SpriteRenderer` 연결. 다른 적 유닛 프리팹에도 동일하게 적용 필요.
- 적 건물 마커(`Square` 등, 아직 안 만드셨다면)도 동일.
- **아군 마커(`Worker Drone.prefab`의 `Circle`)에는 붙이지 않음** — 아군은 항상 보여야 하므로 토글 불필요.

## 확인 필요 사항 (적용 전)

- 위 방식(안개 상태를 직접 조회해서 SpriteRenderer 토글)으로 진행해도 되는지
- 적용 대상이 지금 보여주신 두 프리팹뿐인지, 아니면 모든 적 유닛/건물 프리팹에 일괄 적용해야 하는지
  (적 유닛/건물 프리팹 개수 확인 필요)
- `PreviouslyRevealed`(한 번 밝혀졌던 곳, 반투명)에서도 마커를 보이게 할지, 아니면 `Revealed`(현재
  시야 안)일 때만 보이게 할지 — 위 제안은 기존 메쉬 가림 동작과 맞춰 전자로 잡음

## 적용 (2026-08-02)

사용자 지시로 방향 변경: 별도 컴포넌트 대신 **`EnemyUnitController`(구 `EnemyController`) 안에 직접**
안개 판정 + 마커 토글 로직을 넣음("EnemyController안에 그럼 안개 안에 있는지 없는지를 판단해서
미니맵 마커를 토글로 켜고 끄도록 해줘, 인스펙터 상으로 연결해야하면 내가 할게"). 인스펙터에서
`minimapIcon` 필드에 마커의 `SpriteRenderer`를 연결하는 작업은 사용자가 직접 진행하기로 함 — 코드
쪽에서는 필드만 노출해두고 프리팹은 건드리지 않음. 적 건물(`EnemyBuildingController`)은 이번 범위
밖(미니맵 마커 자체가 아직 없음, 요청도 "EnemyController" 한정).

**`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`**

Before:
```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyUnitController : MonoBehaviour, IDestructible
{
    [SerializeField]
    private GameObject enemyMarker;

    [SerializeField]
    private Sprite icon; // Info_panel에 표시할 아이콘
```

After:
```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using FischlWorks_FogWar;

public class EnemyUnitController : MonoBehaviour, IDestructible
{
    [SerializeField]
    private GameObject enemyMarker;

    [SerializeField]
    private Sprite icon; // Info_panel에 표시할 아이콘

    // 미니맵에 표시하는 y40대 스프라이트 마커(자식 오브젝트, 인스펙터에서 연결). 이 프로젝트의 안개(csFogWar)는
    // 실제 3D Plane(Y≈1)으로 구현돼 있어 이렇게 Y가 높은 오브젝트는 깊이 테스트로 가려지지 않는다 - 그래서
    // Update()에서 안개 상태를 직접 조회해 이 렌더러를 켜고 끈다 (doc/0356).
    [SerializeField]
    private SpriteRenderer minimapIcon;

    [SerializeField]
    private int minimapFogVisibilityMargin = 1; // UserControl.fogVisibilityMargin과 동일한 목적

    private csFogWar fogWar;
```

`Start()` — `rtsController` 바로 아래에 `fogWar` 캐싱 추가:
```diff
     rtsController = FindFirstObjectByType<RTSUnitController>();
+    fogWar = FindFirstObjectByType<csFogWar>(); // 안개가 없는 씬(테스트 씬 등)에서는 null - Update()에서 그 경우 마커를 항상 켜둠
```

`Update()` 끝 + 신규 메서드 두 개(`AttackMoveTick()` 바로 뒤):
```diff
     AttackMoveTick();
+    UpdateMinimapIconVisibility();
 }

+// 미니맵 마커를 안개 상태에 맞춰 켜고 끈다 - UserControl.IsRevealedByFog()와 동일한 로직(Revealed와
+// PreviouslyRevealed 둘 다 "보임"으로 인정, 안개가 없는 씬에서는 항상 보임)이지만, 여기서는 유닛 자신의
+// fogWar 참조로 물어본다 (doc/0356 - 마커가 Y40대라 안개 Plane 깊이 테스트로는 안 가려지는 문제의 대안).
+private void UpdateMinimapIconVisibility()
+{
+    if (minimapIcon == null)
+        return;
+
+    if (fogWar == null)
+    {
+        minimapIcon.enabled = true;
+        return;
+    }
+
+    minimapIcon.enabled = IsRevealedByFog();
+}
+
+private bool IsRevealedByFog()
+{
+    Vector2Int center = fogWar.WorldToLevel(transform.position);
+
+    for (int x = -minimapFogVisibilityMargin; x <= minimapFogVisibilityMargin; x++)
+    {
+        for (int y = -minimapFogVisibilityMargin; y <= minimapFogVisibilityMargin; y++)
+        {
+            Vector2Int cell = new Vector2Int(center.x + x, center.y + y);
+
+            if (!fogWar.CheckLevelGridRange(cell))
+                continue;
+
+            Shadowcaster.LevelColumn.ETileVisibility visibility = fogWar.shadowcaster.fogField[cell.x][cell.y];
+
+            if (visibility == Shadowcaster.LevelColumn.ETileVisibility.Revealed ||
+                visibility == Shadowcaster.LevelColumn.ETileVisibility.PreviouslyRevealed)
+                return true;
+        }
+    }
+
+    return false;
+}
```

`npx uloop-cli compile` 통과 (에러 0, 경고 28개 — 새로 추가된 `FindFirstObjectByType` obsolete 경고
1건 포함해서 전부 이미 프로젝트 전역에 있던 동일 패턴의 기존 경고, 신규 문제 없음).

**남은 작업 (사용자가 직접)**: 적 유닛 프리팹(`Brute Mech.prefab` 등)의 `EnemyUnitController` 인스펙터에서
`minimapIcon` 필드에 `Circle` 자식의 `SpriteRenderer`를 연결. 연결 전까지는 `minimapIcon == null`이라
`UpdateMinimapIconVisibility()`가 조용히 아무 것도 안 하므로(기존 동작 그대로 항상 보임) 안전함.

## 인스펙터 연결 (2026-08-02)

사용자가 나머지 적 유닛 프리팹에 `MiniMapIcon` 자식(스프라이트 마커)을 전부 추가한 뒤, `minimapIcon`
필드 연결을 요청("적 유닛 프리팹에다가 MinimapIcon 다 추가했고 너가 인스펙터 상으로 연결해줘"). 이미
사용자가 직접 연결해둔 두 개(`Nanobot Repair.prefab`, `Brute Mech.prefab`)를 빼고, `minimapIcon:
{fileID: 0}`(미연결)로 남아있던 나머지 7개 프리팹의 `EnemyUnitController` 컴포넌트에 각자의
`MiniMapIcon` 자식 오브젝트 밑 `SpriteRenderer` fileID를 프리팹 YAML에서 직접 채워 넣음:

- `Assets/prefabs/OC/Unit/Tier1/Cyborg Soldier .prefab`
- `Assets/prefabs/OC/Unit/Tier1/Railgunner.prefab`
- `Assets/prefabs/OC/Unit/Tier1/Striker.prefab`
- `Assets/prefabs/OC/Unit/Tier2/Heavy Assault Tank.prefab`
- `Assets/prefabs/OC/Unit/Tier2/Ironhawk.prefab`
- `Assets/prefabs/OC/Unit/Tier3/Raven.prefab`
- `Assets/prefabs/OC/Unit/Tier3/Strike Drone.prefab`

각 프리팹의 `MiniMapIcon` GameObject 블록에서 `m_Component` 두 번째 항목(Transform 다음)이
`SpriteRenderer`(`!u!212`)임을 먼저 확인한 뒤, 해당 fileID로 `minimapIcon: {fileID: 0}` →
`minimapIcon: {fileID: <SpriteRenderer fileID>}`로 교체.

YAML을 텍스트로 직접 고친 것이라, Unity 에디터가 실제로 이 값을 올바르게 읽는지 `execute-dynamic-code`로
`AssetDatabase.Refresh()` 후 9개 프리팹 전부 `SerializedObject`로 `minimapIcon`을 다시 읽어 검증함 —
9개 전부 자기 `MiniMapIcon` 자식의 `SpriteRenderer`를 정확히 가리키는 것을 로그로 확인:

```
[Verify] .../Nanobot Repair.prefab: minimapIcon -> MiniMapIcon (SpriteRenderer) OK
[Verify] .../Cyborg Soldier .prefab: minimapIcon -> MiniMapIcon (SpriteRenderer) OK
[Verify] .../Railgunner.prefab: minimapIcon -> MiniMapIcon (SpriteRenderer) OK
[Verify] .../Striker.prefab: minimapIcon -> MiniMapIcon (SpriteRenderer) OK
[Verify] .../Brute Mech.prefab: minimapIcon -> MiniMapIcon (SpriteRenderer) OK
[Verify] .../Heavy Assault Tank.prefab: minimapIcon -> MiniMapIcon (SpriteRenderer) OK
[Verify] .../Ironhawk.prefab: minimapIcon -> MiniMapIcon (SpriteRenderer) OK
[Verify] .../Raven.prefab: minimapIcon -> MiniMapIcon (SpriteRenderer) OK
[Verify] .../Strike Drone.prefab: minimapIcon -> MiniMapIcon (SpriteRenderer) OK
```

적 유닛 9종 전부 연결 완료. (`Worker Drone.prefab`은 아군 - `UnitController`를 쓰므로 `minimapIcon`
필드 자체가 없음, 해당 없음.)
