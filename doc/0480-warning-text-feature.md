# 0480 - 경고 문구(WaringText) 표시 기능 추가

## 요청 내용
"WaringText라는 텍스트를 canvas에 추가했는데 이건 건물 건설실패, 인구수부족, 자원부족 이런 경고시
경고문으로 같이 띄울 생각인데 해당 내용에선 해결법 같은 느낌으로 간결하게 알려주는 문구를 출력해주면
될거 같아 ... 일단 영어로 출력해주도록 해줘 경고문은 2초 있다가 없어지도록해줘"
(+ 후속: "한줄로 출력되도록 해줘")

## 조사 내용
Canvas 직속으로 이미 추가돼있던 `WaringText`(TextMeshProUGUI, GameManager.prefab)를 찾음. 기존 코드에
이미 세 가지 경고 상황이 사운드로만 구분되어 있었음:
- `SoundManager.PlayInsufficientResourcesWarning()` - 자원부족 (`RTSUnitController.cs`: 유닛 생산/
  연구/건물 건설 3곳)
- `SoundManager.PlayInsufficientPopulationWarning()` - 인구수부족 (`RTSUnitController.cs`: 유닛 생산)
- `UnitAudio.PlayBuildFailVoice()` - 건설실패(장애물/도달불가) (`PlacementSystem.cs`, `UnitController.cs`
  각 1곳)

## 변경 내용
- `UIController.cs`: `warningText` 필드 + `ShowWarning(string)`(2초 후 자동으로 빈 문자열로 되돌리는
  코루틴, 표시 중 재호출되면 타이머 재시작) 추가. `PlacementSystem.cs`/`UnitController.cs`처럼
  `RTSUnitController`를 거치지 않는 곳에서도 바로 부를 수 있도록 `TooltipUI.Instance`와 동일한 패턴의
  `UIController.Instance` 싱글턴 추가.
- 위 5개 기존 사운드 호출 지점 옆에 `ShowWarning(...)` 호출 추가:
  - 자원부족 → "Gather more resources."
  - 인구수부족 → "Build a Supply Depot."
  - 건설실패 → "Build somewhere else."
- `GameManager.prefab`: `UIController.warningText`를 `WaringText`에 연결.
- 후속 - `WaringText`의 `m_TextWrappingMode`를 1(줄바꿈)→0(줄바꿈 없음)으로 변경해 항상 한 줄로
  출력되게 함(박스 폭 200에 폰트 36이라 줄바꿈되던 문제).

컴파일 확인 완료(에러 0).
