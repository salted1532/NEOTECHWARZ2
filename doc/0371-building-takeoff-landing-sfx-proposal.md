# 0371 - 건물 이륙/착륙 SFX 추가

**날짜:** 2026-08-03

**승인 후 구현 완료.** 열린 질문 2개 모두 권장안(이륙음 일단 비워둠 / 진영당 1개 공유 뱅크 유지)으로 확정.

## 요청 내용

> 건물 사운드뱅크에다가 이륙, 착륙시 발생하는 SFX 사운드 추가해줘

## 조사 결과

- 건물 리프트/착륙 로직은 `BuildingController.cs`에 이미 있음: `LiftOff()`(270줄)에서
  `GetComponent<BuildingEffects>()?.PlayTakeoff();`(291줄), `Land()`(357줄)에서
  `GetComponent<BuildingEffects>()?.PlayLanding();`(379줄) — 파티클 이펙트만 재생되고 있고, 사운드는
  아직 안 걸려 있음.
- `BuildingSoundBankSO.cs`(doc/0255)에는 현재 `constructLoopSFX`/`constructCompleteSFX`/`destroySFX`/
  `selectVoice` 4개 슬롯만 있고 이륙/착륙 슬롯이 없음.
- `BuildingAudio.cs`(건설/파괴/선택 사운드를 재생하는 전담 컴포넌트, `BuildingEffects`와 나란히 붙는
  구조)에도 이륙/착륙용 재생 메서드가 없음.
- 사운드 파일: 사용자가 이미 6개 건물별 폴더(`Barracks`/`CommandCenter`/`Factory`/`Lab`/`Spaceport`/
  `SupplyDepot`, 전부 NTA만)에 `Landing_Sound.mp3` + `Landing_Sound2.mp3`를 추가해둠(현재 git에 미커밋
  상태). 6개 폴더의 파일을 해시 비교해보니 전부 동일한 내용(placeholder 복사본) — **이륙(Takeoff)용
  사운드 파일은 아직 없음.**
- 건물 사운드뱅크 에셋은 건물 종류별이 아니라 **진영별로 딱 1개씩**만 존재함
  (`NTA Building Sound Bank SO.asset`, `OC Building Sound Bank SO.asset`) — 모든 건물이 같은 뱅크를
  공유하는 구조. 다만 확인해보니 이 두 에셋은 `NTA Building Data SO.asset`의 어떤 건물 항목에도 아직
  연결(`soundBank` 필드)돼 있지 않음 — doc/0255 마지막에 "에디터에서 직접 해야 함"으로 남겨둔 미완료
  작업이라, 지금 건드리는 범위 밖(기존에 있던 별개 이슈)이라 이번 건에서는 손대지 않음.
- OC 쪽은 이륙/착륙 사운드 파일이 폴더 자체가 비어있어(`Assets/Sound/OC/Building/*/SFX` 전부 빈 폴더)
  아직 채울 게 없음.

## 코드 변경 (제안)

### `Assets/Scripts/ScriptableObject/BuildingSoundBankSO.cs`

기존 코드:
```csharp
[field: SerializeField]
public SoundClipSet destroySFX { get; private set; }
[field: SerializeField]
public SoundClipSet selectVoice { get; private set; } // "건물 음성" - 선택 시 재생
```

변경 코드:
```csharp
[field: SerializeField]
public SoundClipSet destroySFX { get; private set; }
[field: SerializeField]
public SoundClipSet takeoffSFX { get; private set; } // 리프트 이륙 시
[field: SerializeField]
public SoundClipSet landingSFX { get; private set; } // 착륙 완료 시
[field: SerializeField]
public SoundClipSet selectVoice { get; private set; } // "건물 음성" - 선택 시 재생
```

### `Assets/Scripts/Audio/BuildingAudio.cs`

기존 코드 (`PlaySelectVoice()` 위):
```csharp
    // BaseStructure.CompleteConstruction()에서 ConstructionEffects.StopLoopAndPlayComplete()와 나란히 호출된다.
    public void PlayConstructComplete()
    {
        BuildingSoundBankSO bank = GetBank();
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.constructCompleteSFX, transform.position);
    }
```

변경 코드 (사이에 두 메서드 추가):
```csharp
    // BaseStructure.CompleteConstruction()에서 ConstructionEffects.StopLoopAndPlayComplete()와 나란히 호출된다.
    public void PlayConstructComplete()
    {
        BuildingSoundBankSO bank = GetBank();
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.constructCompleteSFX, transform.position);
    }

    // BuildingController.LiftOff()에서 BuildingEffects.PlayTakeoff()와 나란히 호출된다.
    public void PlayTakeoff()
    {
        BuildingSoundBankSO bank = GetBank();
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.takeoffSFX, transform.position);
    }

    // BuildingController.Land()에서 BuildingEffects.PlayLanding()와 나란히 호출된다.
    public void PlayLanding()
    {
        BuildingSoundBankSO bank = GetBank();
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.landingSFX, transform.position);
    }
```

### `Assets/Scripts/Building/BuildingController.cs`

기존 코드 (291줄):
```csharp
        GetComponent<BuildingEffects>()?.PlayTakeoff();
    }
```

변경 코드:
```csharp
        GetComponent<BuildingEffects>()?.PlayTakeoff();
        GetComponent<BuildingAudio>()?.PlayTakeoff();
    }
```

기존 코드 (379줄):
```csharp
        GetComponent<BuildingEffects>()?.PlayLanding();
    }
```

변경 코드:
```csharp
        GetComponent<BuildingEffects>()?.PlayLanding();
        GetComponent<BuildingAudio>()?.PlayLanding();
    }
```

### 에셋 클립 연결

- `NTA Building Sound Bank SO.asset`의 `landingSFX.clips`에 이미 있는 `Landing_Sound.mp3` /
  `Landing_Sound2.mp3`를 연결 (6개 폴더 내용이 동일하므로 대표로 `CommandCenter` 폴더 것을 참조).
- `takeoffSFX`는 소스 오디오 파일이 없어서 **비워둠**(슬롯만 만들어두고, `SoundClipSet.GetRandomClip()`이
  빈 리스트면 null을 반환해 조용히 스킵되는 기존 안전장치 그대로 적용). 이륙음 파일을 추가하면 그때
  같은 방식으로 채워 넣으면 됨.
- `OC Building Sound Bank SO.asset`은 이륙/착륙 오디오 파일 자체가 없어서 두 슬롯 다 비워둠.

## 열린 질문 (확정된 답)

1. **이륙(Takeoff) 사운드 파일이 아직 없음** → **일단 비워둠** (권장안 채택). `takeoffSFX` 슬롯만
   만들어두고 클립은 비워서, `SoundClipSet.GetRandomClip()`이 null을 반환해 조용히 스킵됨. 이륙음
   파일이 생기면 그때 `NTA Building Sound Bank SO.asset`/`OC Building Sound Bank SO.asset`의
   `takeoffSFX.clips`에 채워 넣으면 됨.
2. 건물별로 다른 소리를 낼지 vs 진영당 1개 공유 뱅크 유지 → **지금처럼 진영당 1개 공유 유지**(권장안
   채택, 최소변경). 건물 종류별로 다른 착륙음을 나중에 원하면 `BuildingSoundBankSO` 에셋을 건물별로
   쪼개는 별도 작업으로 진행.

## 구현 결과

제안대로 구현 완료, 컴파일 오류 0건(기존에도 있던 `FindFirstObjectByType` obsolete 경고만 있고 이번
변경으로 추가된 경고 없음).

### 실제 수정된 파일

- `Assets/Scripts/ScriptableObject/BuildingSoundBankSO.cs`: `takeoffSFX`/`landingSFX` 필드 추가.
- `Assets/Scripts/Audio/BuildingAudio.cs`: `PlayTakeoff()`/`PlayLanding()` 메서드 추가.
- `Assets/Scripts/Building/BuildingController.cs`: `LiftOff()`(291줄 부근)에 `PlayTakeoff()` 호출,
  `Land()`(379줄 부근)에 `PlayLanding()` 호출 추가.
- `Assets/Scripts/ScriptableObject/Sound/NTA/Building/NTA Building Sound Bank SO.asset`: `landingSFX.clips`에
  `CommandCenter/SFX/Landing_Sound.mp3` + `Landing_Sound2.mp3` 연결 (guid `934ce331b8a51fb43b221f589ec4316e`,
  `2215786d60d666b4db766fa540eb3785`). `takeoffSFX`는 빈 슬롯만 추가.
- `Assets/Scripts/ScriptableObject/Sound/OC/Building/OC Building Sound Bank SO.asset`: `takeoffSFX`/
  `landingSFX` 빈 슬롯만 추가 (OC 쪽은 이륙/착륙 오디오 파일 자체가 없음).

### 미변경(이번 범위 밖)

- `NTA Building Data SO.asset`의 각 건물 항목에 `soundBank`가 아직 연결 안 된 것 — doc/0255에서부터
  이어져온 별개의 기존 이슈(에디터에서 직접 연결해야 함). 이번 이륙/착륙 SFX 추가와는 무관하게 이 연결이
  안 돼 있으면 건물 사운드뱅크 전체(건설/파괴/이륙/착륙 전부)가 조용함.
