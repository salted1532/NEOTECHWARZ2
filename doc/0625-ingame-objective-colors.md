# 0625 - 인게임 목표 체크리스트 (주목표)/(서브) 색상

## 요청
브리핑룸뿐 아니라 실제 인게임 목표 체크리스트에서도 "(주목표)"는 빨간색, "(서브)"는 노란색으로 표시.

## 조사
`Stage0~5Objectives.cs`, `SubStage1~4Objectives.cs` 전부 목표 텍스트를 직접 조립하지 않고 공용 헬퍼 `ObjectiveTextUtil.SetObjectiveText()`/`SetSurvivalObjectiveText()`를 거침 - 색칠을 이 한 곳에만 추가하면 모든 미션의 목표 체크리스트에 자동 반영됨(11개 파일 개별 수정 불필요).

## 구현
- `ObjectiveTextUtil.cs`에 `ColorizeBracketPrefix(string text)`를 추가 - "(주목표)"/"(Main)" 접두어는 빨강, "(서브)"/"(Sub)" 접두어는 노랑으로 감싸고, 셋 다 아니면 원문 그대로 반환. `SetObjectiveText`(완료 bool/개수 오버로드 둘 다) · `SetSurvivalObjectiveText`에서 텍스트 조립 전에 이 함수를 거치도록 함. 취소선(`<s>`)이 색칠된 텍스트를 감싸도 TMP가 중첩 태그를 정상 렌더링.
- `BriefingRoomController.cs`의 중복 색칠 로직(`mainObjectiveColor`/`subObjectiveColor` 필드, 자체 `ColorizeBracketPrefix`)을 제거하고 `ObjectiveTextUtil.ColorizeBracketPrefix()`를 재사용하도록 변경 - 브리핑룸과 인게임이 같은 팔레트를 공유.

## 검증
컴파일 성공. Mission0을 Play Mode로 실행해 좌상단 목표 체크리스트 스크린샷 확인 - "(주목표)" 3줄 빨강, "(서브)" 2줄 노랑으로 정상 표시.

## 상태
완료.
