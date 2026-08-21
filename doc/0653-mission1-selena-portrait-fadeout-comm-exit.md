# 0653 - 메인 미션1 브리핑: 셀레나 대사→아드리안 대사 이후 셀레나 초상화 페이드아웃

## 날짜
2026-08-21

## 요청 내용
"메인 미션1 브리핑에서 셀레나 대사하고 아드리안 대사하고 나서 셀레나 인물 이미지가 페이드아웃 됬으면 좋겠어 / 브리핑 통신에서 나간 느낌으로"

대상은 `BriefingRoomController.briefingEntries`에서 `missionNumber: 1, isSubMission: 0`인 항목(3인 화자: 부관/셀레나/아드리안, `[Selena]Mission 1 - Line 1.mp3` 등 이번에 연결한 대사가 속한 바로 그 항목 - doc/0652 참고). 대사 순서: 부관(1.0)→셀레나(1.1)→아드리안(1.2)→부관(1.3)→아드리안(1.4). "셀레나 대사하고 아드리안 대사하고 나서"는 1.1(셀레나)과 1.2(아드리안)가 끝난 직후를 가리킴.

## 조사 내용
기존 `BriefingRoomController`는 각 슬롯 초상화를 그 슬롯이 처음 말할 때 1회만 페이드인하고(`RevealPortraitIfNeeded`), 전체 페이드아웃은 브리핑이 통째로 끝날 때(`FadeOutAllPortraits`) 한 번뿐이었다 - 대화 중간에 특정 인물만 먼저 퇴장하는 연출 수단이 없었다. 대사 데이터(`BriefingLine`)가 인스펙터/씬 파일에 완전히 데이터 기반으로 들어있으므로, 다른 미션에서도 재사용 가능하도록 줄 단위로 "이 줄이 끝나면 어떤 슬롯을 페이드아웃할지" 지정하는 필드를 추가하는 방식으로 구현.

## 코드 변경

### `Assets/Scripts/UI/BriefingRoomController.cs`

**BriefingLine에 필드 추가**
```csharp
// 기존
public string textKey; // 그 줄의 대사, 예: "briefing.line.1.0"
public AudioClip voiceClip; // TTS로 뽑은 이 줄의 음성 (doc/0630). 없으면 텍스트만 타이핑되고 무음.
}

// 변경
public string textKey; // 그 줄의 대사, 예: "briefing.line.1.0"
public AudioClip voiceClip; // TTS로 뽑은 이 줄의 음성 (doc/0630). 없으면 텍스트만 타이핑되고 무음.
public int fadeOutPortraitSlotAfter; // 0=없음, 1~3이면 이 줄이 끝난 뒤 그 슬롯 초상화를 페이드아웃 (통신 이탈 연출, doc/0653)
}
```

**PlayDialogue에서 줄이 끝난 뒤 처리**
```csharp
// 기존
            SetTalkingIndicator(line.speakerSlot, false);
            yield return new WaitForSeconds(pauseBetweenLines);
        }

// 변경
            SetTalkingIndicator(line.speakerSlot, false);
            yield return new WaitForSeconds(pauseBetweenLines);

            // 특정 인물이 대화 중간에 통신에서 먼저 빠지는 연출(doc/0653) - 지정된 줄이 끝나면 그 슬롯
            // 초상화만 페이드아웃한다. revealedSlots에서 제거해서 같은 슬롯이 나중에 다시 말하면 처음처럼
            // 다시 페이드인되게 한다.
            if (line.fadeOutPortraitSlotAfter != 0)
            {
                Image slotImage = GetSlotImage(line.fadeOutPortraitSlotAfter);
                if (slotImage != null)
                    StartCoroutine(FadeTo(slotImage, 0f, portraitFadeDuration));
                revealedSlots.Remove(line.fadeOutPortraitSlotAfter);
                SetTalkingIndicator(line.fadeOutPortraitSlotAfter, false);
            }
        }
```

### `Assets/Scenes/Missions/Briefing_Room.unity`
`missionNumber: 1, isSubMission: 0` 항목의 아드리안 줄(`briefing.line.1.2`)에 `fadeOutPortraitSlotAfter: 2`(셀레나 슬롯) 추가.
```yaml
# 기존
    - speakerSlot: 3
      speakerLabelKey: briefing.speaker.adrian
      textKey: briefing.line.1.2
      voiceClip: {fileID: 8300000, guid: 485fa206db5b39f48acd593b64177230, type: 3}

# 변경
    - speakerSlot: 3
      speakerLabelKey: briefing.speaker.adrian
      textKey: briefing.line.1.2
      voiceClip: {fileID: 8300000, guid: 485fa206db5b39f48acd593b64177230, type: 3}
      fadeOutPortraitSlotAfter: 2
```
(다른 줄들은 이 필드가 씬 파일에 없어도 C# 기본값 0으로 취급돼 영향 없음 - 굳이 전부에 `fadeOutPortraitSlotAfter: 0`을 써넣지 않음)

## 결과
`npx uloop-cli compile --wait-for-domain-reload true`로 컴파일 확인 - Success, 에러 0건(경고 49건은 전부 기존에 있던 것, 이번 변경과 무관).

미션1(missionNumber:1) 브리핑에서 셀레나(1.1) → 아드리안(1.2) 대사가 끝나면 셀레나 초상화만 페이드아웃되고, 부관(1.3)→아드리안(1.4)은 그대로 이어진다. 다른 미션에도 같은 연출이 필요하면 해당 줄에 `fadeOutPortraitSlotAfter`만 지정하면 재사용 가능.

## 변경된 파일
- `Assets/Scripts/UI/BriefingRoomController.cs`
- `Assets/Scenes/Missions/Briefing_Room.unity`
