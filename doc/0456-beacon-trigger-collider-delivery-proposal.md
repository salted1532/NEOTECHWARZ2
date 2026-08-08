# 0456. 비콘 반납 판정을 거리 계산 대신 실제 트리거 콜라이더 접촉으로 - 제안

**날짜:** 2026-08-08

## 요청 내용
> 일꾼으로 유물 +데이터 챙기고 비콘으로 가면 비콘은 게임오브젝트로 해서 비콘안에 콜리전 트리거에
> 닿아야 임무가 완료 되도록 하게 해줘

## 조사 내용

### 지금은 순수 거리 계산

`Stage2Objectives.UpdateCarry()`가 매 프레임 `Vector3.Distance(item.position, beacon.position) <=
deliverRadius`로 반납을 판정함 - `beacon`은 그냥 `Transform` 하나만 참조하고 실제 콜라이더/트리거는
전혀 안 씀(주석에도 "트리거 콜라이더를 따로 붙이지 않고" 명시돼 있음 - 이전 요청사항이었음).

### `Beacon.prefab`은 이미 트리거 콜라이더를 갖고 있음

`Assets/prefabs/MissionObject/Beacon.prefab`을 열어보니 **이미 `SphereCollider`(반지름 10,
`m_IsTrigger: 1`)가 붙어 있음** - 지금은 아무 스크립트도 이걸 참조하지 않아서 그냥 장식(캡처 포인트
비주얼이 자식으로 붙어있는 것만 재사용됨)으로만 존재하는 상태. 즉 프리팹 쪽은 이미 준비돼 있고,
`Stage2Objectives`가 이 콜라이더를 실제로 조회하도록 코드만 바꾸면 됨.

### 물리 트리거가 실제로 발동하려면

`item.position`을 매 프레임 직접 대입해서 옮기는 지금 방식(물리 힘이 아니라 스크립트로 순간이동)에서
`OnTriggerEnter`가 안정적으로 발동하려면, 부딪히는 두 콜라이더 중 최소 하나에는 Rigidbody가 있어야
함(Unity 물리 규칙). 비콘 쪽엔 안 붙여도 되고, **유물/데이터베이스 쪽에 Kinematic Rigidbody**를
하나 추가하면 됨(물리 시뮬레이션에 영향받지 않고 트리거 감지만 가능해짐 - 스크립트로 위치를 계속
덮어써도 문제없음).

## 제안하는 변경

### 1) `MissionItem.cs` - 트리거 접촉 추적 추가

```csharp
using System.Collections.Generic;
using UnityEngine;

public class MissionItem : MonoBehaviour
{
    // ...기존 필드(itemName/icon/selectionMarker) 그대로...

    // 비콘 등 트리거 콜라이더에 실제로 겹쳐 있는지 판정한다 - 겹친 트리거를 전부 추적해서, 특정
    // 콜라이더(비콘)에 지금 닿아 있는지 IsTouching()으로 물어볼 수 있게 한다 (doc/0456).
    private readonly HashSet<Collider> overlappingTriggers = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other) => overlappingTriggers.Add(other);
    private void OnTriggerExit(Collider other) => overlappingTriggers.Remove(other);

    public bool IsTouching(Collider other) => other != null && overlappingTriggers.Contains(other);

    // ...기존 SelectItem/DeselectItem/GetIcon/GetItemName 그대로...
}
```

### 2) `Stage2Objectives.cs` - 비콘을 `Transform` 대신 `Collider`로 받고, 거리 대신 접촉으로 판정

```csharp
[SerializeField] private MissionItem artifact;       // Transform → MissionItem으로 변경(트리거 조회도 여기서 함께)
[SerializeField] private Collider artifactBeacon;     // Transform → Collider(비콘의 트리거)로 변경
...
[SerializeField] private MissionItem researchData;
[SerializeField] private Collider researchDataBeacon;
...
// deliverRadius 필드 삭제 (더 이상 안 씀)

private void UpdateCarry(MissionItem item, Collider beacon, ref UnitController carrier, ref bool delivered)
{
    if (delivered || item == null || rtsController == null)
        return;

    if (carrier == null)
        carrier = FindNearestWorkerInRange(item.transform.position, pickupRadius);

    if (carrier == null)
        return;

    item.transform.position = carrier.transform.position + carryOffset;

    if (beacon != null && item.IsTouching(beacon))
    {
        delivered = true;
        item.gameObject.SetActive(false);
        carrier = null;
    }
}
```

클래스 상단 주석의 "트리거 콜라이더를 따로 붙이지 않고" 부분도 실제로 붙이는 쪽으로 갱신.

### 3) 프리팹 설정

- `Artifact.prefab`/`Database.prefab` 루트에 `Rigidbody` 추가(`Is Kinematic` 체크, 물리 영향 없이
  트리거 감지만 가능하게).
- `Stage2Objectives`가 배치된 씬(예: `Mission2.unity`)에서 인스펙터 참조를
  `artifact`/`researchData`는 그대로(자동으로 `MissionItem` 컴포넌트 참조로 재연결됨),
  `artifactBeacon`/`researchDataBeacon`은 각 비콘 오브젝트의 `SphereCollider`로 다시 연결 필요
  (필드 타입이 바뀌므로 기존 `Transform` 연결이 끊어짐 - 제가 씬 파일을 직접 편집해서 다시 연결해
  드릴게요).

## 확인하고 싶은 점 (승인됨)

이대로 진행해도 될까요? (`MissionItem.cs`/`Stage2Objectives.cs` 수정, `Artifact.prefab`/
`Database.prefab`에 Kinematic Rigidbody 추가, `Mission2.unity`의 비콘 필드 재연결)

사용자가 "진행시켜줘"로 승인함.

## 구현 결과

제안 그대로 적용함.

- `MissionItem.cs` - `OnTriggerEnter`/`OnTriggerExit`로 겹친 콜라이더를 추적하는 `HashSet<Collider>`
  + `IsTouching(Collider)` 추가.
- `Stage2Objectives.cs` - `artifact`/`researchData` 필드 타입 `Transform` → `MissionItem`,
  `artifactBeacon`/`researchDataBeacon` 필드 타입 `Transform` → `Collider`로 변경. `UpdateCarry()`의
  반납 판정을 거리 계산에서 `item.IsTouching(beacon)`으로 교체. 더는 안 쓰는 `deliverRadius` 필드 삭제.
  상단 주석도 새 방식에 맞게 갱신.
- `Artifact.prefab`/`Database.prefab` - Kinematic Rigidbody 추가(`useGravity: false`).
- `Mission2.unity` - 현재 씬(이미 열려 있던, 사용자가 유물/데이터베이스/비콘을 직접 배치해둔 저장 전
  상태)에서 `Stage2Objectives` 인스턴스를 찾아 `artifact`→`Artifact`의 `MissionItem`,
  `researchData`→`Database`의 `MissionItem`, `artifactBeacon`/`researchDataBeacon`→`Beacon`의
  `SphereCollider`로 재연결 후 씬 저장. 씬에는 `Beacon`이 하나만 배치돼 있어서(기존에도 두 필드가
  같은 오브젝트를 가리키고 있었음), 유물/데이터 둘 다 같은 비콘에 반납하는 구조를 그대로 유지함 -
  다른 배치를 원하시면 인스펙터에서 각각 다른 비콘으로 바꿔주시면 됨.

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`.
- Unity 콘솔 Error 0건.
- 재연결 후 필드 값 직접 확인: `artifact`/`researchData`가 각각 올바른 `MissionItem` 인스턴스,
  `artifactBeacon`/`researchDataBeacon`이 `Beacon`의 `SphereCollider`를 정확히 가리킴.
- `git status`로 부수 변경 없음 확인(워터 메시 재직렬화 등 이번엔 발생하지 않음).

## 변경된 파일

- `Assets/Scripts/System/MissionItem.cs`
- `Assets/Scripts/System/Stage2Objectives.cs`
- `Assets/prefabs/MissionObject/Artifact.prefab`
- `Assets/prefabs/MissionObject/Database.prefab`
- `Assets/Scenes/Missions/Mission2.unity` (비콘 필드 재연결 - 사용자가 미리 배치해둔 유물/데이터베이스/
  비콘 오브젝트 자체는 이번 작업 이전부터 있던 것)
