# 0605 — 미션 선택 씬 툴팁에 행성 이름 표시

날짜: 2026-08-18

## 요청 내용

미션 선택 씬에서 `Docs/Campaign.md`에 정리된 행성 이름이 (미션 0~5 기준) 함께 뜨도록 해달라는 요청.
서브미션은 아직 미션 선택 씬에 별도 버튼/씬이 없으므로, 서브미션이 실제로 구현된 뒤에 연결하기로 하고
이번에는 제외.

## 조사 내용

- `Assets/Scripts/UI/MissionSelectManager.cs`의 `SetupHoverTooltip()`이 버튼 호버 시
  `TooltipUI.Instance.Show(rect, "<미션이름>", 서브타이틀)`을 호출한다. 서브타이틀은 기존
  `missionselect.tooltip.subtitle` 키("미션 {0}" / "Mission {0}")를 그대로 쓰고 있었음.
- 미션 이름은 `missionselect.name.{미션번호}` 로컬라이제이션 키로 관리 중 (`ko.json`/`en.json`).
  같은 패턴으로 행성 이름도 `missionselect.planet.{미션번호}` 키를 추가.
- `LocalizationManager.GetText(string key)` (인자 없는 오버로드)를 그대로 사용, 새 필드/폴백 로직 불필요.
- `Docs/Campaign.md`에서 확정된 행성 이름(미션 0/1=데메테르, 2=아레스, 3=칼립소, 4=에리스,
  5=제우스 플랫폼)을 그대로 옮겨 씀.

## 코드 변경

### 1) 로컬라이제이션 키 추가

**`Assets/Resources/Localization/ko.json`** — `missionselect.name.5` 다음 줄에 추가:
```json
    { "key": "missionselect.planet.0", "value": "데메테르" },
    { "key": "missionselect.planet.1", "value": "데메테르" },
    { "key": "missionselect.planet.2", "value": "아레스" },
    { "key": "missionselect.planet.3", "value": "칼립소" },
    { "key": "missionselect.planet.4", "value": "에리스" },
    { "key": "missionselect.planet.5", "value": "제우스 플랫폼" },
```

**`Assets/Resources/Localization/en.json`** — 동일 위치:
```json
    { "key": "missionselect.planet.0", "value": "Demeter" },
    { "key": "missionselect.planet.1", "value": "Demeter" },
    { "key": "missionselect.planet.2", "value": "Ares" },
    { "key": "missionselect.planet.3", "value": "Calypso" },
    { "key": "missionselect.planet.4", "value": "Eris" },
    { "key": "missionselect.planet.5", "value": "Zeus Platform" },
```

### 2) `Assets/Scripts/UI/MissionSelectManager.cs` — 툴팁 서브타이틀에 행성 이름 덧붙이기

기존 코드 (`SetupHoverTooltip`):
```csharp
string missionName = LocalizationManager.GetTextOrFallback($"missionselect.name.{entry.missionNumber}", entry.missionName);
TooltipUI.Instance?.Show(rect, $"<{missionName}>", LocalizationManager.GetText("missionselect.tooltip.subtitle", entry.missionNumber));
```

변경 코드:
```csharp
string missionName = LocalizationManager.GetTextOrFallback($"missionselect.name.{entry.missionNumber}", entry.missionName);
string subtitle = LocalizationManager.GetText("missionselect.tooltip.subtitle", entry.missionNumber);
string planetName = LocalizationManager.GetText($"missionselect.planet.{entry.missionNumber}");
TooltipUI.Instance?.Show(rect, $"<{missionName}>", $"{subtitle} · {planetName}");
```

결과: 미션 1 버튼 호버 시 제목 `<국경 분쟁>`, 부제 `미션 1 · 데메테르` 표시(한국어), 영어는
`Mission 1 · Demeter`.

서브미션용 버튼/씬은 아직 `MissionSelectManager`의 `missions` 리스트에 없어 이번 변경에는 포함하지
않음 — 서브미션이 실제 씬/버튼으로 구현되면 그때 같은 방식(`missionselect.planet.{번호}` 키 확장
또는 별도 키 체계)으로 이어붙이면 됨.

## 검증

`npx uloop-cli compile` 실행 — `Success: true, ErrorCount: 0, WarningCount: 0` 확인.

## 요약/남은 작업

미션 선택 씬 툴팁에 행성 이름이 표시되도록 반영 완료. 서브미션 연결은 서브미션이 실제 구현된 이후
후속 작업으로 남겨둠.

## 변경된 파일

- `Assets/Resources/Localization/ko.json`
- `Assets/Resources/Localization/en.json`
- `Assets/Scripts/UI/MissionSelectManager.cs`
