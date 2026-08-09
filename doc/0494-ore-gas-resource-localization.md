# 0494 - Ore/Gas(아이로나이트 광석/페트로나이트) 로컬라이제이션 구현 (완료)

## 요청 내용
"로컬라이제이션 작업 진행해주고 각 광물에 간단한 설명도 추가해서 infotext에 추가해줘 영어,한글
버전으로 각각" - doc/0493에서 확정한 명칭(Ironite Ore/아이로나이트 광석, Petronite/페트로나이트)을
실제로 반영하고, 자원 노드 선택 시 Info Panel의 설명(infoText)에 짧은 로어 텍스트도 표시.

## 조사 내용
- `ResourceNode.cs`에는 `itemName`/`description` 필드가 아예 없음 - 이름은
  `RTSUnitController.cs:2070`에서 `selectedResourceNode.Type == ResourceType.Ore ? "Ore" : "Gas"`로
  하드코딩되어 있었음.
- `UIController.ShowResourceInfoPanel(icon, resourceName, remainingAmount)`
  (`UIController.cs:766`)은 `infoText.text = string.Empty`로 설명을 항상 비움 - "자원 노드는 설명
  데이터가 없음"이라는 주석이 있었음(이번에 데이터가 생기므로 주석과 함께 교체).
- `ResourceType` enum(Ore/Gas)이 이미 자원 종류를 구분하고 있고, 노드 인스턴스마다 이름/설명이
  달라질 이유가 없음(모든 Ore 노드, 모든 Gas 노드가 각각 동일한 이름/설명) - doc/0490의 MissionItem
  패턴과 달리 프리팹별 인스펙터 필드(itemID/description)를 새로 만들 필요 없이, `ResourceType`
  값으로 바로 로컬라이제이션 키를 만들면 됨. 프리팹 변경 없음.

## 코드 변경 (제안)

### `Assets/Scripts/Resource/ResourceNode.cs`
**기존 코드**
```csharp
public Sprite GetIcon() => icon;
```
**변경 코드**
```csharp
public Sprite GetIcon() => icon;

public string GetName() => resourceType == ResourceType.Ore
    ? LocalizationManager.GetTextOrFallback("resource.ore.name", "Ironite Ore")
    : LocalizationManager.GetTextOrFallback("resource.gas.name", "Petronite");

public string GetDescription() => resourceType == ResourceType.Ore
    ? LocalizationManager.GetTextOrFallback("resource.ore.desc",
        "A versatile metal ore refined into anything from armor plating to circuitry - the basic material behind everything NTA builds.")
    : LocalizationManager.GetTextOrFallback("resource.gas.desc",
        "Mined as a green crystal, but refining it yields a fuel as energy-dense as crude oil.");
```

### `Assets/Scripts/UI/UIController.cs` (766번 줄)
**기존 코드**
```csharp
public void ShowResourceInfoPanel(Sprite icon, string resourceName, int remainingAmount)
{
    ...
    if (infoText != null)
        infoText.text = string.Empty; // 자원 노드는 설명 데이터가 없음 - 이전 선택의 설명이 남지 않도록 비움
    ...
}
```
**변경 코드**
```csharp
public void ShowResourceInfoPanel(Sprite icon, string resourceName, int remainingAmount, string description = "")
{
    ...
    if (infoText != null)
        infoText.text = description;
    ...
}
```

### `Assets/Scripts/System/RTSUnitController.cs` (2066번 줄)
**기존 코드**
```csharp
if (selectedResourceNode != null)
{
    uIController.ShowResourceInfoPanel(
        selectedResourceNode.GetIcon(),
        selectedResourceNode.Type == ResourceType.Ore ? "Ore" : "Gas",
        selectedResourceNode.RemainingAmount);
}
```
**변경 코드**
```csharp
if (selectedResourceNode != null)
{
    uIController.ShowResourceInfoPanel(
        selectedResourceNode.GetIcon(),
        selectedResourceNode.GetName(),
        selectedResourceNode.RemainingAmount,
        selectedResourceNode.GetDescription());
}
```

### `Assets/Resources/Localization/ko.json` / `en.json` (신규 키 4개씩)
| key | ko.json | en.json |
|---|---|---|
| `resource.ore.name` | 아이로나이트 광석 | Ironite Ore |
| `resource.ore.desc` | 장갑판부터 회로까지 뭐든 가공할 수 있는 범용 금속광이다. NTA 생산 시설 어디서나 기초 재료로 쓰인다. | A versatile metal ore refined into anything from armor plating to circuitry - the basic material behind everything NTA builds. |
| `resource.gas.name` | 페트로나이트 | Petronite |
| `resource.gas.desc` | 초록빛 결정 형태로 채굴되지만, 정제하면 원유 못지않은 고에너지 연료를 뽑아낼 수 있는 자원이다. | Mined as a green crystal, but refining it yields a fuel as energy-dense as crude oil. |

## 영향받는 파일
- `Assets/Scripts/Resource/ResourceNode.cs`
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Resources/Localization/ko.json`
- `Assets/Resources/Localization/en.json`

## 확인 결과
- **JSON 키**: ko/en 각 181개, 중복 없음, 키셋 완전 일치(`resource.ore.name/.desc`,
  `resource.gas.name/.desc` 4개 포함).
- **컴파일**: `ErrorCount: 0` (경고 39개는 전부 프로젝트 전역의 기존 `FindFirstObjectByType` 계열
  deprecated API 경고 - 이번 변경과 무관, 기존부터 있던 것).
- **런타임 동작** (`uloop-cli execute-dynamic-code`로 Editor 내에서 `LocalizationManager`를 직접
  생성해 `Awake`/`LoadLanguage` 리플렉션 호출 후, `Ore.prefab`/`Gas.prefab`의 `ResourceNode`에서
  `GetName()`/`GetDescription()` 직접 호출):
  - EN: `Ironite Ore` / "A versatile metal ore refined into anything from armor plating to
    circuitry - the basic material behind everything NTA builds." / `Petronite` / "Mined as a
    green crystal, but refining it yields a fuel as energy-dense as crude oil."
  - KO: `아이로나이트 광석` / "장갑판부터 회로까지 뭐든 가공할 수 있는 범용 금속광이다. NTA 생산
    시설 어디서나 기초 재료로 쓰인다." / `페트로나이트` / "초록빛 결정 형태로 채굴되지만, 정제하면
    원유 못지않은 고에너지 연료를 뽑아낼 수 있는 자원이다."

프리팹 변경 없음(ResourceType enum 기반으로 코드에서 분기하므로 Ore.prabab/Gas.prefab 인스펙터
값을 건드릴 필요가 없었음). Play Mode에서 자원 노드를 실제로 클릭해 Info Panel에 시각적으로
표시되는지 확인하는 건 선택 사항으로 남겨둠(로직 자체는 위 검증으로 확인됨).
