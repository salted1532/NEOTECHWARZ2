# UnitSpawner

`Assets/Scripts/UnitSpawner/UnitSpawner.cs`

## 개요

건물에 부착되어 유닛 생산 대기열을 관리하고, 시간이 다 되면 실제로 유닛을 스폰하는 컴포넌트. 대기열은 항상 맨 앞의 한 항목만 진행되는 순차(FIFO) 생산 방식이다.

## ProductionData

생산 대기열 한 항목의 상태 (`[System.Serializable]`).

| 필드/프로퍼티 | 타입 | 설명 |
|---|---|---|
| `UnitID` | `int` | 생산할 유닛 ID |
| `RemainTime` | `float` | 남은 생산 시간 |
| `TotalTime` | `float` | 총 생산 시간 |
| `Progress` | `float` | `1f - (RemainTime / TotalTime)`, 0~1 사이 진행률 (프로그레스 바 표시용) |

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `database` | `UnitDataSO` (SerializeField) | 유닛 스펙 데이터베이스 |
| `MaxQueueSize` | `const int` (=5) | 대기열 최대 크기 |
| `productionQueue` | `List<ProductionData>` | 생산 대기열 (최대 `MaxQueueSize`개) |
| `buildingController` | `BuildingController` | 부모 건물 컨트롤러 (랠리 포인트 조회용) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 부모 오브젝트의 `BuildingController`를 캐싱 |
| `Update()` | 매 프레임 `Produce()` 호출 |
| `Enqueue(unitID)` | 지정한 유닛 ID를 생산 대기열에 추가한다. 대기열이 `MaxQueueSize` 이상이면 무시된다. (대기열 가득참/자원 확인은 호출측인 `RTSUnitController.TryProduceUnit`에서 먼저 처리된 뒤 호출됨) |
| `IsQueueFull()` | 대기열이 가득 찼는지 (`RTSUnitController.TryProduceUnit`이 자원을 소모하기 전에 먼저 확인) |
| `Spawn(unitID)` (private) | 생산이 완료된 유닛을 실제로 `Instantiate`하고, 스포너 위치에서 랠리 포인트로 이동을 명령 |
| `Produce()` (private) | 매 프레임 대기열 맨 앞(index 0) 항목의 남은 시간을 줄이고, 0 이하가 되면 스폰을 실행 |
| `Cancel(index)` | 대기열의 특정 인덱스 항목을 취소(제거). UI의 대기열 슬롯 클릭 시 호출. **환불에 쓸 수 있도록 취소된 유닛ID를 반환**(인덱스가 유효하지 않으면 -1) |
| `ClearQueue()` | 건물이 파괴될 때 호출 — 대기열에 남아있던 항목 전체를 반환(제거)하고 비움. 환불 자체는 호출측(`RTSUnitController.RefundProductionQueue`)이 처리 |
| `PrintQueue()` (private) | 콘솔 디버그용 대기열 출력 |
| `GetProductionQueue()` | 대기열 반환 (읽기 전용, UI 표시용) |
| `GetProductionProgress()` | 현재 생산 중인(맨 앞) 항목의 진행률(0~1) 반환 |

## 연관 컴포넌트

- **BuildingController**: `SpawnUnit`/`GetProductionQueue`/`GetProductionProgress`/`CancelProduction`/`IsProductionQueueFull`/`ClearProductionQueue`를 이 컴포넌트에 위임
- **UnitDataSO**: ID로 유닛 스펙(생산시간, 프리팹) 조회
- **UnitController**: 스폰된 유닛의 `MoveTo(rallyPos)`를 호출해 랠리 포인트로 이동시킴
- **RTSUnitController**: `TryProduceUnit`이 `IsQueueFull()`로 대기열 확인 후 자원을 소모하고, `CancelProduction`/`Die()` 경로에서 `Cancel`/`ClearQueue`의 반환값으로 환불(`RefundUnit`/`RefundProductionQueue`) 처리
