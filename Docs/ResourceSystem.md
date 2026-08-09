# 자원 시스템 — 아이로나이트 광석 / 페트로나이트

> 최종 갱신: 2026-08-09
> 명칭/컨셉 확정 과정은 [`doc/0493`](../doc/0493-ore-gas-resource-concept.md), 로컬라이제이션 구현은
> [`doc/0494`](../doc/0494-ore-gas-resource-localization.md) 참고. 스크립트별 필드/메소드 상세는
> [`ResourceManager.md`](ResourceManager.md)(팀 자원 저장소) /
> [`ResourceNode.md`](ResourceNode.md)(채취 지점) 참고 — 이 문서는 그 둘을 아우르는 컨셉+동작 개요다.

## 컨셉

내부 코드상 명칭은 여전히 `ResourceType.Ore`/`Gas`(과거 "광물"/"가스")지만, 표시되는 이름과 설정은
아래처럼 확정되어 있다.

### 아이로나이트 광석 (Ironite Ore) — `ResourceType.Ore`
행성 지각 어디서나 흔히 채굴되는 범용 금속광. 정제 방식만 바꾸면 장갑판이든 프레임이든 회로든
탄약이든 뭐로든 가공할 수 있어서, NTA 모든 생산 라인(유닛·건물 가리지 않고)의 기초 재료로 쓰인다 -
"원하는 대로 쓸 수 있는 광물"이 곧 이 자원의 정체성.

### 페트로나이트 (Petronite) — `ResourceType.Gas`
겉보기엔 초록빛 결정 광물이지만, 정제로에 넣으면 원유처럼 고에너지 연료·추진제·플라즈마 충전재를
뽑아낼 수 있는 자원 - 지구로 치면 석유에 대응하는 존재다. 채굴 상태는 고체 결정인데도 여전히
"가스"라 불리는 이유는, 옛 개척단이 정제 후 뿜어져 나오는 가연성 기체 산출물만 보고 자원 자체를
"가스"로 잘못 보고해 관습적으로 이름이 굳어버렸다는 설정. 고급 유닛/건물, 특수 무기 등 소모가 큰
항목의 두 번째 비용 자원 포지션은 그대로 유지한다.

---

## 시스템 구조

### 팀 저장소 — `ResourceManager`
- 시작값: 아이로나이트 광석 50 / 페트로나이트 0 (`startOre`/`startGas`, `Awake()`에서 반영)
- `CanAfford(oreCost, gasCost, populationCost)` — 소모 없이 생산/건설 가능 여부만 확인
- `TrySpend(oreCost, gasCost, populationCost)` — 확인 + 실제 소모를 한 번에 처리
- `AddOre`/`AddGas` — 채취 완료 시 증가, 환불 시에도 동일 경로 사용
- 자세한 필드/메소드는 [`ResourceManager.md`](ResourceManager.md) 참고

### 채취 지점 — `ResourceNode`
- 씬에 배치된 개별 채취 지점(광맥) 하나당 1개, `ResourceType`(Ore/Gas)과 `remainingAmount`(남은
  채취 가능량)를 가짐
- **대기열(줄서기)**: 여러 일꾼이 동시에 채취하지 못하게 `workerQueue`로 순서를 관리 - 맨 앞
  (`IsTurnToGather`)만 실제로 채취
- **채취량**: `UnitController.amountPerTrip`(기본 5)만큼 왕복 1회당 `Extract()`로 캐감. 고갈 임박
  시 요청량보다 적게 반환될 수 있음
- **고갈 연출**: 초기량의 1/4씩 줄어들 때마다 시각적 크기를 0.2씩 축소(`ShrinkByRemainingRatio`),
  `remainingAmount`가 0이 되면 스스로 파괴
- **혼잡도**: 대기열이 `waitWorkerCount`(기본 2) 이상이면 "혼잡"으로 보고 새로 오는 일꾼이 다른
  자원을 먼저 찾아보게 함(하드 캡 아님)
- 자세한 필드/메소드는 [`ResourceNode.md`](ResourceNode.md) 참고

### 이름/설명 조회 (Info Panel 표시용)
`ResourceNode.GetName()`/`GetDescription()`이 `resourceType`으로 분기해
`LocalizationManager.GetTextOrFallback`으로 조회한다. 프리팹별 인스펙터 필드가 아니라 코드에서
`ResourceType` 값으로 직접 분기하는 이유는, 같은 타입의 모든 노드가 동일한 이름/설명을 공유하기
때문(doc/0490의 `MissionItem`처럼 인스턴스마다 다른 값이 필요한 경우와 다름).

```csharp
public string GetName() => resourceType == ResourceType.Ore
    ? LocalizationManager.GetTextOrFallback("resource.ore.name", "Ironite Ore")
    : LocalizationManager.GetTextOrFallback("resource.gas.name", "Petronite");
```

`RTSUnitController`가 자원 노드 선택 시 `UIController.ShowResourceInfoPanel(icon, name,
remainingAmount, description)`을 호출해 아이콘/이름/남은 채취량/설명을 Info Panel에 반영한다.

---

## 로컬라이제이션 키

`Assets/Resources/Localization/ko.json` / `en.json` 공통:

| key | ko.json | en.json |
|---|---|---|
| `resource.ore.name` | 아이로나이트 광석 | Ironite Ore |
| `resource.ore.desc` | 장갑판부터 회로까지 뭐든 가공할 수 있는 범용 금속광이다. NTA 생산 시설 어디서나 기초 재료로 쓰인다. | A versatile metal ore refined into anything from armor plating to circuitry - the basic material behind everything NTA builds. |
| `resource.gas.name` | 페트로나이트 | Petronite |
| `resource.gas.desc` | 초록빛 결정 형태로 채굴되지만, 정제하면 원유 못지않은 고에너지 연료를 뽑아낼 수 있는 자원이다. | Mined as a green crystal, but refining it yields a fuel as energy-dense as crude oil. |

## 구현 상태 참고

- 프리팹/에셋 파일명(`Assets/prefabs/Resource/Ore.prefab`/`Gas.prefab`)은 리네임하지 않고 표시
  텍스트만 로컬라이제이션으로 교체했다 — 내부 참조가 많아 리스크 대비 이득이 크지 않다는 doc/0493의
  판단에 따름. 파일명과 실제 표시 이름(아이로나이트 광석/페트로나이트)이 다르다는 점에 유의.
- 유닛/건물 생산·건설 비용(`UnitDataSO`/`BuildingDataSO`의 `mineral`/`gas` 필드)이 화면에 숫자로만
  표시되는 부분(커맨드 패널 툴팁 등)은 이번 작업 범위 밖 - `ResourceManager`의 원시 int 값을 그대로
  쓰고, 이름/설명이 필요한 곳(Info Panel)에서만 `GetName()`/`GetDescription()`을 거친다.

## 관련 문서
- [`doc/0493`](../doc/0493-ore-gas-resource-concept.md) — 명칭/컨셉 확정 제안
- [`doc/0494`](../doc/0494-ore-gas-resource-localization.md) — 로컬라이제이션 구현 + 검증 기록
- [`ResourceManager.md`](ResourceManager.md) / [`ResourceNode.md`](ResourceNode.md) — 스크립트별 상세 문서
