# 0069 - 전장의 안개(Fog of War) 설계 및 구현 제안

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **설계 + 제안 코드**만 담고
> 실제 프로젝트 파일(`Assets/Scripts/**`)은 아직 건드리지 않았다. 아래 내용을 검토한 뒤 실제로
> 적용할지(전체/일부) 알려주면 그때 `Assets/Scripts`에 반영한다.

## 1. 요청

"전장의 안개(fog of war)를 지금 이 프로젝트 유니티 환경에서 어떻게 구현할지 설계 및 구현할 코드에 대한 문서를 작성해줘."

## 2. 현재 프로젝트 분석 (설계 근거)

구현안을 짜기 전에 확인한 기존 구조:

- **렌더 파이프라인**: `ProjectSettings/GraphicsSettings.asset`에 `PC_RPAsset`/`Mobile_RPAsset`이 연결된 **URP(Universal Render Pipeline)** 프로젝트.
- **지면**: `CameraControl.cs`, `MinimapController.cs` 모두 `Plane(Vector3.up, Vector3.zero)` (Y=0 평면)로 지면 좌표를 계산 — 사실상 평지에 가까운 맵으로 취급해도 된다. 카메라 이동 제한(`CameraControl.minX/maxX/minZ/maxZ` = `-130~40`)이 대략적인 맵 크기 참고값.
- **중앙 상태 관리자**: `RTSUnitController`가 `UnitList`/`BuildingList`/`ResourceNodeList`를 들고 있고, 매 `Update()`에서 `RemoveAll(x => x == null)`로 죽은/파괴된 참조를 정리하는 패턴이 이미 있음(`RTSUnitController.cs:119-127`). **`EnemyList`는 아직 없음** — `selectedEnemyList`(선택된 것만)만 존재. FoW가 "적을 매 틱마다 순회"하려면 이 리스트가 필요.
- **적 등록/해제 패턴**: `UnitController.Start()`가 `rtsController.UnitList.Add(this)`, `UnitController.Die()`가 `controller?.UnitList.Remove(this)` 하는 패턴을 `EnemyController`에도 그대로 적용하면 됨(`UnitController.cs:170-172`, `1122-1124`).
- **전투 스탯 배치 관례**: `attackDamage`/`armor`가 SO(ScriptableObject)가 아니라 `UnitController`/`EnemyController`에 직접 `[SerializeField]`로 붙어있음(`UnitController.cs:29-30`). 시야 반경(`sightRange`)도 같은 관례를 따라 각 컨트롤러에 직접 필드로 추가하는 게 일관적.
- **체력바**: `HealthManager.SetHealthBarVisible(bool)`이 이미 있어(`HealthManager.cs:46-50`) 체력바 UI(Slider)를 켜고 끌 수 있음 — FoW로 적을 숨길 때 이 메서드를 그대로 재사용 가능.
- **클릭 판정**: `UserControl.cs`는 레이어마스크(`layerEnemy` 등)로 `Physics.Raycast`를 쏴서 대상을 찾는다(`UserControl.cs:152-157, 381-386`). **콜라이더는 렌더러와 별개**이므로, 렌더러만 꺼서 "안 보이게" 해도 레이캐스트는 여전히 맞을 수 있다 → 안 보이는 적을 클릭/타겟팅할 수 있는 구멍이 생김(3-6절에서 다룸).
- **적의 특성**: `EnemyController`는 별도의 "적 AI 플레이어"(건물/자원/생산 시스템)가 없는 단일 태그(`"Enemy"`) 기반 개체다. 즉 이 프로젝트의 FoW는 스타크래프트처럼 "적 기지 전체를 가리는" 개념보다는, **적 유닛(몬스터/수비대)의 존재를 숨기는 실시간 시야(shroud)** 개념에 더 가깝다. 지형/자원 노드는 "한 번 밝히면 계속 보이는" 편이 자연스럽다.

## 3. 설계

### 3.1 3단계 가시성 모델
셀 단위 그리드로 맵을 나누고 각 셀은 3가지 상태 중 하나:

| 상태 | 값 | 의미 | 시각 효과 |
|---|---|---|---|
| Unexplored | 0 | 한 번도 밝힌 적 없음 | 완전 검정(불투명) |
| Explored | 1 | 밝혔었지만 지금은 시야 밖 | 반투명 회색(어둑함) |
| Visible | 2 | 지금 시야 안 | 완전 투명(안개 없음) |

- 지형/자원 노드: **숨기지 않는다.** Explored가 되면 계속 보이는 상태로 둔다 (안개 텍스처가 시각적으로 어둡게만 표시).
- 플레이어 자신의 유닛/건물: 항상 100% 보임(당연히 자기 자신은 안 가림).
- **적(`EnemyController`) 유닛만** 이 그리드의 "지금 Visible인가"에 따라 렌더러를 켜고 끈다 — 시야를 벗어나면 실시간으로 사라진다(마지막 위치를 "기억"해서 잔상으로 보여주지 않음. 이 프로젝트엔 적 전용 AI 기지가 없어 "정찰 기억" 기능의 이득이 적고, 몬스터/수비대가 이동하므로 잔상을 보여주면 오히려 혼란을 줌).

### 3.2 시야 소스
- `UnitController`, `BuildingController`에 `sightRange`(float) 필드를 추가(공격력/방어력과 같은 배치 방식).
- 매 재계산 틱마다 `RTSUnitController.UnitList` + `BuildingList`를 순회하며 각자의 위치를 중심으로 `sightRange` 반경 안의 셀을 Visible로 표시. **기존에 이미 유지되던 리스트를 그대로 재사용**하므로 별도 등록 시스템이 필요 없다.

### 3.3 그리드 ↔ 텍스처 통합
- 셀 상태를 그대로 `Texture2D`의 알파값으로 인코딩한다: Unexplored=alpha 1(불투명 검정), Explored=alpha `exploredAlpha`(기본 0.55), Visible=alpha 0(투명).
- 텍스처의 `FilterMode.Bilinear`를 사용하면 셀 경계가 GPU 보간으로 자연스럽게 부드러워진다 — CPU 쪽에서 블러 처리를 직접 구현할 필요가 없다.
- **같은 텍스처 하나**를 3D 월드용 안개 평면과 미니맵 오버레이 양쪽에 동시에 물려서 이중 관리를 피한다.

### 3.4 3D 월드 렌더링
- 맵 전체를 덮는 `Plane` 프리미티브(또는 `Quad`)를 지면보다 살짝 위(예: Y=0.3)에 배치.
- 머티리얼: URP 기본 `Universal Render Pipeline/Unlit` 셰이더, **Surface Type = Transparent**로 설정하고 Base Map에 위 텍스처를 연결. 커스텀 셰이더 코드가 필요 없다(URP 기본 언릿 셰이더만으로 알파 블렌딩이 됨).
- **주의**: 이 평면의 `MeshCollider`는 반드시 제거(또는 비활성화)해야 한다. `UserControl`/`MinimapController`/`PlacementSystem`이 지면 레이캐스트(`layerGround`, `Plane(Vector3.up, Vector3.zero)`)에 의존하는데, 안개 평면이 그 레이를 가로채면 클릭 좌표 계산이 깨진다.
- 안개 평면은 어디까지나 "지면 색을 어둡게 덮는 시각 효과"일 뿐, 유닛 모델을 가리는 용도가 아니다(적을 숨기는 건 3.5절의 렌더러 토글로 처리). 유닛보다 낮은 높이에 두면 된다.

### 3.5 미니맵 렌더링
- 기존 미니맵 `RawImage` 위에 같은 텍스처를 쓰는 `RawImage`를 하나 더 얹는다(같은 부모, 같은 RectTransform 크기, 더 위 계층).
- 새 `RawImage`의 `Raycast Target`은 반드시 `false`로 꺼야 한다 — 켜두면 `MinimapController`(`IPointerClickHandler`)의 클릭 이벤트를 가로채서 미니맵 클릭 이동이 멈춘다.

### 3.6 적 유닛 은폐
- `EnemyController`에 `ApplyFogVisibility(bool visible)` 추가: 자식 `Renderer`들의 `enabled`만 토글하고, **콜라이더/컴포넌트는 그대로 둔다.**
  - 이유: `AttackRange`(플레이어 유닛 쪽 트리거), `HealthManager` 등 기존에 잘 동작하는 전투 상태머신을 전혀 건드리지 않기 위함. 시야 밖이어도 물리적으로는 그대로 존재하고, 오직 "화면에 그려지느냐"만 바뀐다.
  - 체력바는 이미 있는 `HealthManager.SetHealthBarVisible()`을 재사용.
  - 선택 마커(`enemyMarker`)는 숨겨질 때 강제로 끄고, 다시 보일 때는 "지금 선택된 상태인지"를 확인해서 복원한다(`FlashMarkerRoutine`이 이미 쓰는 것과 동일한 복원 패턴, `EnemyController.cs:61-66`).
- **알려진 구멍과 최소 수정**: 렌더러만 끄면 콜라이더는 살아있으므로, `UserControl`의 `Physics.Raycast(..., layerEnemy)`가 숨겨진 적도 계속 맞힌다 → 안 보이는 적을 클릭 선택/공격 지정할 수 있는 버그가 생긴다. 이를 막기 위해 `EnemyController`에 `IsFogHidden()`을 노출하고, `UserControl.HandleLeftClick`/`HandleRightClick`의 적 처리 분기에 `&& !enemy.IsFogHidden()` 조건을 추가하는 것을 권장한다(5.5절 코드).

### 3.7 재계산 주기
- 매 프레임이 아니라 **0.2초 간격 타이머**로 재계산(`FogOfWarManager.recomputeInterval`). 유닛 수십~백 단위, 128×128 그리드 기준으로도 충분히 저렴하지만, 굳이 매 프레임 할 필요가 없다.
- 재계산 비용 원인 두 가지 — (1) 각 시야 소스 주변 바운딩박스 순회, (2) 텍스처 전체 픽셀 재작성. 둘 다 그리드가 128×128(=16384셀) 정도면 0.2초에 한 번 하기엔 무시할 수준.

## 4. 신규/수정 파일 목록 (제안)

| 파일 | 종류 | 내용 |
|---|---|---|
| `Assets/Scripts/System/FogOfWarManager.cs` | 신규 | 그리드/텍스처/재계산 로직 전체 |
| `Assets/Scripts/System/RTSUnitController.cs` | 수정 | `EnemyList` 추가 (기존 `UnitList`/`BuildingList`와 동일 패턴) |
| `Assets/Scripts/Enemy/EnemyController.cs` | 수정 | 등록/해제, 렌더러 캐싱, `ApplyFogVisibility`, `IsFogHidden` |
| `Assets/Scripts/Unit/UnitController.cs` | 수정 | `sightRange` 필드 + `GetSightRange()` |
| `Assets/Scripts/Building/BuildingController.cs` | 수정 | `sightRange` 필드 + `GetSightRange()` |
| `Assets/Scripts/UserControl/UserControl.cs` | 수정 (권장) | 숨겨진 적 클릭/타겟팅 방지 |
| 씬(`SampleScene.unity`) | 에디터 작업 | 안개 평면 GameObject, 안개 머티리얼, 미니맵 오버레이 `RawImage`, `FogOfWarManager` 오브젝트 배치 및 인스펙터 연결 |

## 5. 제안 코드

### 5.1 `FogOfWarManager.cs` (신규)

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;

// 전장의 안개(Fog of War)를 총괄하는 매니저.
// 맵을 격자(그리드)로 나눠 각 셀을 Unexplored(미탐색) / Explored(탐색했지만 비가시) / Visible(가시)
// 3단계로 관리하고, 그 상태를 텍스처 알파값으로 인코딩해 3D 월드 평면과 미니맵에 동시에 입힌다.
// 적(EnemyController)은 "지금 Visible인 셀에 있는가"에 따라 렌더러를 켜고 끈다(전투 로직은 안 건드림).
public class FogOfWarManager : MonoBehaviour
{
    private enum CellState : byte { Unexplored = 0, Explored = 1, Visible = 2 }

    [Header("참조")]
    [SerializeField] private RTSUnitController rtsController;
    [SerializeField] private Renderer fogWorldRenderer; // 3D 안개 평면의 MeshRenderer
    [SerializeField] private RawImage minimapFogImage;  // 미니맵 위에 겹쳐진 안개 RawImage

    [Header("맵 범위 (실제 지면/플레이 가능 영역 기준으로 씬에서 확인 후 조정할 것)")]
    [SerializeField] private Vector2 worldMin = new Vector2(-130f, -130f); // (worldX, worldZ) 최소
    [SerializeField] private Vector2 worldMax = new Vector2(40f, 40f);    // (worldX, worldZ) 최대

    [Header("그리드")]
    [SerializeField] private int gridResolution = 128; // 한 변당 셀 개수 (정사각형 그리드)

    [Header("갱신 주기/표현")]
    [SerializeField] private float recomputeInterval = 0.2f;
    [SerializeField, Range(0f, 1f)] private float exploredAlpha = 0.55f; // 탐색했지만 비가시인 영역의 어둡기

    private CellState[,] state;
    private bool[,] visibleMask; // 이번 틱에 새로 계산된 가시 셀 (재사용해서 GC 방지)
    private Color32[] pixelBuffer;
    private Texture2D fogTexture;

    private float timer;
    private float cellSizeX;
    private float cellSizeZ;

    private void Awake()
    {
        if (rtsController == null)
            rtsController = FindFirstObjectByType<RTSUnitController>();

        cellSizeX = (worldMax.x - worldMin.x) / gridResolution;
        cellSizeZ = (worldMax.y - worldMin.y) / gridResolution;

        state = new CellState[gridResolution, gridResolution];
        visibleMask = new bool[gridResolution, gridResolution];
        pixelBuffer = new Color32[gridResolution * gridResolution];

        fogTexture = new Texture2D(gridResolution, gridResolution, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        // 시작 시 전체 미탐색(불투명 검정)으로 초기화
        for (int i = 0; i < pixelBuffer.Length; i++)
            pixelBuffer[i] = new Color32(0, 0, 0, 255);
        fogTexture.SetPixels32(pixelBuffer);
        fogTexture.Apply(false);

        if (fogWorldRenderer != null)
            fogWorldRenderer.material.mainTexture = fogTexture; // .material 접근 시 자동으로 인스턴스화됨

        if (minimapFogImage != null)
        {
            minimapFogImage.texture = fogTexture;
            minimapFogImage.color = Color.white;
        }

        // 시작하자마자 한 번은 즉시 계산해서 첫 프레임부터 자기 진영 시야가 보이게 한다
        Recompute();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < recomputeInterval)
            return;

        timer = 0f;
        Recompute();
    }

    private void Recompute()
    {
        Array.Clear(visibleMask, 0, visibleMask.Length);

        foreach (UnitController unit in rtsController.UnitList)
        {
            if (unit == null) continue;
            MarkVisible(unit.transform.position, unit.GetSightRange());
        }

        foreach (BuildingController building in rtsController.BuildingList)
        {
            if (building == null) continue;
            MarkVisible(building.transform.position, building.GetSightRange());
        }

        ApplyVisibilityToState();
        RebuildTexture();
        UpdateEnemyVisibility();
    }

    // worldPos를 중심으로 sightRange 반경 안의 셀을 이번 틱 가시 셀로 표시한다.
    private void MarkVisible(Vector3 worldPos, float sightRange)
    {
        if (sightRange <= 0f)
            return;

        Vector2Int center = WorldToCell(worldPos);

        int radiusCellsX = Mathf.CeilToInt(sightRange / cellSizeX);
        int radiusCellsZ = Mathf.CeilToInt(sightRange / cellSizeZ);

        int minCx = Mathf.Max(0, center.x - radiusCellsX);
        int maxCx = Mathf.Min(gridResolution - 1, center.x + radiusCellsX);
        int minCy = Mathf.Max(0, center.y - radiusCellsZ);
        int maxCy = Mathf.Min(gridResolution - 1, center.y + radiusCellsZ);

        float sqrRangeX = radiusCellsX * radiusCellsX;
        float sqrRangeZ = radiusCellsZ * radiusCellsZ;

        for (int y = minCy; y <= maxCy; y++)
        {
            for (int x = minCx; x <= maxCx; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;

                // 셀 크기가 X/Z로 다를 수 있어 각 축 반경으로 정규화한 타원 판정 (정사각 그리드면 원과 동일)
                if ((dx * dx) / Mathf.Max(sqrRangeX, 0.0001f) + (dy * dy) / Mathf.Max(sqrRangeZ, 0.0001f) <= 1f)
                    visibleMask[x, y] = true;
            }
        }
    }

    // 이번 틱 가시 마스크를 실제 셀 상태에 반영한다: 가시 → Visible, 가시였다가 빠짐 → Explored로 강등.
    private void ApplyVisibilityToState()
    {
        for (int y = 0; y < gridResolution; y++)
        {
            for (int x = 0; x < gridResolution; x++)
            {
                if (visibleMask[x, y])
                    state[x, y] = CellState.Visible;
                else if (state[x, y] == CellState.Visible)
                    state[x, y] = CellState.Explored;
            }
        }
    }

    private void RebuildTexture()
    {
        for (int y = 0; y < gridResolution; y++)
        {
            for (int x = 0; x < gridResolution; x++)
            {
                byte alpha = state[x, y] switch
                {
                    CellState.Visible => (byte)0,
                    CellState.Explored => (byte)(exploredAlpha * 255),
                    _ => (byte)255,
                };

                pixelBuffer[y * gridResolution + x] = new Color32(0, 0, 0, alpha);
            }
        }

        fogTexture.SetPixels32(pixelBuffer);
        fogTexture.Apply(false);
    }

    // 적 유닛은 "지금 이 순간 Visible인 셀에 있는가"만 본다 (Explored든 Unexplored든 전부 숨김 대상).
    private void UpdateEnemyVisibility()
    {
        foreach (EnemyController enemy in rtsController.EnemyList)
        {
            if (enemy == null) continue;

            bool visible = IsVisible(enemy.transform.position);
            enemy.ApplyFogVisibility(visible);
        }
    }

    private Vector2Int WorldToCell(Vector3 worldPos)
    {
        float u = Mathf.InverseLerp(worldMin.x, worldMax.x, worldPos.x);
        float v = Mathf.InverseLerp(worldMin.y, worldMax.y, worldPos.z);

        int cx = Mathf.Clamp(Mathf.FloorToInt(u * gridResolution), 0, gridResolution - 1);
        int cy = Mathf.Clamp(Mathf.FloorToInt(v * gridResolution), 0, gridResolution - 1);

        return new Vector2Int(cx, cy);
    }

    // ===== 외부 조회용 API (필요해지면 다른 시스템에서도 재사용 가능) =====
    public bool IsVisible(Vector3 worldPos) => state[WorldToCell(worldPos).x, WorldToCell(worldPos).y] == CellState.Visible;
    public bool IsExplored(Vector3 worldPos) => state[WorldToCell(worldPos).x, WorldToCell(worldPos).y] != CellState.Unexplored;
}
```

### 5.2 `RTSUnitController.cs` — `EnemyList` 추가

```csharp
// 필드 추가 (UnitList/BuildingList 옆)
public List<EnemyController> EnemyList;

// Awake() 안, 기존 초기화 옆에 추가
EnemyList = new List<EnemyController>();

// Update() 안, 기존 RemoveAll들 옆에 추가
EnemyList.RemoveAll(enemy => enemy == null);
```

### 5.3 `EnemyController.cs` — 등록/해제 + 안개 은폐

```csharp
[SerializeField] private GameObject enemyMarker;
[SerializeField] private Sprite icon;
[SerializeField] private string enemyName;
[SerializeField] private int attackDamage;
[SerializeField] private int armor;

// ===== 전장의 안개 =====
private Renderer[] renderers;
private HealthManager healthManager;
private bool fogHidden; // true면 현재 시야 밖(숨김 상태)

private void Awake()
{
    renderers = GetComponentsInChildren<Renderer>(true);
    healthManager = GetComponent<HealthManager>();
}

void Start()
{
    if (enemyMarker != null)
        enemyMarker.SetActive(false);

    rtsController = FindFirstObjectByType<RTSUnitController>();
    rtsController.EnemyList.Add(this); // FogOfWarManager가 매 틱 순회할 수 있도록 등록
}

// FogOfWarManager가 재계산 틱마다 호출한다. 시야 밖이면 렌더러/체력바/마커만 끄고
// 콜라이더·HealthManager·AttackRange 등 전투 판정 관련 컴포넌트는 그대로 둔다(순수 시각적 은폐).
public void ApplyFogVisibility(bool visible)
{
    if (visible == !fogHidden)
        return; // 상태 변화 없음

    fogHidden = !visible;

    foreach (Renderer r in renderers)
        r.enabled = visible;

    healthManager?.SetHealthBarVisible(visible);

    if (enemyMarker != null)
    {
        bool isSelected = rtsController != null && rtsController.selectedEnemyList.Contains(this);
        enemyMarker.SetActive(visible && isSelected);
    }
}

public bool IsFogHidden() => fogHidden;

// ... FlashMarker/SelectEnemy/DeselectEnemy/GetIcon 등 기존 코드는 그대로 ...

public void Die()
{
    rtsController?.selectedEnemyList.Remove(this);
    rtsController?.EnemyList.Remove(this);

    Destroy(gameObject);
}
```

### 5.4 `UnitController.cs` / `BuildingController.cs` — 시야 반경 필드

```csharp
// UnitController.cs, attackDamage/armor 옆에 추가
[SerializeField] private float sightRange = 10f; // 이 유닛이 밝히는 시야 반경 (FogOfWarManager가 사용)
public float GetSightRange() => sightRange;
```

```csharp
// BuildingController.cs, buildingID 옆에 추가
[SerializeField] private float sightRange = 12f; // 이 건물이 밝히는 시야 반경 (FogOfWarManager가 사용)
public float GetSightRange() => sightRange;
```

### 5.5 `UserControl.cs` — 숨겨진 적 클릭/타겟팅 방지 (권장)

```csharp
// HandleLeftClick() 안, "2. 적 클릭" 분기
if (clickedEnemy)
{
    EnemyController enemy = enemyHit.transform.GetComponent<EnemyController>();

    if (enemy != null && !enemy.IsFogHidden())
    {
        // ... 기존 로직 그대로 ...
    }
}
```
```csharp
// HandleRightClick() 안, "1. 적 우클릭" 분기도 동일하게
if (clickedEnemy && rtsUnitController.IsUnitSelect())
{
    EnemyController enemy = enemyHit.transform.GetComponent<EnemyController>();

    if (enemy != null && !enemy.IsFogHidden())
    {
        // ... 기존 로직 그대로 ...
    }
}
```

## 6. 씬/에디터 설정 체크리스트 (코드 적용 후 수동 작업)

1. **안개 평면**: 빈 곳에 `GameObject > 3D Object > Plane` 생성 → 이름 `FogOfWarPlane`.
   - 위치: `((worldMin.x+worldMax.x)/2, 0.3, (worldMin.y+worldMax.y)/2)` (일단 지면보다 살짝 위, 유닛보다는 아래)
   - 스케일: 기본 Plane은 10×10 유닛이므로 `localScale.x = (worldMax.x-worldMin.x)/10`, `localScale.z = (worldMax.y-worldMin.y)/10`.
   - **`Mesh Collider` 컴포넌트 제거** (지면 클릭/미니맵/건설 배치 레이캐스트를 가리지 않도록).
2. **안개 머티리얼**: 새 Material 생성 → Shader `Universal Render Pipeline/Unlit` → Surface Type `Transparent` → Base Map은 비워둠(런타임에 스크립트가 채움) → `FogOfWarPlane`의 Renderer에 연결.
3. **미니맵 오버레이**: 기존 미니맵 `RawImage`의 형제로 새 `RawImage` 추가(같은 RectTransform 앵커/크기, 계층상 더 위) → `Raycast Target` 체크 해제.
4. **`FogOfWarManager` 오브젝트**: 씬에 빈 GameObject 생성(예: `RTSUnitController`와 같은 위치의 매니저 오브젝트 옆) → 스크립트 부착 → 인스펙터에 `rtsController`, `fogWorldRenderer`(위 1번), `minimapFogImage`(위 3번) 연결.
5. **`worldMin`/`worldMax` 값 확인**: `CameraControl`의 min/max X,Z를 참고값으로 넣었지만, 실제 지면 메시 크기와 다를 수 있으니 씬에서 직접 확인 후 조정.
6. 각 유닛/건물 프리팹에서 `sightRange` 값을 유닛 성격에 맞게 조정(정찰용 유닛은 크게, 공격 사거리보다 항상 크거나 같게 — 7절 참고).

## 7. 성능/튜닝 노트
- 그리드 128×128 기준 재계산 비용은 시야 소스 수십 개, 0.2초 주기에서 무시할 수준. 유닛 수가 매우 많아지면(수백+) `recomputeInterval`을 늘리거나 `gridResolution`을 낮추는 것으로 조정.
- `exploredAlpha`를 낮추면(예: 0.35) "탐색된 지역"이 더 밝게, 높이면(예: 0.7) 더 어둡게 보임 — 취향껏 조정.
- **`sightRange`는 항상 해당 유닛의 공격 사거리(`AttackRange.UnitRange`)보다 크거나 같게 설정하는 것을 권장**한다. 현재 설계는 안개 은폐를 콜라이더가 아니라 렌더러만 끄는 방식으로 처리하므로(3.6절), 만약 공격 사거리가 시야보다 넓게 설정된 유닛이 있으면 "보이지도 않는데 자동으로 교전하는" 어색한 상황이 생길 수 있다.

## 8. 다음 단계
이 문서는 설계 + 제안 코드까지만이다. 실제로 `Assets/Scripts`에 반영할지, 반영한다면 5.1~5.5 중 전체/일부(특히 5.5는 선택사항) 중 어디까지 적용할지 알려주면 그 다음에 실제 코드 수정과 씬 설정 안내를 진행하겠다.
