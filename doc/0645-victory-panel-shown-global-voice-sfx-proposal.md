# 0645. 승리화면 출력 시 사운드 - Global Voice Bank에 추가

## 요청 내용

> 승리화면 출력시 사운드 도 Global 사운드 뱅크에 추가해줄래

## 조사 내용

승리화면은 `VictoryPanelController.cs`가 담당한다. `StageManager.OnVictory` → `HandleVictory()` →
`ShowVictoryPanelAfterDelay()` 코루틴이 `victoryDelay`(기본 3초) 뒤에 `victoryPanel.SetActive(true)`로
패널을 띄운다(`VictoryPanelController.cs:70-76`). 지금은 이 시점에 사운드가 전혀 없음.

doc/0643에서 주목표 달성 순간(`StageManager.ReportVictory()`)에 이미 Mission Success 나레이션을
연결했는데, **이건 승리 "판정"이 나는 순간**이고 **승리화면이 "실제로 뜨는" 순간(그 몇 초 뒤)**은
별개 타이밍이다. 요청하신 건 후자 - 화면이 뜨는 순간의 사운드라서, 기존 `missionSuccess`와는
다른 슬롯을 새로 추가하는 게 맞다(둘 다 같은 클립을 재활용하고 싶다면 그렇게 연결해도 되지만,
슬롯 자체는 "승리화면" 전용으로 분리해두면 나중에 다른 효과음으로 바꾸고 싶을 때 서로 안 엮임).

기존 `GlobalVoiceBankSO`/`SoundManager` 나레이션 패턴을 그대로 따른다(doc/0255, doc/0464,
doc/0642 등과 동일).

## 변경 계획

### `GlobalVoiceBankSO.cs`
```diff
     [field: SerializeField]
     public SoundClipSet territoryCaptured { get; private set; } // 거점 점령 완료 시(doc/0642) - ...
+    [field: SerializeField]
+    public SoundClipSet victoryScreen { get; private set; } // 승리화면이 실제로 표시되는 순간(doc/0645)
 }
```

### `SoundManager.cs`
```diff
     public void PlayTerritoryCapturedVoice()
     {
         if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.territoryCaptured);
     }
+
+    // 승리화면이 실제로 표시되는 순간(doc/0645) - VictoryPanelController가 패널을 활성화할 때 호출.
+    public void PlayVictoryScreenVoice()
+    {
+        if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.victoryScreen);
+    }
```

### `VictoryPanelController.cs`
```diff
     private IEnumerator ShowVictoryPanelAfterDelay()
     {
         yield return new WaitForSecondsRealtime(victoryDelay);
         victoryPanel?.SetActive(true);
+        SoundManager.Instance?.PlayVictoryScreenVoice(); // 승리화면 표시 사운드(doc/0645)
         Time.timeScale = 0f;
         UserControl.IsPaused = true;
     }
```

### 에디터 작업 (코드 적용 후 직접 해주셔야 함)
- `Assets/Scripts/ScriptableObject/Sound/Global Voice Bank SO.asset`을 인스펙터에서 열어 새로 생긴
  `Victory Screen` 슬롯에 사운드 클립을 등록해야 실제로 소리가 남 (클립을 안 넣으면 무음으로 조용히
  스킵, 에러 없음). 이전처럼 `Assets/Sound/General/`에 파일을 넣어주시면 guid로 바로 연결해드립니다.

## 영향받는 파일
- `Assets/Scripts/ScriptableObject/GlobalVoiceBankSO.cs`
- `Assets/Scripts/Audio/SoundManager.cs`
- `Assets/Scripts/UI/VictoryPanelController.cs`

이대로 진행해도 될까요?

## 적용 결과

사용자 승인 후 위 diff 3개 그대로 적용. `npx uloop-cli compile` 결과 `Success: true, ErrorCount: 0`
확인(WarningCount 49는 전부 이 변경과 무관한 기존 `FindFirstObjectByType` obsolete 경고 등).

## 후속: 클립 연결 (2026-08-20)

> VictoryPanel_Sound라는 클립을 추가했어 승리화면 패널이 출력될때 같이 사운드가 나오면 될거같아

사용자가 `Assets/Sound/General/VictoryPanel_Sound.mp3`를 추가함. `.meta`에서 guid
(`8c0d55cfc02f4c14c845f25e01edce50`)를 확인해서, `Global Voice Bank SO.asset`에 다른 나레이션
슬롯과 동일한 형식으로 `victoryScreen` 블록을 직접 추가:

```yaml
  <victoryScreen>k__BackingField:
    <clips>k__BackingField:
    - {fileID: 8300000, guid: 8c0d55cfc02f4c14c845f25e01edce50, type: 3}
    <volumeScale>k__BackingField: 1
    <pitchVariance>k__BackingField: 0
```

`uloop get-logs --log-type Error`로 확인한 결과 에러 0건.

## 변경된 파일 (전체)
- `Assets/Scripts/ScriptableObject/GlobalVoiceBankSO.cs`
- `Assets/Scripts/Audio/SoundManager.cs`
- `Assets/Scripts/UI/VictoryPanelController.cs`
- `Assets/Scripts/ScriptableObject/Sound/Global Voice Bank SO.asset`
