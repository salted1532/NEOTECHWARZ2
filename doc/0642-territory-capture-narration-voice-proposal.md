# 0642. 거점 점령 시 나레이션 음성 추가

## 요청 내용

> 거점 점령시 나레이션 음성 출력되도록 해줘

## 조사 내용

거점(비콘) 점령 로직은 `CaptureSystem.cs`가 담당한다. `controlValue`(아군 밀면 +, 적이 밀면 -)가
`±captureDuration`에 도달하면 `UpdateOwnerFromControlValue()`(`CaptureSystem.cs:141`)에서 실제
소유자가 바뀌고, 그 순간이 정확히 "점령 완료" 시점이다(`CaptureSystem.cs:150-155`, 지금은 `Debug.Log`만
찍음).

나레이션 음성은 기존에 자원/인구부족, 피격 경고, 업그레이드 완료, 임무 성공, 행동 실패 등에 이미 쓰이는
`GlobalVoiceBankSO` + `SoundManager.PlayGlobalVoice()` 패턴이 있다(doc/0255, doc/0271, doc/0464,
doc/0524). 같은 패턴으로 슬롯 하나(`territoryCaptured`)만 추가하면 된다.

**범위**: 아군(Ally)이 거점을 점령한 순간에만 재생한다. `newOwner == CaptureOwner.Ally`로 바뀔 때만
(적이 점령했을 때나 중립으로 되돌아갈 때는 재생 안 함) - "점령시 나레이션"이라는 요청 문구와
doc/0464(임무 성공)·doc/0524(행동 실패) 등 기존 나레이션들이 전부 "아군 시점의 특정 이벤트"에만
반응하는 것과 일관됨.

## 변경 계획

### `GlobalVoiceBankSO.cs`
```diff
     [field: SerializeField]
     public SoundClipSet actionFailed { get; private set; } // 행동 실패 공통 SFX(doc/0524) - ...
+    [field: SerializeField]
+    public SoundClipSet territoryCaptured { get; private set; } // 거점 점령 완료 시(doc/0642) - 아군이 거점을 점령했을 때만 재생
 }
```

### `SoundManager.cs`
```diff
     public void PlayActionFailedWarning()
     {
         if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.actionFailed);
     }
+
+    // 거점 점령 완료 시(doc/0642) - CaptureSystem이 아군 점령으로 바뀌는 순간 한 번만 호출한다.
+    public void PlayTerritoryCapturedVoice()
+    {
+        if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.territoryCaptured);
+    }
```

### `CaptureSystem.cs`
```diff
         CurrentOwner = newOwner;
         ApplyEffect(newOwner);

+        if (newOwner == CaptureOwner.Ally)
+            SoundManager.Instance?.PlayTerritoryCapturedVoice(); // 거점 점령 나레이션(doc/0642)
+
         Debug.Log($"점령 상태 변경: {newOwner}");
```

### 에디터 작업 (코드 적용 후 직접 해주셔야 함)
- `SoundManager`의 `globalVoiceBank` 필드가 참조하는 `Assets/Scripts/ScriptableObject/Sound/Global
  Voice Bank SO.asset`을 인스펙터에서 열어, 새로 생긴 `Territory Captured` 슬롯에 나레이션 음성 클립을
  등록해야 실제로 소리가 난다. 클립을 안 넣으면 `HasClips`가 false라 조용히 스킵됨(에러 없음, 무음).
  프로젝트에 아직 점령 관련 음성 파일이 없어서(`Assets/Sound/General/` 확인함) 이 부분은 자동으로 채울
  수 없음.

## 변경 예정 파일
- `Assets/Scripts/ScriptableObject/GlobalVoiceBankSO.cs`
- `Assets/Scripts/Audio/SoundManager.cs`
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs`

이대로 진행해도 될까요?

## 적용 결과

사용자 승인 후 위 diff 3개 그대로 적용. `npx uloop-cli compile` 결과 `Success: true, ErrorCount: 0`
확인(WarningCount 49는 전부 이 변경과 무관한 기존 `FindFirstObjectByType` obsolete 경고 등).

## 남은 작업 (사용자가 에디터에서 직접 해야 함)
`Assets/Scripts/ScriptableObject/Sound/Global Voice Bank SO.asset`을 인스펙터에서 열어 새로 생긴
`Territory Captured` 슬롯에 나레이션 음성 클립을 등록해야 실제로 소리가 재생됨. 클립을 안 넣으면
무음으로 조용히 스킵됨(에러 없음).

## 후속: 클립 연결 (2026-08-20)

> 여성부관_거점점령이라고 클립을 General에다가 넣어뒀는데 연결해줘

사용자가 `Assets/Sound/General/여성부관_거점점령.mp3`를 추가함. 해당 파일의 `.meta`에서 guid
(`67bb3d7f81375cb48815cb86b473abef`)를 확인해서, `Global Voice Bank SO.asset`에 다른 나레이션
슬롯과 동일한 형식으로 `territoryCaptured` 블록을 직접 추가:

```yaml
  <territoryCaptured>k__BackingField:
    <clips>k__BackingField:
    - {fileID: 8300000, guid: 67bb3d7f81375cb48815cb86b473abef, type: 3}
    <volumeScale>k__BackingField: 1
    <pitchVariance>k__BackingField: 0
```

`uloop get-logs --log-type Error`로 확인한 결과 이 에셋과 관련된 임포트 에러 없음(남은 에러 1개는
무관한 기존 `SceneMenuController.optionsPanel` 미할당 경고).

## 변경된 파일 (전체)
- `Assets/Scripts/ScriptableObject/GlobalVoiceBankSO.cs`
- `Assets/Scripts/Audio/SoundManager.cs`
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs`
- `Assets/Scripts/ScriptableObject/Sound/Global Voice Bank SO.asset`

## 변경된 파일
- `Assets/Scripts/ScriptableObject/GlobalVoiceBankSO.cs`
- `Assets/Scripts/Audio/SoundManager.cs`
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs`
