# 0633 - 브리핑 화자 호칭 변경 (별동대장/방어부대장 → 기동지휘관/방위대장)

## 요청
`briefing.speaker.detachment_leader`/`briefing.speaker.defense_leader` 표시명을 "별동대장"/"방어부대장"에서
"기동지휘관"/"방위대장"으로 변경. `briefing.speaker.scout`("정찰병")는 그대로 유지.

## 변경
`Assets/Resources/Localization/ko.json`
```diff
- { "key": "briefing.speaker.detachment_leader", "value": "별동대장" },
+ { "key": "briefing.speaker.detachment_leader", "value": "기동지휘관" },
  { "key": "briefing.speaker.rescue_leader", "value": "구조대장" },
- { "key": "briefing.speaker.defense_leader", "value": "방어부대장" },
+ { "key": "briefing.speaker.defense_leader", "value": "방위대장" },
```

`Assets/Resources/Localization/en.json` (한글 의미 변경에 맞춰 영문 표기도 함께 조정 - 사용자 확인)
```diff
- { "key": "briefing.speaker.detachment_leader", "value": "Detachment Leader" },
+ { "key": "briefing.speaker.detachment_leader", "value": "Maneuver Commander" },
  { "key": "briefing.speaker.rescue_leader", "value": "Rescue Leader" },
- { "key": "briefing.speaker.defense_leader", "value": "Defense Leader" },
+ { "key": "briefing.speaker.defense_leader", "value": "Defense Commander" },
```

[[0628-briefing-room-english-dialogue-script-for-tts]] / [[0629-briefing-dialogue-script-by-character-en]] 문서의 "Detachment Leader"/"Defense Leader" 표기도 "Maneuver Commander"/"Defense Commander"로 동일하게 반영.

`BriefingRoomController.cs`/씬은 키만 참조하므로 코드/씬 변경 없음. JSON 문법 검증 통과.

## 상태
완료.
