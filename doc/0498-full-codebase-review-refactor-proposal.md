# 0498 — 전체 스크립트 리뷰 및 리팩토링 제안

**날짜:** 2026-08-09

## 요청 내용

"전체 스크립트들을 읽어보고 최적화 가능하거나 불필요한거, 버그를 야기할만한 것들을 리팩토링 해줘
하지만 절대로 기존 기능이 작동안하거나 그러면 안돼 그대로 작동해야해"

## 조사 방법

`Assets/Scripts/` 아래 C# 스크립트 94개 전체를 영역별로 9개 그룹(중앙허브, 유닛/전투, 스킬·적/아군
AI, 건물/자원/생산, 점령·카메라·미니맵, UI, 캠페인/미션/로컬라이제이션, 사운드/애니메이션, 이펙트/
데이터)으로 나눠 각각 전체를 읽고 버그·죽은 코드·최적화 후보를 조사했다. 이후 불확실하다고
표시된 항목들(Enemy/Ally 대상 조회 대칭성, Stage1의 이벤트 등록 타이밍, DeselectAll의 null 안전성
등)은 관련 코드를 직접 대조해 실제로 문제가 되는지 재확인했다.

아래는 **실행 순서를 그대로 유지하고 관찰 가능한 동작을 바꾸지 않는** 범위에서 고칠 수 있는 항목만
추린 것이다. "합치면 깔끔하다" 식의 구조 변경 제안은 제외했다(맨 아래 "참고용, 수정 보류 목록" 참고).

---

## Tier 1 — 확정 버그 (실제 플레이에 영향)

### 1-1. `UserControl.HandleRightClick()` — 건물/자원 우클릭 시 이동 명령이 먼저 실행되고 전용 명령이 덮어씀
**파일:** `Assets/Scripts/UserControl/UserControl.cs` (657~731번 줄)

건물/광물/가스도 지면 위에 서 있어서 `layerGround` 레이캐스트가 함께 맞는다(같은 파일 607번 줄
주석이 적 유닛에 대해 이미 이 현상을 설명함). 적/적건물 처리는 땅 처리보다 먼저 하고 매번
`return`하지만, 건물/광물/가스 처리는 땅 처리 **뒤에** 있고 `return`이 없다. 그 결과 유닛 선택 중
건물을 우클릭하면 `IssueRightClickMoveAt`(일반 이동, 이동 보이스 재생)이 먼저 실행된 뒤
`MoveToBuildingSelectedUnits`(건물 전용 명령)가 곧바로 다시 실행됨 — 명령이 이중으로 나가고, 틀린
"이동" 보이스가 먼저 재생되어 건물 전용 SFX가 묻힐 수 있다. 광물/가스도 동일.

**기존 코드**
```csharp
// 2. 땅 클릭 = 명령 처리 (미니맵 클릭에서도 재사용 - IssueRightClickMoveAt 참고, doc/0349)
if (clickedGround)
{
    IssueRightClickMoveAt(groundHit.point);
}

// 건물 우클릭
if(clickedBuilding)
{
    ...
}

// 5. 광물 클릭 = 명령 처리
if (clickedOre)
{
    ...
}

// 5. 가스 클릭 = 명령 처리
if (clickedGas)
{
    ...
}
```
**변경 코드** (건물/광물/가스를 땅 처리보다 먼저 검사하고, 각 처리 끝에 `return` 추가 — 적/적건물과
동일한 우선순위 패턴)
```csharp
// 2. 건물 우클릭 (땅보다 먼저 처리 - 건물도 지면 위에 서 있어 clickedGround가 함께 true가 되므로)
if (clickedBuilding)
{
    ...(기존 내용 동일)...
    return;
}

// 3. 광물 클릭 = 명령 처리 (땅보다 먼저 처리)
if (clickedOre)
{
    ...(기존 내용 동일)...
    return;
}

// 4. 가스 클릭 = 명령 처리 (땅보다 먼저 처리)
if (clickedGas)
{
    ...(기존 내용 동일)...
    return;
}

// 5. 땅 클릭 = 명령 처리 (미니맵 클릭에서도 재사용 - IssueRightClickMoveAt 참고, doc/0349)
if (clickedGround)
{
    IssueRightClickMoveAt(groundHit.point);
}
```
정상 케이스(건물/광물/가스가 없는 순수 땅 클릭)는 동작이 완전히 동일하고, 건물/자원과 겹쳐 클릭될
때만 전용 명령이 단독으로 실행된다.

---

### 1-2. `EnemyUnitController` — 구조된 아군 OC(`AllyController`) 공격 시 방어력/장갑/크기/공중여부를 항상 기본값으로 오판
**파일:** `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (478~514번 줄)

`EnemyAttackRange`는 `AllyController`를 실제로 공격 대상(태그 `"AllyOC"`)으로 삼는데(4스테이지부터
등장하는 구조된 OC 병력), `GetTargetArmor`/`GetTargetSizeType`/`GetTargetArmorType`/`IsAirborne` 4개
헬퍼가 `UnitController`/`BuildingController`만 검사하고 `AllyController` 분기가 없다. 반대 방향인
`AllyController`의 동일 4개 메소드는 전부 `EnemyUnitController` 분기를 갖고 있어 비대칭이다. 그
결과 외계종족이 구조된 OC 병력을 공격하면 항상 방어력 0/크기 Medium/장갑 Light/지상으로 취급되어
실제 스탯과 다른 데미지가 들어가고, 공중 유닛이면 대공 판정도 틀린다.

**기존 코드**
```csharp
private int GetTargetArmor(GameObject target)
{
    if (target.TryGetComponent<UnitController>(out var playerUnit))
        return playerUnit.GetArmor();

    return 0;
}

private SizeType GetTargetSizeType(GameObject target)
{
    if (target.TryGetComponent<UnitController>(out var playerUnit))
        return playerUnit.GetSizeType();

    return SizeType.Medium;
}

private ArmorType GetTargetArmorType(GameObject target)
{
    if (target.TryGetComponent<UnitController>(out var playerUnit))
        return playerUnit.GetArmorType();

    return ArmorType.Light;
}

private bool IsAirborne(GameObject target)
{
    if (target.TryGetComponent<UnitController>(out var playerUnit))
        return playerUnit.IsAirUnit();

    if (target.TryGetComponent<BuildingController>(out var building))
        return building.IsLifted();

    return false;
}
```
**변경 코드** (`AllyController` 분기 추가 — `AllyController` 쪽의 대칭 메소드와 동일한 패턴)
```csharp
private int GetTargetArmor(GameObject target)
{
    if (target.TryGetComponent<UnitController>(out var playerUnit))
        return playerUnit.GetArmor();

    if (target.TryGetComponent<AllyController>(out var allyUnit))
        return allyUnit.GetArmor();

    return 0;
}

private SizeType GetTargetSizeType(GameObject target)
{
    if (target.TryGetComponent<UnitController>(out var playerUnit))
        return playerUnit.GetSizeType();

    if (target.TryGetComponent<AllyController>(out var allyUnit))
        return allyUnit.GetSizeType();

    return SizeType.Medium;
}

private ArmorType GetTargetArmorType(GameObject target)
{
    if (target.TryGetComponent<UnitController>(out var playerUnit))
        return playerUnit.GetArmorType();

    if (target.TryGetComponent<AllyController>(out var allyUnit))
        return allyUnit.GetArmorType();

    return ArmorType.Light;
}

private bool IsAirborne(GameObject target)
{
    if (target.TryGetComponent<UnitController>(out var playerUnit))
        return playerUnit.IsAirUnit();

    if (target.TryGetComponent<AllyController>(out var allyUnit))
        return allyUnit.IsAirUnit();

    if (target.TryGetComponent<BuildingController>(out var building))
        return building.IsLifted();

    return false;
}
```
`UnitController`/`BuildingController` 대상 판정은 그대로라 기존 동작에 영향 없고, `AllyController`
대상일 때만 정확한 값을 쓰게 된다.

---

### 1-3. `UIController` — 생산/연구 대기열 슬롯(`queueSlots`) null 체크 누락으로 크래시 위험
**파일:** `Assets/Scripts/UI/UIController.cs` (467~610번 줄)

같은 파일의 커맨드 패널 슬롯(`slots[]`)은 전부 `if (slots[i] == null) continue`로 방어하는데,
생산/연구 대기열용 `queueSlots[]`는 `UpdateQueue`/`UpdateResearchQueue`/`HideProductionUI`/
`HideResearchUI`/`SetEmptyQueueSlot` 어디서도 null 체크가 없다. 인스펙터에서 슬롯 하나만 연결이
빠져도 그 즉시 `NullReferenceException`으로 UI 전체가 멈춘다.

**기존 코드** (해당하는 5곳)
```csharp
// UpdateQueue / UpdateResearchQueue 내부, i번째 슬롯 접근부
queueSlots[i].Clear();
...
queueSlots[i].SetData(...);

// HideProductionUI / HideResearchUI
foreach (var slot in queueSlots)
    slot.Clear();

// SetEmptyQueueSlot
private void SetEmptyQueueSlot(int index)
{
    queueSlots[index].SetData(...);
}
```
**변경 코드** (기존 `slots[i] == null` 가드와 동일한 패턴 적용 — 정상 연결 시 동작 동일, 연결이
빠졌을 때만 조용히 건너뜀)
```csharp
// UpdateQueue / UpdateResearchQueue의 각 접근부 앞에 가드 추가
if (queueSlots[i] == null) continue;
queueSlots[i].Clear();
...
if (queueSlots[i] == null) continue;
queueSlots[i].SetData(...);

// HideProductionUI / HideResearchUI
foreach (var slot in queueSlots)
    slot?.Clear();

// SetEmptyQueueSlot
private void SetEmptyQueueSlot(int index)
{
    if (queueSlots[index] == null) return;
    queueSlots[index].SetData(...);
}
```

---

### 1-4. `SceneMenuController` — "이전 스테이지" 버튼이 빈 씬 이름으로 `LoadScene` 호출 시 크래시
**파일:** `Assets/Scripts/UI/SceneMenuController.cs` (23, 63~68번 줄)

`previousStageSceneName` 기본값이 `""`(스테이지 0처럼 이전 스테이지가 없는 씬은 인스펙터에 비워둠).
같은 프로젝트의 `MissionSelectManager.LoadMission()`은 이미 `IsNullOrEmpty` 가드로 이 패턴을
방어하는데, 이 메소드만 가드가 없어서 버튼이 잘못 활성 상태로 남으면 `SceneManager.LoadScene("")`가
예외를 던지며 크래시한다.

**기존 코드**
```csharp
private void OnPreviousStageClicked()
{
    Time.timeScale = 1f;
    UserControl.IsPaused = false;
    SceneManager.LoadScene(previousStageSceneName);
}
```
**변경 코드**
```csharp
private void OnPreviousStageClicked()
{
    if (string.IsNullOrEmpty(previousStageSceneName))
        return;

    Time.timeScale = 1f;
    UserControl.IsPaused = false;
    SceneManager.LoadScene(previousStageSceneName);
}
```
씬 이름이 정상적으로 채워진 케이스는 동작 그대로, 비어있을 때만 크래시 대신 조용히 무시한다.

---

## Tier 2 — 안전한 정리 (죽은 코드/스텁 제거)

### 2-1. `ResourceController.cs` — 완전히 빈 스텁, README에도 "미사용"으로 명시됨
**파일:** `Assets/Scripts/Resource/ResourceController.cs`
```csharp
using UnityEngine;

public class ResourceController : MonoBehaviour
{
}
```
아무 동작도 하지 않는 컴포넌트가 `Ore`/`Gas`/`RescueUnit` 등 프리팹에 부착돼 인스턴스화되고 있다.
**제안:** 스크립트 파일 삭제 + 부착된 프리팹들에서 해당 컴포넌트 제거. (컴포넌트가 아무 로직도 없으므로
제거해도 동작 변화 없음)

### 2-2. `RTSUnitController` — 코드베이스 어디서도 호출되지 않는 public 메소드 11개
**파일:** `Assets/Scripts/System/RTSUnitController.cs` (304, 2229~2246번 줄)

`Assets/` 전체를 grep한 결과 정의부 외 호출 지점이 하나도 없음(bool 반환형이라 UnityEvent 인스펙터
바인딩 대상도 아님):
```csharp
GetSelectedUnits(), IsNone(), IsEnemySelect(), IsOreSelect(), IsBuildingNone(),
IsMainBase(), IsTier1Building(), IsTier2Building(), IsTier3Building(),
IsSupplyDepot(), IsLab()
```
**제안:** 11개 메소드 삭제. (실제로 쓰이는 `IsUnitSelect()`/`IsBuildingSelect()`/`IsBuildMode()` 등은
그대로 유지)

### 2-3. `UnitSpawner.PrintQueue()` — 프로덕션에도 계속 도는 디버그 콘솔 로그
**파일:** `Assets/Scripts/UnitSpawner/UnitSpawner.cs` (71, 105, 144, 160~180번 줄)

큐가 바뀔 때마다(생산 추가/취소/스폰) 문자열을 조립해 `Debug.Log`로 출력. 자매 클래스인
`ResearchQueue`엔 이런 로그가 없어 비일관적이고, 유닛을 많이 뽑을수록 콘솔 스팸+문자열 조립 비용만
쌓인다.
**제안:** `PrintQueue()` 호출 3곳과 메소드 자체 삭제. (콘솔 출력만 없어지고 큐 동작 자체는 무관)

### 2-4. `PlacementSystem.StartPlacement(int ID)` — "ID==0 선택 해제" 분기가 영원히 도달 불가능
**파일:** `Assets/Scripts/BuildSystem/PlacementSystem.cs` (108~124번 줄)

`FindIndex` 결과가 -1이면 에러 로그를 찍고 먼저 `return`해버려서, 그 아래 있는 "`ID==0`이면 선택
해제" 분기는 절대 실행되지 않는다(건물 DB에 ID=0이 없어 `ID==0` 호출 시 항상 `FindIndex`가 -1을
반환하기 때문). 현재는 `StartPlacement`가 실제 건물 ID(1~6)로만 호출돼 관찰 가능한 오동작은 없지만,
"선택 해제" 용도로 0을 넘기는 코드가 추가되는 순간 그대로 에러 로그+return으로 막혀버린다.

**기존 코드**
```csharp
selectedObjectIndex = database.buildingData.FindIndex(d => d.ID == ID);

if (selectedObjectIndex < 0)
{
    Debug.LogError($"No ID found {ID}");
    return;
}

if (ID == 0)
{
    selectedObjectIndex = -1;
    return;
}
```
**변경 코드** (0 체크를 먼저 수행 — 기존 호출부는 전부 ID 1~6이라 동작 변화 없음)
```csharp
if (ID == 0)
{
    selectedObjectIndex = -1;
    return;
}

selectedObjectIndex = database.buildingData.FindIndex(d => d.ID == ID);

if (selectedObjectIndex < 0)
{
    Debug.LogError($"No ID found {ID}");
    return;
}
```

---

## Tier 3 — 방어 코드 보강 (현재는 미발생, 향후 위험 대비 — 동작 변화 없음)

### 3-1. `BuildingController` — `UnitSpawner` null 가드가 메소드마다 일관되지 않음
**파일:** `Assets/Scripts/Building/BuildingController.cs` (444~495번 줄)

`IsProductionQueueFull()`/`ClearProductionQueue()`는 `UnitSpawner != null`을 확인하지만
`SpawnUnit()`/`GetProductionQueue()`/`GetProductionProgress()`/`CancelProduction()`은 가드가 없다.
현재 `RTSUnitController`의 모든 호출부는 태그 스위치문으로 이미 걸러져 있어 크래시가 나지 않지만,
호출 경로가 바뀌면 즉시 위험해진다.

**변경 방향** (동작 동일, 방어만 추가): 나머지 4개 메소드에도 동일한 null 체크 추가.

### 3-2. `GridData.RemoveObjectAt` — 존재하지 않는 키로 호출되면 `KeyNotFoundException`
**파일:** `Assets/Scripts/BuildSystem/GridData.cs` (71~77번 줄)
```csharp
internal void RemoveObjectAt(Vector3Int gridPosition)
{
    foreach (var pos in placedObjects[gridPosition].occupiedPositions)
    {
        placedObjects.Remove(pos);
    }
}
```
**변경 코드**
```csharp
internal void RemoveObjectAt(Vector3Int gridPosition)
{
    if (!placedObjects.TryGetValue(gridPosition, out var data))
        return;

    foreach (var pos in data.occupiedPositions)
    {
        placedObjects.Remove(pos);
    }
}
```
현재 확인된 모든 호출부는 등록 시점과 짝을 맞춰 호출해 정상 동작하며, 존재하는 키로 호출될 때는
동작이 완전히 동일하다.

### 3-3. `VictoryPanelController` — `SceneMenuController`와 동일한 빈 씬 이름 가드 추가
**파일:** `Assets/Scripts/UI/VictoryPanelController.cs` (63~68번 줄)

기본값이 `"SampleScene"`이라 지금은 크래시 가능성이 낮지만, 1-4와 동일한 패턴을 적용해두면 인스펙터
값이 지워져도 안전하다.
```csharp
private void OnNextStageClicked()
{
    if (string.IsNullOrEmpty(nextStageSceneName))
        return;

    Time.timeScale = 1f;
    UserControl.IsPaused = false;
    SceneManager.LoadScene(nextStageSceneName);
}
```

### 3-4. `HoverBob`/`VehicleShake`/`VehicleIdleAnimation` — 정지 시 시작하는 "원위치 복귀" 트윈이 정리 목록에서 빠짐
**파일:** `Assets/Scripts/Animation/HoverBob.cs`(58~65), `VehicleShake.cs`(63~70),
`VehicleIdleAnimation.cs`(191~200)

`StopBob()`/`StopShake()`/`StopIdleShake()`가 원위치로 되돌리는 새 트윈을 시작하는데, 이 트윈이
필드에 저장되지 않아 `OnDestroy()`가 못 죽인다. DOTween Safe Mode가 보통 자동 정리해주지만, 대상
Transform이 파괴된 뒤 남은 트윈이 계속 접근을 시도할 여지를 없애기 위해 기존 필드에 대입한다.

**기존 코드** (`HoverBob` 예시, 나머지 2개도 동일 패턴)
```csharp
private void StopBob()
{
    bobTween?.Kill();
    transform.DOLocalMoveY(baseY, 0.3f).SetEase(Ease.OutSine);
}
```
**변경 코드**
```csharp
private void StopBob()
{
    bobTween?.Kill();
    bobTween = transform.DOLocalMoveY(baseY, 0.3f).SetEase(Ease.OutSine);
}
```
`OnDestroy()`가 이미 `bobTween?.Kill()`을 호출하므로 이 필드에 담기만 하면 자동으로 함께 정리된다.
재생 결과(복귀 애니메이션)는 동일.

---

## Tier 4 — 성능 최적화 (동작 동일, 비용만 감소)

### 4-1. `HealthManager`/`UnitEffects` — 스폰마다 `FindFirstObjectByType<csFogWar>()` 반복 탐색
**파일:** `Assets/Scripts/Unit/HealthManager.cs`(41), `Assets/Scripts/Effects/UnitEffects.cs`(55)

`EffectPlayer`는 이미 정적 캐시(`GetFogWar()`)로 씬당 1회만 탐색하는데, `HealthManager`/`UnitEffects`는
유닛/건물이 스폰될 때마다(생산 등) 각자 또 씬 전체를 탐색한다.

**변경 방향:** `EffectPlayer.GetFogWar()`를 `internal static`으로 열어 두 파일이 `FindFirstObjectByType`
대신 그걸 호출하도록 교체. `FogVisibility.IsRevealed(fogWar, pos)` 호출부는 그대로 유지되므로 동작
동일, 탐색 횟수만 줄어든다.

### 4-2. `RTSUnitController.Update()` — 매 프레임 람다 3개를 새로 할당
**파일:** `Assets/Scripts/System/RTSUnitController.cs` (179~182번 줄)
```csharp
private void Update()
{
    UnitList.RemoveAll(unit => unit == null);
    BuildingList.RemoveAll(building => building == null);
    ResourceNodeList.RemoveAll(node => node == null);
    ...
```
**변경 코드**
```csharp
private static readonly System.Predicate<UnitController> IsNullUnit = u => u == null;
private static readonly System.Predicate<BuildingController> IsNullBuilding = b => b == null;
private static readonly System.Predicate<ResourceNode> IsNullResourceNode = n => n == null;

private void Update()
{
    UnitList.RemoveAll(IsNullUnit);
    BuildingList.RemoveAll(IsNullBuilding);
    ResourceNodeList.RemoveAll(IsNullResourceNode);
    ...
```
결과 동일, 매 프레임 델리게이트 할당만 제거.

### 4-3. `Stage5Objectives` — 매 프레임 `FindAll()`로 임시 리스트 할당
**파일:** `Assets/Scripts/System/Stage5Objectives.cs` (33번 줄)
```csharp
int destroyedCoreCount = trackedEnergyCores.FindAll(core => core == null).Count;
```
**변경 코드**
```csharp
int destroyedCoreCount = 0;
foreach (var core in trackedEnergyCores)
{
    if (core == null)
        destroyedCoreCount++;
}
```
결과값 동일, 매 프레임 임시 `List` 할당만 제거.

### 4-4. `RadiusIndicator` — 인스턴스마다 만드는 `Material`이 파괴 시 해제되지 않음
**파일:** `Assets/Scripts/Effects/RadiusIndicator.cs` (35~52번 줄)

`Draw()`가 매번 `new Material(cachedLineShader)`를 만드는데, 오브젝트가 파괴돼도 그 머티리얼 인스턴스는
자동으로 해제되지 않는다(스킬을 반복 사용할수록 누적).

**변경 코드** (필드로 저장해 `OnDestroy`에서 해제)
```csharp
private Material lineMaterial;

private void Draw(float radius)
{
    ...
    if (cachedLineShader == null) cachedLineShader = Shader.Find("Sprites/Default");
    lineMaterial = new Material(cachedLineShader);
    line.material = lineMaterial;
    ...
}

private void OnDestroy()
{
    if (lineMaterial != null)
        Destroy(lineMaterial);
}
```
화면에 그려지는 결과는 동일, 파괴 시 머티리얼 인스턴스만 정리된다.

---

## 참고용 — 검토했지만 수정 보류 권장 (구조적이거나 리스크 대비 효과가 낮음)

- `UnitController`의 `GetTargetArmor`/`GetTargetSizeType`/`GetTargetArmorType`/`IsTargetAirborne` 4개가
  `UnitController→EnemyUnitController→AllyController` 조회 패턴을 반복 — 통합 가능하지만
  `EnemyUnitController`/`AllyController`까지 걸친 구조 변경이라 리스크 대비 효과가 낮음.
- `InfantryIdleLookAround`/`VehicleIdleAnimation`/`VehicleShake`/`UnitAnimatorDriver`/`TurretController`
  5개 파일이 "이 오브젝트가 UnitController/EnemyUnitController/AllyController 중 뭘 가졌는지" 판정을
  각자 반복 구현 — 공용 헬퍼로 묶을 수 있으나 5개 파일 동시 수정이라 리스크 대비 효과가 낮음.
- `ResearchQueue` vs `UnitSpawner` — 동일한 FIFO 타이머 큐 구조를 각자 구현 중이나, 둘 다 짧고 독립적으로
  잘 동작해 억지로 공통 베이스를 만들면 결합도만 늘어남.
- `Stage0~5Objectives` 6개 파일의 반복되는 가드/초기화 보일러플레이트 — 스테이지마다 목표 성격이 달라
  의도적으로 분리된 설계로 보여 병합 비권장.
- `UnitController`의 공중 유닛 겹침 분리 로직(`SeparateFromOverlappingAirUnits`)이 공중 유닛마다 전체
  유닛 리스트를 순회(O(n²)) — 실제 공중 유닛 수가 적어 체감 영향 낮고, 고치려면 `RTSUnitController`에
  별도 캐시 리스트를 추가하는 더 큰 변경이 필요함.
- `TurretController`/`AttackRange`가 같은 프레임에 각자 사거리 내 적을 탐색 — 사거리 내 적 수가 보통
  적어 실질 비용 낮음.
- `BuildingAudio.GetBank()` 매번 재조회 — 재생 이벤트가 드물게 호출돼(건설완공 1회 등) 실질 영향 낮음.
- `TerritoryZone.Update()`가 매 프레임 아웃라인을 다시 그림, `ControlGroupPanel.Update()`가 매 프레임
  그룹 10개를 순회 — 둘 다 거점/부대 수가 적어 실측 없이는 손댈 근거가 부족함.
- `Stage1Objectives`의 `OnEnable`이 `EnemyBuildingController.ActiveBuildings`를 즉시 읽는 타이밍 —
  Unity 실행 순서(Awake 전체 → OnEnable 전체 → Start 전체)상 `EnemyBuildingController.Start()`가 자신을
  등록하기 전에 한 번 잘못된 값을 세팅하지만, 각 건물의 `Start()`가 이벤트를 쏘아 같은 프레임(첫
  `Update`/렌더 전에) 다시 보정되므로 **실제로 관찰 가능한 버그가 아님을 확인 — 수정 불필요**.
- `RTSUnitController.DeselectAll()`의 선택 목록 null 가드 부재 — `UnitController.Die()`/
  `BuildingController.Die()` 둘 다 파괴 전에 `selectedUnitList`/`selectedBuildingList`에서 스스로 제거함을
  확인(`EnemyUnitController`/`AllyController`도 동일 패턴, 이전 조사에서 확인) — **실제로 null이 들어갈
  경로가 없음을 확인, 수정 불필요**.
- `GuardianDroneSkill`의 쉴드 종료 시 최대체력 계산 — 다른 `SetMaxHealth` 호출부와 충돌 가능성은
  코드베이스 내에서 확인되지 않아 근거 부족, 보류.

## 영향받는 파일 (Tier 1~4 전체 적용 시)
- `Assets/Scripts/UserControl/UserControl.cs`
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/UI/SceneMenuController.cs`
- `Assets/Scripts/Resource/ResourceController.cs` (삭제) + 부착 프리팹들
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UnitSpawner/UnitSpawner.cs`
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/BuildSystem/GridData.cs`
- `Assets/Scripts/UI/VictoryPanelController.cs`
- `Assets/Scripts/Animation/HoverBob.cs`, `VehicleShake.cs`, `VehicleIdleAnimation.cs`
- `Assets/Scripts/Effects/EffectPlayer.cs`, `UnitEffects.cs`, `Assets/Scripts/Unit/HealthManager.cs`
- `Assets/Scripts/Effects/RadiusIndicator.cs`

## 확인 예정 (승인 시)
컴파일 확인, Play Mode에서 골든 패스(선택/이동/공격/건설/생산/연구/우클릭 건물·자원 명령/스테이지
진행/스킬/승리·패배 화면) 회귀 확인. 특히 1-1(우클릭 이중명령)과 1-2(EnemyUnitController 대상 조회)는
실제 플레이로 재현 후 수정 전/후 비교 권장.

## 적용 결과 (2026-08-09)

사용자가 "전부 진행해줘"로 Tier 1~4 전체 승인. 아래 항목을 제외한 나머지 15건을 전부 코드에 적용:

- **미적용: 2-1 `ResourceController` 삭제** — 4개 프리팹(Gas/Ore/Cyborg Soldier (Rescue)/Heavy Assault
  Tank (Rescue))에서 컴포넌트를 먼저 제거해야 스크립트를 안전하게 지울 수 있는데, Unity Editor
  프리팹 수정에 필요한 `uloop-cli execute-dynamic-code` 실행이 권한 클래시파이어에 두 차례 막혀
  실행되지 못함(`git status`로 프리팹 무변경 확인). 스크립트만 먼저 지우면 4개 프리팹에 "Missing
  Script" 경고가 남는 것을 방지하기 위해 스크립트도 그대로 유지 — 미완료 항목으로 남김.
  (부수적으로 이 작업을 위임한 서브에이전트가 `doc/0497-remove-resourcecontroller-from-prefabs-proposal.md`를
  중복 생성했고 권한 문제로 삭제 못 함 — 참고용 중복 파일로 남아있음)

나머지 15건(Tier 1 전체, Tier 2의 2-2/2-3/2-4, Tier 3 전체, Tier 4 전체) 적용 완료. 컴파일 확인
(`uloop-cli compile`) 결과 `Success: true, ErrorCount: 0` — 이번 변경으로 인한 컴파일 에러/경고 없음
(리스트에 나온 warning들은 전부 이번 세션과 무관한 기존 `FindFirstObjectByType` deprecation 경고).
Play Mode 진입 후 콘솔에 에러 없음을 확인(현재 열려있던 씬 기준 스모크 테스트, 자동화된 테스트
스위트가 프로젝트에 없어 전체 골든 패스 수동 플레이는 진행하지 못함 — 특히 1-1/1-2는 실제 플레이로
재확인 권장).
