# ResourceNode

`Assets/Scripts/Resource/ResourceNode.cs`

## 개요

광물/가스 채취 지점. 여러 일꾼이 동시에 채취하지 못하도록 대기열(줄서기) 방식을 사용하며, 남은 양에 따라 오브젝트 크기가 단계적으로 줄어들고(콜라이더는 월드 크기 유지하도록 보정) 고갈되면 스스로 파괴된다. 좌클릭 선택 마커, 우클릭 채취 명령 피드백(마커 깜빡임)도 `UnitController`/`BuildingController`와 동일한 패턴으로 지원한다.

## 주요 필드 / 프로퍼티

| 필드 | 타입 | 설명 |
|---|---|---|
| `resourceType` | `ResourceType` (Ore/Gas) | 자원 종류 |
| `remainingAmount` | `int` | 남은 채취 가능량 |
| `resourceMarker` | `GameObject` (SerializeField) | 선택 시/우클릭 피드백 시 켜지는 마커 (평소엔 꺼져있음) |
| `icon` | `Sprite` (SerializeField) | Info_panel에 표시할 아이콘 |
| `flashInterval`, `flashCount` | `float`, `int` (SerializeField) | 우클릭 채취 명령 피드백 깜빡임 간격/횟수 (기본 0.3초 × 3회) |
| `flashRoutine` | `Coroutine` | 진행 중인 깜빡임 코루틴 (중복 방지용) |
| `rtsController` | `RTSUnitController` | `Start()`에서 획득, `ResourceNodeList` 등록 및 선택 상태 조회에 사용 |
| `waitWorkerCount` | `int` | 대기열이 이 인원 이상이면 "혼잡"으로 판단하는 임계값. 하드 캡이 아니므로 대체 자원이 없으면 초과해도 계속 줄을 설 수 있음 |
| `ShrinkStepPerQuarter` | `const float` | 초기량의 1/4씩 줄어들 때마다 축소되는 크기 비율(0.2) |
| `MinScale` | `const float` | 스케일 하한(0.1) - 메시가 뒤집히는 것 방지 |
| `initialAmount` | `int` | 최초 채취 가능량 (축소 비율 계산 기준) |
| `consumedQuarters` | `int` | 지금까지 줄어든 구간 수 (0~4) |
| `nodeCollider`, `colliderBaseRadius`, `colliderBaseHeight`, `colliderBaseCenter` | `CapsuleCollider`, `float`, `float`, `Vector3` | 최초 콜라이더 크기 캐시 (오브젝트가 시각적으로 작아져도 콜라이더의 실제 월드 크기는 유지하기 위한 보정 기준값) |
| `workerQueue` | `List<UnitController>` | 대기열. 맨 앞(index 0)만 실제로 채취, 나머지는 대기. 인원 제한 없음 |
| `Type` | `ResourceType` | 자원 종류 (읽기 전용) |
| `IsDepleted` | `bool` | `remainingAmount <= 0` |
| `RemainingAmount` | `int` | 남은 채취량 (읽기 전용) |
| `IsCrowded` | `bool` | 대기열이 `waitWorkerCount` 이상인지 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `JoinQueue(worker)` | 대기열 등록 (인원 제한 없이 항상 성공, 중복 등록 방지) |
| `LeaveQueue(worker)` | 대기열에서 제거 |
| `IsTurnToGather(worker)` | 대기열 맨 앞(=현재 채취할 차례)인지 여부 |
| `Awake()` | `initialAmount`를 현재 `remainingAmount`로 저장, `CapsuleCollider` 기준 크기 캐싱 |
| `Start()` | 마커 비활성화, `RTSUnitController.ResourceNodeList`에 자신을 등록 |
| `SelectResource()` / `DeselectResource()` | 좌클릭 선택 시 마커 on/off |
| `GetIcon()` | Info_panel용 아이콘 반환 |
| `FlashMarker()` | 우클릭 채취 명령을 받았을 때 "이 자원이 대상"임을 마커 깜빡임(0.3초 간격 3회)으로 피드백. 좌클릭 선택 마커와 같은 오브젝트를 재사용하므로 끝나면 실제 선택 상태로 복원 |
| `FlashMarkerRoutine()` (private) | 깜빡임 코루틴 본체 |
| `Extract(amountPerTrip)` | 채취 시도 시 실제로 캐갈 수 있는 양을 반환(고갈 임박 시 `amountPerTrip`보다 적을 수 있음). 채취 후 크기 축소, 고갈되면 선택 상태 정리 후(`ClearSelectedResourceIfMatches`) 자기 자신을 파괴 |
| `ShrinkByRemainingRatio()` (private) | 초기량의 1/4씩 줄어들 때마다 크기를 0.2씩 축소하고, 줄어든 만큼 위치도 아래로 내림 |
| `ApplyColliderSizeCompensation()` (private) | `transform.localScale`이 작아져도 `CapsuleCollider`의 실제(월드) 크기는 최초 그대로 유지되도록 스케일 역보정 |

## 연관 컴포넌트

- **UnitController**: `Gather`/`GatherTick` 상태머신에서 `JoinQueue`/`LeaveQueue`/`IsTurnToGather`/`Extract`를 호출해 채취 로직을 진행
- **RTSUnitController**: `ResourceNodeList`로 모든 자원 노드를 추적(대체 자원 탐색 등에 사용), `selectedResourceNode`/`ClearSelectedResourceIfMatches`로 선택 상태 연동
- **UserControl**: 좌클릭 시 `ClickSelectResource`, 우클릭 시 `GatherSelectedUnits` + `FlashMarker()` 호출
