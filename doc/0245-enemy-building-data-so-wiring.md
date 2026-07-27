# 0245 - 적 건물이 OC Building Data SO에서 체력/이름을 읽어오도록 수정

## 요청

적 건물의 체력/이름 등 정보를 프리팹마다 직접 입력하지 말고, [[0230]]에서 만들어둔 "OC Building Data
SO"(`EnemyBuildingDataSO`)에서 가져오도록 해달라는 요청 - `EnemyUnitController`가 `enemyUnitID`로 OC
Unit Data SO를 조회하는 것과 동일한 패턴([[0232]]).

## 문제: BuildingData에는 애초에 체력/아이콘 필드가 없었음

`UnitData`(유닛용)와 달리 `BuildingData`(건물용) 클래스에는 `hp`나 `Icon` 필드가 아예 없었음 - 아군
건물(`BuildingController`)이 지금까지 체력을 SO가 아니라 프리팹의 `HealthManager`에 직접 박아두는
방식만 써왔기 때문. 그래서 우선 `BuildingData`에 두 필드를 추가함.

## 수정 내용

**`Assets/Scripts/ScriptableObject/BuildingDataSO.cs`**
- `BuildingData`에 `hp`, `Icon` 필드 추가 (아군 쪽은 아직 이 값을 안 쓰고, `EnemyBuildingController`
  전용으로 사용 - 기존 아군 `New Building Data SO.asset`에는 굳이 값을 채우지 않아도 무해함, 기본값
  0/null로 남아도 아무도 안 읽으므로 영향 없음)

**`Assets/Scripts/System/RTSUnitController.cs`**
- `enemyBuildingDatabase`(`EnemyBuildingDataSO`) 필드 추가
- `GetEnemyBuildingData(int enemyBuildingID)` 추가 (`GetEnemyUnitData`와 동일한 패턴)

**`Assets/Scripts/Enemy/EnemyBuildingController.cs`**
- `enemyBuildingID` 필드 추가 (OC Building Data SO의 `BuildingData.ID`와 매칭)
- `Start()`에서 `rtsController.GetEnemyBuildingData(enemyBuildingID)`로 조회한 값을 `ApplyBuildingData()`에
  넘겨서 스스로 이름/아이콘/체력을 적용
- `ApplyBuildingData(BuildingData data)` 추가 - `icon`/`buildingName`을 덮어쓰고
  `HealthManager.InitializeHealth(data.hp)` 호출

**`Assets/Scripts/ScriptableObject/OC Building Data SO.asset`**
- 6개 건물 전부 `hp` 값을 NTA 대응 건물과 동일하게 채움(대칭 밸런스 유지, [[0230]] 원칙과 동일):
  Omega Core 1500(MainBase), Cargo Silo 500(SupplyDepot), Cyber Foundry 1000(Barracks），
  Mech Yard 1250(Factory), Drone Hangar 1300(Spaceport), Neural Lab 850(Lab). `Icon`은 아직 아이콘
  에셋이 없어서 전부 비워둠(fileID: 0).

## 에디터에서 확인해야 하는 부분

[[0232]]와 동일하게, 씬의 `RTSUnitController` 인스펙터에 새로 생긴 **`Enemy Building Database`** 필드에
`OC Building Data SO`를 연결해야 하고, 각 적 건물 프리팹의 `Enemy Building ID`를 SO의 ID(Omega Core=1
~ Neural Lab=6)에 맞게 지정해야 실제로 값이 적용된다.

## 변경 파일

- `Assets/Scripts/ScriptableObject/BuildingDataSO.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/Enemy/EnemyBuildingController.cs`
- `Assets/Scripts/ScriptableObject/OC Building Data SO.asset`
