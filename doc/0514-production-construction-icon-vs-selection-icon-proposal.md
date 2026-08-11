# 0514 - 생산/건설 버튼 아이콘과 선택 아이콘 분리 제안

## 요청 내용
"건물 건설 가능/불가능 버튼 가시성 개선을 할 건데 유닛(샤프슈터 등)도 해당하는 내용이다. 그래서
건물 건설·유닛 생산 버튼에 쓰는 아이콘(건설 건물 아이콘/생산 유닛 아이콘)을, 선택 시 나오는 아이콘과
분리하고 싶다. 해당 스크립트 오브젝트에 생산/건설 아이콘 필드를 따로 만들어주면 내가 거기에 이미지를
넣겠다. 선택 아이콘은 기존 아이콘을 그대로 쓰면 된다."

## 조사 내용

### 현재 아이콘 구조
`UnitData`/`BuildingData` (`UnitDataSO.cs`/`BuildingDataSO.cs`)에는 `Icon` 필드가 하나뿐이고,
유닛과 건물이 서로 다른 방식으로 쓰고 있었다.

**유닛 (문제 있음 - 하나의 필드를 생산 버튼과 선택 아이콘이 공유)**
- 생산 버튼: `RTSUnitController.ShowUnitTierPanel()` (`data.Icon`, 1352행)
- 생산 대기열 슬롯: `UIController.UpdateQueue()` (`data.Icon`, 491행)
- 선택 시 아이콘(Squad Panel/Info Panel): `UnitController.icon = data.Icon`(2093행), `AllyController`(667행), `EnemyUnitController`(664행)가 전부 같은 `Icon` 값을 읽어다 씀

→ 생산 버튼용으로 "비활성 시각 개선"을 위해 다른 이미지를 넣으면, 선택했을 때 보이는 아이콘도 같이 바뀌어버림.

**건물 (이미 분리는 되어 있으나 SO에 없음)**
- 건설 버튼: `UIController`에 `commandCenterIcon`/`supplyDepotIcon`/`barracksIcon`/`factoryIcon`/`airportIcon`/`labIcon` 6개가 **하드코딩된 별도 Sprite 필드**로 존재 (212~218행). `RTSUnitController`의 `BuildMode` 케이스(2124행)가 부르는 `ShowBuildPanel(...)` 고정 시그니처 오버로드(1207행)가 이 필드들을 그대로 사용.
- 선택 시 아이콘: `BuildingController.icon`/`EnemyBuildingController.icon`이 `BuildingData.Icon`을 사용.
- 즉 건물은 "건설 버튼 아이콘"과 "선택 아이콘"이 이미 서로 다른 곳(UIController 필드 vs SO)에 있어서 안 섞임. 다만 건설 버튼 아이콘이 SO가 아니라 UIController 인스펙터에 박혀있어서, 요청하신 "해당 스크립트 오브젝트에 이미지를 넣는" 방식이 안 됨.
- 참고로 `UIController.cs` 220~221행 주석: "유닛 생산 패널 아이콘은 더 이상 여기 고정 필드로 두지 않고, UnitData.Icon을 그대로 사용한다 - 새 유닛을 추가해도 이 파일을 건드릴 필요가 없도록" — 예전에 유닛은 이미 이 방향으로 한 번 옮겨졌던 이력이 있음. 건물만 아직 안 옮겨진 상태.
- 사용되지 않는 다른 오버로드 `ShowBuildPanel(CommandButtonData[] buildingCommands, Action onCancel)` (394행)이 이미 존재함 - 유닛 생산 패널과 동일한 데이터 기반 패턴으로 바꾸는 데 그대로 재사용 가능.

## 제안 (두 가지 범위 중 선택)

### 공통: SO에 필드 추가
- `UnitData`: `Icon` 옆에 `ProductionIcon` 추가 (생산 버튼/생산 대기열 전용)
- `BuildingData`: `Icon` 옆에 `ConstructionIcon` 추가 (건설 버튼 전용)
- 두 경우 다 기존 `Icon`은 그대로 "선택 시 보여주는 아이콘"으로 유지 (요청하신 대로 손 안 댐)

### 유닛 쪽은 범위 상관없이 공통으로 진행
- `RTSUnitController.ShowUnitTierPanel()` 1352행: `data.Icon` → `data.ProductionIcon`
- `UIController.UpdateQueue()` 491행: `data.Icon` → `data.ProductionIcon`
- 선택 아이콘 경로(`UnitController`/`AllyController`/`EnemyUnitController`)는 무변경

### 건물 쪽 - 옵션 A (최소 변경)
`BuildingData.ConstructionIcon` 필드만 추가하고 끝. **주의**: 실제 건설 버튼은 여전히
`UIController`의 기존 6개 하드코딩 필드(`commandCenterIcon` 등)를 읽으므로, `ConstructionIcon`에
이미지를 넣어도 건설 버튼에는 반영되지 않음 (필드만 준비, 배선은 나중에).

### 건물 쪽 - 옵션 B (완전 배선, 권장)
유닛과 동일한 데이터 기반 패턴으로 통일:
1. `RTSUnitController`의 `BuildMode` 케이스(2124~2138행)를 `ShowUnitTierPanel()`처럼 `buildingDatabase`를 순회해 `CommandButtonData[]`를 만들고, 이미 존재하는 `ShowBuildPanel(CommandButtonData[], Action)` 오버로드(394행)를 호출하도록 변경.
2. 각 버튼의 상호작용 가능 여부는 `IsBuildingPrerequisiteMet(data.ID)`로 통일 계산 (현재 Factory/Airport만 개별 체크하던 것도 일반화됨).
3. 건물별 단축키(C/S/B/F/P/L)는 `UnitData.shortcutKey`처럼 SO에 넣기보다, 기존 리터럴 그대로 `RTSUnitController` 안의 작은 `BuildingID → KeyCode` 매핑으로 유지 (SO 스키마 확장은 이번 요청 범위 밖).
4. 이제 안 쓰게 되는 `UIController`의 `commandCenterIcon` 등 6개 필드 + 고정 시그니처 `ShowBuildPanel(ButtonAction, ...)` 오버로드(1207~1230행)는 삭제 (죽은 코드 정리).

옵션 B가 요청하신 "해당 스크립트 오브젝트에 이미지를 넣으면 실제로 반영된다"에 맞고, 유닛과 구조가
대칭이 되어 이후 "건설 가능/불가능 가시성 개선" 작업도 건물/유닛 양쪽에 같은 방식으로 적용하기 쉬워짐.
다만 `UIController`/`RTSUnitController`의 `BuildMode` 관련 코드를 같이 손대야 해서 옵션 A보다 diff가 큼.

## 결과
사용자가 옵션 B(완전 배선)를 선택해 그대로 적용함.

### 변경 내용
- `UnitDataSO.cs`: `UnitData`에 `ProductionIcon` 필드 추가 (기존 `Icon`은 선택 아이콘으로 유지)
- `BuildingDataSO.cs`: `BuildingData`에 `ConstructionIcon` 필드 추가 (기존 `Icon`은 선택 아이콘으로 유지)
- `RTSUnitController.ShowUnitTierPanel()`: 생산 버튼 아이콘을 `data.Icon` → `data.ProductionIcon`으로 변경
- `UIController.UpdateQueue()`: 생산 대기열 슬롯 아이콘을 `data.Icon` → `data.ProductionIcon`으로 변경
- 선택 아이콘 경로(`UnitController`/`AllyController`/`EnemyUnitController`/`BuildingController`/`EnemyBuildingController`)는 무변경 - 계속 `Icon` 사용
- `RTSUnitController`: `ShowUnitTierPanel()`과 동일한 캐싱 패턴으로 새 `ShowBuildModePanel()` 추가 - `buildingDatabase`를 순회해 각 건물의 `ConstructionIcon`/`IsBuildingPrerequisiteMet()`로 `CommandButtonData[]`를 구성. 건물별 단축키(C/S/B/F/P/L)는 SO에 필드를 늘리지 않고 `BuildPanelShortcuts` 딕셔너리로 매핑. `BuildMode` 케이스가 이 메서드를 호출하도록 변경.
- `UIController`: 하드코딩됐던 `commandCenterIcon`/`supplyDepotIcon`/`barracksIcon`/`factoryIcon`/`airportIcon`/`labIcon` 필드 6개와, 이를 쓰던 고정 시그니처 `ShowBuildPanel(ButtonAction, ...)` 오버로드 삭제. 남은 `ShowBuildPanel(CommandButtonData[], ButtonAction onCancel)` 오버로드(및 `AddCancelCommand`)는 취소 버튼 툴팁/단축키(T)를 그대로 유지하기 위해 `onCancel` 매개변수를 `Action`에서 `ButtonAction`으로 변경.
- `npx uloop-cli compile` 성공 확인 (Error 0 / Warning 0)

### 사용자가 채워야 할 것
- 각 `UnitDataSO`/`BuildingDataSO` 에셋의 새 `ProductionIcon`/`ConstructionIcon` 필드에 이미지 연결 (비워두면 해당 버튼 아이콘이 안 보임 - 기존 `Icon`과는 별개 필드이므로 자동으로 채워지지 않음)

## 후속 - NTA 아이콘 연결 (이미지 추가 완료)
사용자가 `Assets/images/Unit/NTA/*_생산.png`(9개), `Assets/images/Building/NTA/*_건설.png`(6개)를 추가함에 따라
`NTA Unit Data SO.asset`/`NTA Building Data SO.asset`의 `ProductionIcon`/`ConstructionIcon` 필드에 연결함
(doc/0474와 동일한 방식 - 신규 이미지가 Sprite Mode Multiple이라 각 `.meta`의 `nameFileIdTable` 해시로 fileID 조회).

| 유닛 (ID) | ProductionIcon |
|---|---|
| Worker Drone (1) | 워커드론_생산.png |
| Assault Trooper (2) | 어썰트트루퍼_생산.png |
| Scout Drone (3) | 스카웃드론_생산.png |
| Sharpshooter (4) | 샤프슈터_생산.png |
| Ranger IFV (5) | IFV레인저_생산.png |
| Pulsar Tank (6) | 펄스탱크_생산.png |
| SkyLancer (7) | 스카이랜서_생산.png |
| Firehawk (8) | 파이어호크_생산.png |
| Guardian Drone (9) | 가디언드론_생산.png |

| 건물 (ID) | ConstructionIcon |
|---|---|
| CommandCenter (1) | 커맨드센터_건설.png |
| SupplyDepot (2) | 보급고_건설.png |
| Barracks (3) | 병영_건설.png |
| Factory (4) | 공장_건설.png |
| Spaceport (5) | 공항_건설.png |
| Lab (6) | 연구소_건설.png |

`AssetDatabase.Refresh()` 후 15개 항목(유닛 9 + 건물 6) 전부 `ProductionIcon`/`ConstructionIcon` non-null +
파일명 스팟체크로 확인함, import 에러 없음.

**남은 것**: OC/Spore Brood 진영은 `_생산`/`_건설` 접미사 이미지가 아직 추가되지 않아서(폴더에 기존 선택용
아이콘만 있음) 이번엔 건드리지 않음 - 필요하면 이미지 추가 후 동일한 방식으로 연결.
