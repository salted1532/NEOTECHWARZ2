# InputManager

`Assets/Scripts/BuildSystem/InputManager.cs`

## 개요

건물 배치(BuildSystem) 전용 입력 처리기. 마우스 좌클릭/ESC 입력을 이벤트로 전달하고, 마우스가 가리키는 월드 좌표(지면)를 계산해준다.

> UserControl(게임플레이 입력)과는 별개의, PlacementSystem 전용 입력 컴포넌트다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `sceneCamera` | `Camera` (SerializeField) | 레이캐스트 기준 카메라 |
| `lastPosition` | `Vector3` | 레이캐스트가 실패했을 때 재사용할 마지막 유효 좌표 |
| `placementLayermask` | `LayerMask` (SerializeField) | 배치 가능 레이어(지면 등) |
| `OnClicked` | `event Action` | 좌클릭 시 발생 (건물 배치 확정용) |
| `OnExit` | `event Action` | ESC 입력 시 발생 (배치 모드 취소용) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Update()` | 좌클릭 시 `OnClicked`, ESC 입력 시 `OnExit` 이벤트 발생 |
| `IsPointerOverUI()` | 마우스 포인터가 UI 위에 있는지 여부 (UI 클릭 시 배치가 발생하지 않도록 방지) |
| `GetSelectedMapPosition()` | 현재 마우스 위치에서 카메라 레이를 쏘아 배치 레이어와의 충돌 지점을 반환. 레이가 아무것도 맞추지 못하면 마지막으로 유효했던 위치를 그대로 반환 |

## 연관 컴포넌트

- **PlacementSystem**: `OnClicked`/`OnExit` 이벤트를 구독해 배치 확정/취소를 처리하고, `GetSelectedMapPosition()`으로 마우스 위치를 조회
