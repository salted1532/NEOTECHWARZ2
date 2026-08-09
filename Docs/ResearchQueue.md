# ResearchQueue

`Assets/Scripts/Building/ResearchQueue.cs`

## 개요

연구소(Lab)에 부착되어 공격력/방어력 연구 대기열(레벨 1~3)을 관리하고, 완료되면 `RTSUnitController`를 통해 전역 보너스를 올리는 컴포넌트. `UnitSpawner`(생산 대기열)와 동일한 FIFO 타이머 구조를 사용한다. 공격/방어 각각 "다음 레벨" 1개씩만 의미가 있으므로 동시에 최대 2개(둘 다 큐잉)까지만 허용한다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `MaxLevel` (const) | 연구 최대 레벨 (3) |
| `MaxQueueSize` (const, private) | 대기열 최대 크기 (2 — 공격/방어 각 1개씩) |
| `attackResearchTime[]` / `armorResearchTime[]` | 레벨별(1~3업) 연구 소요 시간 |
| `researchOreCost[]` / `researchGasCost[]` | 레벨별 연구 비용 (공격/방어 공통) |
| `attackBonusPerLevel` / `armorBonusPerLevel` | 레벨업 1회당 적용되는 누적 보너스량 |
| `researchQueue` | 현재 대기열 (private) |
| `attackLevel` / `armorLevel` | 현재 달성 레벨 (0~3, private) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | `RTSUnitController`/부모 `BuildingController` 캐싱 |
| `Update()` | 매 프레임 `Research()` 호출 |
| `GetLevel(type)` | 현재 레벨 조회 |
| `IsQueued(type)` (private) | 같은 타입이 이미 대기열에 있는지 (공격 1업 연구 중엔 공격 2업을 못 넣게 막기 위함) |
| `CanEnqueue(type)` | 최대 레벨 미만 + 중복 큐잉 아님 + 대기열이 꽉 차지 않음을 모두 만족하는지 |
| `GetCost(type)` | "현재 레벨+1"을 연구하는 비용 — 이미 최대 레벨이면 (0,0) |
| `Enqueue(type)` | 다음 레벨을 대기열에 추가 (자원 소모는 호출측 `RTSUnitController.TryResearch`가 먼저 처리) |
| `Research()` (private) | 대기열 맨 앞 항목의 남은 시간을 매 프레임 줄이고, 0 이하가 되면 완료 처리 — 대기열은 항상 맨 앞 한 항목만 진행되는 순차(FIFO) 방식. 건물이 영토 밖이면 타이머가 그 자리에서 멈춤(생산 큐와 동일한 규칙) |
| `Complete(type)` (private) | 레벨 증가 후 `rtsController.AddGlobalBonus()`로 반영 — `UpgradeManager`를 직접 만지지 않고 `RTSUnitController`를 거침 |
| `Cancel(index)` | 대기열의 특정 항목을 취소하고 환불용 `ResearchType`(int)을 반환(유효하지 않으면 -1). 레벨은 `Complete()`에서만 올라가므로 취소 시점엔 `GetCost()`로 환불액을 그대로 되짚을 수 있음 |
| `ClearQueue()` | 건물 파괴 시 호출 — 대기열 전체를 반환(제거), 환불 처리는 호출측(`RTSUnitController`) 책임 |
| `GetResearchQueue()` | 대기열 읽기 전용 조회 (UI 표시용) |
| `GetResearchProgress()` | 현재 진행 중인 항목의 진행률(0~1) |

## 연관 컴포넌트

- **RTSUnitController**: 비용 확인·차감(`TryResearch`), 완료 시 `AddGlobalBonus` 호출받음, 취소/파괴 시 환불 처리
- **UpgradeManager**: 전역 공격력/방어력 보너스 저장소 — `ResearchQueue`가 직접 만지지 않고 `RTSUnitController`를 통해서만 갱신됨
- **BuildingController**: 영토 판정을 위한 부모 컴포넌트 참조
- **UnitSpawner**: 동일한 FIFO 타이머 큐 구조를 공유하는 생산 대기열 대응 컴포넌트
