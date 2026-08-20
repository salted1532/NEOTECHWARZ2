# 0650. UI 버튼 마우스 호버 사운드 추가 (제안)

## 요청
"마우스 호버시 사운드도 추가해줘 근데 UI 버튼들만 작동하고 건물건설, 유닛생산, 명령버튼,
스쿼드버튼 등 은 빼주고 옵션창이나 인게임 밖 UI에서만"

## 조사
doc/0648에서 클릭 사운드를 넣을 때 버튼을 두 그룹으로 나눴던 것과 정확히 같은 경계선:

- **그룹 1 (제외 대상, `ProductionSlot.cs`)** — 커맨드 패널(건물 건설/유닛 생산), 생산 대기열,
  스킬 선택, 분대(Squad) 패널 버튼. 사용자가 이번에 명시적으로 뺀 "건물건설/유닛생산/명령버튼/
  스쿼드버튼"이 전부 여기 해당함. **손대지 않음.**
- **그룹 2 (포함 대상)** — 메인메뉴/옵션창/미션선택/승리화면/브리핑룸. 8개 파일 중
  `ProductionSlot.cs`를 뺀 나머지가 대상.

호버 이벤트는 클릭과 달리 `Button.onClick` 같은 기본 제공 이벤트가 없어서(UGUI 기본 컴포넌트에는
없음), `EventTrigger`(`PointerEnter`)를 코드로 붙이는 방식을 씀. `SoundManager`에 헬퍼 1개
(`AddHoverSound(Button)`)를 추가해서, 클릭 사운드 때와 동일하게 각 버튼의 기존 리스너 등록 줄
옆에 한 줄씩만 추가.

## 애매한 경계 2곳 — 확인 필요
"UI 버튼들만" / "인게임 밖 UI에서만"이라는 조건과 겹치는 회색지대가 2곳 있음:

1. **`UIController.cs`의 분대 페이지 1~5 버튼** — `ProductionSlot`은 아니지만, 인게임 중 화면에
   떠 있는 분대 패널의 "페이지 넘기기" 버튼. 스쿼드 관련 UI라 "스쿼드버튼" 제외 범위로 볼 수도
   있음.
2. **`ControlGroupPanel.cs`의 컨트롤그룹(부대) 선택 버튼** — 인게임 중 하단에 뜨는 컨트롤그룹
   버튼. "명령버튼"에 가까운 인게임 UI라 제외 범위로 볼 수도 있음.

## 상태
완료.

## 구현/검증
- 애매한 2곳(분대 페이지 버튼, 컨트롤그룹 버튼) 모두 사용자가 "제외"로 확인 → 최종 대상은
  `MainMenuController.cs` / `SceneMenuController.cs` / `MissionSelectManager.cs`(정적 버튼 +
  동적 생성 미션 버튼) / `VictoryPanelController.cs` / `BriefingRoomController.cs` 5개 파일.
- `SoundManager.cs`에 `uiHoverSFX`(`SoundClipSet`) 필드, `PlayUIHover()`, 정적 헬퍼
  `AddHoverSound(Button)` 추가 - `EventTrigger`(`PointerEnter`)를 코드로 붙여서 씬/프리팹은
  안 건드림.
- 위 5개 파일의 각 버튼에 기존 클릭 사운드 등록 줄 바로 아래 `SoundManager.AddHoverSound(xxx)`
  한 줄씩 추가(doc/0648과 동일한 패턴). `ProductionSlot.cs`/`UIController.cs`(분대 페이지)/
  `ControlGroupPanel.cs`(컨트롤그룹)는 손대지 않음.
- `npx uloop-cli compile --wait-for-domain-reload true` 결과 `Success: true, ErrorCount: 0,
  WarningCount: 0`. `git status`로 의도한 6개 파일(`SoundManager.cs` + UI 컨트롤러 5개)만
  변경됐음을 확인.
- 클립은 아직 미연결 - `uiHoverSFX`는 `uiClickSFX`처럼 `SoundManager` 컴포넌트 4곳
  (`GameManager.prefab`/`MainScene.unity`/`Briefing_Room.unity`/`MissionSelect.unity`)마다
  개별 필드라, 사용할 호버 사운드 클립을 알려주시면 guid로 연결해드립니다.

## 후속 - 클립 연결 (2026-08-20)

> 호버 사운드도 연결해줘

사용자가 `Assets/Sound/General/mouse_hover2.mp3`를 추가함(guid `68525ea2fee8c5e4abb9e7095721216d`).
`uiClickSFX`와 동일하게 `uiHoverSFX`가 있는 4곳(`GameManager.prefab`/`MainScene.unity`/
`Briefing_Room.unity`/`MissionSelect.unity`) 전부에 연결. `GameManager.prefab`은 리플렉션으로
재로드해 `uiHoverSFX → mouse_hover2` 확인 완료(PASS).

## 후속 - 범위 축소: "인게임 안" 전부 제외 (2026-08-20)

> 인게임 옵션창으로 가는 옵션버튼도 호버 빼주면 좋을거 같네 그냥 인게임 안에는 호버 사운드를 다 빼줘야겠다

기존엔 "옵션창"이 인게임 밖 취급의 예외로 포함돼 있었으나(`SceneMenuController.cs`), 이번에
그 예외를 없애고 "인게임 안에서 뜨는 UI는 전부 호버 제외"로 기준을 좁힘. 애매했던
`VictoryPanelController`(미션 클리어 직후 화면)도 같은 씬(게임플레이 씬) 안에서 뜨는 패널이라
확인 후 제외로 결정.

- `SceneMenuController.cs` - 옵션 열기/닫기, 메인화면, 미션선택, 재시작 버튼 5개 전부 호버 제거
  (클릭 사운드는 그대로 유지).
- `VictoryPanelController.cs` - 메인화면/다음스테이지/게임복귀 버튼 3개 전부 호버 제거(클릭
  사운드는 유지).
- 최종적으로 호버 사운드가 남는 곳은 `MainMenuController.cs`(메인메뉴), `MissionSelectManager.cs`
  (미션선택), `BriefingRoomController.cs`(브리핑룸) - 전부 미션 진행 중이 아닌, 별도 화면/씬에서만
  뜨는 버튼.
- `SoundManager.cs`의 `uiHoverSFX`/`PlayUIHover()`/`AddHoverSound()`는 위 3곳이 계속 쓰므로 그대로 둠.
- `npx uloop-cli compile --wait-for-domain-reload true` 결과 `Success: true, ErrorCount: 0`
  (WarningCount 49는 전부 이 변경과 무관한 기존 경고).
