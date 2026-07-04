# ResourceNode

`Assets/Scripts/Resource/ResourceNode.cs`

## 개요

광물/가스 채취 지점. 여러 일꾼이 동시에 채취하지 못하도록 대기열(줄서기) 방식을 사용하며, 남은 양에 따라 오브젝트 크기가 단계적으로 줄어들고 고갈되면 스스로 파괴된다.

## 주요 필드 / 프로퍼티

| 필드 | 타입 | 설명 |
|---|---|---|
| `resourceType` | `ResourceType` (Ore/Gas) | 자원 종류 |
| `remainingAmount` | `int` | 남은 채취 가능량 |
| `waitWorkerCount` | `int` | 대기열이 이 인원 이상이면 "혼잡"으로 판단하는 임계값. 하드 캡이 아니므로 대체 자원이 없으면 초과해도 계속 줄을 설 수 있음 |
| `ShrinkStepPerQuarter` | `const float` | 초기량의 1/4씩 줄어들 때마다 축소되는 크기 비율(0.2) |
| `initialAmount` | `int` | 최초 채취 가능량 (축소 비율 계산 기준) |
| `consumedQuarters` | `int` | 지금까지 줄어든 구간 수 (0~4) |
| `workerQueue` | `List<UnitController>` | 대기열. 맨 앞(index 0)만 실제로 채취, 나머지는 대기. 인원 제한 없음 |
| `Type` | `ResourceType` | 자원 종류 (읽기 전용) |
| `IsDepleted` | `bool` | `remainingAmount <= 0` |
| `IsCrowded` | `bool` | 대기열이 `waitWorkerCount` 이상인지 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `JoinQueue(worker)` | 대기열 등록 (인원 제한 없이 항상 성공, 중복 등록 방지) |
| `LeaveQueue(worker)` | 대기열에서 제거 |
| `IsTurnToGather(worker)` | 대기열 맨 앞(=현재 채취할 차례)인지 여부 |
| `Awake()` | `initialAmount`를 현재 `remainingAmount`로 저장 |
| `Start()` | `RTSUnitController.ResourceNodeList`에 자신을 등록 |
| `Extract(amountPerTrip)` | 채취 시도 시 실제로 캐갈 수 있는 양을 반환(고갈 임박 시 `amountPerTrip`보다 적을 수 있음). 채취 후 크기 축소, 고갈되면 자기 자신을 파괴 |
| `ShrinkByRemainingRatio()` (private) | 초기량의 1/4씩 줄어들 때마다 크기를 0.2씩 축소 |

## 연관 컴포넌트

- **UnitController**: `Gather`/`GatherTick` 상태머신에서 `JoinQueue`/`LeaveQueue`/`IsTurnToGather`/`Extract`를 호출해 채취 로직을 진행
- **RTSUnitController**: `ResourceNodeList`로 모든 자원 노드를 추적 (대체 자원 탐색 등에 사용)
