# 0631 - 부관 클립 배선 + 음성 종료 후 다음 대사 진행 (제안)

## 요청
1. `Assets/Sound/General/부관/` 폴더에 미리 준비된 부관 대사 클립 12개(파일명 `[Adjutant]Mission N - Line i.mp3`, [[0629-briefing-dialogue-script-by-character-en]]의 넘버링과 동일)를 해당 `BriefingLine.voiceClip`에 연결.
2. 대사 진행 속도를 사람이 말하는 것처럼 바꿔달라: 음성 클립이 있는 줄은 그 음성이 다 끝나야 다음 인물의 텍스트/음성이 시작되도록. 음성 클립이 아직 없는 줄(부관 외 전원)은 지금처럼 고정 `pauseBetweenLines`만 대기하고 바로 다음으로 넘어감.

## 현재 상태
- `Assets/Sound/General/부관/`에 부관의 대사 12개 전부(mp3, doc/0629 넘버링과 1:1 대응) 확인됨. 나머지 인물(병사/셀리나/아드리안) 폴더는 비어있음.
- `PlayDialogue()`(`BriefingRoomController.cs:161`)는 줄마다 `PlayLineVoice(line.voiceClip)`으로 음성을 재생만 시키고, 다음 줄로 넘어가는 타이밍은 텍스트 타이핑이 끝난 뒤 고정 `pauseBetweenLines`(기본 0.6초) 대기뿐 - 음성 길이는 전혀 안 봄.

## 제안 설계

### 1. 클립 배선 (씬 데이터, 코드 변경 아님)
아래 13줄의 `BriefingLine.voiceClip`에 `Assets/Sound/General/부관/[Adjutant]Mission N (Side) - Line i.mp3`를 그대로 연결:

| 미션 | lines 인덱스 | 클립 |
|---|---|---|
| 0 (본편) | 1 | Mission 0 - Line 1 |
| 1 (본편) | 0 | Mission 1 - Line 0 |
| 1 (본편) | 3 | Mission 1 - Line 3 |
| 1 (사이드) | 2 | Mission 1 Side - Line 2 |
| 2 (본편) | 0 | Mission 2 - Line 0 |
| 3 (본편) | 0 | Mission 3 - Line 0 |
| 3 (본편) | 2 | Mission 3 - Line 2 |
| 3 (사이드) | 0 | Mission 3 Side - Line 0 |
| 4 (본편) | 4 | Mission 4 - Line 4 |
| 5 (본편) | 0 | Mission 5 - Line 0 |
| 5 (본편) | 3 | Mission 5 - Line 3 |
| 5 (본편) | 5 | Mission 5 - Line 5 |

(doc/0629 부관 섹션 12개 항목 전부 반영.)

### 2. 음성 종료까지 대기 (`BriefingRoomController.cs`)
`PlayDialogue()`의 줄 처리 끝, 기존 `yield return new WaitForSeconds(pauseBetweenLines);` 앞에 추가:
```csharp
if (line.voiceClip != null)
{
    yield return new WaitWhile(() =>
        voiceAudioSource != null && voiceAudioSource.isPlaying && voiceAudioSource.clip == line.voiceClip);
}
```
- 클립이 있으면: 텍스트 타이핑이 음성보다 먼저 끝나도 음성이 끝날 때까지 기다렸다가 `pauseBetweenLines`만큼 더 대기 후 다음 줄로.
- 클립이 없으면: 지금과 동일하게 타이핑 종료 직후 `pauseBetweenLines`만 대기.
- `voiceAudioSource.clip == line.voiceClip` 체크는 혹시 모를 레이스(다음 줄이 이미 클립을 바꿔치기한 경우) 방지용.

## 범위 밖
- 음성 길이에 맞춰 타이핑 속도(`charsPerSecond`)를 늘리거나 줄이는 것 - 요청은 "다음 줄로 넘어가는 타이밍"만 언급, 타이핑 자체는 그대로 둠.
- 부관 외 인물 클립 배선 - 아직 준비된 파일이 없어서 범위 밖(폴더가 채워지면 같은 방식으로 이어서 진행).

## 구현 완료
- `BriefingRoomController.cs`: `PlayDialogue()`의 줄 처리 끝에서 `line.voiceClip != null`이면 `WaitWhile(voiceAudioSource.isPlaying && voiceAudioSource.clip == line.voiceClip)`로 음성 종료까지 대기 후 `pauseBetweenLines`. 클립 없으면 기존과 동일. 컴파일 성공(에러 0).
- `Briefing_Room.unity`: 위 표의 12개 `BriefingLine.voiceClip`에 `Assets/Sound/General/부관/` 클립을 SerializedProperty로 배선. 배선 전 각 항목의 `speakerLabelKey`가 `briefing.speaker.adjutant`인지 확인해서 인덱스 오배정 여지를 없앰 - 12개 전부 일치, 12개 전부 배선/저장 완료.

## 상태
완료. 부관 대사는 이제 음성이 끝나야 다음 줄로 넘어가고, 나머지 인물은 클립이 채워지는 대로 같은 방식으로 자동 적용됨(코드 쪽은 이미 대응됨, 클립만 채우면 됨).
