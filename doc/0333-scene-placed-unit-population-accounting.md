# 0333. 씬에 직접 배치한 아군 유닛도 인구수에 반영

**날짜:** 2026-07-31

## 요청

> 게임 시작할때 맵에 있는 아군유닛들이 자신들의 인구수를 인구수에 추가해주도록 해줘 프리팹으로 내가
> 직접 설치한건 인구수에 포함이 안돼네

## 원인

`ResourceManager.currentPopulation`(현재 사용 중인 인구수)은 오직 `TrySpend(...)`를 통해서만
증가한다. `TrySpend`는 유닛 생산을 "큐에 넣는 시점"(`RTSUnitController.TryProduceUnit()`)에 호출되고,
실제 `UnitSpawner.Spawn()`(대기시간이 다 돼서 Instantiate하는 시점)에는 인구수를 다시 건드리지
않는다 — 즉 정상적인 생산 경로는 "주문할 때 미리 자리를 예약해두는" 방식.

반면 씬에 미리 배치해둔 유닛(프리팹을 에디터에서 직접 Hierarchy에 놓은 경우)은 이 생산 큐를 아예
거치지 않으므로 `TrySpend`가 한 번도 호출되지 않아 인구수에 전혀 반영되지 않았음. `UnitController.Start()`는
이미 "생산 큐를 거쳤든 씬에 직접 배치됐든 항상 자기 unitID로 스탯을 적용한다"는 동일한 원칙을 스탯에는
적용하고 있었지만(주석에도 명시돼 있었음), 인구수만 빠져 있었던 것.

## 수정

씬에 직접 배치된 유닛과 정상 생산된 유닛을 구분할 방법이 필요했음(정상 생산 유닛까지 `Start()`에서
또 인구수를 더하면 이중 계산되므로) — `UnitSpawner.Spawn()`이 `Instantiate()` 직후(그 유닛의
`Start()`가 돌기 전) "나는 생산으로 만들어졌다"는 표시를 남기는 방식으로 구분.

- **`Assets/Scripts/Unit/UnitController.cs`**: `[System.NonSerialized] public bool spawnedByProduction;`
  필드 추가(기본값 `false` — 씬에 직접 배치된 유닛은 이 값을 아무도 안 건드리므로 항상 `false`로 남음).
  `Start()`에서 `if (!spawnedByProduction) rtsController.AddPopulationForExistingUnit(unitID);` 추가.
- **`Assets/Scripts/UnitSpawner/UnitSpawner.cs`**: `Spawn()`이 `Instantiate()` 직후
  `unitController.spawnedByProduction = true;`로 표시 — 정상 생산된 유닛은 `Start()`에서 인구수를
  또 더하지 않음(이미 큐잉 시점에 반영됐으므로).
- **`Assets/Scripts/System/RTSUnitController.cs`**: `AddPopulationForExistingUnit(int unitID)` 추가 —
  기존 `ReleaseUnitPopulation(int unitID)`(사망 시 인구수 반환)와 대칭 구조로, unitID로 DB를 조회해
  `resourceManager.AddPopulationDirect(...)` 호출.
- **`Assets/Scripts/Resource/ResourceManager.cs`**: `AddPopulationDirect(int amount)` 추가 —
  `AddMaxPopulation`과 동일하게 `CanAfford` 판정 없이 그냥 현재 값에 바로 더함(이미 존재하는 유닛이라
  "지금 지을 수 있는지"를 물을 필요가 없음).

## 검증

- `npx uloop-cli compile`: 에러 0개.
- Play Mode 실행 중 `execute-dynamic-code`로 실제 검증: `Sharpshooter.prefab`을 `spawnedByProduction`
  기본값(`false`)인 채로 씬에 Instantiate(= "이미 배치돼 있던 유닛"과 동일한 조건) → 인구수가
  `0 → 1`로 정확히 그 유닛의 인구수 비용(`UnitData.population = 1`)만큼 증가함을 확인(PASS). 테스트용
  인스턴스는 확인 후 정리, 씬/에셋 파일은 건드리지 않음.

## 영향받는 파일

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/UnitSpawner/UnitSpawner.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/Resource/ResourceManager.cs`
