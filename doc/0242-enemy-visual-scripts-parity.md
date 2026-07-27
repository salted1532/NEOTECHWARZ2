# 0242 - DOTween 비주얼 스크립트(호버/반동/흔들림/애니메이터) 적 유닛 지원

## 요청

공중유닛 호버(`HoverBob`), 포신 반동(`TurretController.FireRecoil`), 차량형 유닛 이동 흔들림
(`VehicleShake`), 인간형 애니메이션(`UnitAnimatorDriver`, 예: 샤프슈터) - 아군 전용으로 만들어진 이
DOTween 기반 비주얼 스크립트들이 적 유닛 프리팹에 그대로(같은 스크립트, 알맞은 위치에) 붙여도 작동하도록
확인 및 수정.

## 확인 결과 - 전부 `UnitController`만 찾고 있었음

4개 스크립트 모두 `GetComponentInParent<UnitController>()` / `GetComponent<UnitController>()`로만
아군 여부·이동 여부·공격 여부를 조회하고 있어서, `EnemyUnitController`가 붙은 적 유닛 프리팹에서는
전부 조용히 아무 동작도 안 하는 상태였음 (null 체크로 방어는 돼 있어서 에러는 안 남, [[0233]]에서
`UnitEffects`에 남겨둔 후속 메모와 동일한 패턴의 문제).

`TurretController`는 추가로 한 가지가 더 있었음: `EnemyUnitController.Attack()`이 `turretController`
필드 자체를 갖고 있지 않아서 `FireRecoil()`이 아예 호출되지 않았고, 몸체 회전도 포탑 유무와 무관하게
항상 `RotateYOnly`가 실행되고 있었음(아군은 포탑이 있으면 몸체 회전을 건너뜀).

## 수정 내용

**`Assets/Scripts/Enemy/EnemyUnitController.cs`**
- `turretController` 필드 추가, `Awake()`에서 `GetComponentInChildren<TurretController>()`로 조회
  (`UnitController`와 동일한 패턴)
- `Attack()`: 포탑이 있으면 몸체 회전(`RotateYOnly`)을 건너뛰고, 데미지 적용 시 `turretController?.FireRecoil()`
  호출 추가
- `GetAttackRange()` 접근자 추가 (`TurretController`가 조준 대상을 물어볼 때 사용)

**`Assets/Scripts/Enemy/EnemyAttackRange.cs`**
- `GetTrackingTarget()` 추가 - 지정 대상 개념이 없어서 `GetClosestTarget()`과 동일하게 동작

**`Assets/Scripts/Unit/TurretController.cs`**
- `enemyAttackRange` 필드 추가. `Start()`에서 부모에 `UnitController`가 없으면 `EnemyUnitController`를
  찾아 `EnemyAttackRange`를 가져옴. `Update()`의 조준 대상 조회도 둘 중 있는 쪽을 사용하도록 변경

**`Assets/Scripts/Animation/HoverBob.cs`** - `enemyUnitController` 필드 추가, `IsAirUnit()` 판정에 포함

**`Assets/Scripts/Animation/VehicleShake.cs`** - `enemyUnitController` 필드 추가, `IsCurrentlyMoving()`
판정에 포함

**`Assets/Scripts/Animation/UnitAnimatorDriver.cs`** - `enemyUnitController` 필드 추가, `IsMoving`/`Fire`
파라미터 판정에 포함

네 스크립트 전부 아군 컴포넌트가 있으면 아군 것을, 없으면 적 컴포넌트를 쓰는 방식이라(둘 다 없으면
조용히 무시) 기존 아군 유닛 동작에는 영향이 없고, 적 유닛 프리팹의 해당 위치(공중유닛 비주얼 자식,
차량 비주얼 자식, 포탑 오브젝트, 인간형 유닛 루트)에 그대로 붙이기만 하면 자동으로 작동한다.

## 변경 파일

- `Assets/Scripts/Enemy/EnemyUnitController.cs`
- `Assets/Scripts/Enemy/EnemyAttackRange.cs`
- `Assets/Scripts/Unit/TurretController.cs`
- `Assets/Scripts/Animation/HoverBob.cs`
- `Assets/Scripts/Animation/VehicleShake.cs`
- `Assets/Scripts/Animation/UnitAnimatorDriver.cs`
