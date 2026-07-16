# 0142. 영토 기반 건설/자원채취/생산·회복 제한 실제 반영 (`doc/0141` 적용)

## 날짜
2026-07-16

## 요청
`doc/0141` 제안대로, 발판 전체 칸 기준(엄격) + 채취 중 영토 상실은 이번 왕복은 끝까지 허용(새 명령부터 차단)으로 확정 반영.

## 변경 파일
- `Assets/Scripts/CaptureSystem/TerritoryManager.cs` (신규)
- `Assets/Scripts/CaptureSystem/TerritoryZone.cs` (수정 — 등록/해제)
- `Assets/Scripts/BuildSystem/PlacementSystem.cs` (수정)
- `Assets/Scripts/Unit/UnitController.cs` (수정)
- `Assets/Scripts/UnitSpawner/UnitSpawner.cs` (수정)
- `Assets/Scripts/Building/BaseStructure.cs` (수정)

## 코드 변경

### `TerritoryManager.cs` (신규)
`doc/0141` 2.1 제안 그대로: 정적 리스트에 `TerritoryZone`을 등록받아 `IsInsideTerritory(pos, owner)` / `IsInsideAlliedTerritory(pos)`로 질의.

### `TerritoryZone.cs`
```csharp
private void OnEnable() => TerritoryManager.Register(this);
private void OnDisable() => TerritoryManager.Unregister(this);
```

### `PlacementSystem.cs`
- 신규 헬퍼 `IsInsideAlliedTerritory(Vector3Int gridPosition, Vector2Int size)` — `StructureData.CalculatePositionsPublic`로 뽑은 점유 셀 전부가 아군 영토 안인지 검사(발판 전체 칸 기준, 하나라도 밖이면 false).
- `PlaceStructure()`, `Update()`(프리뷰 valid 판정), `PlaceRelocatedBuilding()` 세 곳의 기존 배치 가능 조건에 이 검사를 추가.

### `UnitController.cs`
- `Gather(ResourceNode node)` 최상단(전투유닛 분기 이후)에 `if (!TerritoryManager.IsInsideAlliedTerritory(node.transform.position)) return;` 추가 — 영토 밖 자원은 새 채취 명령 자체를 무시.
- `FindNearestAvailableResourceNode()`의 후보 제외 조건에 `|| !TerritoryManager.IsInsideAlliedTerritory(node.transform.position)` 추가 — 자동 재배정 시에도 영토 밖 노드 제외.
- **범위**: 이미 이동 중/채취 중인 일꾼은 도중에 영토를 잃어도 중단시키지 않음(이번 왕복은 끝까지) — `doc/0141` 결정사항 그대로.

### `UnitSpawner.cs`
```csharp
if (buildingController != null && !TerritoryManager.IsInsideAlliedTerritory(transform.position))
    return; // 영토 밖이면 타이머가 그 자리에서 멈춘다 (대기열은 유지, 리셋하지 않음)
```
`Produce()` 맨 앞에 추가.

### `BaseStructure.cs`
```csharp
if (!TerritoryManager.IsInsideAlliedTerritory(transform.position))
    return; // 영토를 잃으면 건설 진행(및 그에 딸린 체력 회복)도 함께 일시정지
```
`Update()`의 기존 `if (builder == null) return;` 바로 다음에 추가 — 같은 메서드에서 `Heal()`도 호출하므로 회복도 자동으로 같이 멈춤.

## 요약
- 어떤 좌표든 "아군이 점령한 영토(다각형) 안인가?"를 `TerritoryManager.IsInsideAlliedTerritory()` 한 곳으로 질의할 수 있게 됨.
- 건설: 발판 전체 칸이 다 영토 안이어야 배치 가능 (프리뷰도 영토 밖이면 자동으로 빨갛게).
- 채취: 영토 밖 자원 노드로는 새 채취 명령/자동 재배정이 안 감. 이미 진행 중인 채취는 이번 왕복만 허용.
- 생산/건설진행/회복: 영토를 잃으면 매 프레임 검사로 즉시 일시정지(리셋 아님), 되찾으면 자동 재개.
- 연구: 시스템 자체가 없어 이번엔 대상 없음(변동 없음).

## ⚠️ 남은 작업 (코드 아님, 씬 편집)
`doc/0141` 1.5에서 지적한 대로, 게임 시작 시 메인기지 주변엔 아직 점령한 거점이 없어 "아군 영토"가 전혀 없다. 첫 거점을 점령하기 전에도 건설이 가능하려면, 메인기지 주변에 `TerritoryZone`을 하나 더 배치하고 `Owner`를 인스펙터에서 처음부터 `Ally`로 고정(어떤 `CaptureSystem`과도 연결하지 않음)해야 한다 — 이건 코드가 아니라 에디터에서 직접 해야 하는 씬 작업.

## 확인/테스트 필요
유니티 에디터에서:
1. 위 씬 작업(홈 영토 배치) 없이 테스트하면 첫 건물 배치가 막히는지 확인.
2. 홈 영토를 배치한 뒤 그 안/밖에서 건설 가능 여부가 갈리는지.
3. 영토 밖 자원 노드에 채취 명령을 내리면 무시되는지.
4. 거점을 점령/상실했을 때 그 안 건물의 생산 큐 진행이 멈췄다가 재개되는지.

## 비고
[[confirm_before_implementing]] — `doc/0141`에서 제시한 2가지 결정(발판 전체 칸, 채취 중 영토 상실 시 처리)에 대해 둘 다 권장안으로 확인받은 뒤 반영함.
