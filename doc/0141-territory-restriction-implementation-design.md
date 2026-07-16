# 0141. 영토 기반 건설/자원채취/생산·연구·회복 제한 구현 설계

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **설계 제안만** 담고 있고
> 실제 프로젝트 파일(`Assets/Scripts/**`)은 아직 건드리지 않았다. 검토 후 확정되면 코드에 반영한다.

## 날짜
2026-07-16

## 요청
- 거점(비콘) 위에 아군 병력이 일정 시간 있으면 점령 완료 → 중립(흰색)/아군(초록)/적(빨강) 3가지 상태.
- 점령한 영토 안에서만 건설 가능, 영토 밖 자원은 채취 불가.
- 영토를 잃으면 그 안의 건물이 생산/연구/체력회복을 못 하게(비활성화) 막고 싶다.
- 요청 다이어그램: 본진 → 거점 발견 → 거점 점령 → 영토 획득 → 그 안에서만 건설 가능 / 광물이 영토 밖이면 채취 불가 / 거점 상실 → 영토 상실 → 건물 비활성화(건설·생산·연구·채취·회복 전부 불가).

## 1. 현재 상태 조사

### 1.1 점령/영토 시스템 (`doc/0126`~`doc/0140`에서 이미 구현됨)
- `CaptureSystem.cs`: 거점마다 `SphereCollider` 트리거로 점령 판정, `CurrentOwner`(`Neutral`/`Ally`/`Enemy`) 보유. 점령 완료 시 `ApplyEffect()`가 이펙트 전환 + `territoryZone.Owner` 갱신.
- `TerritoryZone.cs`: 핀(빈 오브젝트 리스트)으로 임의의 다각형을 정의, `Contains(Vector3 worldPos)`로 point-in-polygon 판정, `Owner`(`CaptureOwner`) 보유, 외곽선 색 자동 전환(흰/초록/빨강).
- **아직 없는 것**: 여러 `TerritoryZone`을 한 곳에서 조회할 수 있는 전역 질의 API(`doc/0129`에서 제안했던 `TerritoryManager`)가 아직 코드에 없다 — 지금은 각 `TerritoryZone` 인스턴스가 자기 자신의 `Contains()`만 알 뿐, "이 좌표가 (어떤 거점이든) 아군 영토 안에 있는가?"를 물어볼 곳이 없다.
- 씬(`SampleScene.unity`) 확인 결과 현재 `Capture_Point` 인스턴스 1개, 핀 4개짜리 다각형 영토가 이미 배치돼 있음.

### 1.2 건설 (`PlacementSystem.cs`)
`PlaceStructure()`(142행 부근), `Update()`(프리뷰, 421행 부근), `PlaceRelocatedBuilding()`(260행 부근) 세 곳이 동일한 3중 AND 조건(`CanPlaceObejctAt` && `!IsBlocked` && `!IsTooCloseToResource`)으로 배치 가능 여부를 판정. 영토 조건을 넣을 자리도 이 세 곳.

### 1.3 자원 채취 (`UnitController.cs`)
`Gather(ResourceNode node)`(950행)가 유일한 진입점, `FindNearestAvailableResourceNode()`(1003행)가 붐빔/고갈 시 대체 노드를 찾음 — 영토 개념 없음.

### 1.4 생산/건설진행/회복
- `UnitSpawner.Produce()`(110행): 매 프레임 대기열 맨 앞 타이머를 깎음 — 여기에 정지 조건 추가하면 생산이 멈춤(대기열은 유지).
- `BaseStructure.Update()`(88행): `if (builder == null) return;`으로 이미 "담당 일꾼 없으면 건설 일시정지" 패턴이 있고, 같은 메서드에서 `Heal()`도 호출하므로 건설 진행을 멈추면 회복도 자동으로 같이 멈춤.
- **연구**: 여전히 코드베이스에 연구 시스템 자체가 없음(`ResearchQueue` 없음) — 막을 대상이 없으므로 이번엔 범위 밖, 나중에 추가되면 같은 패턴(타이머 진행 전 영토 체크) 적용.
- **완공된 건물의 패시브 회복**: `HealthManager.Heal()`의 유일한 호출자가 `BaseStructure.Update()`(건설 중)뿐이라, 완공 후 자동 회복 시스템 자체가 없음 — "완공된 건물의 회복을 막는다"는 요청은 사실상 대상이 없다(변동 없음).

### 1.5 ⚠️ 중요: 시작 시점 "홈 영토" 문제
`PlacementSystem.SpawnStartingMainBase()`가 게임 시작 시 메인기지를 완성된 상태로 즉시 배치하는데, 지금 설계(영토 = 점령한 거점의 다각형)로는 **메인기지 주변이 처음엔 아무 거점도 점령 전이라 "아군 영토"가 전혀 없다.** 이대로 건설 제한을 걸면 첫 거점을 점령하기 전까진 어떤 건물도 못 짓는 상태가 된다.

**해결책**: `TerritoryZone`은 이미 `CaptureSystem`과 무관하게 독립적으로도 쓸 수 있게 설계돼 있다(`Owner` 필드를 인스펙터에서 직접 `Ally`로 고정, 핀도 직접 배치). 즉 **코드 변경 없이**, 메인기지 주변에 `TerritoryZone` 하나를 추가로 배치하고 `Owner`를 처음부터 `Ally`로 설정해두면(어떤 `CaptureSystem`과도 연결하지 않고, 소유권이 절대 안 바뀌는 "홈 영토"로) 해결된다. 이건 씬 편집 작업이라 이 문서의 코드 변경 범위에는 안 넣고, 구현 후 에디터에서 직접 배치하는 걸 권장한다.

## 2. 설계

### 2.1 `TerritoryManager` (신규) — 여러 `TerritoryZone`을 한 곳에서 질의
```csharp
// Assets/Scripts/CaptureSystem/TerritoryManager.cs (신규)
using System.Collections.Generic;
using UnityEngine;

public static class TerritoryManager
{
    private static readonly List<TerritoryZone> zones = new List<TerritoryZone>();

    public static void Register(TerritoryZone zone)
    {
        if (!zones.Contains(zone)) zones.Add(zone);
    }

    public static void Unregister(TerritoryZone zone) => zones.Remove(zone);

    // owner가 소유한 영토(다각형) 중 하나라도 포함하면 true (여러 영토의 합집합)
    public static bool IsInsideTerritory(Vector3 worldPos, CaptureOwner owner)
    {
        foreach (TerritoryZone zone in zones)
        {
            if (zone == null || zone.Owner != owner) continue;
            if (zone.Contains(worldPos)) return true;
        }
        return false;
    }

    public static bool IsInsideAlliedTerritory(Vector3 worldPos) => IsInsideTerritory(worldPos, CaptureOwner.Ally);
}
```

`TerritoryZone.cs`에 등록/해제만 추가:
```csharp
private void OnEnable() => TerritoryManager.Register(this);
private void OnDisable() => TerritoryManager.Unregister(this);
```

### 2.2 건설 제한 (`PlacementSystem.cs`)
발판 전체 칸이 모두 영토 안이어야 하는지, 중심점 한 점만 보면 되는지는 2절 결정 필요 — 아래는 "전체 칸" 기준(엄격) 예시:
```csharp
private bool IsInsideAlliedTerritory(Vector3Int gridPosition, Vector2Int size)
{
    List<Vector3Int> cells = StructureData.CalculatePositionsPublic(gridPosition, size);
    foreach (Vector3Int cell in cells)
    {
        if (!TerritoryManager.IsInsideAlliedTerritory(grid.CellToWorld(cell)))
            return false;
    }
    return true;
}
```
`PlaceStructure()`, `Update()`(프리뷰), `PlaceRelocatedBuilding()` 세 곳의 기존 AND 체인에 `&& IsInsideAlliedTerritory(gridPos, data.Size)` 한 항목만 추가 — 영토 밖으로 마우스를 가져가면 기존 `IsBlocked`/`IsTooCloseToResource` 실패 시와 동일하게 프리뷰가 자동으로 빨갛게 바뀜(공짜 시각 피드백).

### 2.3 자원 채취 제한 (`UnitController.cs`)
```csharp
public void Gather(ResourceNode node)
{
    if (isConstructing) return;
    if (!TerritoryManager.IsInsideAlliedTerritory(node.transform.position))
        return; // 영토 밖 자원은 채취 명령 자체를 무시

    ...
}
```
`FindNearestAvailableResourceNode()`의 제외 조건에도 추가:
```csharp
if (node == null || node == exclude || node.IsDepleted || node.IsCrowded
    || !TerritoryManager.IsInsideAlliedTerritory(node.transform.position))
    continue;
```
→ 붐빔/고갈로 자동 재배정될 때도 영토 밖 노드로는 안 보내짐.

### 2.4 생산 정지 (`UnitSpawner.cs`)
```csharp
private void Produce()
{
    if (productionQueue.Count == 0) return;

    if (buildingController != null && !TerritoryManager.IsInsideAlliedTerritory(transform.position))
        return; // 영토 밖이면 타이머가 그 자리에서 멈춤 (대기열은 유지, 리셋 안 됨)

    ProductionData current = productionQueue[0];
    ...
}
```

### 2.5 건설 진행/회복 정지 (`BaseStructure.cs`)
```csharp
private void Update()
{
    if (builder == null) return; // 기존: 담당 일꾼 없음
    if (!TerritoryManager.IsInsideAlliedTerritory(transform.position)) return; // 추가: 영토 상실

    remainingBuildTime -= Time.deltaTime;
    ...
}
```
`Heal()` 호출이 같은 메서드 안에 있으므로 이 한 줄로 건설 진행 정지 + 회복 정지가 동시에 적용됨.

### 2.6 연구
시스템 자체가 없어 이번엔 처리할 대상이 없음(1.4 참고) — 나중에 추가되면 2.4와 같은 패턴 적용.

## 3. 신규/수정 파일 목록 (제안)

| 파일 | 종류 | 내용 |
|---|---|---|
| `Assets/Scripts/CaptureSystem/TerritoryManager.cs` | 신규 | 정적 레지스트리 + `IsInsideTerritory`/`IsInsideAlliedTerritory` |
| `Assets/Scripts/CaptureSystem/TerritoryZone.cs` | 수정 | `OnEnable`/`OnDisable`에서 `TerritoryManager` 등록/해제 |
| `Assets/Scripts/BuildSystem/PlacementSystem.cs` | 수정 | `PlaceStructure()`/`Update()`/`PlaceRelocatedBuilding()`에 영토 검사 추가 |
| `Assets/Scripts/Unit/UnitController.cs` | 수정 | `Gather()` 진입 차단, `FindNearestAvailableResourceNode()` 후보 제외 |
| `Assets/Scripts/UnitSpawner/UnitSpawner.cs` | 수정 | `Produce()` 진행 전 영토 체크 |
| `Assets/Scripts/Building/BaseStructure.cs` | 수정 | `Update()` 일시정지 조건에 영토 체크 추가 |
| (씬 편집, 코드 아님) | - | 메인기지 주변에 `Owner=Ally` 고정 `TerritoryZone`을 별도로 배치해 "홈 영토" 확보 (1.5 참고) |

## 4. 결정이 필요한 부분
1. **건물 발판 전체 vs 중심점**: 2.2에서 발판 전체 칸이 다 영토 안이어야 하는 엄격한 방식으로 제안했는데, 이걸로 갈지 중심점 한 점만 볼지.
2. **채취/생산 중 영토를 나중에 잃는 경우**: 생산(2.4)/건설·회복(2.5)은 매 프레임 재확인이라 영토를 잃는 즉시 자동으로 멈춘다. 다만 **이미 채취하러 이동 중이거나 채취 중인 일꾼**은 이번 설계에서 "새 명령(`Gather` 호출) 시점"에만 막고, 이미 진행 중인 왕복은 끝까지 하도록 뒀다 — 이 정도 범위면 충분한지, 아니면 매 틱 재확인해서 즉시 중단시키는 것까지 필요한지.

## 다음 단계
위 2가지 결정 사항에 답을 주면 3절 파일들을 실제로 반영한다.
