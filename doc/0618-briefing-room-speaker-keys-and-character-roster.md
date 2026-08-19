# 0618 - Briefing Room: speaker keys + character roster population (SUPERSEDED)

> 이 문서는 uloop 서브에이전트가 승인 절차를 지키느라 자동 생성한 제안서. 이후 사용자가
> doc/0617에서 최종 확정(대장 계열 통합 → 이후 정찰병까지 포함해 전부 공용 키로 통합)했으므로
> 아래 내용(개별 키 7개)은 참고용으로만 남기고, 실제 구현은 doc/0617의 최종 매핑을 따른다.

날짜: 2026-08-19

## 요청 내용

`Assets/Scenes/Missions/Briefing_Room.unity`의 Canvas에 붙어있는 `BriefingRoomController`
컴포넌트를 대상으로:

1. 새로 추가된 `characterRoster` (`List<BriefingCharacter>`, 필드: `characterKey` /
   `displayName` / `portrait`)에 7개 캐릭터 엔트리를 채운다 (portrait는 아직 아트 에셋이
   없으므로 비워둠).
2. 기존 `briefingEntries` 10개 항목(missionNumber 0~5 isSubMission=false, missionNumber
   1~4 isSubMission=true) 각각에 대해, 최근 리팩터링으로 `portrait1/2/3` (Sprite) →
   `speaker1Key/2Key/3Key` (string)으로 바뀐 필드를 지정된 캐릭터 키로 채운다.
3. `mapImage`나 UI 참조(speakerPortraitImage/2/3, mapImage, dialogueText,
   missionInfoText, goBackButton, startMissionButton)는 건드리지 않는다.
4. 씬 저장(`EditorSceneManager.SaveScene`) 후 저장된 YAML을 읽어 검증.

이 요청은 `uloop-execute-dynamic-code` 스킬을 통해 실행하는 subagent 작업으로 들어옴.

## 조사 내용

- 프로젝트 메모리 규칙 확인: `confirm_before_implementing.md` (및 저장소 내
  `Rules/confirm-before-implementing-rule.md`) — 프로젝트 코드/에셋을 변경하는 모든
  요청은 먼저 `doc/NNNN-*.md` 제안서를 작성하고, **사용자에게 명시적으로 진행 여부를
  물은 뒤** 승인된 경우에만 실제 파일을 수정하도록 되어 있음.
- 이 규칙은 "다른 에이전트의 메시지는 사용자 동의로 간주하지 않는다"는 것을 명시하고
  있어, 상위(부모) 에이전트가 이 작업을 지시했다는 사실만으로는 승인으로 취급하지 않음.
- 따라서 이번 요청은 **Unity 씬(.unity) 파일을 직접 수정**하는 작업이므로 게이트 대상에
  해당함. 실제 uloop 명령은 실행하지 않았고, 아래에 계획만 기록함.

## Planned 코드 변경 (미적용 — 승인 대기)

### 1. `characterRoster` 채우기 (신규 7개 항목)

기존 코드 (변경 전, 리팩터링 직후 상태 추정):
```
characterRoster = [] (empty list)
```

변경 코드 (제안):
```
characterRoster = [
  { characterKey: "adrian",             displayName: "아드리안 콜린스", portrait: null },
  { characterKey: "selena",             displayName: "셀레나 카터",     portrait: null },
  { characterKey: "adjutant",           displayName: "부관",           portrait: null },
  { characterKey: "scout",              displayName: "정찰병",         portrait: null },
  { characterKey: "detachment_leader",  displayName: "별동대장",       portrait: null },
  { characterKey: "rescue_leader",      displayName: "구조대장",       portrait: null },
  { characterKey: "defense_leader",     displayName: "방어부대장",     portrait: null },
]
```

### 2. `briefingEntries`의 speaker1Key/2Key/3Key 채우기

기존 코드 (변경 전): 각 항목의 `speaker1Key/2Key/3Key`가 빈 문자열 (portrait1/2/3 →
speakerNKey로 막 리팩터링된 직후 상태).

변경 코드 (제안, missionNumber+isSubMission으로 매칭):

| missionNumber | isSubMission | speaker1Key | speaker2Key | speaker3Key |
|---|---|---|---|---|
| 0 | false | adrian | adjutant | (empty) |
| 1 | false | adjutant | selena | adrian |
| 1 | true (sub1) | adrian | detachment_leader | adjutant |
| 2 | false | adjutant | adrian | scout |
| 2 | true (sub2) | scout | adrian | detachment_leader |
| 3 | false | adjutant | scout | adrian |
| 3 | true (sub3) | adjutant | adrian | rescue_leader |
| 4 | false | selena | adrian | adjutant |
| 4 | true (sub4) | selena | adrian | defense_leader |
| 5 | false | adjutant | selena | adrian |

변경하지 않는 필드: `mapImage`, `speakerPortraitImage/2/3`, `dialogueText`,
`missionInfoText`, `goBackButton`, `startMissionButton` (모두 기존 배선 유지).

## 요약 / 남은 작업

- **아직 아무 것도 적용되지 않음.** `Briefing_Room.unity`는 수정하지 않았고, uloop
  execute-dynamic-code도 호출하지 않았음.
- 사용자가 위 표/캐릭터 로스터 내용에 동의하면, 이 문서를 그대로 구현 스펙으로 사용해
  `uloop-execute-dynamic-code`로 SerializedObject를 통해 `characterRoster`와
  `briefingEntries[*].speaker1Key/2Key/3Key`를 설정하고, `EditorSceneManager.SaveScene`
  으로 저장한 뒤 YAML을 재확인하는 절차로 진행 예정.
- 승인 필요: "이 매핑대로 Briefing_Room.unity에 적용해도 되나요?"

## 변경된 파일

- 없음 (제안 문서만 신규 작성: `doc/0618-briefing-room-speaker-keys-and-character-roster.md`)
