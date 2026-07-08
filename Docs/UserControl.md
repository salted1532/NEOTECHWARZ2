# UserControl

`Assets/Scripts/UserControl/UserControl.cs`

## 개요

플레이어의 마우스/키보드 입력을 해석하는 컨트롤러. 좌클릭 선택(드래그 박스 포함), 우클릭 명령(이동·공격·따라다니기·순찰·정지·홀드·랠리·채취·건설 재개) 발행, 명령 대기 상태(`OrderState`)에 따른 커서 포인터 표시를 담당하며 실제 명령 실행은 `RTSUnitController`에 위임한다. 커맨드 패널 버튼 단축키는 더 이상 여기서 처리하지 않고 각 `ProductionSlot`이 스스로 감지한다(`Docs/ProductionSlot.md` 참고).

## 상태 정의

| 열거형 | 값 | 설명 |
|---|---|---|
| `OrderState` (private enum) | `None, Attack, Move, Patrol, Rally` | 다음 클릭이 어떤 명령으로 처리될지 대기 중인 상태 |

## 주요 필드

| 필드 | 설명 |
|---|---|
| `layerUnit`, `layerGround`, `layerEnemy`, `layerBuilding`, `layerOre`, `layerGas` | 클릭 대상 판별용 레이어 마스크 (`BaseStructure`도 실제 건물과 같은 "Building" 레이어를 사용하므로 `layerBuilding`으로 함께 감지됨) |
| `mainCamera` | 레이캐스트 기준 카메라 |
| `dragRectangle` | 드래그 선택 박스 UI |
| `pointer`, `attackPointer`, `movePointer` | 명령 대기 상태에 따른 커서 표시용 오브젝트 |
| `rtsUnitController` | 실제 명령 실행을 위임할 대상 |
| `start`, `end`, `dragRect` | 드래그 박스 좌표 계산용 |
| `UsercurrentState` | 현재 `OrderState` |

## 메소드

### 생명주기
| 메소드 | 설명 |
|---|---|
| `Awake()` | 카메라/`RTSUnitController` 캐싱, 공격/이동 포인터 인스턴스 생성(비활성 상태로 시작) |
| `Update()` | 매 프레임 `HandleMouse()` → `HandlekeyBoard()` → `UpdatePointer()` 순으로 처리 |

### 마우스 입력
| 메소드 | 설명 |
|---|---|
| `HandleMouse()` (private) | 좌클릭 다운(드래그 시작), 좌클릭 홀드(드래그 박스 갱신), 좌클릭 업(드래그 종료→선택), 우클릭 다운을 각각 처리 |
| `HandleLeftClick()` (private) | 6개 레이어로 순차 레이캐스트: 유닛 클릭 시 선택(A 모드면 아군 강제공격, Shift 여부에 따라 단일/토글 선택), 적 클릭 시 선택(A 모드면 강제공격), 건물 클릭 시 선택(A 모드면 강제공격) — 건물이 아니면 이어서 `BaseStructure` 컴포넌트 확인 후 좌클릭 선택(항상 단일, A 모드 대응 없음), 땅 클릭 시 현재 `OrderState`(Move/Attack/Patrol/Rally)에 따라 명령 실행, 광물/가스 클릭 시 선택, 그 외(아무 것도 아닌 곳) 클릭 시 전체 선택 해제 |
| `HandleRightClick()` (private) | 컨텍스트에 따라 동작 분기: 아군 유닛 우클릭(유닛 선택 중)→계속 따라다니기(`FollowSelectedUnits`) + 마커 깜빡임, 적 우클릭→추격 공격 + 마커 깜빡임, 유닛 선택 중 땅 우클릭→이동, 건물 선택 중 땅 우클릭→랠리 설정, 유닛 선택 중 건물 우클릭→해당 건물로 이동(자원 반환 겸용), 건물이 아니면 `BaseStructure` 확인 후 우클릭 시 `AssignBuilderToStructure`로 선택된 일꾼을 보내 건설 재개 + 마커 깜빡임, 광물/가스 우클릭→채취 명령 + 마커 깜빡임 |

### 키보드 입력
| 메소드 | 설명 |
|---|---|
| `HandlekeyBoard()` (private) | 이제 대부분의 단축키(공격/이동/정지/순찰/홀드/반환/건설, 건물 건설, 유닛 생산)는 각 커맨드 버튼의 `ProductionSlot`이 자기 `Shortcut`을 직접 감지해 스스로 클릭되므로 여기서 처리하지 않는다. 여기 남아있는 건 대응하는 버튼이 없는 순수 키보드 전용 상태 전환뿐: 유닛 선택 중 `Y`=랠리모드 대기, 건설 모드 중 `Escape`=상태 복귀(`ReturnState`) |

### 드래그 박스 / 선택
| 메소드 | 설명 |
|---|---|
| `DrawDragRectangle()` (private) | 드래그 범위를 나타내는 UI 이미지의 위치/크기 갱신 |
| `CalculateDragRect()` (private) | 시작점과 현재 마우스 위치로 `Rect`(min/max) 계산 |
| `SelectObject()` (private) | `RTSUnitController.UnitList`의 각 유닛을 화면 좌표로 변환해 드래그 사각형 안에 있으면 `DragSelectUnit` 호출 |

### 포인터 / 상태 전환
| 메소드 | 설명 |
|---|---|
| `UpdatePointer()` (private) | 현재 명령 대기 상태(공격/이동/순찰/랠리)에 맞는 포인터 아이콘을 마우스가 가리키는 지면 위치에 표시 |
| `SetOrderState(string state)` | 외부(`RTSUnitController.EnterXMode`)에서 문자열로 명령 대기 상태를 전환할 때 사용 (예: `"Move"`, `"Attack"`, `"Patrol"`, `"Rally"`) |

## 연관 컴포넌트

- **RTSUnitController**: 모든 선택/명령 호출의 대상. `IsUnitSelect()`/`IsBuildingSelect()`/`IsBuildMode()` 등 상태 조회에도 사용
- **UnitController** / **BuildingController** / **ResourceNode** / **BaseStructure**: 레이캐스트로 감지된 대상의 실제 컴포넌트
- **ProductionSlot**: 커맨드 패널 버튼 단축키(A/M/S/P/H/R/B, 건설/생산 단축키 등)의 실제 감지·실행 주체 — `UserControl`은 더 이상 이 단축키들을 직접 처리하지 않음
