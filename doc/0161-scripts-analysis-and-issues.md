# 0161 - Scripts 폴더 전체 분석 (문제점/비효율 리포트)

**날짜:** 2026-07-17

## 요청 내용
"전체 프로젝트 파일들을 분석하고 Scripts 안에 있는 스크립트들에서 문제점이나 비효율적인 부분을 분석해서 doc폴더 안에 문서화 해줘" — `Assets/Scripts` 하위 35개 C# 스크립트 전체를 대상으로 버그/성능/유니티 안티패턴/구조적 중복을 분석해 문서로 남겨달라는 요청. **순수 조사·문서화 요청이며 코드 변경은 요청되지 않음** (confirm-before-implementing 규칙과 무관 — 프로젝트 코드는 건드리지 않음).

## 조사 방법
`Assets/Scripts` 하위 35개 `.cs` 파일을 전부 읽고, 폴더별(BuildSystem/Resource/UI/Enemy/Camera/System/Building/Unit/Effects/Animation/UserControl/CaptureSystem/UnitSpawner)로 correctness 버그, 성능(Update/FixedUpdate 안의 비싼 호출·GC 할당), 유니티 안티패턴, 구조적 중복(특히 UnitController vs EnemyController vs BuildingController vs BaseStructure 간 마커 깜빡임 로직, UnitController vs BuildingController 간 공중 이동 로직)을 교차 대조했다.

---

## 종합 요약 (영향도 높은 순)

1. **[치명적 — 빌드 실패 가능성] Editor/Test 전용 네임스페이스가 런타임 스크립트에 참조됨.**
   `RTSUnitController.cs`에 `using UnityEditor;`, `UnitController.cs`에 `using static UnityEditor.PlayerSettings;`, `PlacementSystem.cs`에 `using NUnit.Framework;`가 들어있다. 셋 다 실제로는 파일 본문 어디서도 사용되지 않는 죽은 import인데, `UnityEditor`/`NUnit` 어셈블리는 기본적으로 플레이어(빌드) 어셈블리에는 포함되지 않으므로, 에디터가 아닌 실제 빌드(Build Settings → Build) 시 컴파일 에러로 이어질 가능성이 매우 높다. 현재 이 프로젝트를 실행 파일로 빌드하면 실패할 수 있다.
2. **[성능] `RTSUnitController.Update() → UpdateUI()`가 매 프레임(60fps) 전체 커맨드 패널을 무조건 재구성.** 선택 상태가 바뀌지 않아도 매 프레임 `UnitButtonAction`/`BuildingButtonAction`이 `List.Find()`(선형 탐색)로 데이터베이스를 조회하고 버튼 슬롯 전체를 다시 채운다. 상태 변경 시에만 갱신하도록 dirty-flag가 필요하다.
3. **[성능/GC] `TerritoryZone.GetPolygonXZ()`가 호출될 때마다 `new Vector2[]`를 할당.** 이 메서드는 `TerritoryManager.IsInsideAlliedTerritory()`를 통해 워커 채취 틱(`UnitController.GatherTick`, 매 프레임·워커마다), `BaseStructure.Update()`(매 프레임·건설 중인 건물마다), `UnitSpawner.Produce()`(매 프레임·생산 건물마다), 배치 검사 등 여러 핫패스에서 프레임마다 반복 호출된다. 유닛/건물 수가 늘어날수록 프레임마다 GC 압박이 커진다.
4. **[성능/GC] `UIController.UpdateResourceUI()`가 매 프레임 Ore/Gas/Population 텍스트를 무조건 재할당.** `ToString()`/문자열 보간이 값이 바뀌지 않아도 매 프레임 실행되어 GC 가비지를 만든다. 정작 `ResourceManager.OnResourceChanged` 이벤트가 이미 이 용도로 존재하는데 사용되지 않고 있다.
5. **[성능] `UserControl.Update() → UpdateCursor()/GetHoveredTarget()`가 매 프레임 최대 3회 `Physics.Raycast`를 쏜다.** 단순히 커서 색을 정하기 위해서다. 또한 `HandleLeftClick()`/`HandleRightClick()`은 클릭 한 번마다 레이어별로 6번(Unit/Ground/Enemy/Building/Ore/Gas) 개별 레이캐스트를 쏘는데, 통합 레이어마스크 1회 레이캐스트 + 컴포넌트 타입 분기로 줄일 수 있다.
6. **[버그성 비효율] `UnitController.Die()`가 이미 캐싱된 `rtsController` 필드를 두고 `FindFirstObjectByType<RTSUnitController>()`를 다시 호출.** 다수 유닛이 동시에 죽는 상황(광역 데미지 등)에서 불필요한 씬 전체 탐색이 반복된다.
7. **[구조/캡슐화] `RTSUnitController`가 `selectedUnitList`/`selectedEnemyList`/`selectedBuildingList` 등을 public 필드로 노출**, 다른 클래스(`EnemyController.Die()` 등)가 직접 리스트를 `Remove()`하는 식으로 침범한다. 1220줄짜리 god-class에 선택 상태·자원·UI 오케스트레이션·생산 로직이 전부 뒤섞여 있다.
8. **[중복] `FlashMarker()`/`FlashMarkerRoutine()`이 `EnemyController`/`BuildingController`/`ResourceNode`/`UnitController`/`BaseStructure` 5곳에 거의 동일하게 복사돼 있다** (총 75줄 이상). 공용 컴포넌트/헬퍼로 뽑을 수 있다.
9. **[중복] 공중 이동 보간 로직(수평/수직 독립 수렴 + `SampleGroundHeight`)이 `UnitController`(공중유닛)와 `BuildingController`(리프트 건물)에 거의 동일하게 두 번 구현돼 있다.** 주석에서도 "동일한 패턴"이라고 스스로 명시하면서도 공유되지 않았다.
10. **[죽은 코드] `UnitController.stuckTimer` 필드가 선언·직렬화만 되고 어디서도 읽거나 갱신되지 않는다** ("갇힘 감지" 기능이 구현되다 만 흔적으로 보임). `Resource/ResourceController.cs`는 완전히 빈 스텁 스크립트(Start/Update 본문 없음).
11. **[캡슐화 누락] `UnitController.alreadyAttacked`, `timeBetweenAttacks`가 `public`으로 선언**되어 다른 클래스가 내부 공격 쿨다운 상태를 직접 건드릴 수 있다.
12. **[디버그 잔재] `UnitSpawner.Enqueue()`의 `FindIndex` 람다 안에 `Debug.Log`가 박혀 있어 스캔하는 원소 수만큼 로그가 찍히고, 바로 아래 줄에 동일 로직의 주석 처리된 죽은 코드가 남아있다.**

---

## 상세 분석 (폴더별)

### BuildSystem

#### `InputManager.cs`
- 특이사항 없음. 단순하고 책임이 명확함.

#### `GridData.cs`
- (경미) `CalculatePositionsPublic()`이 `private CalculatePositions()`를 그대로 감싸기만 하는 래퍼 메서드다. `CalculatePositions` 자체를 `public`으로 바꾸면 래퍼가 불필요해진다.

#### `PreviewSystem.cs`
- 특별한 버그는 없음. `ApplyGhostMaterial`이 렌더러마다 `Material[]`를 복사해 전부 같은 프리뷰 머티리얼 인스턴스로 교체하는 방식이라, 렌더러 수가 많은 프리팹일수록 배치 모드 진입 시 약간의 오버헤드가 있으나 1회성이라 문제 되지 않음.

#### `PlacementSystem.cs`
- **[치명적] 1번 줄 `using NUnit.Framework;`** — 테스트 프레임워크 네임스페이스가 실제로 쓰이지 않는데 import돼 있음. 플레이어 빌드에서 컴파일 에러 위험. → 삭제 권장.
- Update()는 배치 모드일 때만(`selectedObjectIndex >= 0`) 동작하도록 이미 가드돼 있어 평상시 오버헤드는 없음.
- `IsBlocked()`가 `Physics.OverlapBox` 결과를 `foreach`로 순회하며 매번 `Debug.Log`를 찍는다(324행). 배치 모드에서 그리드 셀이 바뀔 때만 호출되므로 빈도는 낮지만, 상용 빌드에는 불필요한 로그이므로 정리 대상.

### ScriptableObject

#### `UnitDataSO.cs` / `BuildingDataSO.cs`
- 특이사항 없음. 순수 데이터 컨테이너.

### Resource

#### `ResourceController.cs`
- **[죽은 코드]** Start()/Update() 본문이 완전히 비어있는 스텁 스크립트. 실제로 씬의 오브젝트에 붙어 있다면 아무 일도 하지 않으면서 빈 `Update()`를 매 프레임 호출하는 오버헤드만 발생시킨다. 사용하지 않으면 삭제 권장.

#### `ResourceNode.cs`
- 대체로 양호. `ShrinkByRemainingRatio()`의 `while` 루프는 최대 4회로 bounded되어 있어 문제없음.
- (경미) `FlashMarker`/`FlashMarkerRoutine`이 다른 4개 클래스와 거의 동일 — 종합 요약 8번 참고.

#### `ResourceManager.cs`
- 특이사항 없음. 자원/인구 캡 로직이 명확하고 이벤트(`OnResourceChanged`) 설계도 적절함 — 다만 이 이벤트를 실제로 구독하는 곳이 없다(UI가 폴링 방식이라 이벤트가 사장돼 있음, 종합 요약 4번).

### UI

#### `Tooltip/TooltipUI.cs`
- 특이사항 없음. 싱글턴 패턴이 단순하고 Raycast 차단 이슈도 이미 인지하고 처리돼 있음(44~47행).

#### `ProductionSlot.cs`
- 특이사항 없음. 단축키 시뮬레이션 코루틴 처리가 깔끔함.

#### `UIController.cs`
- **[성능/GC] `UpdateResourceUI()`(233~242행)가 `Update()`에서 매 프레임 무조건 호출**되어 `OreText.text`, `GasText.text`, `PopulationText.text`를 매번 재할당한다. `ResourceManager.OnResourceChanged` 이벤트를 구독해 값이 실제로 바뀔 때만 갱신하도록 바꾸면 프레임당 3회의 문자열 할당(`ToString()`, 보간 문자열)을 없앨 수 있다. → 종합 요약 4번.
- `UpdateProductionProgress()`도 매 프레임 호출되지만 슬라이더 값 대입 자체는 저비용이라 상대적으로 덜 급함.
- `BuildingLiftSlotIndex`/`BuildingMoveSlotIndex`를 "보호 슬롯"으로 다루는 설계(133~138행 주석)는 실제 버그(단축키 코루틴이 매 프레임 SetActive로 끊기는 문제)를 겪고 나온 해결책으로 보이며 현재는 잘 처리돼 있음 — 문제라기보다는 향후 슬롯 인덱스가 늘어나면 매직넘버 유지보수 부담이 커질 수 있다는 정도.

#### `HealthBarBillboard.cs`
- 특이사항 없음.

### Enemy

#### `EnemyController.cs`
- 특이사항 없음(단독으로는). `FlashMarker` 중복 패턴 — 종합 요약 8번.

### Camera

#### `MinimapController.cs`
- (경미) `groundPoint.z -= 30f;`(33행)처럼 하드코딩된 매직 넘버 오프셋이 있음. 카메라 피치/거리가 바뀌면 이 상수도 같이 손봐야 해서 유지보수 부담이 될 수 있음 — `[SerializeField]`로 노출하면 좋음.

#### `MinimapViewIndicator.cs`
- 특이사항 없음. `Update()`가 매 프레임 4개 레이캐스트를 쏘지만(카메라 4개 꼭짓점) UI 인디케이터 갱신 목적상 불가피하고 비용도 낮음.

#### `CameraControl.cs`
- 특이사항 없음. 이동/줌/회전/지형 단 보정 로직이 명확하게 분리돼 있음.

### System

#### `RTSUnitController.cs`
- **[치명적] 5번 줄 `using UnityEditor;`** — 본문에서 전혀 사용되지 않는데 import돼 있음. `PlacementSystem`의 NUnit import와 동일한 이유로 플레이어 빌드 컴파일 실패 위험. → 삭제 권장. (`using UnityEngine.UIElements;`는 런타임에서도 사용 가능한 네임스페이스라 상대적으로 안전하지만, 이 파일에서도 실제로 쓰이지 않는 것으로 보여 정리 대상.)
- **[성능] `Update()`(119~127행)가 `UnitList.RemoveAll`/`BuildingList.RemoveAll`/`ResourceNodeList.RemoveAll`을 매 프레임 실행** — 죽은 오브젝트 정리를 이벤트 기반(사망 시 1회 제거)이 아니라 매 프레임 전체 리스트 스캔으로 처리한다. 현재 유닛 스케일에서는 크게 문제 없겠지만 유닛 수가 늘어날수록 선형 비용이 누적된다.
- **[성능] `UpdateUI()`(915~1150행, 230줄짜리 switch 블록)가 매 프레임 무조건 실행되며, 선택 상태별로 `UnitButtonAction`/`BuildingButtonAction`을 호출할 때마다 `unitDatabase.unitData.Find(...)`/`buildingDatabase.buildingData.Find(...)`(선형 탐색)가 버튼 개수만큼 반복된다.** 선택 상태(`RTScurrentSate`, `BuildingSelectState` 등)가 바뀔 때만 패널을 다시 그리도록 dirty-check를 추가하면 이 모든 조회/재구성 비용을 없앨 수 있다. → 종합 요약 2번.
- **[캡슐화]** `selectedUnitList`, `selectedBuildingList`, `selectedEnemyList`, `UnitList`, `BuildingList`, `ResourceNodeList`, `selectedResourceNode`, `selectedBaseStructure`가 전부 `public` 필드다. `EnemyController.Die()`(rtsController.selectedEnemyList.Remove(this))처럼 다른 클래스가 리스트를 직접 조작하고 있어, 캡슐화가 깨져 있고 나중에 선택 로직에 검증을 추가하기 어렵다. → 종합 요약 7번.
- (경미) `RTScurrentSate`(오타로 보이는 필드명, State가 아니라 Sate) — 기능에는 지장 없지만 가독성/검색성을 해치는 오탈자.
- (경미) `#region Test용`의 `TestMethod()`가 완전히 빈 메서드로 남아있음 — 죽은 코드.
- 클래스 전체가 선택 상태 관리 + 자원/생산 검증 + UI 오케스트레이션 + 컨트롤 그룹을 모두 담당하는 1220줄짜리 god-class. 기능별로(SelectionManager/ProductionEconomy/UIPresenter 등) 분리하면 유지보수성이 개선될 여지가 큼 — 다만 현재 규모에서 당장 문제를 일으키는 것은 아님.

### Building

#### `BuildingController.cs`
- 특이사항 없음(단독으로는). 리프트 이동 로직이 복잡하지만 주석으로 각 보정 이유가 잘 설명돼 있음.
- `SampleGroundHeight()`(173~183행)가 `UnitController.SampleGroundHeight`와 완전히 동일한 구현 — 종합 요약 9번(중복).
- `LiftOff()`/`Land()`에서 `GetComponent<BuildingEffects>()`를 매번 새로 조회(223, 311행) — 호출 빈도가 낮아(이착륙 시점 1회) 문제는 아니지만, 필드로 캐싱하면 일관성이 좋아짐.

#### `BaseStructure.cs`
- **[성능, 경미]** `Update()`(88~112행)가 매 프레임 `TerritoryManager.IsInsideAlliedTerritory(transform.position)`를 호출 — 건설 중인 건물마다, 매 프레임, 등록된 모든 영토 zone을 순회하며 그때마다 `GetPolygonXZ()`가 배열을 새로 할당(종합 요약 3번과 동일한 원인의 또 다른 호출부).
- 그 외 로직(건설 진행률, 담당 일꾼 교체, 완공 시 체력 이관)은 견고하게 작성됨.

### Unit

#### `HealthManager.cs`
- 특이사항 없음. 데미지/힐/사망 처리와 이벤트 설계가 깔끔함.

#### `AttackRange.cs`
- 특이사항 없음. `enemiesInRange.RemoveAll(enemy => enemy == null)`을 매 프레임 실행하지만 유닛별 트리거 범위 내 소규모 리스트라 비용이 낮음.

#### `UnitController.cs` (1294줄, 가장 큰 스크립트)
- **[치명적] 8번 줄 `using static UnityEditor.PlayerSettings;`** — 본문 어디서도 `PlayerSettings`의 멤버가 쓰이지 않음에도 import돼 있음. 플레이어 빌드 컴파일 실패 위험. → 삭제 권장.
- **[정리 필요] 그 외에도 쓰이지 않는 import가 다수:** `System.Net`, `System.Resources`, `UnityEngine.Audio`, `using static UnityEngine.GraphicsBuffer;` — 전부 본문에서 참조되지 않는 죽은 using문. IDE 자동완성이 남긴 흔적으로 보임.
- **[버그성 비효율] `Die()`(1258~1270행)가 이미 `Start()`에서 캐싱해둔 `rtsController` 필드가 있는데도 `FindFirstObjectByType<RTSUnitController>()`를 다시 호출**한다(1264행). 다수 유닛이 동시에 사망하는 상황(광역 공격 등)마다 불필요한 씬 전체 탐색이 반복된다. → 종합 요약 6번.
- **[죽은 필드]** `[SerializeField] private float stuckTimer;`(45행)가 선언만 되고 클래스 어디에서도 읽거나 쓰이지 않음 — "이동 경로가 막혔을 때"를 감지하려던 미완성 기능의 흔적으로 보임.
- **[캡슐화 누락]** `public bool alreadyAttacked = false;`, `public float timeBetweenAttacks;`(76~77행)가 `public`으로 선언돼 외부에서 직접 값을 바꿀 수 있음. `attackDamage`/`armor` 등 다른 전투 스탯은 이미 `[SerializeField] private`로 올바르게 캡슐화돼 있는 것과 대조적임.
- **[중복]** 공중 유닛 이동 보간 로직(수평 MoveTowards + 매 프레임 재계산되는 수직 고도 + `SampleGroundHeight`)이 `BuildingController.UpdateLiftedMovement()`와 사실상 동일하게 중복 구현돼 있음 — 종합 요약 9번.
- **[중복 패턴]** `MoveTo`/`AttackUnitTarget`/`AttackMoveTo`/`AttackFriendlyTarget`/`FollowUnit`/`GoBuild`/`StopUnit`/`PatrolUnit`/`HoldUnit`/`Gather`/`ReturnCargo`/`MoveToBuilding` 12개 메서드가 전부 `if (isConstructing) return;` 가드와 `CancelGatheringForNewCommand(); CancelAttackOrder();` 초기화를 앞부분에 반복하고 있음. 기능상 문제는 없으나 새 명령 타입이 추가될 때마다 이 보일러플레이트를 또 복사해야 함 — 공용 `BeginNewOrder()` 헬퍼로 뽑으면 실수(가드 빠뜨림 등) 여지를 줄일 수 있음.
- `Attack()`(781~808행)이 `Invoke(nameof(ResetAttack), timeBetweenAttacks)`로 공격 쿨다운을 처리 — 문자열 기반 리플렉션 디스패치라 코루틴/필드 타이머 방식보다 미세하게 느리지만, 호출 빈도(공격 시마다 1회)를 고려하면 실질적 영향은 미미함.
- **[중복]** `FlashMarker`/`FlashMarkerRoutine` — 종합 요약 8번.
- 그 외 채취(Gather) 상태머신, 공격 명령 추적(AttackOrderTick), 아군 강제공격(FriendlyAttackTick) 로직은 매우 꼼꼼하게 엣지케이스(시야 이탈, 대기열 이탈, 영토 상실 등)를 처리하고 있어 전반적인 완성도는 높음.

### Effects

#### `HitEffectSet.cs` / `EffectPlayer.cs` / `TrailRotationFollower.cs`
- 특이사항 없음. `EffectPlayer`가 자동 파괴 타이머, 루프 강제 설정, 부착/비부착 옵션 등을 잘 정리된 정적 헬퍼로 제공하고 있어 오히려 이 프로젝트에서 가장 잘 설계된 유틸리티 중 하나로 보인다.

#### `BuildingEffects.cs` / `ConstructionEffects.cs` / `UnitEffects.cs`
- 특이사항 없음. `OnEnable`/`OnDisable`에서 `HealthManager` 이벤트를 정확히 구독/해제하고 있어 이벤트 리스너 누수가 없다(이 프로젝트에서 유일하게 구독-해제 짝을 제대로 지킨 3곳).

### Animation

#### `HoverBob.cs` / `VehicleShake.cs` / `AutoRotate.cs`
- 특이사항 없음. DOTween 트윈을 `OnDestroy`에서 `Kill()`하는 패턴이 세 파일 모두 일관되게 지켜지고 있어 트윈 누수가 없다.

### UserControl

#### `UserControl.cs`
- **[성능] `UpdateCursor() → GetHoveredTarget()`(772~787행)가 매 프레임 최대 3회의 `Physics.Raycast`를 실행**한다(Enemy, Unit|Building, Ore|Gas 레이어 각각). 커서 아이콘 갱신이라는 목적에 비해 비용이 크며, 마우스가 정지해 있어도 매 프레임 재실행된다. 마지막 프레임과 마우스 위치가 같으면 스킵하거나, 레이어를 하나로 합쳐 1회 레이캐스트 후 결과 오브젝트의 컴포넌트로 분기하는 방식으로 줄일 수 있다.
- **[성능] `HandleLeftClick()`/`HandleRightClick()`가 클릭 1회마다 레이어별로 6번의 개별 `Physics.Raycast`를 순차 실행**(Unit/Ground/Enemy/Building/Ore/Gas, 184~189행 및 419~424행). 클릭 이벤트 자체는 빈도가 낮아 치명적이진 않지만, 통합 레이어마스크로 1회 레이캐스트한 뒤 `hit.collider.GetComponent<T>()`로 타입을 판별하는 구조가 더 저렴하고 코드도 짧아진다.
- `UpdatePointer()` 역시 명령 대기 상태일 때 매 프레임 지면 레이캐스트를 1회 추가로 실행 — 위 두 항목과 합치면 상태에 따라 프레임당 최대 4회까지 레이캐스트가 누적될 수 있다.
- 기능적 정확성 자체(드래그 선택, Shift 병합, 컨트롤 그룹, 우클릭 문맥 분기)는 매우 꼼꼼하게 짜여 있고 주석으로 각 분기의 이유가 잘 설명돼 있다.

### CaptureSystem

#### `TerritoryManager.cs`
- **[성능/GC]** `IsInsideTerritory()`가 등록된 모든 `zones`를 매 호출마다 선형 순회하며, 매치되는 zone마다 `zone.Contains()`를 호출해 `TerritoryZone.GetPolygonXZ()`의 배열 재할당을 유발한다(아래 참고). 호출 빈도가 매우 높은 정적 유틸리티라는 점에서 이 프로젝트 전체 GC 압박의 핵심 원인 중 하나.

#### `CaptureSystem.cs`
- 특이사항 없음. 점령치 로직(±captureDuration, contested 처리)이 명확하고 에디터 전용 디버그 오너 동기화(`#if UNITY_EDITOR`)도 올바르게 가드돼 있음.

#### `TerritoryZone.cs`
- **[성능/GC] `GetPolygonXZ()`(78~87행)가 호출될 때마다 `new Vector2[pinPoints.Count]`를 새로 할당**한다. `Contains()`가 이 메서드를 매번 다시 호출하고, `Contains()` 자체가 위에서 설명한 여러 핫패스(워커 채취 틱, BaseStructure/UnitSpawner의 매 프레임 Update)에서 반복 호출되므로, 결과적으로 이 배열 할당이 게임 전체에서 프레임당 여러 번 반복되는 GC 할당원이 된다. 핀 위치가 에디터에서만 바뀌는 사실상 정적인 데이터이므로, 핀이 변경될 때만 캐시를 갱신하고 `Contains()`는 캐시된 배열을 재사용하도록 바꾸면 이 할당을 완전히 없앨 수 있다. → 종합 요약 3번.
- **[성능, 경미] `Update()`(51~55행)가 매 프레임 `ApplyOutlineStyle()`(LineRenderer 색상/두께 대입)과 `RefreshOutline()`(모든 핀 위치 재대입)을 무조건 실행**한다. `owner`나 핀 위치가 실제로 바뀔 때만 갱신해도 충분해 보이며, 특히 씬에 영토 zone이 여러 개면 누적 비용이 커진다.

### UnitSpawner

#### `UnitSpawner.cs`
- **[디버그 잔재] `Enqueue()`(47~82행) 안에서 `database.unitData.FindIndex(d => { Debug.Log($"Compare: {d.ID} == {unitID}"); return d.ID == unitID; })`처럼 탐색 조건 람다 내부에 `Debug.Log`가 박혀 있어, 유닛을 큐에 넣을 때마다 데이터베이스 원소 수만큼 로그가 출력된다.** 바로 아래 줄(67행)에 동일 로직을 주석 처리한 죽은 코드(`//int index = ...`)도 남아있다. 둘 다 상용 코드에서는 제거 대상.
- `PrintQueue()`가 `Enqueue`/`Cancel`/`Produce` 완료 시점마다 문자열을 `+=` 로 이어붙여 로그를 만든다(159~178행) — 프레임마다 호출되는 것은 아니라 임팩트는 낮지만, 큐 길이가 늘어나면 `StringBuilder`가 더 적절하다.
- `Produce()`가 매 프레임 `TerritoryManager.IsInsideAlliedTerritory()`를 호출 — 위 GC 할당 이슈의 또 다른 호출부.

---

## 요약/제안
이번 세션은 **분석 전용**이며 `Assets/` 하위 어떤 파일도 수정하지 않았다. 발견된 이슈는 심각도 기준으로 크게 세 그룹이다.

- **당장 위험한 것**: `UnityEditor`/`NUnit` 네임스페이스가 런타임 스크립트 3곳(`RTSUnitController.cs`, `UnitController.cs`, `PlacementSystem.cs`)에 죽은 import로 남아있는 문제 — 실제 빌드를 시도해본 적이 없다면 지금 빌드가 실패할 수 있다. 확인 및 제거를 가장 먼저 권장.
- **누적되면 체감되는 성능/GC 이슈**: `TerritoryZone.GetPolygonXZ()` 배열 재할당, `RTSUnitController.UpdateUI()`/`UIController.UpdateResourceUI()`의 매 프레임 무조건 갱신, `UserControl`의 매 프레임 다중 레이캐스트. 지금 유닛 수 규모에서는 눈에 띄지 않을 수 있지만 유닛/건물/영토 zone이 늘어나면 프레임 드랍의 원인이 될 가능성이 큼.
- **구조/유지보수성**: `FlashMarker` 5중 복사, `UnitController`/`BuildingController` 간 비행 로직 중복, `RTSUnitController`의 public 선택 리스트 노출과 god-class화. 지금 당장 버그를 일으키진 않지만 앞으로 기능을 추가할 때 실수 여지를 늘린다.

어느 항목부터 실제로 수정할지 알려주면, 각 항목에 대해 `confirm-before-implementing` 규칙에 따라 변경 제안(기존 코드 → 변경 코드)을 담은 새 `doc/NNNN-*.md`를 먼저 작성한 뒤 승인을 받고 진행하겠습니다.

## 변경된 파일
- `doc/0161-scripts-analysis-and-issues.md` (신규 작성 — 이 문서)

프로젝트 코드(`Assets/...`)는 변경하지 않았습니다.
