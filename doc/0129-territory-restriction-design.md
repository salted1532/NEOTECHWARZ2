# 0129. 점령 영토에 따른 건설/자원채취/생산·연구·회복 제한 설계

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **설계 제안만** 담고 있고
> 실제 프로젝트 파일(`Assets/Scripts/**`)은 아직 건드리지 않았다. 검토 후 어디까지 실제로 반영할지
> 알려주면 그때 코드에 반영한다.

## 요청
점령한 영역 안에서만 건설이 가능하고, 그 영역 밖의 자원은 채취할 수 없고, 영토를 상실하면 그 안의 건물이 생산/연구/체력회복을 못 하게 막고 싶다 — 이걸 어떻게 구현할지 문서화.

## 1. 현재 상태 조사

### 1.1 지금까지 만든 것 (`Assets/Scripts/CaptureSystem/CaptureSystem.cs`)
- 거점 하나당 `CaptureSystem` 컴포넌트 하나. `CurrentOwner`(`Neutral`/`Ally`/`Enemy`)를 갖고 있고, 아군이 트리거 콜라이더 안에 30초 머물면 `Ally`로 전환.
- **아직 "영토(반경 안의 영역)" 개념은 없다.** `CaptureSystem`은 자기 자신의 소유 상태만 알 뿐, "이 지점이 어느 거점의 영토 안에 있는지"를 다른 시스템이 물어볼 API가 없다.
- 거점의 트리거 콜라이더(`Capture_Point` 프리팹의 `SphereCollider`, 반지름 10)가 점령 판정 반경이자 자연스럽게 영토 반경으로도 재사용할 수 있는 값이다.

### 1.2 건설 (`Assets/Scripts/BuildSystem/PlacementSystem.cs`, `GridData.cs`)
건설 가능 여부는 `PlaceStructure()`(실제 배치, 클릭 시)와 `Update()`(고스트 프리뷰 초록/빨강 색칠용, 매 프레임)에서 동일하게 아래 3개 조건의 AND로 판정:
```csharp
bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size)   // GridData.cs:51 — 그리드 셀 점유
    && !IsBlocked(mousePos, data.Size)                             // PlacementSystem.cs:328 — 물리 오버랩(유닛/장애물)
    && !IsTooCloseToResource(data.ID, gridPos, data.Size);         // PlacementSystem.cs:361 — 사령부 한정 자원과 최소 거리
```
- 셀 목록은 `GridData.CalculatePositionsPublic(gridPos, data.Size)`(`GridData.cs:45`)로 이미 뽑고 있음 — `IsTooCloseToResource`가 쓰는 것과 같은 헬퍼.
- 건물 이동(리프트/재배치) 흐름도 별도로 존재(`StartBuildingRelocation`/`PlaceRelocatedBuilding`, `PlacementSystem.cs:228-287`) — 착지 시에도 같은 검사가 필요.

### 1.3 자원 채취 (`Assets/Scripts/Unit/UnitController.cs`, `Assets/Scripts/Resource/ResourceNode.cs`)
- 명령 진입점: `public void Gather(ResourceNode node)` (`UnitController.cs:950`).
- 실제 채취: `GatherTick()`의 `Gathering` 케이스(`UnitController.cs:1147`)에서 `gatherTargetNode.Extract(amountPerTrip)` 호출.
- 노드 유효성 판정은 지금 "붐빔(`IsCrowded`)"과 "고갈(`IsDepleted`)"만 본다(`ResourceNode.cs:42,46`) — 소유/영토 개념 없음.
- 자동 재배정: `FindNearestAvailableResourceNode()`(`UnitController.cs:1003`)가 붐비거나 사라진 노드를 대체할 다음 노드를 찾을 때도 영토를 안 봄.

### 1.4 생산 / 건설 진행 / 체력회복
- **유닛 생산 큐**: `UnitSpawner.Enqueue()`(`UnitSpawner.cs:47`)로 등록, `UnitSpawner.Update()` → `Produce()`(`UnitSpawner.cs:110`)가 매 프레임 `RemainTime`을 깎다가 0이 되면 스폰. `BuildingController.SpawnUnit()`(`BuildingController.cs:366`)이 이 큐로 넘겨주는 창구.
- **건물 건설 진행**: `BaseStructure.Update()`(`BaseStructure.cs:88`)가 **담당 일꾼(`builder`)이 없으면 이미 자동으로 일시정지**하는 패턴이 있다(`BaseStructure.cs:90-91`, "담당 일꾼이 없으면(사망 등) 건설 일시정지"). 영토 상실 시 정지시키고 싶은 로직을 끼워넣기 딱 좋은 기존 선례.
- **연구**: 코드베이스에 연구/테크트리 시스템 자체가 아직 없음(`ResearchQueue`류 클래스 없음) — 지금은 막을 대상이 없다. 나중에 연구 큐가 생기면 유닛 생산과 동일한 패턴(진행 타이머 정지)으로 다루면 된다.
- **체력 회복**: 상시 자동 회복(패시브 리젠) 시스템 자체가 없다. `HealthManager.Heal()`(`HealthManager.cs:83`)의 유일한 호출자는 `BaseStructure.Update()`(건설 중 진행률에 비례해 체력 채우기)뿐이다. 즉 "영토 상실 시 회복 막기"는 사실상 "영토 상실 시 건설 진행(그리고 그에 딸린 체력 채우기)을 막기"와 같은 문제로 합쳐진다.
- **소유/진영 개념**: 코드 전체에 `Owner`/`Faction` 데이터 모델이 없다. 아군/적 구분은 태그(`"Enemy"`)와 컴포넌트 타입(`UnitController` vs `EnemyController`)으로만 한다. 즉 `CaptureSystem.CurrentOwner`가 이 프로젝트 최초의 "소유권" 개념이고, 영토 시스템은 이걸 중심으로 새로 쌓아야 한다.

## 2. 설계

### 2.1 핵심 아이디어: `TerritoryManager` — 영토 질의를 한 곳으로 모은다
거점이 여러 개일 수 있으므로, "이 좌표가 아군 영토 안인가?"라는 질문에 답하려면 모든 `CaptureSystem` 인스턴스를 순회해야 한다. 이 로직을 PlacementSystem/UnitController/UnitSpawner/BaseStructure에 각각 중복 구현하지 않고 **정적 레지스트리 + 질의 API 하나**로 모은다.

```csharp
// Assets/Scripts/CaptureSystem/TerritoryManager.cs (신규)
public static class TerritoryManager
{
    private static readonly List<CaptureSystem> points = new List<CaptureSystem>();

    public static void Register(CaptureSystem point) => points.Add(point);
    public static void Unregister(CaptureSystem point) => points.Remove(point);

    // owner가 점령한 어떤 거점이든 반경 안에 들어오면 true (원형 영토의 합집합)
    public static bool IsInsideTerritory(Vector3 worldPos, CaptureOwner owner)
    {
        foreach (CaptureSystem point in points)
        {
            if (point == null || point.CurrentOwner != owner) continue;
            if ((point.transform.position - worldPos).sqrMagnitude <= point.TerritoryRadius * point.TerritoryRadius)
                return true;
        }
        return false;
    }

    public static bool IsInsideAlliedTerritory(Vector3 worldPos) => IsInsideTerritory(worldPos, CaptureOwner.Ally);
}
```
- `CaptureSystem`이 `Awake()`/`OnDestroy()`에서 `TerritoryManager.Register(this)`/`Unregister(this)` — 기존 `RTSUnitController.UnitList` 등록/해제 관례와 동일한 패턴.
- `CaptureSystem`에 `TerritoryRadius` 프로퍼티를 추가해서 `SphereCollider.radius`를 그대로 노출(점령 판정 반경 = 영토 반경, 값 두 곳에 중복 관리 안 함).
- 순회 비용은 거점 개수가 맵에 몇 개 안 되므로(수 개~십여 개) 매 프레임 호출해도 무시할 수준 — FogOfWar 설계(0069)처럼 별도 그리드/재계산 주기가 필요 없다.

### 2.2 소유권 변경 알림 (이벤트)
건물마다 "지금 내가 아군 영토 안에 있는가"를 매 프레임 다시 계산하는 건 낭비다 — 거점 소유가 바뀌는 건 드문 이벤트(점령 완료 시 한 번)이므로, **폴링이 아니라 이벤트**로 전파한다.

```csharp
// CaptureSystem.cs에 추가
public static event Action TerritoryChanged; // 아무 거점이든 소유가 바뀔 때마다 1회 발행

private void CompleteCapture(CaptureOwner newOwner)
{
    CurrentOwner = newOwner;
    ApplyEffect(newOwner);
    SetCaptureBarVisible(false);
    Debug.Log("점령이 되었다");

    TerritoryChanged?.Invoke();
}
```
건물 쪽(`BuildingController` 또는 `BaseStructure`/`UnitSpawner`)은 `OnEnable`에서 `CaptureSystem.TerritoryChanged += Recheck` 구독하고, `Recheck()`에서 `TerritoryManager.IsInsideAlliedTerritory(transform.position)` 결과를 `bool isInAlliedTerritory` 캐시 필드에 저장한다. 이후 매 프레임은 이 캐시 값만 읽으면 되므로 비용이 없다. `Start()`에서 최초 1회도 호출해 초기 상태를 맞춘다.

### 2.3 건설 제한 (PlacementSystem)
기존 `&&` 체인에 그대로 한 항목만 추가:
```csharp
bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size)
    && !IsBlocked(mousePos, data.Size)
    && !IsTooCloseToResource(data.ID, gridPos, data.Size)
    && TerritoryManager.IsInsideAlliedTerritory(mousePos); // 추가
```
`PlaceStructure()`(`PlacementSystem.cs:142` 부근, `TryConstructBuilding` 호출 전)와 `Update()`(`PlacementSystem.cs:421` 부근, 고스트 프리뷰 색칠 전) 양쪽에 동일하게 넣는다 — 이러면 건설 가능 지점만 클릭이 통과하고, 영토 밖으로 마우스를 가져가면 프리뷰가 자동으로 빨갛게 바뀐다(기존 `IsBlocked`/`IsTooCloseToResource` 실패 시와 동일한 시각 피드백을 공짜로 얻음). 재배치 착지(`PlaceRelocatedBuilding`)에도 동일 검사 추가 권장.

**결정 필요한 부분**: 지금 제안은 건물의 "중심점 한 점"만 영토 안인지 검사한다(원형 영토라서 셀 단위 전수검사보다 훨씬 저렴). 건물 크기가 커서 영토 경계에 걸치는 경우까지 엄격하게(전체 발판이 영토 안에 있어야) 막고 싶다면 `GridData.CalculatePositionsPublic`로 뽑은 모든 점유 셀을 순회하며 전부 영토 안인지 검사하는 걸로 강화할 수 있다 — 필요하면 알려달라.

### 2.4 자원 채취 제한
```csharp
// UnitController.Gather() 맨 앞에 추가 (UnitController.cs:950 부근)
public void Gather(ResourceNode node)
{
    if (!TerritoryManager.IsInsideAlliedTerritory(node.transform.position))
        return; // 영토 밖 자원은 채취 명령 자체를 무시

    ...
}
```
- `FindNearestAvailableResourceNode()`(`UnitController.cs:1010`)의 후보 제외 조건에도 `|| !TerritoryManager.IsInsideAlliedTerritory(node.transform.position)`를 추가해서, 붐빔/고갈로 자동 재배정할 때 영토 밖 노드로 보내지지 않게 한다.
- 이미 채취 중이던 노드가 있는 영토를 나중에 잃는 경우(거점을 뺏김)도 고려해야 하면, `GatherTick()`의 `MovingToResource`/`WaitingInQueue`/`Gathering` 케이스에서 매 틱(또는 2.2의 이벤트 콜백에서) 현재 노드가 여전히 영토 안인지 재확인해 아니면 `CancelGatheringForNewCommand()`로 중단시키는 로직 추가가 필요 — MVP 범위를 넘는지 확인 후 반영.

### 2.5 생산 / 건설 진행 / 체력회복 차단
`BuildingController`에 2.2의 캐시 필드(`isInAlliedTerritory`)를 두고, 아래 두 곳에서 참조한다:

- **유닛 생산 정지**: `UnitSpawner.Produce()`(`UnitSpawner.cs:110`)가 타이머를 깎기 전에 `if (!building.IsInAlliedTerritory) return;` 한 줄 추가 — 이미 큐에 쌓인 항목은 유지되고, 영토를 되찾으면 이어서 진행(리셋이 아니라 일시정지 — `CaptureSystem`이 아군 이탈 시 타이머를 리셋하지 않고 멈추는 것과 같은 관례).
- **건설 진행/체력회복 정지**: `BaseStructure.Update()`의 기존 `if (builder == null) return;`(`BaseStructure.cs:90-91`) 조건에 `|| !isInAlliedTerritory`를 추가 — 이미 "담당 일꾼 없으면 정지"라는 선례가 있으니 자연스럽게 확장된다. 이 메서드가 `Heal()`도 여기서 같이 호출하므로 진행을 멈추면 체력회복도 자동으로 같이 멈춘다(별도 처리 불필요).
- **연구**: 시스템 자체가 없으므로 지금은 처리할 대상이 없음. 나중에 연구 큐가 추가되면 위 유닛 생산과 같은 패턴(타이머 진행 전 `IsInAlliedTerritory` 체크)을 그대로 적용하면 된다.
- **완공된 건물의 표시**: 영토를 잃은 완공 건물을 시각적으로 "비활성화됨"으로 보여주고 싶다면(예: 반투명/아이콘 오버레이), `BuildingController`가 2.2 이벤트로 캐시를 갱신하는 시점에 이펙트/셰이더 토글을 같이 걸 수 있다 — 요청에 명시되지 않아 이번 설계엔 포함 안 함, 필요하면 후속 문서로 분리.

## 3. 신규/수정 파일 목록 (제안)

| 파일 | 종류 | 내용 |
|---|---|---|
| `Assets/Scripts/CaptureSystem/TerritoryManager.cs` | 신규 | 정적 레지스트리 + `IsInsideTerritory`/`IsInsideAlliedTerritory` 질의 API |
| `Assets/Scripts/CaptureSystem/CaptureSystem.cs` | 수정 | `TerritoryRadius` 프로퍼티, `Awake/OnDestroy`에서 `TerritoryManager` 등록/해제, `TerritoryChanged` 정적 이벤트 발행 |
| `Assets/Scripts/BuildSystem/PlacementSystem.cs` | 수정 | `PlaceStructure()`/`Update()`/재배치 착지에 영토 검사 추가 |
| `Assets/Scripts/Unit/UnitController.cs` | 수정 | `Gather()` 진입 차단, `FindNearestAvailableResourceNode()` 후보 제외 |
| `Assets/Scripts/Building/BuildingController.cs` | 수정 | `isInAlliedTerritory` 캐시 필드 + `TerritoryChanged` 이벤트 구독/재계산 |
| `Assets/Scripts/UnitSpawner/UnitSpawner.cs` | 수정 | `Produce()` 진행 전 영토 체크 |
| `Assets/Scripts/Building/BaseStructure.cs` | 수정 | `Update()`의 일시정지 조건에 영토 체크 추가 |

## 4. 결정이 필요한 부분 (구현 전 확인 요청)
1. **건물 발판 전체 vs 중심점**: 2.3에서 제안한 대로 중심점 한 점만 검사할지, 발판 전체 셀이 영토 안이어야 하는지.
2. **채취 중 영토 상실**: 2.4 마지막 문단 — 이미 채취 중인 노드의 영토를 도중에 뺏기면 즉시 중단시킬지(추가 구현 필요), 아니면 이번엔 "새로 채취 명령을 내릴 때만" 막는 걸로 좁힐지.
3. **비활성화 시각 표시**: 영토 잃은 건물을 반투명/아이콘 등으로 눈에 띄게 표시할지, 아니면 내부적으로 진행만 멈추고 외형은 그대로 둘지.
4. **거점 여러 개가 겹칠 때**: 지금은 "합집합"(아군 거점 반경 중 하나라도 포함되면 영토)으로 설계했는데, 이 정의로 충분한지.

## 다음 단계
이 문서는 설계까지만이다. 위 4가지 결정 사항에 답을 주면, 그 답을 반영해서 3절의 파일들을 실제로 수정하겠다.
