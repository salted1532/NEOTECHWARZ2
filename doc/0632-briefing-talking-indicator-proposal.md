# 0632 - 말하는 중 표시(Taking 이미지) on/off (제안)

## 요청
`speakerPortraitImage`/`2`/`3` 아래에 사용자가 새로 추가한 자식 오브젝트 `Taking`(말풍선/발화 표시용 이미지로 보임)을,
해당 슬롯 인물이 지금 대사를 말하고 있는 동안만 활성화하고 그 줄이 끝나면 꺼달라.

## 현재 상태
씬 확인 결과 `Taking` 오브젝트가 이미 3개 존재하고 각각 정확히 슬롯별로 배치되어 있음:
- `speakerPortraitImage` (slot 1) → 자식 `Taking` (fileID 842166391)
- `speakerPortraitImage2` (slot 2) → 자식 `Taking` (fileID 1978342655)
- `speakerPortraitImage3` (slot 3) → 자식 `Taking` (fileID 2146566827)

`BriefingRoomController.cs`는 지금 이 오브젝트들을 전혀 참조하지 않음. `PlayDialogue()`가 줄마다 `speakerSlot`을 이미 알고 있으므로 그 슬롯의 `Taking`을 켜고, 그 줄의 텍스트+음성이 다 끝나면 끄면 됨.

## 제안 설계
1. `BriefingRoomController`에 슬롯별 참조 3개 추가 (기존 `speakerPortraitImage1/2/3` 패턴과 동일):
   ```csharp
   [SerializeField] private GameObject talkingIndicator1;
   [SerializeField] private GameObject talkingIndicator2;
   [SerializeField] private GameObject talkingIndicator3;
   ```
2. `GetSlotImage(slot)`와 같은 패턴으로 `GetSlotTalkingIndicator(slot)` 추가, `SetTalkingIndicator(slot, active)` 헬퍼로 SetActive 호출.
3. `PlayDialogue()`의 각 줄 처리:
   - 줄 시작 시(`RevealPortraitIfNeeded` 직후) `SetTalkingIndicator(line.speakerSlot, true)`.
   - 그 줄의 텍스트 타이핑 + (있으면) 음성 종료 대기 + `pauseBetweenLines`까지 전부 끝난 직후 `SetTalkingIndicator(line.speakerSlot, false)`.
4. `ApplyStaticContent()`(브리핑 진입 시 초기화)에서 3개 전부 `false`로 시작 - 씬 진입 시 꺼진 상태로 시작.
5. 씬 배선: `Briefing_Room.unity`의 `Canvas`(BriefingRoomController) 인스펙터에서 `talkingIndicator1/2/3`을 각각 위 3개 `Taking` 오브젝트로 연결.

## 범위 밖
- `[System]` 안내문("브리핑 시작."/"브리핑 끝.")에는 화자가 없으므로 talking indicator 동작 없음.
- 같은 인물이 연속으로 말하는 경우 깜빡임(꺼졌다 바로 켜짐) 방지 같은 디테일 - 요청에 없음, 매 줄마다 단순 on/off.

## 구현 완료
- `BriefingRoomController.cs`: `talkingIndicator1/2/3`(GameObject) 필드 추가, `SetTalkingIndicator(slot, active)` 헬퍼 추가. `ApplyStaticContent()`에서 3개 전부 초기 `false`. `PlayDialogue()`의 줄 처리에서 `RevealPortraitIfNeeded`/`PlayLineVoice` 직후 `SetTalkingIndicator(line.speakerSlot, true)`, 텍스트 타이핑 + (있으면) 음성 종료 대기까지 끝난 직후(= `pauseBetweenLines` 대기 전) `SetTalkingIndicator(line.speakerSlot, false)`. 컴파일 성공(에러 0).
- `Briefing_Room.unity`: `talkingIndicator1/2/3`을 각각 `speakerPortraitImage`/`2`/`3` 밑의 `Taking` 오브젝트로 연결, 씬 저장 완료.

## 상태
완료. 지금 클립이 채워진 부관 대사는 텍스트+음성 재생 내내 `Taking`이 켜져 있다가 끝나면 꺼지고, 클립 없는 인물도 텍스트 타이핑 구간 동안은 동일하게 켜졌다 꺼짐.
