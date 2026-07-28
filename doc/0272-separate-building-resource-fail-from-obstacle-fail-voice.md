# 0272 - 건설 실패 원인별로 음성 분리 (자원부족 vs 도착 시 장애물)

**날짜:** 2026-07-28

## 요청 내용

> 자원이 부족해서 건물을 못지어서 음성이 나와야하는데 일꾼이 장애물이 있다고 음성이 나와버리네
> 일꾼을 건물을 지으러 도착했을때 장애물이있으면 그때 건설실패해야하는데 자원부족으로 실패가
> 겹치는거 같아 서로 분리하거나 어떤 건설실패인지 확인하도록 해야겠어

## 원인

건설 실패는 사실 두 가지 서로 다른 시점/원인이 있는데, 둘 다 **같은** 음성 카테고리
(`UnitSoundBankSO.buildFailVoice`, 일꾼 전용)를 재생하고 있었다:

1. **클릭 시점** - 자원(광물/가스) 부족: `PlacementSystem.PlaceStructure()`가
   `rtsController.TryConstructBuilding(data.ID)`이 실패하면 곧바로
   `worker.GetComponent<UnitAudio>()?.PlayBuildFailVoice()`를 호출했다.
2. **도착 시점** - 장애물(doc/0266/0268): `StartConstruction()`이 도착 시 `IsBlocked` 재검사에
   걸리면 역시 같은 `PlayBuildFailVoice()`를 호출했다.

`buildFailVoice`에 채워둔 클립들(doc/0261의 `Worker Drone_buildfail1~3.mp3`)이 "장애물이 있어서 못
짓는다"는 취지의 대사라서, 실제로는 자원이 부족해 실패한 상황에서도 무작위로 그 대사가 나가버려 마치
"장애물 때문에 실패했다"는 것처럼 들렸다. 반대로 자원부족 상황은 유닛 생산(`TryProduceUnit`)/연구
(`TryResearch`)에서는 이미 전역 "자원부족" 나레이션(`GlobalVoiceBankSO.insufficientResources`)을
쓰고 있어서, 건설만 이 관례에서 벗어나 있었다.

## 코드 변경

**원인별로 서로 다른 음성 카테고리를 쓰도록 분리**했다 - 자원부족은 유닛 생산/연구와 동일한 전역
"자원부족" 나레이션으로, 장애물은 일꾼의 `buildFailVoice`로 그대로 남긴다.

### 1. `Assets/Scripts/System/RTSUnitController.cs` - `TryConstructBuilding`

Before:
```csharp
    public bool TryConstructBuilding(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return false;

        if (!IsBuildingPrerequisiteMet(buildingID))
            return false;

        return resourceManager.TrySpend(data.mineral, data.gas);
    }
```

After:
```csharp
    public bool TryConstructBuilding(int buildingID)
    {
        BuildingData data = buildingDatabase.buildingData.Find(d => d.ID == buildingID);
        if (data == null)
            return false;

        if (!IsBuildingPrerequisiteMet(buildingID))
            return false; // 버튼 자체가 비활성화되므로(doc/0189) 일반 플레이에서는 도달하지 않는 방어용 분기

        if (!resourceManager.TrySpend(data.mineral, data.gas))
        {
            SoundManager.Instance?.PlayInsufficientResourcesWarning();
            return false;
        }

        return true;
    }
```

### 2. `Assets/Scripts/BuildSystem/PlacementSystem.cs` - `PlaceStructure()`

클릭 시점 실패 분기에서 일꾼의 `PlayBuildFailVoice()` 호출 제거(이제 자원부족 음성은
`TryConstructBuilding` 내부에서 재생됨).

Before:
```csharp
        if (rtsController == null || !rtsController.TryConstructBuilding(data.ID))
        {
            worker.GetComponent<UnitAudio>()?.PlayBuildFailVoice(); // "일꾼의 건설 실패 음성"
            return; // 자원/인구가 부족하면 배치하지 않음 (여기서 자원이 실제로 차감됨)
        }
```

After:
```csharp
        // 자원부족 등 클릭 시점의 실패는 TryConstructBuilding 내부에서 전역 "자원부족" 나레이션을 재생한다
        // (doc/0272) - 일꾼의 건설 실패 음성(PlayBuildFailVoice)은 도착 시 장애물 발견 케이스 전용으로 남겨둔다.
        if (rtsController == null || !rtsController.TryConstructBuilding(data.ID))
            return; // 자원/인구가 부족하거나 선행 건물 조건을 못 채우면 배치하지 않음
```

`StartConstruction()`의 도착 시 장애물 검사(doc/0266/0268)는 그대로 유지 - 여전히
`worker.GetComponent<UnitAudio>()?.PlayBuildFailVoice()`를 호출한다.

## 결과 정리

| 실패 원인 | 재생되는 소리 |
|---|---|
| 클릭 시점 - 자원(광물/가스) 부족 | 전역 "자원부족" 나레이션(`GlobalVoiceBankSO.insufficientResources`) - 유닛 생산/연구와 동일 |
| 클릭 시점 - 선행 건물 조건 미충족 | (버튼이 이미 비활성화돼 있어 사실상 도달 불가) 소리 없음 |
| 도착 시점 - 그 자리에 장애물 발견 | 일꾼의 `buildFailVoice`(장애물 관련 대사) |

## 요약/영향받는 파일

`Assets/Scripts/System/RTSUnitController.cs`(`TryConstructBuilding`에 자원부족 나레이션 재생 추가),
`Assets/Scripts/BuildSystem/PlacementSystem.cs`(클릭 시점 실패 분기에서 일꾼 음성 호출 제거).
