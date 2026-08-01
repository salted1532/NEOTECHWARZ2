# 0345 — 버그수정(조사): 프로토타입 빌드 테스트 피드백 일괄 조사

**날짜:** 2026-08-01

## 요청 내용

어제 프로토타입 빌드를 테스트하면서 발견한 버그 목록:

1. 인구수 최대 초과시 보급고도 안지어지는 버그
2. 빌드시 헤비 탱크랑 브루트 투명화 버그 → 투명 드래곤 (이미 조사/수정 완료, [[0344-bugfix-enemy-unit-transparent-in-build]] 참고)
3. 이동, 공격 명령 마커 2개 동시에 생기는 버그
4. A공격 잘 씹히는거 수정
5. 많은 일꾼이 자원 채취중 멈춤
6. 일꾼이 건물이랑 멀어졌을때 광물 들고 있는 상태에서 리턴을 안함 / 다른 명령을 했을때 리턴 명령을 되돌리거나 / 메인건물이 착륙후 일꾼이 리턴 명령을 수행하도록

2번은 이미 별도 문서로 조사·수정이 끝나 있어(작업 트리에 `ProjectSettings/GraphicsSettings.asset` 변경분 존재, 아직 커밋 전) 이 문서에서는 제외. 나머지 5건을 조사했다.

---

## 버그 1: 인구수 최대시 보급고 안 지어짐

**결론: 인구수와는 실제로 무관한 별개의 버그.**

### 원인

`RTSUnitController.GetSelectedWorker()` (`Assets/Scripts/System/RTSUnitController.cs:277-284`)가 선택 목록의 **인덱스 0번만** 확인한다:

```csharp
public UnitController GetSelectedWorker()
{
    if (selectedUnitList.Count == 0)
        return null;

    UnitController unit = selectedUnitList[0];
    return unit != null && unit.CompareTag("Worker") && !unit.IsConstructing() ? unit : null;
}
```

드래그로 일꾼과 전투유닛을 함께 선택하면, `UnitSelectState`(패널 표시용)는 "마지막으로 처리된 유닛"의 태그로 결정되는 반면, `GetSelectedWorker()`가 보는 `selectedUnitList[0]`은 "가장 먼저 선택된 유닛"이라 서로 무관하다. 그래서 Build 버튼(일꾼용 패널)은 떠 있는데 0번 유닛이 전투유닛이면 `GetSelectedWorker()`가 `null`을 반환한다.

`PlacementSystem.PlaceStructure()` (`PlacementSystem.cs:164-166`)는 `worker == null`이면 **아무 피드백 없이 조용히 return**한다 — 사운드도 로그도 없어서 사용자 입장에선 "그냥 안 지어짐"으로 보인다.

인구수가 높을수록(유닛이 많을수록) 드래그 선택에 일꾼+전투유닛이 섞일 확률이 커지므로 "인구수 최대일 때" 증상처럼 보였을 뿐, population 수치 자체는 이 실패 경로 어디에도 관여하지 않는다.

### 확인했지만 원인이 아니었던 것

- `isConstructing` 플래그가 stuck되는 경로: 없음 (완공/취소/파괴 3개 경로 모두 `FinishConstruction()` 정확히 호출).
- `BuildingDataSO` 에셋의 SupplyDepot `population` 필드: `0`. 애초에 `TryConstructBuilding()`도 이 필드를 안 씀.
- 빌드모드 진입(B키), UI 버튼 interactable: 인구수 체크 코드 자체가 없음.

### 제안 수정

```diff
--- a/Assets/Scripts/System/RTSUnitController.cs
+++ b/Assets/Scripts/System/RTSUnitController.cs
@@ public UnitController GetSelectedWorker()
     public UnitController GetSelectedWorker()
     {
-        if (selectedUnitList.Count == 0)
-            return null;
-
-        UnitController unit = selectedUnitList[0];
-        return unit != null && unit.CompareTag("Worker") && !unit.IsConstructing() ? unit : null;
+        foreach (UnitController unit in selectedUnitList)
+        {
+            if (unit != null && unit.CompareTag("Worker") && !unit.IsConstructing())
+                return unit;
+        }
+        return null;
     }
```

선택 목록 어디에 있든 건설 가능한 일꾼을 찾도록 — 인덱스 0 가정이라는 근본 원인 자체를 제거하는 최소 diff. `AssignBuilderToStructure()` 등 이 메서드를 쓰는 다른 호출부도 함께 혜택을 받는다.

---

## 버그 2: 이동/공격 마커 2개 동시 표시

**원인 확정** (`Assets/Scripts/UserControl/UserControl.cs`)

`movePointer`/`attackPointer`는 재사용되는 단일 GameObject 2개인데, 명령을 확정하는 코드(`HandleLeftClick`/`HandleRightClick`)가 자기 쪽만 `SetActive(true)`로 켜고 **상대 포인터를 끄지 않는다.** (참고로 명령 대기 중 마우스를 따라다니는 미리보기용 `UpdatePointer()`(919-959줄)는 서로 배타적으로 켜고 끄지만, 이건 클릭 "확정" 시점 코드가 아니다.)

- `movePointer`만 켜고 `attackPointer`를 안 끄는 지점: 415-416, 439-440, 451-452, 463-464, 542-543, 571-572, 596-597, 613-614, 629-630, 648-649, 661-662, 682-683, 704-705줄
- `attackPointer`만 켜고 `movePointer`를 안 끄는 지점: 236-237, 250-251, 289-290, 303-304, 326-327, 354-355, 379-380, 401-402, 427-428, 564-565, 589-590줄

이동 명령 직후 바로 공격 명령을 내리면(또는 반대) 이전 포인터가 안 꺼진 채 새 포인터가 추가로 켜져서 두 마커가 동시에 보인다.

### 제안 수정

호출부 20여 곳을 각각 고치는 대신, 공용 헬퍼 2개로 정리(같은 실수가 다시 나올 여지를 없앰):

```csharp
private void ShowMovePointer(Vector3 position)
{
    movePointer.transform.position = position;
    movePointer.SetActive(true);
    attackPointer.SetActive(false);
}

private void ShowAttackPointer(Vector3 position)
{
    attackPointer.transform.position = position;
    attackPointer.SetActive(true);
    movePointer.SetActive(false);
}
```

이후 위에 나열한 모든 `xPointer.transform.position = ...; xPointer.SetActive(true);` 페어를 `ShowMovePointer(...)` / `ShowAttackPointer(...)` 한 줄로 교체.

---

## 버그 3: A공격 잘 씹힘

**원인 확정** (`Assets/Scripts/UI/ProductionSlot.cs:156-170`)

Attack 버튼은 단축키 `A`로 연결돼 있다(`RTSUnitController.cs` 1611/1622줄). 단축키를 누르면 `ProductionSlot.Update()`가 실제 클릭 대신 `SimulateClickRoutine()` 코루틴을 돈다:

```csharp
ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerDownHandler);
yield return new WaitForSeconds(0.08f);   // ← 여기
ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerUpHandler);
ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerClickHandler); // 실제 콜백(EnterAttackMode)은 여기서만 실행
```

버튼 눌림 애니메이션을 재현하려고 넣은 0.08초 지연인데, **실제 게임 로직 콜백이 `pointerClickHandler`에만 걸려있어 그 0.08초 뒤에야 실행된다.** "A키 누르고 바로 클릭"처럼 80ms보다 빠른 입력 콤보는, `UserControl`의 상태가 아직 Attack으로 안 바뀐 시점에 클릭이 처리되어 평범한 좌클릭(선택/이동)으로 씹힌다. Attack 버튼뿐 아니라 단축키가 달린 모든 커맨드 버튼(Move/Patrol 등)에 공통되는 지점.

### 제안 수정

```diff
 private IEnumerator SimulateClickRoutine()
 {
     PointerEventData eventData = new PointerEventData(EventSystem.current);

     ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerDownHandler);
-    yield return new WaitForSeconds(0.08f);
-    ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerUpHandler);
     ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerClickHandler);
+    yield return new WaitForSeconds(0.08f);
+    ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerUpHandler);
 }
```

실제 콜백(pointerClick)은 즉시 실행하고, 시각적 "눌림" 상태만 0.08초 유지하도록 순서만 바꾼다. 이 함수 하나만 고치면 단축키가 달린 모든 커맨드 버튼에 동일하게 적용됨.

---

## 버그 4~6: 일꾼 채취 멈춤 / 리턴 관련 (하나의 근본 원인 군집)

조사 결과, 사용자가 보고한 "많은 일꾼이 채취 중 멈춤", "건물과 멀어지면 리턴 안 함", "다른 명령이 리턴을 안 되돌림", "메인건물 착륙 후 리턴 재개 안됨" 4가지가 서로 얽힌 **3개의 코드 결함**으로 좁혀졌다.

### 원인 A (CONFIRMED) — 리프트된 건물이 반납 대상 필터링에서 안 빠짐

`Assets/Scripts/Unit/UnitController.cs:1468-1487`의 `FindNearestDepositBuilding()`:

```csharp
foreach (BuildingController building in rtsController.BuildingList)
{
    if (building == null) continue;
    if (!building.CompareTag("MainBase")) continue; // 메인기지에만 반납
    ...
}
```

`BuildingController.LiftOff()`로 이륙해도 `rtsController.BuildingList`에서 제거되지 않는다(`Die()`에서만 Remove). 즉 메인건물이 공중에 떠 있어도 여전히 "가장 가까운 반납 건물"로 선택되는데, 실제로는 도달 불가능한 위치라 절대 도착 판정에 들어오지 못한다.
→ **버그 6(메인건물 착륙 후 리턴 안됨)의 핵심 원인.**

### 원인 B (CONFIRMED) — 반납 목적지가 스냅샷이라 건물이 움직이면 못 따라감

`UnitController.cs:1411-1419` (`GatherState.MovingToBase`)는 거리는 매 틱 실시간으로 재지만(`SqrDistanceToTarget`), NavMeshAgent의 실제 이동 목적지는 `ReturnCargo()`/`Deposit()` 시점(`UnitController.cs:1209, 1277, 1406, 1447`)에 **딱 한 번만** 설정된다. 건물이 그 뒤 이동(`MoveWhileLifted` 등)하면 일꾼은 옛 좌표로 계속 걸어가고 도착 판정은 영원히 실패한다.
→ **버그 6(건물과 멀어졌을 때 리턴 안 함)의 핵심 원인.**

### 원인 C (CONFIRMED 절반 / A는 가설) — 다른 명령이 화물 상태를 방치

`UnitController.cs:1304-1320`의 `CancelGatheringForNewCommand()`는 `MoveTo`/`AttackUnitTarget`/`StopUnit`/`HoldUnit` 등 거의 모든 명령 진입점에서 호출되는데:

```csharp
if ((gatherState == WaitingInQueue || gatherState == Gathering) && gatherTargetNode != null)
    gatherTargetNode.LeaveQueue(this);
gatherState = GatherState.None;
```

일꾼이 `MovingToBase`/`Depositing` 상태(이미 자원을 들고 있는 중)일 때 다른 명령을 받으면 `gatherState`만 `None`으로 리셋되고, **들고 있는 자원(carrying 플래그)은 그대로 남는다.** `IsCarryingResource()`가 계속 `true`인데 아무 상태도 그걸 처리하지 않아 "자원 들고 멈춰선 일꾼"이 된다 — 플레이어가 수동으로 R(반환)을 다시 눌러야 풀림.
→ **버그 6(다른 명령이 리턴을 안 되돌림)의 원인이자, 여러 일꾼을 한꺼번에 선택해 정지/이동시키는 상황에서 다수가 동시에 이 상태에 빠지면 버그 5("많은 일꾼이 채취 중 멈춤")로 보였을 가능성이 높음.**

다만 버그 5는 NavMesh 혼잡/대기열(`ResourceNode.cs`의 `workerQueue`) 문제일 가능성도 배제 못 함 — 정적 분석상 대기열 이탈/합류 로직 자체는 결함을 못 찾았고(모든 경로에서 짝이 맞음), 실제로 몇 마리가 몰릴 때만 재현되는 문제라면 Play Mode에서 5~10마리를 한 노드로 몰아 재현 테스트가 필요.

### 제안 수정

**1) 공중에 뜬 건물은 반납 대상에서 제외**
```diff
 foreach (BuildingController building in rtsController.BuildingList)
 {
     if (building == null) continue;
     if (!building.CompareTag("MainBase")) continue; // 메인기지에만 반납
+    if (building.IsLifted()) continue; // 공중에 뜬 동안은 반납 불가
     ...
```

**2) `MovingToBase` 중 매 틱 목적지 재동기화 + 착륙 대기**
`GatherTick()`의 `MovingToBase` 케이스 진입 시, 대상 건물이 `IsLifted()`면 도착 판정을 보류하고 제자리 대기, 아니면 매 틱 `MoveAgentTo(depositTargetTransform.position)`로 목적지를 갱신. "노드가 고갈되면 재탐색"과 같은 패턴으로 "건물이 공중에 뜨면 대기 → 착륙하면 자동 재개"를 구현하면 원인 A·B(=버그 6 전체)가 함께 해결됨.

**3) 화물을 든 채 다른 명령을 받으면 자동으로 반환 재개**
`CancelGatheringForNewCommand()`가 `IsCarryingResource()`인데 상태를 그냥 버리는 대신, 명시적으로 멈추는 명령(정지/홀드)이 아니라면 화물이 있을 때 자동으로 `ReturnCargo()`를 다시 걸어주는 처리 추가. (정지/홀드는 사용자가 의도적으로 멈춘 것이므로 자동 재개 대상에서 제외할지는 확인 필요 — 아래 질문 참고)

---

## 요약 / 영향받는 파일

| 버그 | 파일 | 성격 |
|---|---|---|
| 1. 보급고 안 지어짐 | `Assets/Scripts/System/RTSUnitController.cs` | CONFIRMED |
| 3. 마커 중복 | `Assets/Scripts/UserControl/UserControl.cs` | CONFIRMED |
| 4. A공격 씹힘 | `Assets/Scripts/UI/ProductionSlot.cs` | CONFIRMED |
| 6-B/D. 리턴 관련 | `Assets/Scripts/Unit/UnitController.cs` | CONFIRMED |
| 6-C. 명령이 화물 상태 방치 | `Assets/Scripts/Unit/UnitController.cs` | CONFIRMED (원인) / 정확한 처리 방식은 확인 필요 |
| 5. 다수 일꾼 채취 멈춤 | `Assets/Scripts/Unit/UnitController.cs`, `Assets/Scripts/Resource/ResourceNode.cs` | 6-C와 동일 원인일 가능성 높음(가설), 별도 재현 테스트 필요 |

---

## 적용 결과 (2026-08-01)

사용자 확인 후 1·3·4·6(A·B·D) 전부 적용, 6-C는 확인 질문 결과에 따라 **정지/홀드 명령에도 자동 반납 재개 적용**. `npx uloop-cli compile` 통과(에러 0, 기존 경고 25개만 — 이번 변경으로 인한 경고 없음).

- **버그 1**: 제안 diff 그대로 적용 (`RTSUnitController.cs`).
- **버그 3**: 제안대로 `ShowMovePointer`/`ShowAttackPointer` 헬퍼 2개를 추가하고, 클릭 확정 지점 24곳을 전부 이 헬퍼 호출로 교체 (`UserControl.cs`). ESC/그룹전환 시 양쪽을 그냥 끄는 기존 2곳(745·790줄 부근)과, 대기 중 미리보기용 `UpdatePointer()`(이미 상호 배타적으로 동작)는 그대로 둠.
  - **추가 요청(같은 세션)**: "마커가 뜨고 3초 있다가 자동으로 사라지게" — `movePointerHideTime`/`attackPointerHideTime` 필드를 추가해, 마커가 갱신될 때마다(명령 확정 시 `ShowXPointer`, 조준 중엔 `UpdatePointer()`가 매 프레임) `Time.time + 3f`로 다시 밀어두고, `Update()`에서 매 프레임 `UpdatePointerAutoHide()`로 만료 여부만 확인해 끈다. 조준 중엔 매 프레임 갱신되므로 타이머가 만료되지 않고(원하는 만큼 오래 조준 가능), 명령이 확정된 뒤 아무도 안 건드리는 시점부터 정확히 3초 뒤에 사라진다.
- **버그 4**: 제안 diff 그대로 적용 (`ProductionSlot.cs`).
- **버그 6-A/D (건물 리프트 시 반납 대상 문제)**: 제안했던 단순 `if (building.IsLifted()) continue;` 대신, **착륙한 메인기지를 우선 탐색하고 그마저 없으면 리프트 중인 곳을 그대로 목표로 잡는 2단계 탐색**(`FindNearestMainBase(requireLanded:)`)으로 구현 — 메인기지가 하나뿐이고 리프트 중이라도 반납 목표를 아예 잃지 않게 하기 위함.
- **버그 6-B/D (건물이 움직이면 못 따라감 / 착륙 후 재개)**: `GatherTick()`의 `MovingToBase` 케이스에서, 목표 건물이 `IsLifted()`면 제자리에서 대기(매 틱 재확인)하고, 아니면 목적지가 실제로 바뀌었을 때만 `MoveAgentTo()`로 재동기화하도록 수정. 착륙 이벤트를 별도로 통지하는 콜백 배선 없이, 매 틱 상태만 확인하는 것만으로 "착륙할 때까지 대기 → 착륙하면 자동 재개"가 자연히 성립함(제안했던 이벤트 기반 방식보다 더 단순한 구현).
- **버그 6-C**: 애초 제안(모든 명령에 자동 반납)은 Move/Attack처럼 **플레이어가 명시적 목적지를 지정하는 명령과 충돌**함을 구현 중 재검토로 확인 — 매 틱 반납 목적지로 재동기화하는 6-B 로직이 방금 내린 Move 명령의 목적지를 다음 틱에 덮어써버리는 문제가 있었음. 그래서 자동 반납 재개는 **목적지가 없는 명령인 정지(Stop)·홀드(Hold)에만** 적용 (`StopUnit()`/`HoldUnit()`에서 `IsCarryingResource()`면 그 자리에 멈추는 대신 `ReturnCargo()` 호출). Move/Attack/Patrol 등은 기존 동작(화물을 든 채 이동, 나중에 수동으로 반납해야 함) 유지 — 플레이어가 지정한 목적지를 존중하기 위함.
- **버그 5**: 코드 수정 없음. 6-C의 근본 원인(정지/홀드가 화물을 방치하는 문제)을 고쳤으니 재현되는지 다시 테스트 필요.

**빌드 확인 필요**: 6-A/B/D는 "메인기지 리프트 → 이동 → 착륙" 시나리오를, 6-C는 "자원 들고 있는 일꾼에게 정지/홀드" 시나리오를 실제 플레이로 확인 부탁.

---

## 2차 라운드: 디밸로퍼 빌드 실플레이 피드백 (2026-08-01, TestBuild)

실제 `TestBuild`로 플레이하면서 발견된 추가 버그. `Player.log`의 `[UnitDiag]` 로그도 함께 확인.

### 버그 7: 건물 리프트→위치 이동→착륙 중 일꾼이 자원/메인기지 우클릭하면 완전히 멈춤

**증상**: 자원 채취 중 메인기지가 이륙해서 다른 위치로 이동 후 착륙하는 동안, 일꾼에게 자원 우클릭(채취 명령)을 내리면 그대로 고장남 — 이동/공격 명령으로도 안 풀림. 메인기지 근처로 직접 이동시킨 뒤 우클릭해야만 정상화됨.

**원인**: 1차 라운드에서 고친 "건물이 리프트 중이면 대기" 로직(`GatherTick`의 `MovingToBase` 케이스)은 이미 `MovingToBase` 상태에 들어간 뒤에만 동작한다. 그런데 `Gather()`(자원 우클릭 시 화물 들고 있으면 반납 리다이렉트), `ReturnCargo()`, `GatherTick`의 채취 완료 시점 3곳은 전부 `depositTargetTransform`(반납 대상 건물)이 **지금 리프트 중인지 확인하지 않고** 곧바로 `MoveTo(depositTargetTransform.position)`을 호출했다 — 건물이 공중에 떠 있으면 그 위치가 NavMesh 밖(허공)이라 `NavMeshAgent.SetDestination`이 실패해서 그 자리에 멈춰버리고, 이후 어떤 명령을 다시 내려도 같은 경로로 똑같이 실패해서 계속 안 풀렸던 것.

**수정**: `Assets/Scripts/Unit/UnitController.cs`에 `MoveToDepositTargetOrWait()` 헬퍼를 추가 — 대상 건물이 리프트 중이면 이동 명령 자체를 걸지 않고 제자리에서 대기(`gatherState = MovingToBase`만 세팅), 아니면 기존처럼 바로 이동. `Gather()`/`ReturnCargo()`/`GatherTick`의 채취 완료 분기 3곳 전부 이 헬퍼로 교체.

### 버그 8: 인구수가 초과(22/10)된 상태에서는 인구수가 필요 없는 건물도 "자원 부족"으로 건설 불가

**증상**: 인구수 22/10인 상태에서 건물을 지으려 하면 "자원부족"이라며 거부됨. 초과분 유닛을 죽여서 10/10으로 맞추면 정상적으로 건설됨.

**원인 확정**: `Assets/Scripts/Resource/ResourceManager.cs`의 `CanAfford()`가 `currentPopulation + populationCost > GetMaxPopulation()`을 검사하는데, 건물 건설은 `populationCost = 0`으로 호출됨에도 이 식은 `populationCost`와 무관하게 **이미 초과된 절대값(currentPopulation)** 자체를 기준으로 판정한다. 즉 인구수를 전혀 안 쓰는 요청까지도 "현재 인구수가 한도를 넘었다"는 이유로 막혀버렸다. (인구수가 22까지 갈 수 있었던 이유: [[0333-scene-placed-unit-population-accounting]]에서 씬에 미리 배치된 유닛은 `CanAfford` 판정 없이 인구수를 직접 더하는 경로가 있어서, 정상적으로 이 값이 한도를 넘을 수 있음.)

**수정**: `populationCost > 0`일 때만 인구수 여유를 검사하도록 변경 — 인구수를 쓰지 않는 요청(건물 등)은 현재 인구수 상태와 무관하게 항상 통과.

### 버그 9: 병영(및 다른 건물)을 기존 건물에 딱 붙여서 지으면 막힘

**증상**: 건물을 기존 건물에 완전히 인접하게(붙여서) 지으려 하면 배치 거부됨.

**조사**: 그리드 크기(`BuildingDataSO.Size`)와 실제 콜라이더 풋프린트는 모든 건물에서 정확히 일치함을 확인(불일치 아님). `PlacementSystem.IsBlocked()`가 `Physics.OverlapBox`로 물리 충돌을 추가로 검사하는데, 이 판정 박스를 실제 풋프린트보다 살짝 줄이는 여유값(`margin`)이 `0.02`(양쪽 합 0.04유닛)로 매우 얇았다. 이론상 계산으로는 정확히 붙여 지어도 통과해야 하는 수치이지만, 지형 높이 샘플링이나 그리드→월드 변환 과정의 미세한 부동소수점 오차를 흡수하기엔 이 여유가 너무 얇아서 실전에서 막히는 것으로 추정.

**수정**: `margin`을 `0.02f` → `0.1f`로 확대. 실제 겹침 자체는 그리드 셀 점유 체크(`GridData`/`StructureData`)가 정수 단위로 별도로 정확히 막고 있어서, 이 물리 판정 여유를 넉넉히 늘려도 진짜 겹치는 배치가 통과할 위험은 없음.

### 버그 10: 다수의 일꾼이 자원 채취 중 자원 "사이"로 들어가 리턴을 안 함

**증상**: 여러 일꾼이 동시에 자원을 캘 때 일부가 자원 노드들 사이 틈으로 들어가 버리고 채취 후 반납을 안 함.

**원인(가설, 근거 있음)**: `Gather()`/`TryRedirectToNearbyResource()`가 전부 자원 노드의 **중심 좌표**(`node.transform.position`)를 이동 목적지로 삼았다. 자원 노드가 여러 개 가까이 붙어 있으면, 여러 일꾼이 전부 각자 목표 노드의 중심을 향해 걷다가 "도착 판정 반경"(`gatherInteractRange`) 안에 들어온 시점에 멈추는데, 그 지점이 노드들 사이의 좁은 틈일 수 있다. `JoinQueue`/`LeaveQueue` 카운트 누락 등 대기열 로직 자체의 결함은 전수 확인했으나 발견되지 않음.

**수정**: `SqrDistanceToTarget`/`AssignBuilderToStructure`에서 이미 쓰던 패턴과 동일하게, 목적지를 노드 중심이 아니라 **콜라이더 표면의 가장 가까운 지점**(`Collider.ClosestPoint`)으로 바꾸는 `GetApproachPoint()` 헬퍼를 추가해 `Gather()`/`TryRedirectToNearbyResource()`에 적용. 각자 접근한 방향(바깥쪽)에서 자연히 멈추게 되어 여러 일꾼이 같은 좁은 틈으로 몰리는 걸 줄인다. **완전히 해결 안 될 수 있음** — 한 노드에 아주 많은 일꾼이 몰리는 경우까지 막으려면 노드별로 실제 대기 슬롯 좌표를 두는 별도 작업이 필요할 수 있음. 재현되면 후속 조치.

### 버그 11(진행 중): 헤비탱크/브루트메크/스카이랜서가 땅속에 박힌 것 같음

**증상**: 파이어호크로 공격 확인 시 레이저가 땅속으로 발사됨(공격은 됨). 보병/공중유닛은 안 그럼.

**조사**: `Player.log`의 `[UnitDiag]` 로그를 확인한 결과 헤비탱크/브루트메크의 렌더러·메쉬·셰이더는 전부 정상(모든 렌더러 `enabled=True`, 실제 메쉬 로드됨, `Universal Render Pipeline/Lit`, `isSupported=True`, `renderQueue=2000`)이었다 — [[0344-bugfix-enemy-unit-transparent-in-build]]의 "빌드에서만 투명해짐" 문제와는 무관해 보인다(그 수정은 유효했던 것으로 보임). 다만 "땅과의 거리"를 재려던 레이캐스트가 유닛 자신의 `EnemyAttackRange` 감지용 트리거 콜라이더에 먼저 맞아버려서 실제 지형 높이가 아닌 무의미한 값을 남기고 있었다 — `QueryTriggerInteraction.Ignore`를 추가해 트리거를 무시하도록 수정(`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`).

프리팹의 모델 로컬 Y오프셋(`Heavy Assault Tank.prefab`의 중첩 모델 `unit_Tank_Heavy_B_yup`이 `m_LocalPosition.y: -1`)도 확인했으나, 정상으로 알려진 Ironhawk도 동일하게 `-1`이라 이것만으로는 원인을 특정 못함. **다시 빌드해서 로그를 남겨주시면(고쳐진 레이캐스트로) 실제로 땅속에 박혔는지 정확한 수치를 확인할 수 있음.**

## 요약 / 영향받는 파일 (2차 라운드)

| 버그 | 파일 | 상태 |
|---|---|---|
| 7. 리프트 중 반납 완전 고장 | `Assets/Scripts/Unit/UnitController.cs` | 수정 완료 |
| 8. 인구수 초과시 0-cost 건설 불가 | `Assets/Scripts/Resource/ResourceManager.cs` | 수정 완료 (원인 확정) |
| 9. 건물 인접 배치 차단 | `Assets/Scripts/BuildSystem/PlacementSystem.cs` | 수정 완료 (가설 기반, 재테스트 필요) |
| 10. 다수 일꾼 자원 사이 끼임 | `Assets/Scripts/Unit/UnitController.cs` | 수정 완료 (가설 기반, 재테스트 필요) |
| 11. 헤비탱크 등 땅속 박힘 | `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` | 진단 로그만 수정, 원인 미확정 — 재빌드 후 로그 필요 |

전부 컴파일 확인 완료(에러 0, 기존 경고만).

---

## 3차 라운드: 재빌드 재테스트 피드백 (2026-08-01, 2번째 TestBuild)

### 버그 8 재확인: 인구수 초과 건물 건설 — **정상 동작 확인**
### 버그 2 재확인: 마커 중복/자동소멸 — **정상 동작 확인**

### 버그 7 후속: 여전히 리프트/착륙 중 반납이 끊기거나 아예 안 됨

2차 라운드 수정(건물이 리프트 중이면 대기)을 적용했음에도 "어느 정도까지만 가다가 멈추거나 아예 멈추는" 증상이 남아있다는 재현 보고. 사용자 요청: 자원 우클릭 시엔 "착륙 중인(=현재 착륙해 있는) 메인기지 중 가장 가까운 곳"을 찾고, 메인기지를 직접 우클릭하면 "그 건물"을 반납 대상으로 명시적으로 지정해서 반납 과정을 수행해달라는 것.

**조사 결과**: `MoveToBuilding(BuildingController building)`(우클릭한 건물로 이동/반납하는 진입점)이 실제로 클릭한 건물을 무시하고 있었다 — 화물을 들고 있으면 무조건 `ReturnCargo()`(가장 가까운 기지를 다시 검색)를 호출해서, 사용자가 명시적으로 착륙한 메인기지를 지정해 우클릭해도 그 지정이 반영되지 않았다. 자원 우클릭(`Gather()`) 쪽은 이미 1차 수정에서 "착륙한 기지 우선, 없으면 리프트 중인 곳"으로 찾고 있었음(요청과 일치).

**수정**: `Assets/Scripts/Unit/UnitController.cs`에 `ReturnCargoTo(BuildingController building)`을 추가 — `FindNearestDepositBuilding()`으로 다시 검색하지 않고 인자로 받은 건물을 그대로 반납 대상으로 삼는다. `MoveToBuilding()`이 클릭한 건물이 `MainBase` 태그면 이 메서드를, 아니면 기존 `ReturnCargo()`를 호출하도록 분기.

**치명적인 버그 추가 발견 및 수정**: 이 조사 도중, 2차 라운드에서 추가한 "정지(Stop)/홀드 명령에 화물이 있으면 자동으로 반납 재개" 로직이 "반납할 메인기지를 아예 못 찾는" 상황(예: 메인기지가 전부 파괴된 경우)과 만나면 **무한 재귀로 스택 오버플로우(크래시)가 나는 버그**를 발견했다: `CancelGathering()`(반납 건물 없음 → 멈춤)이 `StopUnit()`을 불렀는데, `StopUnit()`은 화물이 있으면 다시 `ReturnCargo()`를 부르고, 그게 또 반납 건물을 못 찾으면 다시 `CancelGathering()`을 부르는 순환 구조였다. `StopUnit()`의 "그 자리에 멈추는" 부분만 `HaltInPlace()`로 분리하고 `CancelGathering()`은 이 저수준 헬퍼만 쓰도록 고쳤다 — 실제로 이 경로를 밟았었다면 지금까지의 "완전히 멈춤" 보고 중 일부가 이 크래시/먹통이었을 가능성도 있음.

**추가로 남긴 진단 로그** (`Assets/Scripts/Unit/UnitController.cs`, 원인 확정되면 삭제 가능):
- `MoveToDepositTargetOrWait()` 시작 시점: `[GatherDiag] {유닛명}: 반납 시작 target=... lifted=... targetPos=... myPos=...`
- `Deposit()` 완료 시점: `[GatherDiag] {유닛명}: 반납 완료 amount=... type=...`
- `CancelGathering()`(반납 건물 못 찾음) 시점: `[GatherDiag] {유닛명}: 반납할 메인기지를 찾지 못해 화물을 든 채 정지함`

다시 테스트해서 이 로그들을 남겨주시면, "가다가 멈추는" 정확한 지점(리프트 대기 중 멈춤 vs 도착 판정 문제 vs 그냥 반납 시작 자체가 아예 안 됨)을 특정할 수 있다.

### 버그 10 후속: 일꾼이 쌓이면 자원 사이가 아니라 반납 자체를 아예 안 함

**재평가**: 사용자가 명확히 구분해줌 — 끼임(버그 10, 1차 수정)과는 다른 증상으로, 일꾼 수가 늘어나면 반납 과정 자체가 시작되지 않는다. 이는 "메인기지 위치 문제"와 관련 있어 보인다는 사용자 추정과, 버그 7과 같은 원인(리프트 관련 대기/재동기화 로직, 혹은 방금 발견한 무한 재귀/멈춤 버그)일 가능성이 있어 별도 절로 분리하지 않고 위 진단 로그로 함께 확인하기로 함.

### 버그 11 후속: 헤비탱크/브루트메크 — 로그 재확인 결과 예상과 다른 원인

고쳐진(트리거 무시) 레이캐스트로 다시 받은 로그를 확인한 결과, **뜻밖에도 "땅속에 박힘"이 아니라 반대로 "땅에서 붕 떠 있음"으로 나타났다**:

| 유닛 | 루트 위치 Y | 실측 지면 Y | 지면과의 간격 | 몸체 렌더러 바닥면 |
|---|---|---|---|---|
| Ironhawk(정상 기준) | 4.00 | 2.50 | **+1.50** | 지면 위 0.26 |
| Heavy Assault Tank | 6.00 | 2.50 | **+3.50** | 지면 위 2.25 |
| Brute Mech | 5.75~6.00 | 2.50 | **+3.25~3.50** | (유사) |

헤비탱크/브루트메크는 정상 기준(Ironhawk)보다 지면에서 약 2유닛 더 높이 떠 있다 — "박혀있다"가 아니라 반대로 "공중에 뜬 채 착지가 안 된" 상태로 보인다. 육안으로는 몸체와 땅 사이에 눈에 띄는 틈이 생겨 "뭔가 위치가 이상하다(박힌 것 같다)"로 느껴졌을 가능성이 높다. (파이어호크의 레이저가 땅으로 향하는 현상은 이 값만으로는 설명이 안 돼서 별도 원인일 수 있음 — 파이어호크 자신도 이번에 진단 로그를 추가했으니 다음 로그에서 같이 확인 가능.)

이 수치 차이(SampleScene에 배치해둔 두 유닛만 유독 지면보다 2유닛가량 높음)는 코드 버그라기보다 **씬에 배치할 때의 Y좌표 자체가 잘못 입력됐을 가능성**이 높아 보인다(다른 유닛들은 스폰될 때 자동으로 지면에 맞춰지는 로직이 없다 — `BuildingController`와 달리 `UnitController`/`EnemyUnitController`엔 `SnapToGround()`에 해당하는 보정이 아예 없음).

**사용자 확인 후 씬 데이터 직접 수정(1차 시도)**: `Assets/Scenes/SampleScene.unity`에서 Heavy Assault Tank 2개 인스턴스(fileID 271867994, 1831433764)와 Brute Mech 2개 인스턴스(fileID 700619749, 1719390415)의 `m_LocalPosition.y` 오버라이드 값을 Ironhawk와 동일한 관례(지면 + 1.50)에 맞춰 전부 `4`로 수정. 이 시점엔 "왜 이 두 유닛만 지면보다 높이 떠 있는지"까지는 원인을 못 찾고 증상만 보고 좌표를 보정한 것.

### 진짜 근본 원인 확정 (사용자 직접 발견, 2026-08-01)

사용자가 SampleScene을 직접 조사해서 **진짜 원인**을 찾음: SampleScene에 mission 맵 지형 아래로 **testmap이 겹쳐서 남아있었고**, NavMesh가 이 두 지형에 대해 이중으로 구워져 있었다 — 즉 같은 XZ 좌표에 "밟을 수 있는 표면"이 위(mission맵)와 아래(testmap) 두 군데 존재했던 것. 헤비탱크/브루트메크 등 일부 유닛이 NavMesh 상에서 자기 위치를 잡을 때(스폰/배치 시 가장 가까운 NavMesh 지점을 찾는 과정) 위쪽이 아니라 **아래쪽 testmap의 NavMesh로 스냅**되면서, 실제로는 지면(mission맵) 아래에 위치하게 됐던 것 — 이게 "땅속에 박힌 것처럼 보인다"는 증상의 진짜 원인.

testmap을 제거하자 정상으로 보임을 확인함. 이 발견으로 위의 "1차 시도"(Y좌표를 4로 수동 보정한 것)와 [[0344-bugfix-enemy-unit-transparent-in-build]]의 셰이더 스트리핑 가설(Always Included Shaders 추가)은 둘 다 **진짜 원인이 아니었던 것으로 결론**. 진단 로그가 보여준 "지면보다 2유닛 높이 떠 있음" 수치는, 레이캐스트가 (아래 testmap이 아니라) 위쪽 mission맵 표면에 먼저 맞아서 "정상 지면"으로 측정한 반면, 실제 유닛 배치는 NavMesh를 통해 그 아래 testmap 쪽을 따라갔기 때문에 두 값이 어긋나 보였던 것으로 설명됨.

**정리**: 코드 수정 불필요 — SampleScene에서 testmap(중복 지형)을 제거하는 것으로 해결. 1차 시도로 넣은 Y좌표 보정(4개 인스턴스)은 testmap 제거 후에도 유지해도 무방(오히려 정확한 착지 지점에 더 가깝게 만들어주는 보정으로 남음).

**추가로 남긴 것**: `Assets/Scripts/Unit/UnitController.cs`에도 `EnemyUnitController`와 동일한 `LogSpawnDiagnostics()`를 추가 — 지금까지는 적 유닛만 로그가 남았는데, 스카이랜서/파이어호크 등 아군 유닛도 다음 빌드부터 같은 로그가 남는다.

### 버그 7 최종 확정: Play Mode 실제 재현으로 새로운 근본 원인 발견

정적 분석으로는 더 이상 진전이 없어서, Unity Editor Play Mode에서 실제로 "일꾼에게 화물 들리기 → 메인기지 리프트 → 재배치 → 착륙"을 전부 실행시켜 `gatherState`/`navMeshAgent` 값을 직접 관찰했다.

**좋은 소식**: 이번 세션에 고친 "리프트 중엔 대기, 착륙하면 자동 재개" 로직 자체는 정확히 의도대로 동작함을 확인 — 리프트 중엔 `gatherState=MovingToBase`, `isStopped=True`로 정확히 대기하다가, 착륙(`IsLifted()=False`)이 감지되자마자 실제로 다시 걷기 시작했다.

**새로 발견한 진짜 원인**: 착륙 직후 다시 멈춰서 재현됐는데, 원인은 전혀 다른 곳이었다. `NavMeshAgent.SetDestination()`은 목적지가 **NavMesh로 연결되지 않은 영역**(맵이 끊긴 다른 구역 등)이면 조용히 `false`를 반환하고 아무 것도 하지 않는데, 이 프로젝트 코드 어디에도 이 반환값을 확인하는 곳이 없었다. 실패하면 `navMeshAgent.destination`이 워커 자신의 위치 근처로 되돌아가 버려서, `GatherTick`의 "목적지가 바뀌었을 때만 다시 길을 잡는다" 리싱크 조건이 매 프레임 다시 참이 되고, 또 실패하는 `SetDestination`을 무한 반복 — 겉보기엔 그냥 "가다가 멈춘" 것처럼 보이지만 실제로는 매 프레임 실패를 계속 재시도만 하고 있었다. 메인기지를 착륙시킨 지점이 그 일꾼이 서있는 곳과 NavMesh로 안 이어진 곳(지형이 끊긴 곳)이면 재현되는 것으로 확인.

**수정**: `Assets/Scripts/Unit/UnitController.cs`의 `MoveAgentTo()`가 `NavMeshAgent.SetDestination()`의 성공/실패를 반환하도록(void → bool) 변경. `GatherTick()`의 `MovingToBase` 리싱크 지점에서 이 반환값을 확인해서, 실패하면 무한 재시도 대신 "반납 대상을 못 찾은 경우"와 동일하게 `CancelGathering()`(화물을 든 채 그 자리에 정지)으로 처리하고 `[GatherDiag]` 경고 로그를 남기도록 함.

이제 리프트/착륙 관련해서는: (1) 대기/재개 로직 정상 확인, (2) 도달 불가능한 목적지에 대한 무한 재시도 버그 수정 — 두 가지가 합쳐져서 "가다가 멈추거나 아예 멈춤" 증상이 해소될 것으로 기대. 다만 메인기지를 NavMesh가 끊긴 곳에 착륙시키면(2)로 인해 일꾼이 여전히 화물을 든 채 멈추긴 한다(다만 이번엔 "왜 멈췄는지" 로그가 남고, 무한 루프 없이 확실하게 멈춘 상태가 됨) — 이건 애초에 그런 곳에 기지를 착륙시키는 게 설계상 맞는지 확인이 필요한 별개 사안.

### 버그 7 후속2: 반납 목적지를 건물 피벗이 아니라 표면 접근점으로 변경

사용자 지적: 건물엔 `NavMeshObstacle`이 붙어있으므로, 반납 목적지를 건물의 "피벗(중심)"으로 잡으면 그 지점 자체가 장애물이 뚫어놓은 구멍(NavMesh 없음) 안일 수 있다 — Play Mode 재현에서 발견한 "NavMesh 미연결"의 상당 부분이 사실 이 케이스였을 가능성이 높음.

**수정**: `GetApproachPoint()`(자원 노드용)와 동일한 패턴으로 `GetDepositApproachPoint()`를 추가 — 건물 콜라이더 표면에서 가장 가까운 지점을 목적지로 삼는다. `MoveToDepositTargetOrWait()`와 `GatherTick`의 `MovingToBase` 리싱크 양쪽에 적용. 표면 지점은 항상 장애물 경계 바로 바깥이라 NavMesh 길찾기가 훨씬 안정적이며, 이걸로도 실패하면(진짜로 지형이 끊긴 경우) 앞서 추가한 "실패 감지 → 화물 든 채 정지" 처리가 그대로 안전망 역할을 한다.

### 추가 기능: 건물 우클릭 = 계속 따라다니기 + 마커 3회 깜빡임

요청: "일꾼이나 유닛이 건물을 우클릭 하면 건물을 따라가는거로 해주고 건물 마커가 3번 깜박이는 메커니즘 추가해줘".

기존엔 건물 우클릭(자원 없는 워커/전투유닛)이 `MoveTo(building.transform.position)` — 그 순간의 위치로 딱 한 번만 이동하는 방식이라, 건물이 리프트로 나중에 움직이면 유닛은 옛 자리에 그대로 남았다. 아군 유닛 우클릭(`FollowUnit`/`FollowTick`)엔 이미 "계속 따라다니기" 패턴이 있어서 동일한 구조를 건물에도 적용했다.

- **`Assets/Scripts/Unit/UnitController.cs`**: `FollowBuilding(BuildingController)`/`FollowBuildingTick()` 추가(`FollowUnit`/`FollowTick`과 동일한 패턴 - 대상이 파괴되면 정지, 교전 중이면 유지, 가까워지면 정지, 그 외엔 매 프레임 최신 위치로 이동). `MoveToBuilding()`의 "자원 없을 때" 분기가 이제 이걸 호출한다. `CancelAttackOrder()`(모든 새 명령이 공통으로 거치는 취소 지점)에 이 팔로우 상태도 함께 리셋하도록 추가.
  - 목적지는 건물 피벗이 아니라 `GetClosestSurfacePoint()`(표면 접근점) — 이번 세션에 반납 이동에 적용한 것과 같은 이유(NavMeshObstacle 구멍 회피). 기존에 3곳에 중복돼 있던 "콜라이더 있으면 ClosestPoint, 없으면 피벗" 패턴(`SqrDistanceToTarget`/`GetApproachPoint`/`GetDepositApproachPoint`)을 이 공용 헬퍼 하나로 통합.
- **`Assets/Scripts/UserControl/UserControl.cs`**: 건물 우클릭 지점(657~669줄 근방)에서 `building.FlashMarker()`를 한 번 호출 — 아군 유닛 우클릭 시 `unit.FlashMarker()`를 호출하는 기존 관례와 동일한 위치(개별 유닛이 아니라 커맨드 발행 지점에서 한 번만). `BuildingController.FlashMarker()`는 이미 `markerFlashCount=3`(기본값)이라 별도 수치 변경 없이 요청한 "3번 깜빡임"을 그대로 만족.

자원을 든 워커가 메인기지를 우클릭하는 경우(반납)는 이 "따라다니기"를 거치지 않고 기존 `ReturnCargoTo`/`ReturnCargo` 경로를 그대로 쓴다 — 그 경우도 `UserControl.cs`의 동일한 `FlashMarker()` 호출로 마커는 똑같이 깜빡인다.

## 요약 / 영향받는 파일 (3차 라운드)

| 항목 | 파일 | 상태 |
|---|---|---|
| 메인기지 우클릭이 지정한 건물을 무시하던 버그 | `Assets/Scripts/Unit/UnitController.cs` | 수정 완료 (`ReturnCargoTo` 추가) |
| 반납 건물 못 찾을 때 무한 재귀(크래시) | `Assets/Scripts/Unit/UnitController.cs` | 수정 완료 (`HaltInPlace` 분리) |
| 반납 과정 진단 로그 3곳 | `Assets/Scripts/Unit/UnitController.cs` | 추가 (임시, 재테스트용) |
| 아군 유닛 스폰 진단 로그 | `Assets/Scripts/Unit/UnitController.cs` | 추가 (임시, 재테스트용) |
| 헤비탱크/브루트메크 위치 | `Assets/Scenes/SampleScene.unity` | 수정 완료 (Y좌표 4개 인스턴스 보정) |
| NavMesh 미연결 목적지 무한 재시도 (Play Mode 실제 재현으로 확정) | `Assets/Scripts/Unit/UnitController.cs` | 수정 완료 |

전부 컴파일 확인 완료(에러 0, 기존 경고만).
