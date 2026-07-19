# 0175 - 점령된 영역(아군 영토) 내부 영구 시야 확보 설계

## 요청

"현재 fogofwar 스크립트에서 점령된 영역 안은 시야가 확보되도록 하려면 어떤식으로 하면 좋을까" —
`CaptureSystem`으로 아군(`CaptureOwner.Ally`)이 점령한 `TerritoryZone` 영역은, 그 안에 유닛/건물이
없어도(즉 `FogRevealerAgent`의 시야 범위 밖이어도) 항상 밝게(Fog 없이) 보이도록 하고 싶다는 질문.
[[confirm_before_implementing]] 규칙에 따라 먼저 설계만 정리하고 구현 여부를 확인받는다.

## 현재 구조 조사

- 안개 자체는 `Assets/AssetFolder/AOSFogWar/csFogWar.cs`(에셋, 이미 0173에서 grid cap 등 일부
  수정함) + `Shadowcaster.cs`가 담당. 매 갱신 주기마다:
  1. `shadowcaster.ResetTileVisibility()` — 전체 타일을 `Hidden`(또는 `PreviouslyRevealed`)으로 리셋
  2. 등록된 `FogRevealer`(유닛/건물, [[fogofwar-eye-scripts-implementation]] 참고)마다 시야 범위만큼
     `Shadowcaster.ProcessLevelData()`로 `Revealed` 마킹
  3. `UpdateFogPlaneTextureTarget()` — 그 결과를 텍스처로 반영
  - 즉 지금은 오직 "현재 그 타일 반경 안에 FogRevealer가 있는가"만으로 밝기가 결정됨. 유닛이 점령지를
    떠나면 그 자리는 다시 안개가 낀다.
- 점령 상태는 `Assets/Scripts/CaptureSystem/CaptureSystem.cs`가 관리 — 점령 완료 시
  `territoryZone.Owner = CaptureOwner.Ally`(또는 `Enemy`/`Neutral`)로 설정.
- `Assets/Scripts/CaptureSystem/TerritoryZone.cs`는 핀 포인트 다각형으로 영역을 정의하고,
  `Contains(Vector3 worldPos)`로 point-in-polygon 판정을 이미 제공함(오목 다각형도 정확).
- `Assets/Scripts/CaptureSystem/TerritoryManager.cs`는 씬의 모든 `TerritoryZone`을 등록해두고
  `IsInsideAlliedTerritory(worldPos)` 같은 질의만 제공 — 하지만 "존(zone) 목록 자체"를 순회할 방법은
  아직 외부에 노출되어 있지 않음(내부 `zones` 리스트가 private).
- `csFogWar`는 `shadowcaster`(public get), `levelData`(public get, `levelDimensionX/Y` 포함),
  `WorldToLevel()`/`GetWorldX()`/`GetWorldY()`/`CheckLevelGridRange()`가 전부 public이라, **에셋 파일을
  전혀 건드리지 않고도** 외부에서 특정 그리드 타일을 강제로 `Revealed`로 마킹할 수 있음
  (`fogWar.shadowcaster.fogField[x][y] = Shadowcaster.LevelColumn.ETileVisibility.Revealed;`).
  [[fogofwar-eye-scripts-implementation]](0173)에서 만든 `FogRevealerAgent`와 동일하게, "에셋은
  안 건드리고 public API로만 어댑터를 붙인다"는 기존 방식을 그대로 따를 수 있다는 뜻.

## 설계 방향

새 컴포넌트 `TerritoryFogReveal`을 씬에 하나 배치(예: 기존 `FogWar` 오브젝트에 추가)해서, 매 프레임
`LateUpdate()`에서 "아군 소유 `TerritoryZone`" 각각에 대해:

1. 그 zone의 다각형 바운딩 박스(월드 좌표 min/max X·Z)만 구하고,
2. 그 바운딩 박스에 해당하는 그리드 셀 범위만 순회(zone 하나당 보통 수십~수백 셀 수준이라 매 프레임
   돌려도 부담 없음 — 그리드 전체(현재 114×110, 최대 256×256)를 매 프레임 스캔하지는 않음),
3. 각 셀의 월드 중심 좌표가 실제로 `zone.Contains(...)` 안에 들어가면 `fogField`에서 그 좌표를
   `Revealed`로 강제 마킹.

`LateUpdate`은 Unity 실행 순서상 모든 오브젝트의 `Update()`(= `csFogWar.Update()` 포함) 이후에
실행되는 게 보장되므로, `csFogWar`가 그 프레임에 안개를 갱신(`ResetTileVisibility` → revealer 반영)한
직후 항상 영토 강제 반영이 덧씌워진다. 안개 텍스처 자체는 `csFogWar`가 주기적으로만(FogRefreshRate,
그리고 아무도 안 움직이면 스킵) 다시 그리므로, "영토를 막 점령한 그 프레임"에 시야가 100% 즉시 반영되진
않을 수 있지만(다음 유닛 이동 시점에 반영), 점령 판정 자체가 "그 영역 안에 아군 유닛이 있어야" 성립하는
구조라 사실상 그 유닛의 자체 시야로 이미 그 부근은 보이는 상태이므로 실사용에 체감되는 지연은 없음.

이 방식을 선택한 이유(대안과 비교):
- **대안 A**: `csFogWar.cs`(에셋)의 `UpdateFogField()` 안에 영토 반영 로직을 직접 끼워 넣기 — 더
  "즉시" 반영되지만, 에셋 파일에 프로젝트 전용 클래스(`TerritoryManager`) 의존성이 생겨 결합도가
  올라가고, 이미 0173에서 채택한 "에셋은 최소한만 건드리고 어댑터로 분리" 원칙과 어긋남.
- **대안 B(채택)**: 완전히 별도 컴포넌트로, `csFogWar`/`Shadowcaster`/`TerritoryZone`의 기존 public
  API만 사용 — 에셋 파일은 한 줄도 안 건드림. 유일하게 필요한 프로젝트 코드 변경은
  `TerritoryManager`에 zone 목록을 읽기 전용으로 노출하는 것뿐(신규 필드 추가 아님, 기존 private
  리스트를 읽기 전용으로 노출).

## 계획된 코드 변경

### 1. `Assets/Scripts/CaptureSystem/TerritoryManager.cs` — zone 목록 읽기 전용 노출

Before:
```csharp
public static class TerritoryManager
{
    private static readonly List<TerritoryZone> zones = new List<TerritoryZone>();

    public static void Register(TerritoryZone zone)
    {
        if (!zones.Contains(zone)) zones.Add(zone);
    }

    public static void Unregister(TerritoryZone zone) => zones.Remove(zone);

    public static bool IsInsideTerritory(Vector3 worldPos, CaptureOwner owner)
    {
        ...
    }

    public static bool IsInsideAlliedTerritory(Vector3 worldPos) => IsInsideTerritory(worldPos, CaptureOwner.Ally);
}
```

After:
```csharp
public static class TerritoryManager
{
    private static readonly List<TerritoryZone> zones = new List<TerritoryZone>();

    // TerritoryFogReveal 등 zone을 직접 순회해야 하는 외부 코드를 위한 읽기 전용 접근
    public static IReadOnlyList<TerritoryZone> Zones => zones;

    public static void Register(TerritoryZone zone)
    {
        if (!zones.Contains(zone)) zones.Add(zone);
    }

    public static void Unregister(TerritoryZone zone) => zones.Remove(zone);

    public static bool IsInsideTerritory(Vector3 worldPos, CaptureOwner owner)
    {
        ...
    }

    public static bool IsInsideAlliedTerritory(Vector3 worldPos) => IsInsideTerritory(worldPos, CaptureOwner.Ally);
}
```

### 2. `Assets/Scripts/FogOfWar/TerritoryFogReveal.cs` (신규)

Before: (파일 없음)

After:
```csharp
using FischlWorks_FogWar;
using UnityEngine;

// 아군(CaptureOwner.Ally)이 점령한 TerritoryZone 내부는 그 안에 FogRevealer(유닛/건물)가 없어도
// 항상 밝게 보이도록, csFogWar/Shadowcaster의 기존 public API만으로 매 프레임 강제 반영한다.
// csFogWar.cs / Shadowcaster.cs(에셋)는 전혀 건드리지 않는다 - FogRevealerAgent와 동일한 원칙.
public class TerritoryFogReveal : MonoBehaviour
{
    private csFogWar fogWar;

    private void Start()
    {
        fogWar = FindFirstObjectByType<csFogWar>();

        if (fogWar == null)
            Debug.LogWarning($"{name}: csFogWar를 씬에서 찾지 못해 영토 시야를 반영하지 못합니다.", this);
    }

    // csFogWar.Update()(안개 갱신)가 끝난 뒤에 실행되도록 보장하기 위해 LateUpdate 사용
    private void LateUpdate()
    {
        if (fogWar == null) return;

        foreach (TerritoryZone zone in TerritoryManager.Zones)
        {
            if (zone == null || zone.Owner != CaptureOwner.Ally) continue;

            RevealZone(zone);
        }
    }

    private void RevealZone(TerritoryZone zone)
    {
        Vector2[] polygon = zone.GetPolygonXZ();
        if (polygon.Length < 3) return;

        float minX = polygon[0].x, maxX = polygon[0].x;
        float minZ = polygon[0].y, maxZ = polygon[0].y;

        foreach (Vector2 p in polygon)
        {
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.y); maxZ = Mathf.Max(maxZ, p.y);
        }

        Vector2Int gridMin = fogWar.WorldToLevel(new Vector3(minX, 0, minZ));
        Vector2Int gridMax = fogWar.WorldToLevel(new Vector3(maxX, 0, maxZ));

        for (int gx = gridMin.x; gx <= gridMax.x; gx++)
        {
            for (int gy = gridMin.y; gy <= gridMax.y; gy++)
            {
                Vector2Int cell = new Vector2Int(gx, gy);
                if (!fogWar.CheckLevelGridRange(cell)) continue;

                Vector3 cellWorldPos = new Vector3(fogWar.GetWorldX(gx), 0, fogWar.GetWorldY(gy));
                if (!zone.Contains(cellWorldPos)) continue;

                fogWar.shadowcaster.fogField[gx][gy] = Shadowcaster.LevelColumn.ETileVisibility.Revealed;
            }
        }
    }
}
```

## 적용 후 남는 수동 작업

- 새 컴포넌트 `TerritoryFogReveal`을 씬의 아무 오브젝트에나(예: 기존 `FogWar` 오브젝트) 추가로 붙여야
  동작함 — 이번 변경은 스크립트 파일만 만들고 씬(.unity) 파일은 건드리지 않을 것이므로, 이 컴포넌트
  부착은 사용자가 에디터에서 직접 하거나, 원하면 이 세션에서 이어서 `SampleScene.unity`에 YAML로
  추가해줄 수 있음(0174에서 `FogWar` 오브젝트를 그렇게 추가한 전례 있음).
- 적 점령지(`CaptureOwner.Enemy`)에도 같은 상시 시야를 줄지 여부는 이번 범위 밖으로 뒀음(질문이
  "점령된 영역"이라 아군 기준으로만 해석함) — 필요하면 `zone.Owner != CaptureOwner.Ally` 조건만
  바꾸면 됨.

## 확인 결과 및 구현

사용자가 "이대로 점령지만 시야 확보되도록 해줘, 그리고 점령이 해제되면 그 시야도 다시 없어지도록"으로
승인 — 위 설계 그대로 적용함(`TerritoryManager.Zones` 노출 + `TerritoryFogReveal.cs` 신규).

**"점령 해제 시 시야도 다시 사라지는 것"은 별도 코드가 필요 없었음** — `TerritoryFogReveal.LateUpdate()`가
매 프레임 `zone.Owner`를 다시 확인하므로, 점령이 풀려 `Owner`가 `Ally`가 아니게 되는 순간부터 그 zone은
더 이상 강제 반영 대상에서 제외된다. 그러면 `csFogWar`가 다음 갱신 주기에 `ResetTileVisibility()`로
타일을 되돌린 뒤(기본값 `keepRevealedTiles = false`라 완전히 `Hidden`으로) 그 자리를 비추는
`FogRevealer`가 없으면 자연히 다시 안개가 낀다. 즉 "매 프레임 현재 소유자 기준으로만 반영"하는 설계
자체가 해제 케이스를 이미 포함하고 있어 위 계획안에서 코드 변경은 없었음.

## 남은 수동 작업

- 새 컴포넌트 `TerritoryFogReveal`을 씬의 아무 오브젝트에나(예: 기존 `FogWar` 오브젝트) 붙여야 실제로
  동작 시작함 — 이번 세션에서는 스크립트 파일만 추가했고 `.unity` 씬 파일은 건드리지 않음.
