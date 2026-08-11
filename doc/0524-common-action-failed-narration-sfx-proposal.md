# 0524. 행동 실패 시 공통 나레이션 SFX 추가 - 제안

**날짜:** 2026-08-11

## 요청 내용

> 어떤 행동을 못할때 ex)건물 유닛 생산중이라 착륙이 불가할때. 같이 어떤 행동 실패시 공통적으로 작동하는
> 나레이션 SFX를 추가해줘 이건 자원부족, 인구부족, 건설실패 등 모든 실패시 작동해야해

## 조사 내용

현재 "행동 실패" 경고는 전부 `UIController.ShowWarning(string message)` (`UIController.cs:728`) 한
곳으로 모인다. 실제 호출부는 6곳뿐이고, 전부 실패 상황이다:

| 호출부 | 실패 종류 | 현재 사운드 |
|---|---|---|
| `RTSUnitController.cs:1478` (유닛 생산) | 자원부족 | `PlayInsufficientResourcesWarning()` (전역 나레이션) |
| `RTSUnitController.cs:1484` (유닛 생산) | 인구부족 | `PlayInsufficientPopulationWarning()` (전역 나레이션) |
| `RTSUnitController.cs:1513` (연구) | 자원부족 | `PlayInsufficientResourcesWarning()` |
| `RTSUnitController.cs:1605` (건설 시작) | 자원부족 | `PlayInsufficientResourcesWarning()` |
| `RTSUnitController.cs:730` (이륙) | 생산 중이라 이륙 불가 (doc/0519) | **없음** |
| `UnitController.cs:1102` (건설 이동 중 도달 불가) | 건설 실패 | `unitAudio.PlayBuildFailVoice()` (일꾼 개별 목소리) |
| `PlacementSystem.cs:224` (건설 위치 장애물) | 건설 실패 | `worker.GetComponent<UnitAudio>().PlayBuildFailVoice()` (일꾼 개별 목소리) |

즉 지금은 실패 종류별로 사운드가 제각각이다: 자원/인구부족만 전역 나레이션이 있고, 건설실패는 일꾼마다
다른 개별 음성(있는 유닛만), 이륙 실패는 아예 무음이다. 요청대로 "모든 실패에 공통으로 작동하는" 소리를
만들려면, 실패마다 개별로 챙기는 대신 이미 모든 실패가 반드시 거쳐가는 `ShowWarning()` 한 곳에 훅을 걸면
된다 (root-cause 방식 - 호출부 6곳을 일일이 고칠 필요 없고, 나중에 실패 케이스가 추가돼도 자동으로
적용됨).

기존 전역 나레이션(`GlobalVoiceBankSO`)과 동일한 패턴으로 새 `SoundClipSet` 슬롯 하나
(`actionFailed`)를 추가하고, `SoundManager`에 재생 래퍼를 추가한 뒤, `ShowWarning()` 안에서 호출한다.
기존의 자원/인구부족 전용 나레이션(구체적인 대사)은 그대로 유지하고, 이 공통 SFX는 그 위에 추가로
겹쳐 재생된다(길이가 짧은 "행동 거부" 효과음이면 목소리 대사와 겹쳐도 자연스러움 - 스타크래프트 류
게임에서 "삐" 하는 거부음 + 대사가 같이 나오는 것과 동일한 패턴).

## 변경 계획

### `GlobalVoiceBankSO.cs`
```diff
     [field: SerializeField]
     public SoundClipSet missionSuccess { get; private set; } // 임무(스테이지) 목표 달성 시(doc/0464)
+    [field: SerializeField]
+    public SoundClipSet actionFailed { get; private set; } // 행동 실패 공통 SFX(doc/0524) - 자원/인구부족, 건설실패, 이륙불가 등 ShowWarning()이 뜨는 모든 경우에 공통 재생
 }
```

### `SoundManager.cs`
```diff
     public void PlayMissionSuccessVoice()
     {
         if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.missionSuccess);
     }
+
+    // 행동 실패 공통 SFX(doc/0524) - UIController.ShowWarning()이 호출될 때마다 재생된다.
+    public void PlayActionFailedWarning()
+    {
+        if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.actionFailed);
+    }
```

### `UIController.cs`
```diff
     public void ShowWarning(string message)
     {
         if (warningText == null)
             return;
 
+        SoundManager.Instance?.PlayActionFailedWarning(); // 모든 실패 경고 공통 SFX(doc/0524)
         warningText.text = message;
 
         if (warningHideCoroutine != null)
             StopCoroutine(warningHideCoroutine);
         warningHideCoroutine = StartCoroutine(HideWarningAfterDelay());
     }
```

### 에디터 작업 (코드 적용 후 직접 해주셔야 함)
- `GlobalVoiceBankSO` 에셋(어디에 있는지: `SoundManager`의 `globalVoiceBank` 필드가 참조하는 에셋)의
  인스펙터에서 새로 생긴 `Action Failed` 슬롯에 클립을 등록해야 실제로 소리가 난다. 클립을 안 넣으면
  `HasClips`가 false라 조용히 스킵됨(에러 없음).

## 변경 예정 파일
- `Assets/Scripts/ScriptableObject/GlobalVoiceBankSO.cs`
- `Assets/Scripts/Audio/SoundManager.cs`
- `Assets/Scripts/UI/UIController.cs`

---

## 적용 (사용자 승인 후)

> 진행 (Recommended)

제안대로 3개 파일 전부 위 diff 그대로 적용함. `npx uloop-cli compile` 성공 확인 (Error 0개, Warning
0개).

## 남은 작업 (사용자가 에디터에서 직접 해야 함)
- `SoundManager`의 `globalVoiceBank` 필드가 참조하는 `GlobalVoiceBankSO` 에셋을 인스펙터에서 열어,
  새로 생긴 `Action Failed` 슬롯에 효과음 클립을 등록해야 실제로 소리가 재생된다. 클립을 안 넣으면
  `HasClips`가 false라 조용히 스킵됨(에러 없음, 그냥 무음).

## 변경된 파일
- `Assets/Scripts/ScriptableObject/GlobalVoiceBankSO.cs`
- `Assets/Scripts/Audio/SoundManager.cs`
- `Assets/Scripts/UI/UIController.cs`
