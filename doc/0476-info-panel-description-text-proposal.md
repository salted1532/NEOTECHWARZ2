# 0476 - Info Panel 선택 유닛/건물 설명(description) 표시

## 추가 요청 및 변경
최초 제안 후 사용자가 "기존 description은 사실 생산/건설 버튼 호버 툴팁용이었다"고 알려줘서 설계를
변경함 — 기존 `description` 필드(버튼 호버 툴팁)는 그대로 두고, **Info Panel 전용 새 필드
`infoDescription`**을 `UnitData`/`BuildingData`에 추가해서 분리함. 36개 항목(유닛 21 + 건물 15) 전부
새 영어 설명 문구를 직접 작성해 SO 에셋에 채워넣음 — NTA 쪽 기존 UI 텍스트(유닛명/버튼 문구)가 전부
영어라 그 컨벤션을 따름.

**결과**: 컴파일 통과, 유니티에서 36/36 `infoDescription` non-empty + `UIController.infoText` →
`InfoText`(TextMeshProUGUI) 연결까지 전부 확인 완료.

## (아래는 최초 제안 — 이후 위 변경사항 반영해서 실제로는 `description`이 아니라 `infoDescription`을
## 새로 만들어 연결함)

## 요청 내용
"info_panel의 선택한 건물, 유닛의 설명을 infoText에다가 넣어줘"

## 조사 내용

### `infoText`는 이미 씬에 존재함(미사용 상태)
`Assets/prefabs/Game/GameManager.prefab`에 `InfoText`라는 이름의 `TextMeshProUGUI` 오브젝트가 이미
있고, 계층 구조를 따라가보니 `Canvas > SelectInfo > Info_panel`의 **직계 자식**으로, `infoIcon`/
`infoNameText`/`infoHpText`와 같은 부모(Info_panel) 밑에 나란히 있음 — 자리는 맞게 잡혀 있는데
`UIController.cs`의 어떤 `[SerializeField]` 필드에도 연결이 안 돼 있어서(코드에서 `infoText`/
`InfoText`를 검색해도 매치 없음) 계속 "New Text" 플레이스홀더만 떠 있는 상태였음. 즉 이번 작업은
UI를 새로 만드는 게 아니라 **이미 있는 빈 슬롯을 연결 + 데이터 공급**만 하면 됨.

### `UnitData`/`BuildingData`엔 이미 `description` 필드가 있음
`UnitDataSO.cs`/`BuildingDataSO.cs`의 `UnitData.description`/`BuildingData.description`은 이미
존재하고 SO 에셋에도 값이 채워져 있음(예: OC Unit Data SO의 Cyborg Soldier "OC의 자원 채집/건설
담당 나노봇." 등). 다만 지금은 툴팁(`description` → `TooltipUI`/`ObjectiveTextUtil`)에서만 쓰이고,
Info Panel에는 전달되는 경로가 없음.

### 아이콘과 동일한 패턴으로 붙일 수 있음
`GetIcon()`이 있는 5개 컨트롤러가 전부 "생성/스폰 시 `data.Icon`을 캐싱해뒀다가 `GetIcon()`으로
꺼내주는" 동일한 패턴을 쓰고 있어서, `description`도 옆에 나란히 캐싱하면 됨:

| 클래스 | 현재 icon 캐싱 위치 | 비고 |
|---|---|---|
| `UnitController.cs` | `ApplyUnitData(UnitData data)` (2085줄) | 생산 시 스탯 적용할 때 |
| `EnemyUnitController.cs` | 초기화 시 `icon = data.Icon` (645줄) | |
| `AllyController.cs` | 초기화 시 `icon = data.Icon` (662줄) | |
| `EnemyBuildingController.cs` | `Start()`에서 `icon = data.Icon` (143줄) | `buildingName = data.Name`도 같이 |
| `BuildingController.cs` | **없음** — icon이 SO가 아니라 프리팹에 수동 할당(doc/0475에서 이미 확인) | `BuildingData` 조회 자체는 `Start()`에 있지만 "건설 흐름을 안 거친 건물"일 때만 인구수 보정용으로 조회함 — description은 항상 필요하므로 이 조회를 조건 없이 실행하도록 살짝 고쳐야 함 |

`RTSUnitController.cs`가 이 5개 컨트롤러의 `GetIcon()`을 `uIController.ShowInfoPanel(...)` 호출부에
그대로 넘기고 있으므로, `GetDescription()`도 같은 자리에 인자로 추가하면 됨.

### 자원 노드/미션 아이템/건설 중 건물은 범위 밖
`ResourceNode`/`MissionItem`은 `description` 데이터 자체가 없음(요청도 "건물, 유닛"으로 한정).
건설 중인 `BaseStructure`(파운데이션)는 완공될 건물의 아이콘만 프리팹에서 미리 읽어오는 방식이라
description을 추가하려면 별도 로직이 더 필요함 — 이번 범위에서 제외하고, `ShowBaseStructureInfoPanel`/
`ShowResourceInfoPanel` 호출부는 그냥 빈 문자열로 둬서(infoText가 이전 선택의 설명을 그대로 보여주는
잔상 방지) 텍스트만 비움.

## 변경 계획

### 1. `Assets/Scripts/UI/UIController.cs`
- `infoHpText` 옆에 필드 추가: `[SerializeField] private TextMeshProUGUI infoText;`
- `ShowInfoPanel(Sprite, string, HealthManager)` / `ShowInfoPanel(Sprite, string, HealthManager, int, int, AttackEffectType, ArmorType, SizeType, int)` 둘 다 끝에 `string description = ""` 매개변수 추가(디폴트값이라 기존 호출부는 안 건드려도 컴파일됨), `infoText.text = description;` 반영
- `ShowResourceInfoPanel(...)` / `ShowBaseStructureInfoPanel(...)`도 동일하게 `string description = ""` 추가해서 최소한 이전 값이 안 남게 함

### 2~6. 5개 컨트롤러에 `description` 필드 + `GetDescription()` 추가
`UnitController.cs`, `EnemyUnitController.cs`, `AllyController.cs`, `EnemyBuildingController.cs`는
위 표의 icon 캐싱 지점 옆에 한 줄씩만 추가. `BuildingController.cs`는 `Start()`의 `BuildingData` 조회를
"건설 흐름 안 거친 건물일 때만"이 아니라 항상 실행하도록 살짝 재구성.

### 7. `Assets/Scripts/System/RTSUnitController.cs`
5개 `ShowInfoPanel(...)` 호출부(유닛/건물/적유닛/아군유닛/적건물, 1863·1884·1987·2005·2024줄)
끝에 각각 `.GetDescription()` 인자 추가.

### 8. `Assets/prefabs/Game/GameManager.prefab`
`UIController` 컴포넌트의 직렬화 필드에 `infoText: {fileID: 847192614712833648}` 한 줄 추가해서
기존 InfoText 오브젝트를 연결(같은 프리팹 안이라 guid 없이 fileID만).

## 참고
`InfoText`의 현재 RectTransform 크기(200×50, localScale 0.3)가 설명문 길이에 비해 작을 수 있음 —
일단 연결만 하고 실제 표시 결과를 보면서 크기/워드랩은 필요하면 별도로 조정하는 게 나을 듯.

이대로 진행할까요?
