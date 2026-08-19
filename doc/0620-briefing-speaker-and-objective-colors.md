# 0620 - 브리핑 인물 이름/목표 접두어 색상 구분

## 요청
- 인물 이름("아드리안:", "병사:" 등)에 색상을 넣어 대사와 구별되도록. 아드리안/병사(soldier 공용) = 파란색, 셀레나 = 빨간색.
- 미션정보의 "(주목표)"는 빨간색, "(서브)"는 노란색으로 표시.

## 문제 - 타이프라이터와 리치 텍스트 태그 충돌
기존 타이프라이터는 문자열을 한 글자씩 이어붙이는 방식(`log + prefix + typed`)이었는데, 이름/목표 접두어에 `<color=#RRGGBB>...</color>` 태그를 넣으면 태그 문자가 잘린 채로 화면에 노출되는 문제가 생김(예: 타이핑 도중 `<col`까지만 보임).

## 해결
`BriefingRoomController.cs`를 리치 텍스트 안전한 방식으로 재작성:
- 매 줄마다 완성된 최종 문자열(태그 포함)을 TMP `.text`에 한 번에 설정하고, TMP의 `maxVisibleCharacters`만 늘려가며 타이핑 효과를 냄. 태그는 항상 완전한 상태로 파싱되고, TMP가 태그를 너비 0으로 취급해 보이는 글자 수 계산에서 자동으로 제외해준다.
- `BriefingCharacter`에 `labelColor` 필드 추가. `PlayDialogue()`가 그 줄 화자의 `characterKey`로 로스터에서 색을 찾아 `"이름:"` 부분만 `<color>`로 감쌈(대사 내용은 색 없음).
- `AppendObjectiveLines()`에서 main/sub 여부에 따라 `mainObjectiveColor`(빨강)/`subObjectiveColor`(노랑)로 괄호 접두어(`(주목표)`/`(서브)`, 영어는 `(Main)`/`(Sub)`)만 `ColorizeBracketPrefix()`로 색칠 - 첫 `)`까지만 잘라 태그를 씌우므로 언어별 분기 불필요.
- `missionInfoText`용 `TypeText()`도 동일하게 maxVisibleCharacters 방식으로 교체.

## 색상 배정
로스터(`characterRoster`)에 `labelColor` 설정: `selena`(OC)만 빨강, 나머지(`adrian`/`adjutant`/`soldier`, 전부 NTA)는 파랑.

## 검증
컴파일 성공. Play Mode로 미션0 재생 - "아드리안:" 파란색, "(주목표)" 빨간색으로 렌더링되고 타이핑 도중 태그 깨짐 없음을 스크린샷으로 확인.

## 상태
완료.
