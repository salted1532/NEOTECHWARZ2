# 0166 - 전장의 안개(Fog of War): `FogOfWar` 폴더 + `Eye` 컴포넌트 재설계

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **설계 + 제안 코드**만 담고
> 실제 프로젝트 파일(`Assets/Scripts/**`)은 아직 건드리지 않았다. 아래 내용을 검토한 뒤 실제로
> 적용할지(전체/일부) 알려주면 그때 코드로 반영한다.

## 1. 요청

"`FogOfWar` 폴더 안에 스크립트를 작성할 것. 요구사항: (1) 안개 셀 크기를 조절할 수 있어야 한다,
(2) 맵 크기에 따라 셀 개수를 조절할 수 있어야 한다, (3) 시야가 있는 유닛/건물에 붙일 `Eye` 스크립트가
있으면 좋겠고, `Eye`는 부착된 대상별로 시야 범위(셀 칸 수 기준)를 조절할 수 있어야 한다."

## 2. 이전 문서(0069)와의 관계

`doc/0069-fog-of-war-design.md`에서 이미 한 번 전장의 안개를 설계했지만, **실제 코드에는 반영되지
않은 상태**였다(`RTSUnitController`에 `EnemyList`/`sightRange`/`GetSightRange` 등이 지금 코드베이스에
전혀 없음을 확인). 그래서 이번이 사실상 처음 구현이며, 이번 요청은 0069와 구조가 다르다:

| 항목 | 0069 (구) | 이번 요청 (신) |
|---|---|---|
| 파일 위치 | `Assets/Scripts/System/`, 기존 폴더에 분산 | `Assets/Scripts/FogOfWar/` 전용 폴더 |
| 시야 소스 등록 방식 | `UnitController`/`BuildingController`에 `sightRange` 필드 직접 추가 + `RTSUnitController.UnitList`/`BuildingList` 순회 | 독립된 `Eye` 컴포넌트가 스스로 등록/해제(다른 기존 스크립트 **무수정**) |
| 시야 범위 단위 | 월드 단위 미터(float) | **셀 칸 수**(int) |
| 그리드 크기 | 고정 `gridResolution`(예: 128×128) 인스펙터 값 | **셀 크기 + 맵 크기로부터 자동 산출** |
| 적 은폐(안 보이는 적 렌더러 끄기) | 포함 | **이번 범위에서 제외**(8절 참고) |

0069에서 여전히 유효하게 재사용하는 부분: 3단계 가시성 모델(Unexplored/Explored/Visible), 텍스처
알파 인코딩 + 3D 평면/미니맵 오버레이 공유, 0.2초 재계산 주기, `MeshCollider` 제거 주의사항.

## 3. 현재 프로젝트 재확인

- `Assets/Scripts/FogOfWar/` 폴더는 아직 없음(신규 생성 필요).
- `RTSUnitController`, `UnitController`, `BuildingController`, `EnemyController` 어디에도 시야 관련
  필드/리스트가 없음 — 이번 설계는 **이 파일들을 전혀 건드리지 않고** `Eye`가 독립적으로 동작하도록 한다.
  (요청에서 "유닛이나 건물에 집어넣을 eye 스크립트"라고 했으므로, `Eye`는 유닛/건물 프리팹에 컴포넌트로
  붙이기만 하면 되고 `UnitController`/`BuildingController` 소스 수정은 필요 없음.)
- `CameraControl.cs`(`minX/maxX/minZ/maxZ = -130~40`)와 `MinimapController.cs`(`Plane(Vector3.up, Vector3.zero)`
  기준 Y=0 지면 평면)는 0069와 동일하게 유효 — 맵을 Y=0 평면 위 XZ 사각형으로 취급.

## 4. 설계

### 4.1 폴더/파일 구조 (신규)

```
Assets/Scripts/FogOfWar/
├── FogOfWarManager.cs   # 그리드/텍스처/재계산을 총괄하는 싱글턴 매니저
└── Eye.cs               # 시야 소스 컴포넌트 (유닛/건물 등 아무 오브젝트에나 부착)
```

### 4.2 셀 크기 → 맵 크기 → 셀 개수

기존(0069)처럼 "셀 개수(gridResolution)"를 고정 인스펙터 값으로 직접 넣는 대신, 두 개의 독립된
조절 값에서 셀 개수를 **자동 산출**한다:

- `cellSize` (float, 월드 단위) — 셀 하나의 한 변 길이. **조절 가능.**
- `mapOrigin` (Vector2, XZ) + `mapSize` (Vector2, XZ 폭/깊이) — 안개를 덮을 맵의 실제 영역. **조절 가능.**
- `gridWidth = Ceil(mapSize.x / cellSize)`, `gridHeight = Ceil(mapSize.y / cellSize)` — **자동 계산**(직접 입력 X).

이렇게 하면 "셀 크기를 조절할 수 있어야 하고" + "맵 크기에 따른 셀 개수를 조절할 수 있어야 하고"
두 요구사항이 하나의 관계식으로 동시에 만족된다: 셀 크기를 바꾸면 셀 개수가 자동으로 따라 바뀌고,
맵 크기를 바꿔도 마찬가지다. `OnValidate()`에서 매 인스펙터 변경마다 `gridWidth`/`gridHeight`를
재계산해 에디터에서 바로 확인 가능하게 한다.

### 4.3 3단계 가시성 모델 (0069 재사용)

| 상태 | 값 | 의미 | 시각 효과 |
|---|---|---|---|
| Unexplored | 0 | 한 번도 밝힌 적 없음 | 완전 검정(불투명) |
| Explored | 1 | 밝혔었지만 지금은 시야 밖 | 반투명 회색 |
| Visible | 2 | 지금 시야 안 | 완전 투명 |

### 4.4 `Eye` 컴포넌트

- 유닛/건물 프리팹(또는 씬 오브젝트) 아무 곳에나 붙이는 독립 컴포넌트.
- `[SerializeField] private int sightRadiusCells` — **부착된 대상별로 인스펙터에서 직접 조절**하는
  시야 반경, 단위는 "셀 칸 수"(요청사항 그대로). 예: 정찰 유닛은 8칸, 건물은 4칸처럼 프리팹마다 다르게.
- `OnEnable()`에서 `FogOfWarManager.Register(this)`, `OnDisable()`(파괴 시에도 자동 호출됨)에서
  `FogOfWarManager.Unregister(this)` — **`RTSUnitController`나 각 컨트롤러의 리스트/생명주기를 전혀
  건드리지 않고** 스스로 등록·해제한다. 유닛이 죽어서 `Destroy()`되면 `OnDisable`이 자동 호출되므로
  별도 사망 처리 훅도 필요 없다.
- 시야 판정은 셀 좌표계에서 정수 반경으로 바로 계산(월드 미터 → 셀 변환 왕복이 없어 0069보다 단순함).

### 4.5 텍스처 통합 / 3D 월드 렌더링 / 미니맵 렌더링

0069의 3.3~3.5절과 동일한 방식 재사용:
- 셀 상태 → `Texture2D` 알파값(Unexplored=255, Explored=`exploredAlpha*255`, Visible=0), `Bilinear` 필터로
  경계 자연 보간.
- 같은 텍스처를 3D 안개 평면(URP Unlit, Transparent, **MeshCollider 제거 필수**)과 미니맵 오버레이
  `RawImage`(**Raycast Target 끄기 필수** — 안 끄면 미니맵 클릭 이동이 막힘) 양쪽에 공유.

### 4.6 재계산 주기

0.2초 간격 타이머(`recomputeInterval`)로 재계산. 등록된 `Eye` 목록을 순회하며 셀 반경만큼 마킹 →
`Visible`이 아니게 된 셀은 `Explored`로 강등 → 텍스처 재작성.

### 4.7 적 은폐는 이번 범위 밖

이번 요청은 "셀 크기/셀 개수 조절 + Eye 컴포넌트"까지다. 0069에서 다뤘던 "안개 밖 적 렌더러 끄기"
(`EnemyController.ApplyFogVisibility`, `UserControl`의 숨겨진 적 클릭 방지 등)는 **의도적으로 포함하지
않았다** — 지금 단계에서는 안개 시각 효과(맵을 어둡게 덮는 것)까지만 구현하고, "적을 실제로 숨길지"는
별도 결정 사항으로 남겨둔다(8절 질문 참고).

## 5. 신규 파일 목록 (제안)

| 파일 | 종류 | 내용 |
|---|---|---|
| `Assets/Scripts/FogOfWar/FogOfWarManager.cs` | 신규 | 그리드(셀 크기/맵 크기→셀 개수 자동 산출)/텍스처/재계산, `Eye` 등록소 |
| `Assets/Scripts/FogOfWar/Eye.cs` | 신규 | 시야 소스 컴포넌트 (셀 단위 반경, 자가 등록/해제) |

기존 파일(`RTSUnitController.cs`, `UnitController.cs`, `BuildingController.cs`, `EnemyController.cs`,
`UserControl.cs`)은 **수정 없음.**

## 6. 제안 코드

### 6.1 `Eye.cs` (신규)

```csharp
using UnityEngine;

// 시야를 제공하는 소스에 부착하는 컴포넌트. 유닛/건물 등 시야가 필요한 오브젝트에 붙이기만 하면
// FogOfWarManager에 스스로 등록/해제한다(다른 컨트롤러 스크립트를 건드릴 필요 없음).
public class Eye : MonoBehaviour
{
    [Header("시야 범위 (셀 칸 수 기준, 대상별로 조절)")]
    [SerializeField, Min(1)] private int sightRadiusCells = 5;

    private void OnEnable()
    {
        FogOfWarManager.Register(this);
    }

    private void OnDisable()
    {
        // 오브젝트가 Destroy()될 때도 자동 호출되므로, 유닛/건물 사망 처리 쪽을 따로 손댈 필요가 없다.
        FogOfWarManager.Unregister(this);
    }

    public int GetSightRadiusCells() => sightRadiusCells;
    public Vector3 GetWorldPosition() => transform.position;
}
```

### 6.2 `FogOfWarManager.cs` (신규)

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 전장의 안개(Fog of War)를 총괄하는 싱글턴 매니저.
// 맵을 격자(그리드)로 나눠 각 셀을 Unexplored(미탐색) / Explored(탐색했지만 비가시) / Visible(가시)
// 3단계로 관리하고, 그 상태를 텍스처 알파값으로 인코딩해 3D 월드 평면과 미니맵에 동시에 입힌다.
// 시야 소스는 Eye 컴포넌트가 스스로 등록/해제하는 방식이라 이 매니저가 유닛/건물 목록을 직접 알 필요가 없다.
public class FogOfWarManager : MonoBehaviour
{
    private enum CellState : byte { Unexplored = 0, Explored = 1, Visible = 2 }

    public static FogOfWarManager Instance { get; private set; }

    [Header("참조")]
    [SerializeField] private Renderer fogWorldRenderer; // 3D 안개 평면의 MeshRenderer
    [SerializeField] private RawImage minimapFogImage;  // 미니맵 위에 겹쳐진 안개 RawImage

    [Header("맵 범위 (씬의 실제 지면/플레이 가능 영역 기준으로 확인 후 조정)")]
    [SerializeField] private Vector2 mapOrigin = new Vector2(-130f, -130f); // (worldX, worldZ) 최소 코너
    [SerializeField] private Vector2 mapSize = new Vector2(170f, 170f);    // (width, depth)

    [Header("셀 크기 (조절 가능 - 이 값과 맵 크기로 셀 개수가 자동 산출됨)")]
    [SerializeField, Min(0.1f)] private float cellSize = 2f;

    [Header("갱신 주기/표현")]
    [SerializeField] private float recomputeInterval = 0.2f;
    [SerializeField, Range(0f, 1f)] private float exploredAlpha = 0.55f;

    [Header("디버그 표시용 (읽기 전용, 자동 계산됨)")]
    [SerializeField] private int gridWidth;
    [SerializeField] private int gridHeight;

    private static readonly HashSet<Eye> eyes = new HashSet<Eye>();

    private CellState[,] state;
    private bool[,] visibleMask;
    private Color32[] pixelBuffer;
    private Texture2D fogTexture;
    private float timer;

    public static void Register(Eye eye) => eyes.Add(eye);
    public static void Unregister(Eye eye) => eyes.Remove(eye);

    private void OnValidate()
    {
        // 인스펙터에서 cellSize/mapSize를 바꿀 때마다 셀 개수를 바로 재계산해 보여준다.
        gridWidth = Mathf.Max(1, Mathf.CeilToInt(mapSize.x / cellSize));
        gridHeight = Mathf.Max(1, Mathf.CeilToInt(mapSize.y / cellSize));
    }

    private void Awake()
    {
        Instance = this;

        gridWidth = Mathf.Max(1, Mathf.CeilToInt(mapSize.x / cellSize));
        gridHeight = Mathf.Max(1, Mathf.CeilToInt(mapSize.y / cellSize));

        state = new CellState[gridWidth, gridHeight];
        visibleMask = new bool[gridWidth, gridHeight];
        pixelBuffer = new Color32[gridWidth * gridHeight];

        fogTexture = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int i = 0; i < pixelBuffer.Length; i++)
            pixelBuffer[i] = new Color32(0, 0, 0, 255);
        fogTexture.SetPixels32(pixelBuffer);
        fogTexture.Apply(false);

        if (fogWorldRenderer != null)
            fogWorldRenderer.material.mainTexture = fogTexture;

        if (minimapFogImage != null)
        {
            minimapFogImage.texture = fogTexture;
            minimapFogImage.color = Color.white;
        }

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

        foreach (Eye eye in eyes)
        {
            if (eye == null) continue;
            MarkVisible(WorldToCell(eye.GetWorldPosition()), eye.GetSightRadiusCells());
        }

        ApplyVisibilityToState();
        RebuildTexture();
    }

    // centerCell을 기준으로 radiusCells 반경(셀 단위 원)을 이번 틱 가시 셀로 표시한다.
    private void MarkVisible(Vector2Int centerCell, int radiusCells)
    {
        int minCx = Mathf.Max(0, centerCell.x - radiusCells);
        int maxCx = Mathf.Min(gridWidth - 1, centerCell.x + radiusCells);
        int minCy = Mathf.Max(0, centerCell.y - radiusCells);
        int maxCy = Mathf.Min(gridHeight - 1, centerCell.y + radiusCells);

        int sqrRadius = radiusCells * radiusCells;

        for (int y = minCy; y <= maxCy; y++)
        {
            for (int x = minCx; x <= maxCx; x++)
            {
                int dx = x - centerCell.x;
                int dy = y - centerCell.y;

                if (dx * dx + dy * dy <= sqrRadius)
                    visibleMask[x, y] = true;
            }
        }
    }

    private void ApplyVisibilityToState()
    {
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
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
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                byte alpha = state[x, y] switch
                {
                    CellState.Visible => (byte)0,
                    CellState.Explored => (byte)(exploredAlpha * 255),
                    _ => (byte)255,
                };

                pixelBuffer[y * gridWidth + x] = new Color32(0, 0, 0, alpha);
            }
        }

        fogTexture.SetPixels32(pixelBuffer);
        fogTexture.Apply(false);
    }

    private Vector2Int WorldToCell(Vector3 worldPos)
    {
        float u = (worldPos.x - mapOrigin.x) / mapSize.x;
        float v = (worldPos.z - mapOrigin.y) / mapSize.y;

        int cx = Mathf.Clamp(Mathf.FloorToInt(u * gridWidth), 0, gridWidth - 1);
        int cy = Mathf.Clamp(Mathf.FloorToInt(v * gridHeight), 0, gridHeight - 1);

        return new Vector2Int(cx, cy);
    }

    // ===== 외부 조회용 API =====
    public bool IsVisible(Vector3 worldPos)
    {
        Vector2Int c = WorldToCell(worldPos);
        return state[c.x, c.y] == CellState.Visible;
    }

    public bool IsExplored(Vector3 worldPos)
    {
        Vector2Int c = WorldToCell(worldPos);
        return state[c.x, c.y] != CellState.Unexplored;
    }
}
```

## 7. 씬/에디터 설정 체크리스트 (코드 적용 후 수동 작업)

1. **안개 평면**: `GameObject > 3D Object > Plane` 생성 → 이름 `FogOfWarPlane`.
   - 위치: 맵 중심(대략 `(mapOrigin.x + mapSize.x/2, 0.3, mapOrigin.y + mapSize.y/2)`).
   - 스케일: 기본 Plane은 10×10 유닛이므로 `localScale.x = mapSize.x/10`, `localScale.z = mapSize.y/10`.
   - **`Mesh Collider` 컴포넌트 제거** (지면 클릭/미니맵/건설 배치 레이캐스트를 가리지 않도록).
2. **안개 머티리얼**: Shader `Universal Render Pipeline/Unlit`, Surface Type `Transparent`, Base Map은
   런타임에 스크립트가 채우므로 비워둠 → `FogOfWarPlane`의 Renderer에 연결.
3. **미니맵 오버레이**: 기존 미니맵 `RawImage`의 형제로 새 `RawImage` 추가(같은 앵커/크기, 계층상 더 위)
   → **`Raycast Target` 체크 해제**.
4. **`FogOfWarManager` 오브젝트**: 씬에 빈 GameObject 생성 → `FogOfWarManager` 부착 → 인스펙터에
   `fogWorldRenderer`(1번), `minimapFogImage`(3번) 연결, `mapOrigin`/`mapSize`/`cellSize` 확인·조정.
5. **`Eye` 부착**: 시야를 제공할 유닛/건물 프리팹(예: `Marine`, `CommandCenter` 등)에 `Eye` 컴포넌트를
   추가하고 `sightRadiusCells`를 대상 성격에 맞게 조정(정찰 유닛은 크게, 고정 건물은 작게 등).

## 8. 확인이 필요한 사항

1. **적 은폐 포함 여부**: 이번 설계는 안개 시각 효과(맵을 어둡게 덮는 것)까지만이다. "시야 밖의 적을
   실제로 안 보이게(렌더러 끄기) + 클릭/타겟팅 방지"까지 원하면 0069의 3.6/5.3/5.5절 패턴을
   `EnemyController`/`UserControl`에 별도로 적용해야 한다(이 경우 그 두 파일은 수정이 생김). 포함할지?
2. **`cellSize` 기본값(2)과 `mapSize` 기본값(170×170)**: `CameraControl`의 이동 제한값(-130~40)을
   참고해 넣었지만 실제 지면 메시 크기와 다를 수 있음 — 씬에서 직접 재보고 확정 필요.
3. **`Eye`를 자원 노드(`ResourceNode`)에도 붙일지**: 요청에는 "유닛이나 건물"만 언급됐는데, 자원 노드도
   시야를 밝히는 소스로 둘지(보통 RTS에선 안 둠 — 자원 자체는 시야를 안 주는 경우가 많음) 확인.

이 문서는 설계 + 제안 코드까지만이다. 적용 여부와 8절 질문에 대한 답을 알려주면 그 다음에
`Assets/Scripts/FogOfWar/` 생성과 씬 설정 안내를 진행하겠다.
