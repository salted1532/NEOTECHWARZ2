# 0489 - 미션 선택 화면 미션명 번역 + `<>` 장식

## 요청 내용
"미션 선택 부분에서 미션명도 한글로 번역해주고 미션명에다가 < > 이걸로 양 옆에다가 붙여줘"

## 변경 내용
`MissionSelectManager.cs`의 `MissionSelectEntry.missionName`은 인스펙터에 하드코딩된 영문
문자열이었음(`MissionSelect.unity`에서 확인: Boot Camp/Border Conflict/Unknown Signal/
Invasion/United Front/Final Offensive). `missionselect.name.0`~`.5` 키를 `en.json`/`ko.json`에
추가하고, `SetupHoverTooltip()`에서 `LocalizationManager.GetTextOrFallback(...)`으로 조회하도록
변경(키가 없거나 매니저가 없어도 인스펙터 원문이 그대로 나오는 안전장치는 doc/0487과 동일한
패턴).

한글 번역: Boot Camp→신병 훈련소, Border Conflict→국경 분쟁, Unknown Signal→미확인 신호,
Invasion→침공, United Front→연합 전선, Final Offensive→최후의 공세.

`<`/`>` 장식은 JSON 값이 아니라 코드에서 감싸도록 함(`$"&lt;{missionName}&gt;"`) - 툴팁 제목
텍스트는 TMP 리치 텍스트가 켜져 있어서(다른 곳에서 `<color=...>` 태그를 씀) 그냥 `<`/`>`를 넣으면
태그로 오인되어 사라질 위험이 있음. `&lt;`/`&gt;` HTML 엔티티로 escape해서 리터럴 `<`/`>`가 항상
안전하게 표시되도록 함.

## 확인
컴파일 확인 완료(에러 0). JSON 키 173개, en/ko 키 집합 일치·중복 없음 확인.

## 후속 정정 - `&lt;`/`&gt;`가 실제로는 안 먹힘
"< 표시가 안 되네 확인해주고 안되면 그냥 빼줘"라는 후속 리포트를 받고 Play Mode에서 실제 TMP
컴포넌트에 직접 텍스트를 넣어 `ForceMeshUpdate()` 후 `textInfo.characterInfo`를 읽어 확인:

- `"&lt;Boot Camp&gt;"`를 넣으면 → 렌더링된 글자가 정확히 `&lt;Boot Camp&gt;`(17글자) 그대로.
  이 프로젝트의 TMP 설정에서는 HTML 엔티티가 디코딩되지 않고 글자 그대로 찍힘 - 처음에
  "리치 텍스트라 그냥 `<`/`>`를 넣으면 태그로 오인될 것"이라고 예상해서 엔티티로 이스케이프했던
  게 틀린 가정이었음.
- `"<Boot Camp>"`(리터럴)를 넣으면 → 렌더링된 글자가 정확히 `<Boot Camp>`(11글자) 그대로 정상
  출력. `Boot Camp>`가 유효한 TMP 태그 이름이 아니라서 TMP가 그냥 일반 텍스트로 통과시킴 -
  애초에 이스케이프가 필요 없었음.

`MissionSelectManager.cs`의 `$"&lt;{missionName}&gt;"`를 `$"<{missionName}>"`로 되돌림.
컴파일 확인 완료(에러 0).
