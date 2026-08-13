# GraphicsSettingsPanel

`Assets/Scripts/UI/GraphicsSettingsPanel.cs`

## 개요

`OptionPanel` 프리팹의 "Screen Resolution" `TMP_Dropdown`에 연결되는 해상도 설정 컴포넌트(doc/0501). `Screen.resolutions`로 현재 모니터가 지원하는 해상도 목록을 채우고(가로x세로 기준 중복 제거, 높은 해상도부터 정렬), 선택 시 `Screen.SetResolution` + `PlayerPrefs` 저장을 함께 처리한다. `OptionPanel`이 쓰이는 모든 씬(`MainScene`, 인게임 `GameManager` 프리팹)에서 동일하게 동작.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `resolutionDropdown` | 연결할 `TMP_Dropdown` |
| `options` | `Screen.resolutions`에서 가로x세로 기준으로 합친 실제 표시 옵션 목록 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 이전에 저장한 해상도(`PlayerPrefs`)가 있고 현재 화면 크기와 다르면 즉시 적용 — 씬 로드 시점에 맞춰줌 |
| `Start()` | 드롭다운 옵션 구성 + 값 변경 리스너 등록 |
| `OnEnable()` | 패널이 다시 열릴 때(ESC 메뉴 재진입 등) 현재 해상도로 선택 인덱스 갱신 |
| `BuildOptions()` | 주사율만 다른 중복 해상도를 가로x세로 기준으로 병합해 드롭다운 옵션 생성 |
| `RefreshSelectedIndex()` | 저장된 해상도가 있으면 그 값을, 없으면 현재 `Screen.width/height`를 기준으로 드롭다운 선택 인덱스를 맞춤 — 에디터에서 씬 로드 직후 `Screen.width/height`가 아직 640x480(Game View 렌더 전 기본값)일 수 있어(doc/0502) 저장값을 우선 |
| `OnResolutionSelected(int)` | 드롭다운 선택 시 `Screen.SetResolution` 적용 + `PlayerPrefs` 저장 |
