# 0484 - 로컬라이제이션 후속 버그 4건 수정

## 요청 내용
"건설모드에서 건물 이름이 2글자씩 줄바꿈이 일어나는거 / 옵션창에서 BGM은 배경음악으로
번역할것 / 인게임 옵션창에서 이전 미션으로, 메인화면으로 돌아가기, 다음 미션으로 뭐 이런식으로
번역을 해야지 이동 뒤로 이동은 좀 아닌거 같고 / 메인화면의 play, option, exit 버튼의 텍스트도
변경해줘 / 그리고 미션 선택 씬에선 툴팁의 미션 번호가 아에 번역이나 영어로도 들어가질 않고 있어"

5건 중 4건(1,2,3,5)은 원인이 명확해서 바로 수정. 4번(Play/Option/Exit 텍스트)은 어떻게
바꿔달라는 건지 명시가 없어 별도로 확인 필요 - 아래 "확인 필요" 참고.

## 1) 건설모드 건물 이름 2글자씩 줄바꿈

**원인**: `TooltipContentFitter.Fit()`이 비용(광물/가스/인구) 표시 여부로 제목/설명 텍스트의
`ContentSizeFitter.horizontalFit`을 `PreferredSize`(내용에 맞춰 폭 자동조절, 비용 없을 때) /
`Unconstrained`(폭 안 건드림, 비용 있을 때)로 매번 토글하는데, `Unconstrained`는 "그대로 둔다"는
뜻이라 직전에 `PreferredSize`로 좁아졌던 폭이 그대로 이어짐. 예: "이동"처럼 비용 없는 짧은 툴팁을
먼저 봤다가 → 건설모드에서 비용 있는 건물 버튼(제목=건물명, `Unconstrained`)을 보면, 방금
줄어든 좁은 폭 안에서 건물명이 줄바꿈됨. 로컬라이제이션과 무관한 기존 버그(비용 있는 툴팁이면
전부 재현 가능)였는데 건설모드 건물명이 상대적으로 길어서 눈에 띔.

**수정**: `TooltipContentFitter.cs` - `Configure()`에서 title/description 텍스트의 원래
(에디터에 배치된) 폭을 `defaultTitleWidth`/`defaultDescriptionWidth`로 저장해두고, `autoWidth`가
`false`일 때는 `Unconstrained`로 두는 것에 더해 폭을 매번 그 기본값으로 명시적으로 리셋.

## 2) BGM → 배경음악
`ko.json`의 `settings.audio.bgm` 값을 "BGM"(영문 그대로) → "배경음악"으로 변경.

## 3) 인게임 옵션창 이동/뒤로 버튼 문구

**문제**: 기존에 `GameManager.prefab`의 `GoToNextStage`(OptionPanel+VictoryPanel, 총 2곳)와
`GoToPreviousStage`(OptionPanel, 1곳)를 전부 하나의 키 `ui.goto`="이동"/"Go To"로 묶어놨었음
(doc/0482) - "다음 미션"과 "이전 미션"이 방향이 반대인데 같은 문구를 썼던 게 진짜 버그였음.
`BackToMainMenu`(OptionPanel+VictoryPanel, 2곳)의 `ui.backto`="뒤로"/"Back To"도 너무 모호.

**수정**: 키를 용도별로 분리.
- `ui.goto` → `ui.gotonextstage`="다음 미션으로"/"Next Mission" (GoToNextStage 2곳)
- `ui.goto` → `ui.gotopreviousstage`="이전 미션으로"/"Previous Mission" (GoToPreviousStage 1곳)
- `ui.backto` → `ui.backtomainmenu`="메인화면으로 돌아가기"/"Back to Main Menu" (BackToMainMenu 2곳)

`en.json`/`ko.json`에 새 키 3개 추가하고 기존 `ui.goto`/`ui.backto` 삭제, `GameManager.prefab`의
해당 5개 `LocalizedText` 컴포넌트의 `key` 필드를 각 버튼 용도에 맞게 재배정
(`target` TMP 텍스트 → 부모 버튼 오브젝트 이름으로 GoToNextStage/GoToPreviousStage/
BackToMainMenu 각각 매칭 확인 후 수정). `ReturnToGame`(`ui.returnto`)은 이번 요청 범위 밖이라
그대로 둠.

## 4) 메인화면 Play/Option/Exit 텍스트
방향(문구 직접 지정 / 한글을 더 자연스럽게 / 현재 상태 재점검) 확인 요청 → "한글을 더 자연스럽게"
선택. 영문 로딩값(Play/Option/Exit)은 그대로 두고, `ko.json`의 한글 값만 외래어 그대로였던
것에서 자연스러운 한국어 게임 UI 표현으로 변경.
- `ui.play`: "플레이" → "시작"
- `ui.option`: "옵션" → "설정"
- `ui.exit`: "종료" → "게임 종료"

## 5) 미션 선택 씬 툴팁 미션 번호 안 보임

**원인**: `MissionSelectManager.cs`가 `LocalizationManager.GetText("missionselect.tooltip.subtitle", ...)`을
호출하는데, `MissionSelect.unity` 씬에는 애초에 `LocalizationManager` 인스턴스가 하나도 없었음
(`MainScene.unity`에서 이미 한 번 겪었던 것과 동일한 원인 - doc/0481/0482 작업 당시 이 씬은
빠져있었음). `Instance`가 null이면 `GetText`가 인자 포맷팅 없이 키 문자열을 그대로 반환.

**수정**: `MainScene.unity`에 추가했던 것과 동일한 패턴으로 `MissionSelect.unity`에
`LocalizationManager` 루트 오브젝트 추가 (`LocalizationManager.cs` 컴포넌트만 붙은 빈 오브젝트,
`SceneRoots.m_Roots`에 등록).

## 확인
컴파일 확인 완료(에러 0, 기존부터 있던 obsolete API 경고 39개는 이번 변경과 무관).
