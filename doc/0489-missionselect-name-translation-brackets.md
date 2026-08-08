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
