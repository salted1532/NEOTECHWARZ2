# 0045. 인구수 증가를 maxpopulationamount 필드로 교체 + 200 한도 적용

**날짜:** 2026-07-09

## 요청 내용
> 이제 환불은 잘되네 내가 BuildingDataSO에다가 maxpopulationamount라고 최대 인구수를 추가해주는 수치를 넣었는데 메인기지, 보급고 이 2가지가 인구수를 늘려주는데 최대 인구수는 200으로 하고 인구수 증가 관련된 내용 구현해줘

[[0043-resource-manager-wiring|0043]]에서는 인구수 한도 증가/반환에 `BuildingData.population` 필드를 그대로 재사용했는데, 사용자가 직접 `BuildingData`에 전용 필드 `maxpopulationamount`를 추가해두셨음(확인함: `BuildingDataSO.cs`에 이미 존재) — 이제 이 필드를 실제로 쓰도록 교체하고, 인구수 한도 자체에 200 상한을 추가.

## 변경 내용
- `ResourceManager.cs`: `[SerializeField] private int maxPopulationCap = 200;` 추가, `AddMaxPopulation()`이 `maxPopulation += amount` 대신 `Mathf.Min(maxPopulationCap, maxPopulation + amount)`로 200을 넘지 않도록 클램프.
- `BaseStructure.cs`(`CompleteConstruction()`): `rtsController?.AddMaxPopulation(data.population)` → `data.maxpopulationamount`로 교체.
- `RTSUnitController.cs`(`RemoveMaxPopulationForBuilding()`): `resourceManager.RemoveMaxPopulation(data.population)` → `data.maxpopulationamount`로 교체.

## 참고
- `BuildingData.population` 필드는 그대로 남아있음(다른 용도로 쓰일 수 있어 손대지 않음) — 인구수 한도 증가는 이제 전적으로 `maxpopulationamount`만 사용.
- 200 상한은 `ResourceManager` 인스펙터에서 `Max Population Cap` 값으로 조정 가능(하드코딩 아님).

## 변경된 파일
- `Assets/Scripts/Resource/ResourceManager.cs`
- `Assets/Scripts/Building/BaseStructure.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
