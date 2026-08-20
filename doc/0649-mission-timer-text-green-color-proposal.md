# 0649 - 시간 타이머 텍스트 색상을 초록색으로 변경 (제안)

## 요청
"시간 타이머 글자 색깔을 초록색으로 바꿔줘"

## 조사
`GameManager.prefab`의 `Timer_Text` 오브젝트(`MissionTimerDisplay.cs`가 붙어있고, 미션 시작부터
경과 시간을 "시:분:초"로 표시)의 `TextMeshProUGUI` 컴포넌트가 현재 흰색(`r:1, g:1, b:1, a:1`)으로
설정되어 있습니다. `MissionTimerDisplay.cs`는 텍스트 내용만 갱신하고 색상은 건드리지 않으므로,
색상은 프리팹의 컴포넌트 값입니다.

## 변경 방법
코드 변경 없이 `GameManager.prefab`의 `Timer_Text` 오브젝트, `TextMeshProUGUI` 컴포넌트의 색상
필드(`m_Color`, `m_fontColor32`, `m_fontColor`, `m_fontColorGradient`의 4개 코너)를 흰색에서
초록색(`r:0, g:1, b:0, a:1`)으로 일괄 변경합니다. `GameManager.prefab`은 모든 미션 씬에서
공유되므로 한 곳만 고치면 전체 미션에 적용됩니다.

## 상태
완료.

## 구현/검증
- 순수 초록(0,1,0) vs 부드러운 초록 중 선택을 물어봤고, 부드러운 초록(`r:0.3, g:0.9, b:0.3, a:1`)으로 결정.
- `GameManager.prefab`의 `Timer_Text`(`TextMeshProUGUI`) 컴포넌트에서 `m_Color`, `m_fontColor32`
  (packed rgba: 1306938879), `m_fontColor`, `m_fontColorGradient`(4개 코너) 5개 필드를 흰색에서
  `(0.3, 0.9, 0.3, 1)`로 변경. `git diff --stat` 기준 해당 컴포넌트 블록 12줄만 변경됨(다른 텍스트
  오브젝트 색상은 그대로).
- 코드(`MissionTimerDisplay.cs`) 변경 없음 — 프리팹 컴포넌트 값만 수정.
