# 0634 - 아드리안 클립 배선 (제안)

## 요청
`Assets/Sound/General/아드리안/`에 준비된 아드리안 대사 클립을 해당 `BriefingLine.voiceClip`에 연결.

## 현재 상태
- `Assets/Sound/General/아드리안/`에 mp3 19개 확인됨, 파일명 `[Adrian]Mission N (Side) - Line i.mp3`.
- [[0629-briefing-dialogue-script-by-character-en]]의 `[Adrian]` 섹션 19줄과 1:1로 정확히 일치 (누락/추가 없음).
- `[Adrian]Mission 3 Side - Line 3 .mp3`만 파일명에 `.mp3` 앞 공백이 있음 - 배선 자체엔 영향 없음(파일 선택은 이름이 아니라 에셋 GUID 참조).
- 방식은 [[0631-briefing-adjutant-clips-and-voice-gated-pacing-proposal]]에서 부관 클립 배선 때 쓴 것과 동일: `Briefing_Room.unity`의 `BriefingRoomController` 컴포넌트에서 해당 `BriefingEntry.lines[i].voiceClip`을 SerializedProperty로 직접 배선. 코드 변경 없음(음성 재생/종료 대기 로직은 doc/0630, 0631에서 이미 구현됨 - 클립 유무만으로 자동 분기).

## 배선 대상 (19줄)

| 미션 | lines 인덱스 | 클립 |
|---|---|---|
| 0 (본편) | 0 | Mission 0 - Line 0 |
| 0 (본편) | 2 | Mission 0 - Line 2 |
| 1 (본편) | 2 | Mission 1 - Line 2 |
| 1 (본편) | 4 | Mission 1 - Line 4 |
| 1 (사이드) | 0 | Mission 1 Side - Line 0 |
| 2 (본편) | 1 | Mission 2 - Line 1 |
| 2 (본편) | 3 | Mission 2 - Line 3 |
| 2 (사이드) | 1 | Mission 2 Side - Line 1 |
| 2 (사이드) | 3 | Mission 2 Side - Line 3 |
| 3 (본편) | 3 | Mission 3 - Line 3 |
| 3 (사이드) | 1 | Mission 3 Side - Line 1 |
| 3 (사이드) | 3 | Mission 3 Side - Line 3 |
| 4 (본편) | 1 | Mission 4 - Line 1 |
| 4 (본편) | 3 | Mission 4 - Line 3 |
| 4 (본편) | 6 | Mission 4 - Line 6 |
| 4 (사이드) | 1 | Mission 4 Side - Line 1 |
| 5 (본편) | 2 | Mission 5 - Line 2 |
| 5 (본편) | 4 | Mission 5 - Line 4 |
| 5 (본편) | 6 | Mission 5 - Line 6 |

배선 전 각 항목의 `speakerLabelKey`가 `briefing.speaker.adrian`인지 확인해서 인덱스 오배정 여지를 없앤다(0631과 동일한 안전장치).

## 범위 밖
- 다른 인물(셀레나/정찰병/기동사령관 등) 클립 배선 - 해당 폴더가 비어있어 아직 파일 없음.
- 코드 변경 - 재생/대기 로직은 이미 구현되어 있어 클립만 채우면 됨.

## 구현 완료
- `Briefing_Room.unity`: 위 표의 19개 `BriefingLine.voiceClip`에 `Assets/Sound/General/아드리안/` 클립 GUID를 배선. 배선 전 19개 항목 전부 `speakerLabelKey`가 `briefing.speaker.adrian`인지 확인 완료. 코드 변경 없음.

## 상태
완료. 아드리안 대사 19줄 전부 음성이 연결됨 - 부관과 동일하게 음성이 끝나야 다음 줄로 넘어간다.
