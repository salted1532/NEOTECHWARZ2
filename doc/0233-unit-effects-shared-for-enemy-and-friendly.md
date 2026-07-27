# 0233 - UnitEffects를 적/아군 유닛 공용으로 확장

## 요청

적 유닛(`EnemyUnitController`)이 공격/이동할 때도 기존 `UnitEffects`(공격/이동/피격/사망 이펙트 전담
컴포넌트)가 똑같이 작동하도록 해달라는 요청. 별도 스크립트로 분리하지 말고 `UnitEffects` 하나로
적/아군 유닛 둘 다 처리하는 방식으로 요청받음.

## 원인

`UnitEffects.cs`가 `Awake()`에서 `GetComponent<UnitController>()`만 찾고 있었음. 그래서:

- **이동 이펙트**: `Update()`가 `unitController.IsCurrentlyMoving()`만 폴링했는데, 적 유닛 프리팹에는
  `UnitController`가 없고 `EnemyUnitController`가 붙어있어서 `unitController`가 항상 null → 이동 중이어도
  `moving`이 항상 false로 계산되어 이동 트레일이 절대 재생되지 않음.
- **공격 이펙트**: `PlayAttack()`/`StopAttackEffects()`는 지금까지 `UnitController.Attack()`/
  `CancelAttackOrder()`에서만 호출되고 있었고, `EnemyUnitController.Attack()`/`MoveTo()`에서는 아예
  호출하지 않았음 → 적 유닛이 공격해도 총구 이펙트가 재생되지 않음.
- 피격(`HandleDamaged`)/사망(`HandleDeath`) 이펙트는 `HealthManager`의 이벤트에만 반응해서 애초에 컨트롤러
  타입과 무관하게 이미 적/아군 둘 다 정상 작동하고 있었음 (수정 불필요).

## 수정 내용

**`Assets/Scripts/Effects/UnitEffects.cs`**
- `enemyUnitController` 필드를 추가해 `Awake()`에서 `GetComponent<EnemyUnitController>()`도 함께 조회
  (프리팹에 어느 쪽이 붙어있느냐에 따라 하나만 채워지고 나머지는 null로 남음).
- `Update()`의 이동 판정을 `unitController` 또는 `enemyUnitController` 중 실제로 붙어있는 쪽의
  `IsCurrentlyMoving()`을 물어보도록 변경 (`||`로 연결, 스크립트 분리 없이 하나의 컴포넌트가 양쪽을 지원).

**`Assets/Scripts/Enemy/EnemyUnitController.cs`**
- `IsCurrentlyMoving()` 추가 (`UnitController`의 동명 메서드와 동일한 패턴: 공중 유닛은 `isMovingAirUnit`,
  지상 유닛은 `NavMeshAgent` 이동 여부로 판정).
- `Attack()`에서 실제로 데미지를 적용한 직후 `GetComponent<UnitEffects>()?.PlayAttack();` 호출 추가
  (`UnitController.Attack()`과 동일한 위치/패턴).
- `MoveTo()`에서 `GetComponent<UnitEffects>()?.StopAttackEffects();` 호출 추가 - 공격 중이던 유닛이 이동
  명령으로 전환될 때 재생 중이던 공격 이펙트를 즉시 정리 (`UnitController.CancelAttackOrder()`와 동일한 이유).

프리팹 쪽은 이미 9종 OC 유닛 전부 `UnitEffects` 컴포넌트가 붙어있고 `firePoints`/`moveTrailPoints`/
`hitEffects`/`deathPrefab` 등도 설정돼 있어서 (이전 세션에서 모델링 시 함께 구성됨) 별도 프리팹 수정은
필요 없었음 - 코드만 고치면 바로 적용됨.

## 참고: 이번에 건드리지 않은 것

`UnitAnimatorDriver.cs`(Animator의 IsMoving/Fire 파라미터 갱신)와 `VehicleShake.cs`도 `UnitController`만
찾고 있어서 적 유닛 프리팹(예: Railgunner에 이미 `UnitAnimatorDriver`가 붙어있음)에서는 걷기/사격
애니메이션이 재생되지 않음. `unitController == null`이면 조용히 아무 것도 안 하도록 방어돼 있어서 에러는
안 나지만, 요청 범위가 명시적으로 "UnitEffects"였어서 이번엔 손대지 않음. 적 유닛도 애니메이션이 필요하면
같은 패턴(`EnemyUnitController` 참조 추가 + `||` 연결)으로 후속 작업 가능.

## 변경 파일

- `Assets/Scripts/Effects/UnitEffects.cs`
- `Assets/Scripts/Enemy/EnemyUnitController.cs`
