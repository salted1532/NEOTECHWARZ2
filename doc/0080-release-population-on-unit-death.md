# 0080 — 유닛 사망 시 인구수 반환

## 질문
"유닛이 죽었을때 자신의 인구수에 만큼 현재 인구수에서 빼줬으면 좋겠어"

## 원인

`ResourceManager.cs`에 이미 `ReleasePopulation(int amount)`가 있고, 생산 대기열 "취소"(`RTSUnitController.RefundUnit`)
시에는 이걸 호출해서 인구수를 되돌려주고 있었음. 하지만 이건 "아직 생산되지 않은 유닛을 취소"하는 경로일 뿐,
**실제로 살아있던 유닛이 전투 등으로 죽는 경로**(`UnitController.Die()`, `HealthManager`의 `IDestructible` 구현체로 호출됨)는
`UnitList`/`selectedUnitList`에서 제거하고 오브젝트를 파괴하기만 할 뿐 인구수를 전혀 돌려주지 않고 있었음 — 그래서
유닛이 죽어도 현재 인구수가 계속 그 자리를 차지한 채로 남아있는 문제였음.

## 수정

- **`Assets/Scripts/System/RTSUnitController.cs`**: `ReleaseUnitPopulation(int unitID)` 추가 — `unitID`로
  `unitDatabase`에서 `UnitData`를 조회해 `resourceManager.ReleasePopulation(data.population)`을 호출.
  기존 `RefundUnit()`(생산 취소용, 광물/가스/인구수 전부 환불)과 달리 **인구수만** 돌려줌 — 이미 존재하며
  자원을 다 쓴 유닛이 죽은 것이지 생산 취소가 아니므로 광물/가스는 돌려주지 않는 게 맞음.
- **`Assets/Scripts/Unit/UnitController.cs`**의 `Die()`: `controller?.ReleaseUnitPopulation(unitID);` 한 줄 추가.
  `unitID`는 이미 필드로 갖고 있던 값(`GetUnitID()`가 반환하는 것과 동일)을 그대로 사용.
- `ResourceManager.ReleasePopulation()`은 이미 `Mathf.Max(0, currentPopulation - amount)`로 음수 방지가 되어 있어서
  별도 방어 코드 불필요.
- 적(`EnemyController`)은 플레이어의 `ResourceManager`/인구수와 아예 무관하다는 것을 확인했으므로 건드리지 않음.

## 확인 필요 사항
유닛을 생산해서 인구수가 올라간 걸 확인한 뒤, 그 유닛을 죽여서(적에게 공격당하거나 등) 인구수가 죽은 유닛의
`population` 값만큼 줄어드는지 확인 부탁.
