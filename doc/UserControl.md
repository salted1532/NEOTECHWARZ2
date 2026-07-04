# UserControl

`Assets/Scripts/UserControl/UserControl.cs`

## 개요

플레이어의 마우스/키보드 입력을 해석하는 컨트롤러. 좌클릭 선택(드래그 박스 포함), 우클릭/키보드 명령(이동·공격·순찰·정지·홀드·랠리) 발행, 명령 대기 상태(`OrderState`)에 따른 커서 포인터 표시를 담당하며 실제 명령 실행은 `RTSUnitController`에 위임한다.

## 상태 정의

| 열거형 | 값 | 설명 |
|---|---|---|
| `OrderState` (private enum) | `None, Attack, Move, Patrol, Rally` | 다음 클릭이 어떤 명령으로 처리될지 대기 중인 상태 |

## 주요 필드

| 필드 | 설명 |
|---|---|
| `layerUnit`, `layerGround`, `layerEnemy`, `layerBuilding`, `layerOre`, `layerGas` | 클릭 대상 판별용 레이어 마스크 |
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
| `HandleLeftClick()` (private) | 6개 레이어로 순차 레이캐스트: 유닛 클릭 시 선택(Shift 여부에 따라 단일/토글), 땅 클릭 시 현재 `OrderState`(Move/Attack/Patrol/Rally)에 따라 명령 실행, 건물 클릭 시 선택, 그 외(아무 것도 아닌 곳) 클릭 시 전체 선택 해제 |
| `HandleRightClick()` (private) | 컨텍스트에 따라 동작 분기: 유닛 선택 중 땅 우클릭→이동, 건물 선택 중 땅 우클릭→랠리 설정, 유닛 선택 중 건물 우클릭→해당 건물로 이동(자원 반환 겸용), 광물/가스 우클릭→채취 명령 |

### 키보드 입력
| 메소드 | 설명 |
|---|---|
| `HandlekeyBoard()` (private) | 유닛 선택 중일 때 단축키 처리: `A`=공격모드 대기, `S`=정지, `H`=홀드, `P`=순찰모드 대기, `M`=이동모드 대기, `Y`=랠리모드 대기(참고: 건물이 아닌 유닛 선택 조건에 걸려 있어 실제로는 유닛 선택 시에만 동작), `V`는 건설모드 전환용으로 예약되어 있으나 미구현. 건설 모드 중 `Escape`로 상태 복귀 |

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
- **UnitController** / **BuildingController** / **ResourceNode**: 레이캐스트로 감지된 대상의 실제 컴포넌트
