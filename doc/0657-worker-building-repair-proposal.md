# 0657 - 일꾼 건물 수리 기능 (적용 완료)

## 날짜
2026-08-22

## 요청 내용
"일꾼이 피해입은 건물을 우클릭하여 가까이 붙어서 수리 하는 기능을 추가하려고 하는데 어떤식으로 하면 좋을까. 수리 한다면 체력 당 자원을 사용하고 싶은데 해당부분은 몇정도로하고 어떤식으로 구현하는게 좋을까"

## 조사 내용 - 재사용 가능한 기존 패턴

- **"이동 후 도착하면 콜백" 패턴**: `UnitController.GoBuild(destination, onArrived, onCancelled)` (`Assets/Scripts/Unit/UnitController.cs:1074`)가 이미 건설 재개(`BeginConstruction`)에 이 패턴을 쓰고 있음 — 목적지 근접 반경(`buildInteractRange`) 안에 들어오면 `onArrived` 콜백 실행. 수리도 동일하게 재사용 가능(새 이동 로직 불필요).
- **명령 취소 체크포인트 1곳**: `MoveTo`/`AttackUnitTarget`/`AttackMoveTo`/`FollowUnit`/`FollowBuilding`/`GoBuild`/`StopUnit`/`PatrolUnit`/`HoldUnit`/`Gather` 등 명령을 내리는 모든 진입점이 예외 없이 `CancelGatheringForNewCommand()`를 먼저 호출함(`UnitController.cs:1847`). 수리 취소 로직도 다른 명령이 들어오면 자동으로 끊기게 하려면, 이 함수 본문에 한 줄만 추가하면 모든 호출부에 자동 적용됨 (11곳 개별 수정 불필요 - root-cause 지점).
- **체력 회복**: `HealthManager.Heal(int amount)` (`Assets/Scripts/Unit/HealthManager.cs:114`)가 이미 존재 — 건물/유닛 공용, 최대체력 클램프 포함.
- **자원 소모**: `ResourceManager.TrySpend(oreCost, gasCost)` (`Assets/Scripts/Resource/ResourceManager.cs:83`)가 이미 존재 — 잔액 부족 시 자동으로 실패 반환.
- **건물 원가 조회**: `RTSUnitController.GetBuildingData(buildingID)` (`RTSUnitController.cs:2302`)가 `BuildingData`(광물/가스 원가, `BuildingDataSO`)를 이미 들고 있음. `BuildingController.GetBuildingID()`로 자기 종류를 알 수 있음.
- **우클릭 건물 라우팅**: `UserControl.cs:664~680`에서 건물 우클릭 시 항상 `rtsUnitController.MoveToBuildingSelectedUnits(building)`을 호출함 — 여기서 "피해입은 건물 + 일꾼 선택"이면 수리로 분기.

## 설계안

### 1. 수리 비용 = 건물의 실제 건설 원가 비율을 그대로 사용 (새 매직넘버 도입 안 함)

건물별로 고정 "체력당 자원"을 하드코딩하지 않고, **그 건물의 광물 원가 ÷ 최대체력**을 초당 소모율로 계산한다. 즉 "완전히 파괴된 건물을 100% 수리하면 원가 전액이 든다"는 원칙 — 실제 값으로 계산해보면:

| 건물 | 원가(광물/가스) | 최대체력 | 광물/HP |
|---|---|---|---|
| MainBase | 400 / 0 | 1500 | 0.27 |
| Tier1(배럭) | 100 / 0 | 1000 | 0.10 |
| Tier2(팩토리) | 200 / 100 | 1250 | 0.16 (+가스 0.08) |
| Tier3(공항) | 150 / 0 | 1300 | 0.12 |
| SupplyDepot | 150 / 0 | 500 | 0.30 |
| Lab | 150 / 100 | 850 | 0.18 (+가스 0.12) |

가스는 생략하고 **광물만 소모**하는 걸 제안함(스타1/2의 SCV 수리와 동일한 관례 - 가스/인구수 소모 없음). 필요하면 가스도 같은 비율로 추가 가능.

### 2. 수리 속도 = 초당 일정 HP (예: 20 HP/sec)

- 20 HP/sec 기준 MainBase 완전 수리(1500HP) = 75초, 400광물 전액 소모 — 처음 짓는 것과 체감상 비슷한 무게감.
- 광물 소모는 매 프레임 누적(float)해서 1 이상이 되면 정수만큼 `TrySpend`, 잔액 부족하면 그 틱만 회복을 멈춤(자원 들어올 때까지 유닛은 옆에서 대기, 자동 재개).

### 3. 코드 변경 지점

**`UnitController.cs`**
- `public bool IsWorker() => isWorker;` 게터 추가 (현재 없음, `IsConstructing()`과 동일한 패턴)
- 필드 추가: `private BuildingController repairTarget; private bool isRepairing; private float repairOreAccumulator;`
- `public void Repair(BuildingController building)`: `GoBuild(GetClosestSurfacePoint(building.transform), () => BeginRepair(building), null)` 호출 (건설 이동과 동일 재사용)
- `BeginRepair(building)`: `repairTarget = building; isRepairing = true;`
- `RepairTick()` (다른 Tick들과 같이 `Update()`에서 호출): 대상이 null/이미 만피면 종료, 아니면 `HealthManager.Heal()` + `ResourceManager.TrySpend()`로 매 프레임 소량씩 처리
- `CancelGatheringForNewCommand()` 안에 `isRepairing = false; repairTarget = null;` 한 줄 추가 → 다른 명령 내리면 자동으로 수리 중단됨 (기존 체크포인트 재사용)

**`RTSUnitController.cs`**
- `public void RepairSelectedUnits(BuildingController building)`: 선택된 유닛 중 `IsWorker()`인 것만 `unit.Repair(building)`, 나머지는 기존처럼 `unit.MoveToBuilding(building)` (혼합 선택 시 전투 유닛은 그냥 따라가기만)
- `public bool TrySpendOre(int amount) => resourceManager.TrySpend(amount, 0);` (UnitController가 직접 `ResourceManager`를 들고 있지 않으므로 중계용 - 기존에 `RefundUnit`도 `RTSUnitController` 안에서만 `resourceManager`를 건드리는 것과 동일한 캡슐화 유지)

**`UserControl.cs:664~680`**
- 건물 우클릭 시: `bool damaged = building.GetHealthManager().GetHealth() < building.GetHealthManager().GetMaxHealth();`
- `damaged`이면 `rtsUnitController.RepairSelectedUnits(building)`, 아니면 기존 `MoveToBuildingSelectedUnits(building)` 그대로 유지

## 확인 필요한 사항

1. **자원 소모율**: "원가 ÷ 최대체력" 방식(위 표) 괜찮은지, 아니면 모든 건물 공통 고정값(예: 광물 1당 5HP)을 원함?
2. **수리 속도**: 20 HP/sec 기준 어떤지 (더 빠르게/느리게?)
3. **가스도 소모할지**: 가스 원가가 있는 건물(Tier2/Lab)은 가스도 비율대로 깎을지, 광물만 쓸지
4. **다수 일꾼 동시 수리**: 같은 건물을 여러 일꾼이 동시에 수리하면 속도가 배로 빨라지게 할지(각자 20HP/sec씩 중첩), 아니면 1건물 1일꾼만 허용할지 - 스타크래프트 스타일이면 중첩 허용이 자연스러움
5. **자원 부족 시 UX**: 그냥 조용히 대기만 시킬지, `UIController.ShowWarning()`으로 "자원 부족" 경고를 (최초 1회) 띄울지

## 요약/영향받는 파일 (구현 시)
- `Assets/Scripts/Unit/UnitController.cs` - `IsWorker()` 게터, `Repair()`/`BeginRepair()`/`RepairTick()` 추가, `CancelGatheringForNewCommand()`에 한 줄 추가
- `Assets/Scripts/System/RTSUnitController.cs` - `RepairSelectedUnits()`, `TrySpendOre()` 추가
- `Assets/Scripts/UserControl/UserControl.cs` - 건물 우클릭 분기에 피해 여부 체크 추가

## 최종 확정 사항 (사용자 피드백 반영, 실제 구현)

### 우선순위 (건물 우클릭 시, `UnitController.MoveToBuilding`)
1. 자원을 들고 있고 대상이 메인기지면 → 반납 (기존 동작 그대로)
2. 일꾼이고 대상 건물이 피해입었으면 → 수리
3. 그 외 → 기존처럼 그냥 따라다니기

`isWorker`/`IsCarryingResource()`는 `UnitController`가 이미 들고 있는 값이라, `MoveToBuilding()` 내부에서 분기하는 것만으로 충분함 - 애초 제안서에 있던 `RTSUnitController.RepairSelectedUnits()`/`UserControl.cs` 변경은 불필요해서 제외함(선택된 유닛이 섞여 있어도 각 유닛의 `MoveToBuilding()`이 알아서 판단).

### 정수 단위 회복 (소수 회복 버그 방지)
매 프레임 소수 HP를 누적하는 대신, **`repairTickInterval`(기본 0.5초)마다 정수 HP(`repairHpPerTick`, 기본 10)를 한 번에 회복**하는 틱 방식으로 구현. 초당 20HP. 비용도 그 틱에 필요한 정수 광물량을 수리 시작 시점에 한 번만 계산해서 고정(`Mathf.RoundToInt`, 최소 1)하므로 프레임마다 반올림 오차가 쌓이지 않음.

### 다중 일꾼 동시 수리 = 중첩 허용
일꾼별로 독립된 `RepairTick()`이 각자 도네이므로 별도 잠금/조율 로직 없이 자연스럽게 중첩됨(일꾼 3명이 붙으면 3배속 회복).

### 자원 부족 시
그 틱만 조용히 건너뛰고(회복도 소모도 안 함), 다음 틱에 자동 재시도. 단, 이번 수리 세션에서 최초 1회에 한해 `UIController.ShowWarning(LocalizationManager.GetText("warning.resource"))`로 "자원을 더 채취하세요." 경고를 띄움(기존 생산/건설 자원부족 경고와 동일 키 재사용, 새 로컬라이제이션 키 추가 안 함).

## 실제 변경된 파일
- `Assets/Scripts/Unit/UnitController.cs`
  - 필드: `repairTickInterval`, `repairHpPerTick`, `repairTarget`, `isRepairing`, `repairTickTimer`, `repairOreCostPerTick`, `hasShownRepairOreWarning`
  - `Repair()`/`BeginRepair()`/`RepairTick()` 추가 (`GoBuild` 재사용)
  - `CancelAttackOrder()`(모든 명령 진입점이 거치는 기존 취소 체크포인트)에 수리 취소 두 줄 추가
  - `Update()`에 `RepairTick();` 호출 추가
  - `MoveToBuilding()`에 우선순위 분기 추가, `IsDamaged()` 헬퍼 추가
- `Assets/Scripts/System/RTSUnitController.cs`
  - `public bool TrySpendOre(int amount)` 한 줄 추가 (기존 `ResourceManager` 캡슐화 패턴 유지)

`UserControl.cs`는 변경 없음 (기존 건물 우클릭 라우팅이 그대로 `MoveToBuildingSelectedUnits` → `MoveToBuilding()`으로 이어지고, 판단은 그 안에서 처리).

`npx uloop-cli compile` 컴파일 성공 확인 (Success: true, 에러 0, 경고는 기존 49건 그대로 - 이번 변경과 무관).
