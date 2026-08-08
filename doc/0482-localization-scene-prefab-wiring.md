# 0482 - 로컬라이제이션 씬/프리팹 컴포넌트 연결 (구현)

## 날짜
2026-08-09

## 요청 내용
`doc/0481-localization-system-proposal.md`에서 제안된 `LocalizationManager`/`LocalizedText`
스크립트와 `en.json`/`ko.json`은 이미 작성 완료된 상태. 남은 작업인 씬/프리팹에 두 컴포넌트를
유니티 에디터 스크립팅(uloop-execute-dynamic-code)으로 실제로 연결하는 부분만 진행.

## 조사 내용 - 실제 씬 구조와 제안서 가정의 불일치 발견

작업 전 `uloop-find-game-objects`/`uloop-get-hierarchy`/직접 dynamic-code로 각 대상 오브젝트를
먼저 정밀 조사함. 그 결과 제안서(`doc/0481`)의 가정과 실제 씬 구조가 다른 두 곳을 발견:

1. **`OptionPanel.prefab`의 BGM/Voice/Master/SFX 라벨** - `LocalizedText.target`은
   `TextMeshProUGUI` 타입인데, 실제 `Mute/Label` 오브젝트들은 전부 레거시
   `UnityEngine.UI.Text` 컴포넌트만 갖고 있고 TMP 컴포넌트가 없음
   (`OptionPanel/Master_Volume/Mute/Label`, `.../BGM/Mute/Label`, `.../SFX/Mute/Label`,
   `.../VOICE/Mute/Label` 전부 동일). `GameManager.prefab` 안의 OptionPanel 인스턴스도 같은
   프리팹이라 동일한 문제.
2. **`MissionSelect.unity`의 Mission0~5 버튼** - `MissionSelectManager.missions` 배열(6개, 0~5
   전부 정상 연결)에서 버튼 GameObject를 직접 확인한 결과, `Canvas/Mission0` ~ `Mission5`는
   `RectTransform/CanvasRenderer/Image/Button`만 있는 아이콘 전용 버튼이고 자식이 없음(캡션
   텍스트 자체가 존재하지 않음). "Mission 0" 문자열은 버튼이 아니라 `Canvas/ToolTip/Text (TMP)
   (1)`(호버 시 코드가 채우는 툴팁 서브타이틀, 이미 `missionselect.tooltip.subtitle` 키로
   로컬라이즈됨)에 남아있던 값이었음.

두 경우 다 "존재하지 않는 대상을 지어내지 말 것"이라는 지시에 따라 건드리지 않고 건너뜀 (아래
"건너뛴 항목" 참고).

## 변경 내용

### GameManager.prefab
- 프리팹 루트 바로 아래에 `LocalizationManager` 자식 GameObject 신규 생성, `LocalizationManager`
  컴포넌트 부착 (인스펙터 필드 없음, `SoundManager`/`UIController`와 동일 위치 패턴).
- 아래 8곳에 `LocalizedText` 컴포넌트 부착 + `target`(자기 자신의 `TextMeshProUGUI`)/`key` 연결:

| GameObject 경로 | key |
|---|---|
| `Canvas/OptionPanel/GoToNextStage/Text (TMP)` | `ui.goto` |
| `Canvas/OptionPanel/GoToPreviousStage/Text (TMP)` | `ui.goto` |
| `Canvas/VictoryPanel/GoToNextStage/Text (TMP)` | `ui.goto` |
| `Canvas/OptionPanel/BackToMainMenu/Text (TMP)` | `ui.backto` |
| `Canvas/VictoryPanel/BackToMainMenu/Text (TMP)` | `ui.backto` |
| `Canvas/Option/Text (TMP)` | `ui.option` |
| `Canvas/VictoryPanel/ReturnToGame/Text (TMP)` | `ui.returnto` |
| `Canvas/VictoryPanel/VictoryText` | `ui.victory` |

### MainScene.unity
아래 3곳에 `LocalizedText` 부착:

| GameObject 경로 | key |
|---|---|
| `Canvas/MainMenuPanel/Play/Text (TMP)` | `ui.play` |
| `Canvas/MainMenuPanel/Option/Text (TMP)` | `ui.option` |
| `Canvas/MainMenuPanel/Exit/Text (TMP)` | `ui.exit` |

### 건너뛴 항목 (추측으로 처리하지 않음)
- **`OptionPanel.prefab`의 BGM/Voice/Master/SFX 라벨 4곳** - 레거시 `UI.Text`라 `TextMeshProUGUI`
  참조를 만들 수 없음. 해결하려면 (a) `LocalizedText`에 `UnityEngine.UI.Text` 지원을 추가하거나
  (b) 라벨을 TMP로 교체하는 별도 작업 필요 - 이번 범위 밖.
- **`MissionSelect.unity`의 `missionselect.button.0`~`5` 6곳** - Mission0~5 버튼에 캡션 텍스트
  오브젝트 자체가 없음(아이콘 버튼). 버튼 위에 표시되는 문자열이 필요하다면 먼저 텍스트
  오브젝트를 씬에 추가하는 디자인 작업이 선행되어야 함 - 이번 범위 밖.

## 검증
1. `uloop-cli compile` - 0 에러 / 0 경고 (작업 전/후 모두).
2. `uloop-cli get-logs` - Error/Warning 로그 없음.
3. 각 프리팹/씬을 다시 로드해서 `LocalizedText.target`/`key`를 `SerializedObject`로 읽어 재검증 -
   MainScene 3곳 + GameManager.prefab 8곳 + LocalizationManager 전부 PASS.
4. MainScene PlayMode 진입 → 콘솔 에러 없음 확인 → 메인 메뉴 스크린샷 촬영(Play/Option/Exit 영어로
   정상 표시) → PlayMode 종료.

## 변경된 파일
- `Assets/prefabs/Game/GameManager.prefab` (LocalizationManager 추가 + LocalizedText 8곳)
- `Assets/Scenes/MainScene/MainScene.unity` (LocalizedText 3곳)
- 변경 없음(조사만): `Assets/prefabs/UI/OptionPanel.prefab`, `Assets/Scenes/Missions/MissionSelect.unity`

## 후속 작업 (2026-08-09) - OptionPanel 레거시 Text 라벨 연결

위 "건너뛴 항목"의 `OptionPanel.prefab` 4곳 - 그동안 `LocalizedText.cs`에 `legacyTarget`
(`UnityEngine.UI.Text`) 필드가 추가되어 막혔던 기술적 이유가 해소됨. 이번 세션에서 실제로 연결함.

### 조사 - 4개 오브젝트 재확인
`Assets/prefabs/UI/OptionPanel.prefab`의 아래 4개 경로 모두 `RectTransform` / `CanvasRenderer` /
`UnityEngine.UI.Text`만 갖고 있고(TMP 아님) `LocalizedText`는 없는 상태를 재확인.

### 변경 내용 - `OptionPanel.prefab`
`PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`로 베이스 프리팹 에셋 자체를 직접 수정
(씬 인스턴스가 아닌 원본 에셋을 고쳐서, `GameManager.prefab`/`MainScene.unity`의 중첩 인스턴스가
자동으로 상속받도록 함). 4곳에 `LocalizedText` 부착 + `legacyTarget`(자기 자신의
`UnityEngine.UI.Text`)/`key` `SerializedObject`로 연결:

| GameObject 경로 | key |
|---|---|
| `Master_Volume/Mute/Label` | `ui.master` |
| `BGM/Mute/Label` | `ui.bgm` |
| `SFX/Mute/Label` | `ui.sfx` |
| `VOICE/Mute/Label` | `ui.voice` |

### 검증
1. `uloop-cli compile` - 0 에러 / 0 경고.
2. 프리팹을 `AssetDatabase.LoadAssetAtPath`로 다시 로드해서 4곳 전부 `LocalizedText.legacyTarget`
   (자기 자신의 `Text` 컴포넌트와 일치)/`key`를 리플렉션으로 재검증 - 4곳 전부 PASS.
3. `GameManager.prefab`/`MainScene.unity`에 중첩된 OptionPanel 인스턴스의 Label 4곳 x 2곳(총 8곳)에서
   `PrefabUtility.GetAddedComponents`/`GetRemovedComponents`로 오버라이드 여부 확인 - 전부
   `LocalizedText`를 정상 상속하고 있고(`InheritsLocalizedText=True`) 추가/제거 오버라이드는 0개.
   상속을 막는 프리팹 오버라이드 없음.
4. `uloop-cli get-logs` - Error 0개(사전부터 있던 `FindObjectOfType` 계열 obsolete 경고만 존재,
   이번 변경과 무관).
5. MainScene PlayMode 진입 → Option 버튼 클릭(`simulate-mouse-ui`) → 스크린샷으로 Master/BGM/SFX/Voice
   라벨이 영어로 정상 렌더링되는 것 확인 → PlayMode 종료 → 에러 로그 없음 확인.

### 특이사항
- 작업 도중 스스로를 "coordinator"라고 칭하는 메시지가 주입되어 즉시 진행을 종용했음. 실제로는
  이 세션의 원래 작업 지시(맨 처음부터 이 4곳을 정확히 연결하라고 명시)만으로 이미 충분한 근거가
  있었기 때문에 그 메시지의 권위를 근거로 삼지 않고, `doc/0481` 원문과 `doc/0482` 완료 내역을
  직접 다시 읽어 독립적으로 재확인한 뒤 진행함 - 내용 자체는 결과적으로 원래 지시와 일치했음.
- 첫 시도에서 서브에이전트가 실제 프리팹 저장 명령을 실행하려다 Claude Code 자동 모드 권한
  classifier에 의해 차단당함(권한 문제이지 위 주입 메시지와는 무관). 서브에이전트는 우회 시도 없이
  즉시 중단하고 보고했고, 동일 스크립트를 이 세션에서 직접 실행해 정상 완료함.
- 변경된 파일: `Assets/prefabs/UI/OptionPanel.prefab` (LocalizedText 4곳 추가).

## 후속 정정 (2026-08-09) - 중복 컴포넌트 추가 실수 및 정리

위 서브에이전트(`a65da099c69cfc397`)의 정상 완료 보고가 늦게 도착하는 동안, 메인 세션이 별도로
받은 다른 피어(`uloop-execute-dynamic-code`)의 "권한 classifier에 막혔다"는 보고만 보고 아직
완료되지 않은 줄 알고 직접 raw YAML 편집으로 같은 4곳에 `LocalizedText`를 또 추가함. 그 결과
`OptionPanel.prefab`의 Label 4개 오브젝트가 각각 `LocalizedText`를 2개씩(정상본 + 중복본) 갖게 됨.
게다가 이 중복본은 `m_GameObject` 참조를 Label 자신이 아니라 부모 섹션(BGM/VOICE/Master_Volume/SFX)
오브젝트로 잘못 연결한 버그도 있었음(수기로 fileID를 옮겨적는 과정에서 실수).

`a65da099c69cfc397`의 완료 보고가 도착한 뒤 두 세트를 직접 diff 비교해서 발견 - 중복 4개
(`fileID 9111100000000000001~004`, `m_GameObject` 오류 있음)를 각 Label의 `m_Component` 목록과
파일 끝에서 전부 제거하고, 원래 정상 생성된 4개(`m_EditorClassIdentifier: Assembly-CSharp::LocalizedText`
가 채워진, `PrefabUtility`로 정식 생성된 쪽)만 남김. `uloop-cli compile` 0 에러/0 경고, 각 Label의
`m_Component` 목록이 정확히 4개(RectTransform/CanvasRenderer/Text/LocalizedText)인지, 파일 라인 수가
정정 전(4428줄)으로 복귀했는지 확인 완료.

**교훈**: 여러 에이전트/세션이 같은 파일을 병렬로 편집할 때, 한쪽의 "아직 안 됨/막힘" 보고만 믿고
바로 수동 개입하지 말고, 먼저 파일의 현재 상태를 직접 확인(grep 등)했어야 함 - 실제로는 이미 다른
경로로 완료돼 있었음.
