# 전체 코드 분석 보고서

작성일: 2026-07-03
상태: 분석 보고서 (소스 미수정, 현재 코드베이스 스냅샷 기준)
대상: `Assets/Scripts` 전체 (C# 스크립트 18개)

## 1. 개요

NEOTECHWARZ2는 Unity 기반의 스타크래프트 스타일 RTS(실시간 전략) 게임 프로젝트다.
`Assets/Scripts` 아래 10개 폴더에 18개 스크립트로 구성되어 있으며, 유닛 선택/이동/전투,
자원 채취, 건물 배치(그리드 기반), 유닛 생산 대기열, 커맨드 패널 UI가 핵심 기능이다.

| 폴더 | 파일 | 역할 |
|---|---|---|
| `System` | `RTSUnitController.cs` | 전체 상태(선택/생산/자원)를 총괄하는 중앙 허브 |
| `UserControl` | `UserControl.cs` | 마우스/키보드 입력 해석, 커맨드 발행 |
| `Unit` | `UnitController.cs`, `HealthManager.cs`, `AttackRange.cs` | 유닛 이동/전투/채취 상태머신, 체력, 사거리 감지 |
| `Building` | `BuildingController.cs` | 건물 선택/랠리포인트/생산 위임 |
| `UnitSpawner` | `UnitSpawner.cs` | 건물별 생산 대기열 처리 |
| `Resource` | `ResourceManager.cs`, `ResourceNode.cs` | Ore/Gas/인구수 관리, 채취 노드(대기열) |
| `BuildSystem` | `InputManager.cs`, `GridData.cs`, `PreviewSystem.cs`, `PlacementSystem.cs` | 그리드 기반 건물 배치 |
| `ScriptableObject` | `BuildingDataSO.cs`, `UnitDataSO.cs` | 건물/유닛 스펙 데이터베이스 |
| `UI` | `UIController.cs`, `ProductionSlot.cs` | 커맨드 패널, 생산 대기열, 자원 텍스트 |
| `Camera` | `CameraControl.cs` | 탑다운 카메라 이동/줌 |

`Enemy`, `FogOfWar` 폴더는 존재하지만 내부에 스크립트가 없다 — 아직 구현이 시작되지 않은
영역이다.

## 2. 아키텍처 개요

```
UserControl (입력 해석)
   │  ClickSelectUnit / MoveSelectedUnits / GatherSelectedUnits ...
   ▼
RTSUnitController (중앙 허브 — 선택 상태, 생산, 자원 중개)
   │                              │                    │
   ▼                              ▼                    ▼
UnitController              BuildingController    UIController
(이동/전투/채취 상태머신)      (선택/랠리/생산위임)     (커맨드 패널·자원 표시)
   │                              │
   ▼                              ▼
HealthManager / AttackRange   UnitSpawner (생산 큐) → Instantiate
   │
   ▼
ResourceNode (채취 대기열) ←→ ResourceManager (Ore/Gas/인구수)
```

`PlacementSystem`(+`InputManager`, `GridData`, `PreviewSystem`)은 건물 배치 전용 서브시스템으로,
`RTSUnitController.BuildModeOn()` → `UIController.ShowBuildPanel()`을 거쳐
`PlacementSystem.StartPlacement(ID)`가 호출되는 흐름으로만 메인 흐름과 연결된다.

각 유닛/건물은 `Start()`에서 `FindFirstObjectByType<RTSUnitController>()`로 자신을
전역 목록(`UnitList`/`BuildingList`/`ResourceNodeList`)에 등록하는 방식(self-registration)을
쓰고 있어, 씬에 `RTSUnitController`가 정확히 하나 있다는 전제가 코드 전반에 깔려 있다.

## 3. 모듈별 상세 분석

### 3.1 RTSUnitController (`Assets/Scripts/System/RTSUnitController.cs`)

- 선택 상태(`SelectState`/`UnitState`/`BuildingState`)를 태그 문자열(`"Worker"`, `"MainBase"`,
  `"Tier1"` 등)로 분기하는 구조. 유닛/건물 프리팹의 태그 설정과 강하게 결합되어 있어,
  태그를 잘못 붙이면 즉시 UI 오분기로 이어진다.
- `TryProduceUnit(unitID)` / `TryConstructBuilding(buildingID)`가 자원 검증용으로
  정의돼 있으나(`RTSUnitController.cs:448`, `:462`), **실제 호출부가 코드베이스 어디에도 없다**
  ([4.1](#41-자원-소모-검증-로직이-완전히-미연결) 참고).
- `SpawnUnit(unitID)`(`RTSUnitController.cs:404`)는 선택된 건물 전체에 반복 호출된다.
  건물 3개를 선택하고 생산 버튼을 누르면 3개 건물 모두에 큐잉되는 구조 —
  `doc/resource-manager-design.md`에서 이미 "열린 질문"으로 지적된 부분과 동일하다.

### 3.2 UserControl (`Assets/Scripts/UserControl/UserControl.cs`)

- 좌/우클릭마다 유닛/땅/적/건물/광물/가스 6종 레이어에 대해 각각 `Physics.Raycast`를 수행
  (`HandleLeftClick`, `HandleRightClick`). 클릭 1회당 최대 6번의 레이캐스트가 발생하며,
  우선순위(유닛 > 땅 > 적 > 건물 > 광물 > 가스)를 `if` 순서로만 표현하고 있어 새 레이어 추가 시
  실수하기 쉬운 구조다.
- "적 클릭"(`clickedEnemy`) 분기가 좌/우클릭 모두 빈 블록으로 남아 있음 — 적 유닛 직접 클릭 시
  아무 반응도 없다(공격 대상 지정 기능 미구현).
- `V`(건설모드 전환), `Escape`(건설모드 외 다른 상태에서의 취소) 등 일부 키 핸들러가 주석만
  있고 빈 블록으로 남아 있다.

### 3.3 UnitController (`Assets/Scripts/Unit/UnitController.cs`)

- 이 프로젝트에서 가장 복잡한 파일. 이동(지상 NavMeshAgent / 공중 직접 보간), 전투, 순찰,
  일꾼 채취(6단계 상태머신: `None→MovingToResource→WaitingInQueue→Gathering→MovingToBase→Depositing`)를
  한 컴포넌트가 모두 담당한다.
- 채취 로직은 완성도가 높다: 대기열이 혼잡하면 반경 10 이내 대체 자원을 탐색(`TryRedirectToNearbyResource`),
  채취 중 노드가 파괴되면 재탐색, 채취 중엔 NavMeshAgent 반경을 줄여 서로 부딪히지 않게 하는 등
  RTS 특유의 엣지 케이스가 잘 처리되어 있다.
- `Die()`(`UnitController.cs:699`)가 대기열 정리·`UnitList` 제거는 하지만
  `ResourceManager.ReleasePopulation()`을 호출하지 않는다 — 인구수가 유닛 사망 후에도
  회수되지 않고 계속 점유된 채로 남는다(설계 문서에서 예정된 연결이나 미구현).

### 3.4 HealthManager / AttackRange (`Assets/Scripts/Unit/`)

- `HealthManager`는 `IDestructible` 인터페이스로 사망 처리를 위임하는 깔끔한 구조.
  유닛/건물 어디에 붙여도 동작한다.
- `AttackRange.Update()`(`AttackRange.cs:40`)가 매 프레임 `enemiesInRange` 전체를 순회해
  최근접 적을 찾는다. 사거리 안의 적 수가 적은 소규모 전투에서는 문제없지만, 대규모 교전
  시 유닛 수만큼 매 프레임 선형 탐색이 발생한다.

### 3.5 BuildingController / UnitSpawner (`Assets/Scripts/Building/`, `Assets/Scripts/UnitSpawner/`)

- 생산 큐는 FIFO 단일 슬롯 진행 방식(맨 앞 항목만 타이머 감소)으로 단순하고 견고하다.
- `UnitSpawner.Enqueue()`(`UnitSpawner.cs:45`)에 남아 있는 `Debug.Log` 4줄이 전부 디버그용
  잔재이며, 그중 하나(`Debug.Log($"찾 : {d.ID} == {unitID}")`, `UnitSpawner.cs:62`)는
  `FindIndex`의 predicate 안에서 매 원소마다 로그를 찍는 구조라 유닛 종류가 늘어날수록
  콘솔 스팸이 심해진다.
- `BuildingController.Die()`도 `UnitController.Die()`와 마찬가지로 `ReleasePopulation`/
  `RemoveMaxPopulation`을 호출하지 않는다(보급고 파괴 시 인구 한도가 줄지 않음).

### 3.6 ResourceManager / ResourceNode (`Assets/Scripts/Resource/`)

- `ResourceManager`는 `CanAfford`(조회)와 `TrySpend`(조회+차감)를 분리한 설계로,
  `doc/resource-manager-design.md`에서 제안된 코드가 그대로 반영되어 있다. 로직 자체는
  견고하나, 위에서 지적했듯 **호출부가 없다.**
- `ResourceNode`의 대기열은 인원 제한이 아니라 "혼잡 임계값"(`waitWorkerCount`)만 두고,
  대체 자원이 없으면 계속 줄을 서게 하는 정책 — 데드락 없이 항상 채취가 가능하도록 설계된
  점이 좋다.
- 고갈 시 `Destroy(gameObject)`(`ResourceNode.cs:69`)로 자원 노드가 즉시 파괴되는데,
  이 시점에 대기열에 남아있는 나머지 일꾼들은 `UnitController.GatherTick()`의
  `gatherTargetNode == null` 분기에서 방어적으로 재탐색하도록 처리되어 있어 크래시 없이
  자연스럽게 이어진다(두 파일이 서로 잘 맞물려 있음).

### 3.7 BuildSystem (`Assets/Scripts/BuildSystem/`)

- 그리드 점유는 `Dictionary<Vector3Int, PlacementData>` 기반으로 단순하고 직관적이다.
- `PlacementSystem.StartPlacement(int ID)`(`PlacementSystem.cs:38`)에 도달 불가능한 코드가 있다:
  ```csharp
  selectedObjectIndex = database.buildingData.FindIndex(d => d.ID == ID);
  if (selectedObjectIndex < 0) { Debug.LogError(...); return; }   // ← ID==0이면 대부분 여기서 먼저 반환됨
  if (ID == 0) { selectedObjectIndex = -1; return; }              // ← 사실상 도달 안 함
  ```
  데이터베이스에 ID 0인 항목이 실제로 존재하지 않는 한, "ID 0 = 선택 해제" 분기는 실행되지
  않고 매번 `No ID found 0` 에러 로그만 남는다.
- `GridData.RemoveObjectAt`, `GetRepresentationIndex`, `CalculatePositionsPublic`과
  `PreviewSystem.StartBuildModeCursor`, `StartShowingRemovePreview`는 모두 정의만 있고
  호출부가 없다 — 건물 철거(demolish) 기능을 위한 준비 코드로 보이나 아직 연결되지 않았다.
- `PlacementSystem.PlaceStructure()`(`PlacementSystem.cs:69`)는 그리드 겹침(`CanPlaceObejctAt`)과
  물리 충돌(`IsBlocked`)만 검사하고, `RTSUnitController.TryConstructBuilding()`을 호출하지
  않는다 — 건물은 자원 소모 없이 무한정 지을 수 있는 상태다.

### 3.8 ScriptableObject 데이터 (`Assets/Scripts/ScriptableObject/`)

- `UnitData`/`BuildingData` 모두 `mineral`/`gas`/`population`/`productionTime` 필드를
  갖추고 있어 `doc/resource-manager-design.md`에서 요청했던 데이터 모델 확장은 이미
  완료된 상태다. 자원 체크 로직(3.1, 3.6)만 실제로 배선하면 되는 단계.
- `UnitDataSO.unitData`는 `[SerializeField] public`으로 필드가 직접 노출되어 있는 반면,
  `BuildingDataSO.buildingData`는 `[field: SerializeField]` 프로퍼티 패턴을 쓴다 — 같은
  역할의 두 클래스가 서로 다른 스타일을 쓰고 있어 통일할 여지가 있다.

### 3.9 UI (`Assets/Scripts/UI/`)

- `RTSUnitController`의 `UpdateUI()`가 실제로 연결하는 버튼 콜백은 `SpawnUnit(...)`이지
  `TryProduceUnit(...)`이 아니다(`RTSUnitController.cs:512,520~541`). 즉 UI 자체도 자원
  검증 경로를 타지 않고 있어, 3.1/3.7에서 지적한 미연결 문제가 UI 레이어까지 이어져 있다.
- `ProductionSlot`은 재사용 가능한 슬롯 컴포넌트로 깔끔하게 분리되어 있고,
  `UIController.ShowXXXPanel` 계열이 슬롯 개수 초과를 `Mathf.Min`으로 방어하는 등
  UI 쪽 코드 품질은 전반적으로 양호하다.

### 3.10 CameraControl (`Assets/Scripts/Camera/CameraControl.cs`)

- 특별한 이슈 없음. 방향키/엣지 스크롤/줌/본진 복귀(Space)가 표준적인 RTS 카메라 패턴으로
  구현되어 있다.

## 4. 발견된 주요 이슈

### 4.1 자원 소모 검증 로직이 완전히 미연결

`ResourceManager.TrySpend/CanAfford`, `RTSUnitController.TryProduceUnit/TryConstructBuilding`,
`ReleasePopulation`, `AddMaxPopulation`이 전부 정의는 되어 있지만 **호출하는 코드가 프로젝트
전체에서 하나도 없다** (grep 검증 완료). 현재 동작 기준으로는:

- 유닛 생산: 자원 소모 없이 무한 생산 가능 (`SpawnUnit`만 호출됨)
- 건물 건설: 자원 소모 없이 무한 건설 가능 (`PlaceStructure`가 검증 미호출)
- 유닛 사망/건물 파괴: 인구수가 회수되지 않고 누적되어, 실제 인구는 늘지 않는데 `maxPopulation`
  대비 사용량만 계속 올라가는 결과가 됨

`doc/resource-manager-design.md`가 설계 문서인 것을 감안하면, 이 문서의 설계는 코드에
반영됐지만(데이터 모델 + `ResourceManager` + 중개 메소드) **연결 지점(5번 섹션의 표)**은
아직 실행되지 않은 상태로 보인다.

### 4.2 런타임 스크립트가 `UnityEditor` 네임스페이스를 참조 — 빌드 시 컴파일 실패 가능

- `RTSUnitController.cs:4` — `using UnityEditor;`
- `UnitController.cs:7` — `using static UnityEditor.PlayerSettings;`

`UnityEditor` 어셈블리는 에디터에서만 존재하고 스탠드얼론 빌드에는 포함되지 않는다.
두 파일 모두 실제로는 해당 네임스페이스의 API를 사용하지 않는 것으로 보이므로(에디터에서
자동완성 중 잘못 추가된 `using`으로 추정), 지금은 에디터 플레이 모드에서만 정상 동작하고
**실제 빌드 시 컴파일 에러가 날 가능성이 높다.** 우선순위가 가장 높은 수정 대상이다.

그 외에도 사용하지 않는 `using`이 다수 남아 있다: `RTSUnitController.cs`의
`System.Net.Sockets`, `UnityEngine.UIElements`, `static RTSUnitController`(자기 자신을
static import — 무의미), `static UIController`; `UnitController.cs`의 `System.Net`,
`System.Resources`, `static UnityEngine.GraphicsBuffer`.

### 4.3 문자열 인코딩 깨짐(Mojibake)

`HealthManager.cs:42,76`, `UnitSpawner.cs:47~159`, `UIController.cs`의 주석/열거형
멤버(`UISelectionState`) 등 여러 파일에서 한글 리터럴이 `ü��`, `�Ϲ� �г� ǥ��`처럼
깨진 채로 저장되어 있다. 코드 동작에는 지장이 없지만(문자열 리터럴 자체는 유효),
콘솔 로그와 소스 코드 가독성에 실질적인 문제가 있다 — 파일이 EUC-KR(CP949) 등
비UTF-8 인코딩으로 한 번 저장된 이력이 있는 것으로 보인다. 파일을 UTF-8로 다시 저장하며
해당 문자열들을 복구하는 일괄 정리가 필요하다.

### 4.4 사용되지 않는(죽은) 코드

- `PlacementSystem.StartPlacement`의 "ID==0 → 선택 해제" 분기 (4.7 참고, 사실상 도달 불가)
- `GridData.RemoveObjectAt` / `GetRepresentationIndex` / `CalculatePositionsPublic`
- `PreviewSystem.StartBuildModeCursor` / `StartShowingRemovePreview`
- `RTSUnitController.TestMethod()`(`RTSUnitController.cs:615`, 빈 테스트용 메소드)

건물 철거(demolish) 기능을 위한 준비 코드로 보이는 항목들이 많아, 기능 자체가
개발 중단된 것인지 다음 작업으로 남겨둔 것인지 확인이 필요하다.

### 4.5 `UserControl`의 빈 입력 분기

좌/우클릭의 "적 클릭"(`clickedEnemy`) 처리와 키보드의 `V`(건설모드), 일부 `Escape` 분기가
주석만 있고 실제 로직이 없다. 적을 직접 클릭해 공격 대상으로 지정하는 것이 RTS의 기본
기능이라는 점에서 우선순위가 있는 미구현 항목이다.

### 4.6 `FindFirstObjectByType` 반복 호출

`UnitController.Start/Die`, `BuildingController.Start/Die`, `ResourceNode.Start`,
`UserControl.Awake`, `UIController.Start` 등 다수 지점에서 `FindFirstObjectByType<RTSUnitController>()`를
개별 호출한다. 유닛/건물 수가 늘어날수록(특히 대규모 전투에서 유닛이 자주 생성/파괴될 때)
호출 빈도가 늘어난다. `FindFirstObjectByType` 자체는 `Find` 계열보다 빠르지만, 씬에 하나뿐인
싱글턴 성격의 컴포넌트이므로 정적 싱글턴 패턴이나 최초 1회 캐싱 후 재사용하는 구조로
바꾸면 더 안전하고 빠르다.

### 4.7 `Enemy`, `FogOfWar` 폴더가 비어 있음

두 폴더 모두 스크립트가 없다. `AttackRange`가 `"Enemy"` 태그를 참조하고 있어 적 유닛
자체는 씬에 존재할 수 있지만(프리팹 + 태그만으로 동작), AI(적 행동 로직)와 전장의 안개는
코드 레벨에서 아직 손대지 않은 영역이다.

## 5. 강점

- **관심사 분리가 전반적으로 명확하다.** 입력(`UserControl`) → 중앙 상태(`RTSUnitController`)
  → 개별 유닛/건물 로직 → UI 갱신으로 이어지는 단방향 흐름이 대체로 잘 지켜지고 있다.
- **일꾼 채취 상태머신(`UnitController` GatherTick)**은 대기열, 대체 자원 탐색, 중도 취소,
  노드 파괴 등 RTS 특유의 복잡한 엣지 케이스를 견고하게 처리한다.
- **`HealthManager` + `IDestructible`** 조합으로 유닛/건물이 동일한 체력 관리 코드를
  공유하면서도 사망 시 동작만 다르게 가져가는 구조가 깔끔하다.
- **설계 문서(`doc/*.md`)와 코드가 대체로 일치한다.** `resource-manager-design.md`에서
  제안한 데이터 모델과 `ResourceManager` API가 그대로 구현돼 있는 등, 문서 따라가며 검증하기
  좋은 프로젝트 상태다(단, [4.1](#41-자원-소모-검증-로직이-완전히-미연결)의 연결 공백은 예외).

## 6. 권장 조치 우선순위

| 우선순위 | 항목 | 근거 |
|---|---|---|
| 1 | `UnityEditor` 관련 `using` 제거 ([4.2](#42-런타임-스크립트가-unityeditor-네임스페이스를-참조--빌드-시-컴파일-실패-가능)) | 빌드 자체가 깨질 수 있는 유일한 항목 |
| 2 | 자원 소모 검증 연결 ([4.1](#41-자원-소모-검증-로직이-완전히-미연결)) | 핵심 게임플레이 밸런스(자원 무한 생산/건설) |
| 3 | 인구수 반환(`ReleasePopulation`) 연결 | 2번과 함께 처리하면 비용이 적음 |
| 4 | 문자열 인코딩 정리 ([4.3](#43-문자열-인코딩-깨짐mojibake)) | 가독성/유지보수성, 기능 영향 없음 |
| 5 | 적 클릭/건설모드 키 등 빈 입력 분기 구현 ([4.5](#45-usercontrol의-빈-입력-분기)) | 기본 RTS 조작 완성도 |
| 6 | 죽은 코드 정리 또는 철거 기능 완성 ([4.4](#44-사용되지-않는죽은-코드)) | 코드베이스 정리 |
