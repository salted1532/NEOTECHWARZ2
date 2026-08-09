# 0490 - 미션 오브젝트(유물/연구 데이터베이스) 이름·설명 번역 제안

## 요청 내용
"유물이랑 연구데이터의 경우도 번역이 필요해 Artifact랑 리서치 데이터베이스 이런식으로 영어로
번역해주고 해당 설명도 추가해줘 설명에는 유물 -> 외계종족의 에너지원인거 같다 뭐 신비한 느낌이
난다 이런식으로 적어주고 연구 데이터베이스는 OC가 연구하는 신식 무기에 대한 연구 데이터 라는
식으로 설명 추가해줘"

## 조사 내용
`MissionItem.cs`(`Assets/Scripts/System/MissionItem.cs`)가 `Artifact.prefab`/`Database.prefab`
양쪽에 붙어있는 공용 컴포넌트. 이름은 `itemName` 인스펙터 필드 하나뿐이고 설명 필드는 아직 없음:

- `Artifact.prefab` : `itemName = "외계 유물"`
- `Database.prefab` : `itemName = "OC 연구 데이터"`

선택 시 Info Panel 표시는 `RTSUnitController.cs:2087`:
```csharp
uIController.ShowInfoPanel(selectedMissionItem.GetIcon(), selectedMissionItem.GetItemName(), null);
```
`ShowInfoPanel(icon, name, health, description = "")` 오버로드가 이미 설명 인자를 받으므로
(`UIController.cs:641`), 설명을 안 넘겨서 지금은 항상 빈 값.

기존 SO 유닛/건물 번역(doc/0487)은 `unit.<faction>.<ID>.name/.desc/.info` 키 스킴과
`LocalizationManager.GetTextOrFallback(key, fallback)`(키 없음/매니저 없음/예외 시 원본 인스펙터
값 그대로 표시하는 안전장치, doc/0487)을 사용. 미션 오브젝트는 SO가 아니라 프리팹에 직접 값이
박혀있는 케이스라 doc/0489(미션명)와 동일한 패턴 - 새 키 스킴
`missionitem.<id>.name` / `missionitem.<id>.desc`로 통일 (`id` = `artifact` / `researchdata`,
두 프리팹을 구분하기 위해 `MissionItem`에 `itemID` 필드 신규 추가).

## 코드 변경 (제안)

### `Assets/Scripts/System/MissionItem.cs`
**기존 코드**
```csharp
public class MissionItem : MonoBehaviour
{
    [SerializeField] private string itemName;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject selectionMarker; // 선택 시 표시할 마커 (없으면 그냥 표시 없이 선택만 됨)
    ...
    public Sprite GetIcon() => icon;
    public string GetItemName() => itemName;
}
```
**변경 코드**
```csharp
public class MissionItem : MonoBehaviour
{
    [SerializeField] private string itemID; // 로컬라이제이션 키 구분용 (예: "artifact", "researchdata")
    [SerializeField] private string itemName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject selectionMarker; // 선택 시 표시할 마커 (없으면 그냥 표시 없이 선택만 됨)
    ...
    public Sprite GetIcon() => icon;

    public string GetItemName() =>
        LocalizationManager.GetTextOrFallback($"missionitem.{itemID}.name", itemName);

    public string GetDescription() =>
        LocalizationManager.GetTextOrFallback($"missionitem.{itemID}.desc", description);
}
```

### `Assets/Scripts/System/RTSUnitController.cs` (2087번 줄)
**기존 코드**
```csharp
uIController.ShowInfoPanel(selectedMissionItem.GetIcon(), selectedMissionItem.GetItemName(), null);
```
**변경 코드**
```csharp
uIController.ShowInfoPanel(selectedMissionItem.GetIcon(), selectedMissionItem.GetItemName(), null,
    selectedMissionItem.GetDescription());
```

### 프리팹 인스펙터 값 (신규 필드 채우기)
- `Assets/prefabs/MissionObject/Artifact.prefab` : `itemID: artifact`, `description`(KO 원문, 아래
  번역표의 KO 값)
- `Assets/prefabs/MissionObject/Database.prefab` : `itemID: researchdata`, `description`(KO 원문)

### `Assets/Resources/Localization/ko.json` / `en.json` (신규 키 4개씩)
| key | ko.json | en.json |
|---|---|---|
| `missionitem.artifact.name` | 외계 유물 | Artifact |
| `missionitem.artifact.desc` | 외계종족의 에너지원으로 추정되는 신비로운 유물이다. 정체를 알 수 없는 기운이 감돈다. | A mysterious artifact believed to be an alien race's energy source, radiating an otherworldly aura. |
| `missionitem.researchdata.name` | OC 연구 데이터 | Research Database |
| `missionitem.researchdata.desc` | OC가 연구 중인 신형 무기에 대한 연구 데이터다. | Research data on a new weapon the OC is developing. |

(이름 쪽 ko 값은 프리팹 원문과 동일하게 유지 - 실질적으로는 안전장치 폴백과 같은 값이지만, 다른
언어 전환 시에도 명시적으로 키가 존재하도록 통일)

## 영향받는 파일
- `Assets/Scripts/System/MissionItem.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/prefabs/MissionObject/Artifact.prefab`
- `Assets/prefabs/MissionObject/Database.prefab`
- `Assets/Resources/Localization/ko.json`
- `Assets/Resources/Localization/en.json`

## 확인 예정
구현 승인 시: 컴파일 확인, JSON 키 집합 일치·중복 없음 확인. Play Mode에서 EN/KR 전환하며 Info
Panel에 이름+설명이 정상 표시되는지, ko.json에서 해당 키를 지웠을 때 안전장치(프리팹 원문 표시)가
동작하는지 확인 권장.
