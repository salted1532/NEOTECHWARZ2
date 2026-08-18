# 0609. 서브미션1 다중 비콘 "조종 가능 전환" 시스템 (제안)

**날짜:** 2026-08-18

## 요청 내용
> 서브미션1에서 맵에 추가 아군유닛을 배치하고 싶은데 비콘으로 유닛이 닿으면 해당 유닛을 조종할수
> 있도록 하고 싶은데 아군OC 구조에 매커니즘과 같이 조종불가 + 시야 낮게 설정시키고 비콘과 연계되어
> 해당 비콘에 도착하면 조종가능하게 작동하도록 할 수 있나? 각 비콘별 조종가능한 유닛들을 넣도록
> 리스트로 작동하게 하면 좋을거 같아 비콘도 여러개 둘수도 있게

## 조사 결과 (현재 코드 상태)
- 사용자가 말한 "아군OC 구조 메커니즘"은 Mission3(`Stage3Objectives.cs`)에 이미 완성돼 있음
  ([[0458-rescue-oc-unit-suppression-proposal|0458]] → [[0459-rescue-unit-redesign-isRescueUnit|0459]]
  → [[0461-rescue-beacon-trigger-and-green-marker-effect|0461]]). 핵심은 전부 `UnitController.cs`
  자체에 이미 범용으로 들어가 있어서, **스크립트 수정 없이 인스펙터 설정만으로 재사용 가능**함:
  - `isRescueUnit`(bool): 켜두면 이동/공격/정지 등 명령 진입점 13곳이 전부 무시됨(선택은 그대로 가능,
    자동교전도 그대로 작동 - 스스로 방어는 함). 씬에 배치할 때 인스펙터에서 체크만 해두면 됨.
  - 시야는 같은 오브젝트의 `FogRevealerAgent.sightRange`를 배치 시점에 낮은 값(예: 1)으로 직접
    설정해두면 됨(전용 필드 아님 - 원래 있는 시야 필드를 그냥 낮게 둠).
  - `rescuedSightRange`(기본 25): `Rescue()` 호출 시 시야를 이 값으로 되돌림.
  - `rescuedMarker`/`preRescueMarker`: 구조 전/후 마커 색 구분용 - 선택 사항(비워두면 그냥 안 씀,
    필드가 전부 null 체크로 감싸져 있음).
  - `public void IsTouching(Collider other)` / `OnTriggerEnter`/`OnTriggerExit`: 유닛이 특정 트리거
    콜라이더(비콘)에 실제로 닿아 있는지 판정.
  - `public void Rescue()`: `isRescueUnit = false` + 시야 복원 + 마커 전환 + 인구수 반영. 중복 호출은
    스스로 막음(`if (!isRescueUnit) return;`).
- `Stage3Objectives.cs`가 이 메커니즘을 실제로 쓰는 예시지만, **비콘 1개 + 유닛 리스트 1개**로
  고정돼 있고 승리 조건(외계 전초기지 파괴)까지 같이 처리하는 미션 전용 스크립트라 그대로 재사용은
  안 됨.
- Sub_Mission1엔 이미 `SubStage1Objectives.cs`가 있지만(레이더 기지 파괴/정찰병 전멸 판정) 이 요청과는
  무관한 별개 책임이라, 거기에 얹기보다 **독립된 재사용 컴포넌트**로 새로 만드는 편이 낫다고 판단함
  (Sub_Mission2~4에서도 같은 기능이 필요해지면 그대로 재사용 가능).

## 설계안

### 신규 `Assets/Scripts/System/RescueBeaconSystem.cs`

`Stage3Objectives`의 비콘 판정 로직(`IsAnyUnitTouchingBeacon`/`RescueSequence`)을 그대로 가져오되,
비콘 1개 대신 `List<BeaconEntry>`로 일반화. 각 엔트리가 자기 비콘 콜라이더 + 조종 가능해질 유닛
리스트를 갖고, 엔트리별로 독립적으로 판정/1회만 실행됨.

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 비콘에 (등록되지 않은) 아군 유닛이 닿으면, 그 비콘에 미리 등록해둔 조종불가(isRescueUnit) 유닛들을
// 한꺼번에 조종 가능하게 전환한다. Mission3 "생존자 구조" 메커니즘(Stage3Objectives, doc/0458/0459/0461)을
// 여러 비콘에 대해 동시에 지원하도록 일반화한 것 - 실제 유닛 상태 전환(시야 복원/마커/인구수)은 전부
// UnitController.Rescue()가 담당하므로 여기서는 "비콘 접촉 판정 → Rescue() 호출"만 담당한다.
public class RescueBeaconSystem : MonoBehaviour
{
    [Serializable]
    private class BeaconEntry
    {
        public Collider beacon; // 비콘의 트리거 콜라이더 - 직접 연결
        public List<UnitController> units; // 이 비콘에 도착하면 조종 가능해질 유닛들 - 직접 연결
        [HideInInspector] public bool triggered;
    }

    [SerializeField] private List<BeaconEntry> beacons;
    [SerializeField] private float rescueStaggerInterval = 0.1f; // Stage3Objectives와 동일한 목적(doc/0466)

    private RTSUnitController rtsController;

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    private void Update()
    {
        if (rtsController == null)
            return;

        foreach (BeaconEntry entry in beacons)
        {
            if (entry.triggered || entry.beacon == null)
                continue;

            if (IsAnyUnitTouchingBeacon(entry))
            {
                entry.triggered = true;
                StartCoroutine(RescueSequence(entry));
            }
        }
    }

    private IEnumerator RescueSequence(BeaconEntry entry)
    {
        foreach (UnitController unit in entry.units)
        {
            unit?.Rescue();
            yield return new WaitForSeconds(rescueStaggerInterval);
        }
    }

    // 그 비콘 자신의 리스트에 속한 유닛은 판정에서 제외한다 - isRescueUnit 유닛도 UnitList엔 등록돼
    // 있어서(doc/0459), 처음부터 비콘 근처에 배치돼 있으면 자기 자신만으로 즉시 완료돼버리는 문제를
    // 막기 위함 (Stage3Objectives.IsAnyUnitTouchingBeacon과 동일한 이유).
    private bool IsAnyUnitTouchingBeacon(BeaconEntry entry)
    {
        foreach (UnitController unit in rtsController.UnitList)
        {
            if (unit == null || entry.units.Contains(unit))
                continue;

            if (unit.IsTouching(entry.beacon))
                return true;
        }
        return false;
    }
}
```

`SubStage1Objectives`와는 완전히 독립된 컴포넌트라, 씬의 아무 GameObject(예: 기존 System/Managers
오브젝트)에 붙이고 `beacons` 리스트에 비콘 개수만큼 엔트리를 추가하면 됨.

### 씬 구성 (Sub_Mission1.unity)

1. `Assets/prefabs/MissionObject/Beacon.prefab`을 원하는 위치에 필요한 개수만큼 배치(비콘마다
   `SphereCollider`가 `Is Trigger` 켜진 채로 이미 구성돼 있음).
2. 추가 아군 유닛 프리팹(NTA 유닛 아무거나)을 배치하고, 각 유닛 인스펙터에서:
   - `Is Rescue Unit` 체크
   - `Fog Revealer Agent`의 `Sight Range`를 낮은 값(예: 1)으로 설정
3. 새 `RescueBeaconSystem` 컴포넌트를 하나 붙이고, 비콘 개수만큼 `Beacons` 리스트 엔트리를 추가해서
   각 엔트리에 그 비콘의 `SphereCollider`와, 그 비콘에서 풀어줄 유닛들을 연결.

## 확인하고 싶은 점 (씬 배치는 구체 정보가 있어야 진행 가능)
1. **비콘 개수와 대략적인 배치 의도** - 몇 개를 둘 계획이고, 각 비콘 근처에 몇 명씩 배치할 계획인지.
2. **유닛 종류** - NTA 로스터 중 어떤 유닛을 쓸지(여러 종류 섞어도 되는지), 아니면 특정 컨셉(예:
   낙오된 정찰대)이 있는지.
3. **씬 배치를 제가 유니티 에디터 도구로 직접 진행할지, 아니면 스크립트만 만들어두고 배치는 직접
   하실지.**

## 변경 예정 파일
- `Assets/Scripts/System/RescueBeaconSystem.cs` (신규)
- `Assets/Scenes/Missions/Sub_Mission1.unity` (비콘/유닛 배치 + `RescueBeaconSystem` 컴포넌트 연결 -
  위 확인 사항에 따라 진행)

## 상태
**스크립트만 적용 완료** — `RescueBeaconSystem.cs`를 제안대로 추가함(설계와 구현 간 차이 없음).
컴파일 확인 완료(에러 0, 기존 베이스라인과 동일한 종류의 경고만 +1건 추가 - `FindFirstObjectByType`
obsolete 경고, 이 프로젝트 전역 관례).

씬 배치(비콘 개수/위치, 유닛 종류/수, `RescueBeaconSystem` 컴포넌트 연결)는 사용자가 직접 진행함 -
SkyLancer×4 + Pulsar Tank×2, 총 6기를 `isRescueUnit=true`로 배치하고 비콘에 닿으면 정상적으로 구조/조종
전환됨을 확인함.

## 후속 - "게임 시작 시 구조 유닛 시야가 안 줄어듦" 진단 및 수정

### 요청 내용
> 일단 연결해봤고 비콘에 도착하면 잘 구조되어 조종되는데 현재 게임 시작시 구조 유닛의 시야가
> 줄어들어야하는데 안 줄어들어

### 조사

`find-game-objects`로 씬의 6개 구조 유닛(SkyLancer, SkyLancer (1~3), Pulsar Tank (2~3))을 직접
조회함. 전부 `Is Rescue Unit=true`는 정상이었지만, 같은 오브젝트의 `Fog Revealer Agent.Sight Range`가
기본값 `25`(구조 후 값과 동일)에 그대로 남아있었음. `isRescueUnit`은 명령만 막을 뿐 시야를 자동으로
낮추지 않는다 - 시야는 배치 시점에 `Sight Range` 필드를 직접 낮게 설정해둬야 하는 별개 필드
(`FogRevealerAgent.SetSightRange` 주석 참고: "구조 전엔 낮은 시야로 설정해둔 상태라고 가정"). 즉
코드 버그가 아니라 씬 배치 시 이 필드를 낮추는 걸 빠뜨린 것.

### 적용

Unity Editor 다이나믹 코드로 6개 오브젝트의 `FogRevealerAgent.sightRange`를 `1`로 낮추고(Mission3
구조 유닛과 동일한 값) 씬을 저장함.

### 검증

- `find-game-objects`로 사전 조회해 6개 전부 `Sight Range=25`였음을 먼저 확인.
- 다이나믹 코드 실행 결과: 6개 전부 `sightRange -> 1` 성공 확인.
- `EditorSceneManager.SaveOpenScenes()` 성공(`True`) 확인, `git status`로 `Sub_Mission1.unity`가
  변경 목록에 포함됨을 확인.

### 변경된 파일
- `Assets/Scenes/Missions/Sub_Mission1.unity` (구조 유닛 6개의 `Sight Range` 25 → 1)
