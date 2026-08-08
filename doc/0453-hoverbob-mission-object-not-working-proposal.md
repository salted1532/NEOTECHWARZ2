# 0453. 유물/데이터베이스 오브젝트의 HoverBob이 작동 안 하는 이유 - 진단 및 제안

**날짜:** 2026-08-08

## 요청 내용
> 유물 + 데이터베이스 오브젝트에다가 hover bob를 넣었는데 작동안하는 이유

## 조사 내용

### 원인: `HoverBob.shouldBob` 조건이 이 두 오브젝트가 가진 컴포넌트를 아예 모른다

`Assets/Scripts/Animation/HoverBob.cs`의 `Update()`:

```csharp
bool shouldBob = (unitController != null && unitController.IsAirUnit())
    || (enemyUnitController != null && enemyUnitController.IsAirUnit())
    || (buildingController != null && buildingController.IsLifted());
```

`shouldBob`은 딱 세 가지 경우에만 `true`가 됨 - "공중 유닛"(`UnitController`/`EnemyUnitController`의
`IsAirUnit()`) 또는 "리프트 중인 건물"(`BuildingController.IsLifted()`). `Awake()`에서
`GetComponentInParent<T>()`로 이 세 컴포넌트를 찾는데, **`Artifact.prefab`/`Database.prefab`(2스테이지
미션 오브젝트, `Assets/prefabs/Maps/MissionObject/`)는 셋 중 어느 것도 갖고 있지 않음** - 실제로 두
프리팹을 열어보면 컴포넌트가 `BoxCollider` + `FogRevealerAgent` + `HoverBob`뿐이고, `UnitController`/
`EnemyUnitController`/`BuildingController`는 프리팹 전체 계층에 아예 없음(`Stage2Objectives.cs`가
"트리거 콜라이더도 안 붙이고 매 프레임 거리 판정으로 직접 줍기/따라가기/반납을 처리"하는 순수
장식용/오브젝트 픽업 아이템이라 애초에 RTS 유닛/건물 컴포넌트가 필요 없는 물건임, `doc` 주석 참고).

즉 `unitController`/`enemyUnitController`/`buildingController` 세 필드가 전부 `null`로 남아서
`shouldBob`이 **영원히 `false`** - HoverBob 자체는 정상적으로 붙어 있고 코드에도 문제가 없지만,
"이 오브젝트가 떠 있어야 하는 상황"을 판정하는 조건 목록에 애초에 "장식용 픽업 아이템"이라는 경우의
수가 없어서 절대 재생되지 않는 것.

### 부수적으로 확인해 둘 점 - `Stage2Objectives`가 들고 있는 동안엔 위치를 직접 덮어씀

`Stage2Objectives.UpdateCarry()`가 일꾼이 주운 동안 매 프레임 `item.position = carrier.transform.position
+ carryOffset;`으로 **월드 좌표를 직접 덮어씀**. HoverBob은 `transform.localPosition.y`를 DOTween으로
움직이는데, 지금 두 프리팹 다 HoverBob이 **루트 오브젝트에** 붙어 있음(자식이 아니라) - 즉 같은
Transform을 두 시스템이 동시에 건드리게 됨. `HoverBob.cs` 상단 주석에 이미 "공중 유닛/리프트 건물의
비주얼(메쉬) **자식** 오브젝트에 부착한다(루트가 아님) - 루트는 다른 로직이 매 프레임 좌표를 갱신하므로
충돌한다"고 명시돼 있는 이유가 정확히 이 문제 때문. 지금은 (1)번 문제로 애초에 재생 자체가 안 돼서
드러나지 않았지만, (1)번만 고치면 "줍기 전엔 정상적으로 둥실거리다가, 일꾼이 줍는 순간부터 지지직거리며
어색해지는" 새 증상이 나타날 것.

## 제안하는 수정

### 1) `HoverBob.cs` - "항상 떠 있기" 옵션 추가

```csharp
[SerializeField] private bool alwaysBob = false; // 유닛/건물이 아닌 장식용 오브젝트(미션 아이템 등)용 - 상태 판정 없이 항상 재생

...

bool shouldBob = alwaysBob
    || (unitController != null && unitController.IsAirUnit())
    || (enemyUnitController != null && enemyUnitController.IsAirUnit())
    || (buildingController != null && buildingController.IsLifted());
```

기본값 `false`라 기존 공중 유닛/리프트 건물 쪽 동작에는 전혀 영향 없음.

### 2) `Artifact.prefab`/`Database.prefab` - `HoverBob`을 비주얼 자식으로 옮기고 `alwaysBob` 체크

- 지금 루트에 붙어 있는 `HoverBob`을 메쉬가 있는 자식 오브젝트로 옮김(HoverBob 자체 설계 의도대로) -
  `Stage2Objectives`가 루트 좌표를 들고 다니는 동안(picked up)에도 자식의 local Y만 흔드는 거라
  서로 안 부딪힘.
- 옮긴 `HoverBob`의 `Always Bob` 체크박스를 켬.

이러면 줍기 전/후 모두 자연스럽게 계속 떠 있는 상태가 유지됨.

## 확인하고 싶은 점

이대로 진행해도 될까요? (`HoverBob.cs`에 `alwaysBob` 필드 추가, `Artifact.prefab`/`Database.prefab`
2개의 `HoverBob`을 자식 오브젝트로 이동 + 옵션 체크)
