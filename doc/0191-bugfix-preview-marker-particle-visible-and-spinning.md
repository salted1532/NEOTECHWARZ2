# 0191 - 버그: 프리뷰/고스트에서 선택 마커의 회전 파티클이 그대로 보임 (0190의 부작용)

## 요청

"왜 cellindicator가 회전하지?" → "아마 Marker가 회전하는 속도랑 같은거 보니깐 마커가 문제인거
같아 이를 확인해보고 수정해줘" — 건설 배치 프리뷰(마우스를 따라다니는 고스트)에서 뭔가 계속
회전하는 게 보이는데, 사용자가 "Marker"(선택 링)의 회전 속도와 같아 보인다고 지적. 실제로
확인해보니 사용자 말이 맞다.

## 원인

- `Assets/prefabs/NTA/Building/Tier1.prefab`(병영) 등 건물 프리팹에는 `Marker`라는 자식 오브젝트가
  있고(`BuildingController.buildingMarker`가 참조), 그 밑에 `Circle Select Green`이라는 파티클
  이펙트(선택 링) 프리팹이 중첩되어 있다. 이 파티클 시스템은 `playOnAwake: 1`, `looping: 1`,
  `RotationModule.enabled: 1`(파티클이 계속 회전)으로 설정돼 있다 - 즉 이 오브젝트가 씬에서
  활성화(Active)되는 즉시, 껐다 켜주지 않는 한 계속 재생되며 도는 이펙트다.
- `Marker`(부모) 자신의 `MeshRenderer`는 프리팹에 `m_Enabled: 0`(꺼짐)으로 저장돼 있지만, 이건
  `Marker` 자기 자신의 렌더러만 끄는 것이지 자식인 `Circle Select Green` 파티클의 렌더링/재생과는
  무관하다. `Marker` GameObject 자체의 활성화 상태(`m_IsActive`)는 `1`(켜짐)이라서, 자식 파티클은
  프리팹을 Instantiate하는 순간부터 바로 재생되기 시작한다.
- 원래는 `BuildingController.Start()`(74번째 줄)의 첫 줄 `buildingMarker.SetActive(false);`가 이
  `Marker`(및 그 자식 파티클 전체)를 통째로 꺼서 평소엔 안 보이게 만들어왔다.
- 그런데 [[0190-bugfix-preview-ghost-registers-as-completed-building]]에서 프리뷰/고스트가
  `BuildingList`에 잘못 등록되는 것을 막기 위해 `PreviewSystem.DisableGameplayComponents()`가
  프리뷰/고스트 오브젝트의 `BuildingController` 컴포넌트 자체를 `enabled = false`로 꺼버렸다.
  컴포넌트를 비활성화하면 `Start()`가 아예 호출되지 않으므로, 그 안의
  `buildingMarker.SetActive(false)`도 실행되지 않는다 - 결과적으로 `Marker`(와 그 안의 회전하는
  `Circle Select Green` 파티클)가 프리뷰/고스트에서 그대로 켜진 채 노출되어 계속 회전하는 것처럼
  보인다. 0190에서 고친 버그(테크 트리 조기 해제)를 잡으면서 새로 생긴 부작용이다.

## 계획된 코드 변경

`BuildingController`가 `Start()` 없이도 마커를 확실히 숨길 수 있는 공개 메서드를 추가하고,
`PreviewSystem`이 `BuildingController`를 비활성화하기 직전에 그 메서드를 호출한다.
(`buildingMarker`는 `[SerializeField]`라 `Instantiate()` 직후, `Start()`가 돌기 전에도 이미 값이
채워져 있으므로 즉시 호출 가능하다.)

### 1. `Assets/Scripts/Building/BuildingController.cs`

Before:
```csharp
    public Sprite GetIcon() => icon;
    public int GetBuildingID() => buildingID;
```

After:
```csharp
    public Sprite GetIcon() => icon;
    public int GetBuildingID() => buildingID;

    // 프리뷰/고스트용: PreviewSystem이 이 컴포넌트를 비활성화(Start() 자체가 안 돎)하기 직전에 호출해,
    // 마커(및 그 자식의 상시 재생 파티클인 Circle Select 등)가 켜진 채로 노출되지 않도록 미리 숨긴다.
    public void HideMarkerForGhost()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);
    }
```

### 2. `Assets/Scripts/BuildSystem/PreviewSystem.cs`

Before:
```csharp
        // 프리뷰/고스트는 실제로 지어진 건물이 아니므로 RTSUnitController.BuildingList(테크 트리 선행
        // 조건 판정용)에 등록되면 안 된다. BuildingController.Start()가 아예 호출되지 않도록 미리
        // 비활성화한다(FogRevealerAgent와 동일한 이유 - doc/0190).
        BuildingController[] buildingControllers = obj.GetComponentsInChildren<BuildingController>();
        foreach (BuildingController bc in buildingControllers)
        {
            bc.enabled = false;
        }
    }
```

After:
```csharp
        // 프리뷰/고스트는 실제로 지어진 건물이 아니므로 RTSUnitController.BuildingList(테크 트리 선행
        // 조건 판정용)에 등록되면 안 된다. BuildingController.Start()가 아예 호출되지 않도록 미리
        // 비활성화한다(FogRevealerAgent와 동일한 이유 - doc/0190).
        // Start()를 꺼버리면 그 안의 buildingMarker.SetActive(false)도 같이 스킵되어, 마커 하위의
        // 상시 재생 파티클(Circle Select 등)이 켜진 채로 보이게 된다 - 비활성화 전에 먼저 마커를
        // 직접 숨긴다 (doc/0191).
        BuildingController[] buildingControllers = obj.GetComponentsInChildren<BuildingController>();
        foreach (BuildingController bc in buildingControllers)
        {
            bc.HideMarkerForGhost();
            bc.enabled = false;
        }
    }
```

## 요약/영향받는 파일

- 수정 파일: `Assets/Scripts/Building/BuildingController.cs` (`HideMarkerForGhost()` 공개 메서드
  추가), `Assets/Scripts/BuildSystem/PreviewSystem.cs` (`BuildingController`를 끄기 직전에
  `HideMarkerForGhost()` 호출 추가).
- 동작 변화: 건설 배치 프리뷰(마우스 추적 고스트)와 배치 확정 후 일꾼 도착 대기 고스트 양쪽에서
  선택 마커(및 그 안의 회전 파티클 `Circle Select`)가 더 이상 보이지 않는다. `BuildingList` 미등록
  (0190) 동작은 그대로 유지된다.

## 확인 필요

이대로 진행해도 될까요?
