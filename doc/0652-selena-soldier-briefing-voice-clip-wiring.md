# 0652 - 셀레나/병사(정찰병·대장 등) 브리핑 대사 음성 클립 연결

## 날짜
2026-08-21

## 요청 내용
"셀레나, 병사 대사 사운드 클립들 추가했어 해당하는 위치에 연결해줘 / 병사에 경우 정찰병, 대장 등 나머지 인물의 사운드 클립인데 대사 넘버를 보고 해당하는 위치에 넣어주면 돼"

`Assets/Sound/General/셀리나/`(8개), `Assets/Sound/General/병사/`(7개, 정찰병/기동지휘관/구조대장/방위대장 등 아드리안·셀레나·부관을 뺀 나머지 화자 전부 포함)에 추가된 새 대사 클립을 `Assets/Scenes/Missions/Briefing_Room.unity`의 `BriefingRoomController.briefingEntries[].lines[].voiceClip`에 연결.

## 조사 내용
`BriefingRoomController`는 `briefingEntries`(미션별)마다 `lines`(대사 목록, 각 줄에 `speakerSlot`/`speakerLabelKey`/`textKey`/`voiceClip`)를 인스펙터 직렬화로 씬 파일에 직접 들고 있다. 파일명 규칙은 `[화자]Mission {missionNumber} (Side) - Line {줄 인덱스}.mp3`이며, `missionNumber`/`Side 여부`/`줄 인덱스`가 `textKey`(`briefing.line.{n}.{i}` 또는 `briefing.line.sub{n}.{i}`)와 오프셋 없이 그대로 대응한다(예: `Mission 1 Side - Line 1` = `missionNumber:1, isSubMission:1`의 lines[1]).

기존에 `voiceClip: {fileID: 0}`(미연결)이던 슬롯 15개를 전수 조사한 결과, 셀레나 8개/병사 7개 파일 중 **14개**가 파일명 그대로 대응하는 빈 슬롯에 정확히 1:1로 들어맞았다. 나머지 1개는 대응되는 빈 슬롯이 없다(아래 "연결 안 됨" 참고).

## 연결 매핑 (14건)

| 미션 | 화자 | textKey | 파일 | GUID |
|---|---|---|---|---|
| Mission 1(main) | selena | briefing.line.1.1 | [Selena]Mission 1 - Line 1.mp3 | d867e875d26a9004286ef199962cc81d |
| Mission 2(main) | scout | briefing.line.2.2 | [soldier]Mission 2 - Line 2.mp3 | b8e499f2ed66b9b428fb1d171a71fa4b |
| Mission 4(main) | selena | briefing.line.4.0 | [Selena]Mission 4 - Line 0.mp3 | a9296398635dced42b207956e3393f05 |
| Mission 4(main) | selena | briefing.line.4.2 | [Selena]Mission 4 - Line 2.mp3 | 96a87a12777e29548aacfe9c1aaed9bc |
| Mission 4(main) | selena | briefing.line.4.5 | [Selena]Mission 4 - Line 5.mp3 | 97bf1f26e24e5844591bb28fe395c702 |
| Mission 5(main) | selena | briefing.line.5.1 | [Selena]Mission 5 - Line 1.mp3 | b4d3cde5cae005044947f6b893edf0ba |
| Mission 1 Side | detachment_leader | briefing.line.sub1.1 | [soldier]Mission 1 Side - Line 1.mp3 | 8d30e7c5af437e34eab3b3e46d381829 |
| Mission 1 Side | detachment_leader | briefing.line.sub1.3 | [soldier]Mission 1 Side - Line 3.mp3 | 94ca77ebef13ab744a154dd97f68226f |
| Mission 2 Side | scout | briefing.line.sub2.0 | [soldier]Mission 2 Side - Line 0.mp3 | 1df449ecf7e8a9d459f3542e2d3b359c |
| Mission 2 Side | detachment_leader | briefing.line.sub2.2 | [soldier]Mission 2 Side - Line 2.mp3 | 1aa265b4c10942f4d82b31923b8fe4ea |
| Mission 3 Side | rescue_leader | briefing.line.sub3.2 | [soldier]Mission 3 Side - Line 2.mp3 | cf1d48ae3f8ee0a4aa05c8f1ade4ef4b |
| Mission 4 Side | selena | briefing.line.sub4.0 | [Selena]Mission 4 Side - Line 0.mp3 | b554535422ad105488a56bfd289ed136 |
| Mission 4 Side | defense_leader | briefing.line.sub4.2 | [soldier]Mission 4 Side - Line 2.mp3 | a686950a01da45245a7e171650109ccd |
| Mission 4 Side | selena | briefing.line.sub4.3 | [Selena]Mission 4 Side - Line 3.mp3 | 414092298a1f8f6438bc47aab39d09b4 |

## 후속 조치 (2026-08-21, 같은 세션)
사용자 확인 결과 `[Selena]Mission 3 - Line 1.mp3`는 셀레나가 아니라 병사(정찰병) 대사로 잘못 분류된 파일이었음. 처리:
1. `Assets/Sound/General/셀리나/[Selena]Mission 3 - Line 1.mp3`(+`.meta`) → `Assets/Sound/General/병사/[soldier]Mission 3 - Line 1.mp3`로 이동 + 파일명 태그를 `[Selena]`→`[soldier]`로 변경 (guid `3c7964d8715b2a449b881660b7559d5f` 그대로 보존).
2. `briefing.line.3.1`(scout, "적 전초기지가 방어 태세를 갖추고 있습니다.") 줄의 `voiceClip`을 같은 guid로 연결.

이로써 브리핑룸 대사 음성 슬롯 15개 전부 연결 완료 (`voiceClip: {fileID: 0}` 잔여 0건).

## 코드 변경
`Assets/Scenes/Missions/Briefing_Room.unity`의 `BriefingRoomController` 컴포넌트, 위 14개 `voiceClip` 필드.

### 기존 코드 → 변경 코드 (예시, 나머지 13건도 동일 패턴)
```yaml
# 기존
    - speakerSlot: 2
      speakerLabelKey: briefing.speaker.selena
      textKey: briefing.line.1.1
      voiceClip: {fileID: 0}

# 변경
    - speakerSlot: 2
      speakerLabelKey: briefing.speaker.selena
      textKey: briefing.line.1.1
      voiceClip: {fileID: 8300000, guid: d867e875d26a9004286ef199962cc81d, type: 3}
```
(나머지 13곳도 `textKey`로 위치를 특정해 표에 나온 guid로 동일하게 교체)

## 요약
빈 대사 음성 슬롯 15개 전부 연결. 1건(Mission 3 main의 정찰병 줄)은 처음엔 대응 파일이 없어 미연결이었으나, 잘못 분류돼 있던 `[Selena]Mission 3 - Line 1.mp3`를 병사 폴더로 이동/개명 후 연결하여 해결.

## 변경된 파일
- `Assets/Scenes/Missions/Briefing_Room.unity`
- `Assets/Sound/General/셀리나/[Selena]Mission 3 - Line 1.mp3` → `Assets/Sound/General/병사/[soldier]Mission 3 - Line 1.mp3` (이동+개명, `.meta` 포함)
