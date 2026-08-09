# 0502 - 해상도 드롭다운 에디터 동작 설명 + Screen Resolution 라벨 번역

## 요청 내용
"일단 모든 해상도들을 드롭다운에 다 들어간걸 확인했고 선택시 해상도는 안변하는거 같아 에디터
상에서 확인했을때 현재 해상도 Free Aspect로 설정하고 드롭다운으로 선택해봤는데 변경되지 않아.
그리고 내가 Screen Resolution_Text라는 label를 하나더 추가했는데 이건 이 버튼이 무슨 역할인지
알려주는 라벨이거든 해상도 : Screen Resolution 이렇게 한글 영어로 변역을 넣으면 될거야 이거까지
수정해줘"

## 1. "드롭다운에서 골라도 해상도가 안 바뀐다" - 버그 아님, 에디터의 정상 동작

`Screen.SetResolution()`은 **Unity 에디터의 Play 모드에서는 Game View 창 크기를 바꾸지 않는다** -
Unity 자체 제약. Game View 창 크기는 에디터 UI(Game 탭의 해상도 드롭다운, `Free Aspect` 등)가
따로 관리하고, `Screen.SetResolution`은 실제 빌드된 스탠드얼론(.exe) 창에서만 창 크기를 바꾼다.

즉 지금 확인한 "드롭다운 옵션은 다 들어갔는데 선택해도 화면이 안 바뀐다"는 정확히 예상되는
동작이고, `GraphicsSettingsPanel.cs` 코드에는 문제가 없음. 실제로 해상도가 바뀌는지 확인하려면
프로젝트를 빌드해서(File > Build Settings > Build) 나온 `.exe`를 직접 실행해야 함. 코드 수정은
필요 없음.

## 2. Screen Resolution_Text 라벨 번역

사용자가 `OptionPanel.prefab`에 "Screen Resolution_Text"라는 `TextMeshProUGUI` 라벨(현재
하드코딩된 텍스트 "Screen Resolution")을 새로 추가함. 기존 로컬라이제이션 컨벤션
(`settings.<카테고리>.<항목>`, doc/0483에서 미리 준비해둔 `settings.graphics.*` 자리)을 그대로
따라서 처리:

- `OptionPanel.prefab`의 "Screen Resolution_Text" GameObject(fileID 2185865900666878347)에
  `LocalizedText` 컴포넌트 추가, `target`을 같은 오브젝트의 `TextMeshProUGUI`(fileID
  7869858347054764862)에 연결, `key: settings.graphics.resolution`.
- `en.json`: `"settings.graphics.resolution": "Screen Resolution"`
- `ko.json`: `"settings.graphics.resolution": "해상도"`

기존 오디오 라벨들과 완전히 같은 패턴 (`LocalizedText.cs` 상단 주석에 미리 정리된 컨벤션 그대로
따름) - 코드 수정 없이 프리팹 + json 두 줄만 추가.

## 적용 결과
- `Assets/prefabs/UI/OptionPanel.prefab`: LocalizedText 컴포넌트 추가 완료 (새 fileID
  `8100000000000000001` - 이번엔 Int64 범위 안에서 확인 후 사용).
- `Assets/Resources/Localization/en.json`, `ko.json`에 `settings.graphics.resolution` 키 추가.
- Unity 에디터에서 검증 완료 (Play Mode 없이 정적 확인만):
  - 컴파일: 에러 0, 경고 0.
  - `Screen Resolution_Text` GameObject에 컴포넌트 4개(`RectTransform, CanvasRenderer,
    TextMeshProUGUI, LocalizedText`) 확인, `[MISSING SCRIPT]` 없음.
  - `LocalizedText.target`이 같은 오브젝트의 `TextMeshProUGUI`를 정확히 가리킴, `key ==
    "settings.graphics.resolution"` 확인.
  - `en.json`/`ko.json` 둘 다 `JSON.parse`로 유효성 확인(각 182개 항목, trailing comma 없음),
    새 키의 값도 의도대로("Screen Resolution" / "해상도") 들어감.
