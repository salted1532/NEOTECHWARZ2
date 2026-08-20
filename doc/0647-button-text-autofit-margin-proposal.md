# 0647 - 버튼 텍스트 자동 축소(Auto Size) + 여백 추가 (제안)

## 요청
"각 버튼들의 텍스트가 줄바꿈이 일어날때 폰트크기를 1씩 줄이도록", "버튼 이미지에 텍스트가 꽉 차
보여서 여백이 좀 있으면 좋겠다" — 옵션 버튼, 메인화면 버튼, 브리핑 룸 돌아가기/미션시작 버튼 등
전체적으로 폰트가 버튼을 꽉 채우는 느낌이 있다는 피드백.

## 설계
"줄바꿈될 때마다 1씩 줄이기"를 직접 구현하는 대신, TextMeshPro의 내장 **Auto Size**
(`enableAutoSizing` + `fontSizeMin`/`fontSizeMax`)를 켜는 방식을 제안합니다 — 텍스트가 박스에
안 맞으면(줄바꿈 포함) 엔진이 알아서 지정한 범위 안에서 맞는 크기를 찾아주는, 정확히 같은 결과를
내는 표준 기능입니다(직접 "한 줄씩 줄이며 재측정" 로직을 새로 짤 필요 없음). 추가로 TMP의
`margin`(좌/상/우/하 여백) 값을 넣어 텍스트가 버튼 테두리에 붙지 않게 합니다.

- `fontSizeMax` = 현재 폰트 크기 그대로(한 줄로 들어가는 짧은 텍스트는 지금과 동일하게 보임)
- `fontSizeMin` = `max(14, round(fontSizeMax * 0.65))` — 대략 35% 정도까지는 줄어들 수 있게 허용
- `margin` = `(12, 8, 12, 8)` 고정값 (좌우 12 / 상하 8, 캔버스 유닛) — 모든 버튼에 동일 적용

## 대상 조사
프로젝트 내 실제 "액션 버튼"(아이콘/슬롯 라벨 제외, 폰트 24pt 이상)을 전수 조사한 결과:

| 파일 | 버튼 | 크기 | 현재 폰트 |
|---|---|---|---|
| `GameManager.prefab` | Option(옵션 열기) | 200x100 | 56 |
| 〃 | GoToMissionSelect | 200x100 | 32 |
| 〃 | BackToMainMenu (옵션 패널) | 200x100 | 32 |
| 〃 | RestartMission | 200x100 | 32 |
| 〃 | BackToMainMenu (승리 화면) | 200x100 | 32 |
| 〃 | ReturnToGame (승리 화면) | 200x100 | 32 |
| 〃 | GoToNextStage (승리 화면) | 200x100 | 32 |
| `MainScene.unity` | EN / KR | 100x100 | 24 |
| 〃 | Play / Option / Exit | 300x150 | 72 |
| 〃 | PlayerPrefabReset(디버그용) | 300x100 | 36 |
| `MissionSelect.unity` | UnlockMission(디버그용) | 300x100 | 36 |
| `Briefing_Room.unity` | Go_Back | 120x50 | 24 |
| 〃 | Start_Mission | 120x50 | 24 |

`GameManager.prefab` 하나만 고치면 브리핑/승리화면을 포함해 모든 미션 씬(Mission0~5,
Sub_Mission1~4)에 동시 적용됩니다. 총 4개 파일, 16개 버튼.

미니맵 페이지 번호(`page1~5`), 생산 대기열 슬롯(`Slot0~11`, `100/100`), 단축키 라벨(`Slot0~8`, `w`)
등 8~16pt의 작은 아이콘 라벨은 줄바꿈 문제가 없어 대상에서 제외했습니다.

## 변경 방법
`PrefabUtility.EditPrefabContentsScope`(prefab)와 씬 열기→수정→저장(scene)으로, 각 파일의 모든
`Button`을 순회해 자식 `TextMeshProUGUI`에 위 설정을 일괄 적용하는 에디터 스크립트 1회 실행.
코드/런타임 로직 변경 없음 — 컴포넌트 값만 조정.

## 상태
완료.

## 구현/검증
- 4개 파일(`GameManager.prefab`, `MainScene.unity`, `MissionSelect.unity`, `Briefing_Room.unity`)의
  `Button` 16개(자식 `TextMeshProUGUI` 폰트 24pt 이상 기준) 전수 적용 — `enableAutoSizing = true`,
  `fontSizeMax` = 기존 폰트 크기, `fontSizeMin = max(14, round(fontSizeMax * 0.65))`,
  `margin = (12, 8, 12, 8)`. 프리팹은 `PrefabUtility.EditPrefabContentsScope`, 씬은
  열기→수정→`EditorSceneManager.SaveScene`으로 저장.
- 컴파일 통과(에러 0, 컴포넌트 값만 바꿔 코드 변경 없음).
- Mission0(옵션 패널: 미션 선택/메인화면/재시작 3버튼 + Option 여는 버튼), MainScene(Play/Option/Exit
  + EN/KR), Briefing_Room(Back/Start Mission)에서 Play Mode로 스크린샷 확인 — 한/영 전환 모두 텍스트가
  버튼 테두리에서 떨어져 여백이 생기고, 길게 줄바꿈되던 텍스트(예: "Restart Mission")는 폰트가 줄어들며
  가능하면 한 줄로, 안 되면 여백 있는 2줄로 자연스럽게 표시됨.
