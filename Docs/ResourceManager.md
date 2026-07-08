# ResourceManager

`Assets/Scripts/Resource/ResourceManager.cs`

## 개요

플레이어(팀)의 자원(광물/가스)과 인구수(보급)를 관리하는 중앙 저장소. `RTSUnitController`가 이 컴포넌트를 통해 자원 조회/소모/증가를 처리한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `startOre`, `startGas`, `startMaxPopulation` | `int` (SerializeField) | 게임 시작 시 초기값 |
| `maxPopulationCap` | `int` (SerializeField, 기본 200) | 인구수 한도가 아무리 늘어도 넘을 수 없는 상한선 |
| `currentOre`, `currentGas` | `int` | 현재 보유 자원 |
| `currentPopulation` | `int` | 현재 사용 중인 인구수 |
| `maxPopulation` | `int` | 인구수 한도 (메인기지/보급고 등 건물로 증가, `maxPopulationCap`으로 상한) |
| `OnResourceChanged` | `event Action` | 자원/인구가 변경될 때마다 발생 (UI 갱신용) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 시작값으로 현재 자원/인구 한도 초기화 |
| `GetOre()` / `GetGas()` / `GetPopulation()` / `GetMaxPopulation()` | 현재 값 조회 |
| `AddOre(amount)` / `AddGas(amount)` | 채취(일꾼)로 인한 자원 획득. 0 이하 무시, 변경 시 `OnResourceChanged` 발생 |
| `AddMaxPopulation(amount)` | 건물 건설 완료 시 인구수 한도 증가. `maxPopulationCap`(기본 200)을 넘지 않도록 `Mathf.Min`으로 클램프 |
| `RemoveMaxPopulation(amount)` | 건물 파괴 시 인구수 한도 감소(0 미만으로 내려가지 않게 클램프) |
| `CanAfford(oreCost, gasCost, populationCost)` | 소모 없이 생산/건설 가능 여부만 확인 |
| `TrySpend(oreCost, gasCost, populationCost)` | `CanAfford`로 확인 후 실제 자원을 소모(인구수는 증가). 요청을 "받아들여도 될지" 판정 + 실행을 한 번에 처리 |
| `ReleasePopulation(amount)` | 유닛 생산 취소/사망, 건물 파괴 시 인구수 반환(`currentPopulation` 감소) |

## 연관 컴포넌트

- **RTSUnitController**: `TryProduceUnit`/`TryConstructBuilding`에서 `TrySpend`를 호출해 생산/건설 전 자원을 검증·소모하고, `RefundUnit`/`RefundBuilding`/`RefundProductionQueue`에서 `AddOre`/`AddGas`/`ReleasePopulation`으로 환불. `AddMaxPopulation`/`RemoveMaxPopulationForBuilding`으로 인구수 한도 증감을 그대로 패스스루
- **BaseStructure**: 건설이 실제로 완공되는 순간에만 `RTSUnitController.AddMaxPopulation`을 통해 인구수 한도 반영(건설 중엔 미반영)
- **UIController**: `OnResourceChanged` 또는 매 프레임 Get 메서드를 통해 자원/인구 텍스트 갱신
