# 0451. 유닛이 전투 중(자동교전)일 때도 idle 두리번 모션이 나오는 문제 - 제안

**날짜:** 2026-08-08

## 요청 내용
> 현재 유닛들이 공격중일떄도 idle상태라서 중간중간 두리번 거리는 모션이 들어가는데 idle 상태면서
> 공격중일때는 예외처리를해서 두리번 거리지 않았으면 좋겠어

## 조사 내용

`InfantryIdleLookAround`/`VehicleIdleAnimation`은 이미 `IsIdle()` 판정에서
`unitController.IsAttack()`을 체크해서 공격 중엔 두리번거리지 않도록 예외처리가 되어 있음:

```csharp
// InfantryIdleLookAround.IsIdle()
return !unitController.IsCurrentlyMoving() && !unitController.IsAttack();
```

문제는 `UnitController.IsAttack()`의 정의:

```csharp
public bool IsAttack() => UnitcurrentState == UnitState.Attack;
```

`UnitcurrentState`는 오직 **명시적으로 지정 공격 명령**(우클릭 적 지정 `AttackUnitTarget`, 아군 강제공격
`AttackFriendlyTarget`)을 내렸을 때만 `UnitState.Attack`으로 세팅됨. 반면 아래 세 경로는 의도적으로
`UnitcurrentState = UnitState.Idle`을 유지한 채로 `AttackRange`가 사거리 내 적을 알아서 자동교전하게
맡겨둠 (각 지점 주석에 이미 명시됨 - "Idle 유지 - AttackRange가 사거리 내 적을 자동으로 교전하게 함"):

- `AttackMoveTo` (A 모드로 땅 클릭 - 이동 중 사거리에 적이 들어오면 교전)
- `FollowUnit` / `FollowBuilding` (따라다니기 중 교전)
- 그냥 가만히 서있는 유닛이 근처에 나타난 적을 자동으로 쏘는 패시브 교전(가장 흔한 케이스)

이 경로들에서는 `AttackRange.Update()`가 `unitController.Attack(...)`을 매 프레임 호출해서 실제로
총을 쏘고 있는데도, `UnitcurrentState`는 계속 `Idle`이라 `IsAttack()`이 `false`를 반환함. 그 결과
`InfantryIdleLookAround.IsIdle()`이 `true`로 잘못 판정되어, 실제로 전투 중인데도 몇 초 뒤 랜덤하게
두리번 회전이 끼어듦 (진행 중인 `RotateYOnly` 조준 회전과 두리번 트윈이 충돌).

`AttackRange`에는 정확히 "지금 사거리 안에 교전 대상이 있는지"를 실시간으로 알려주는
`HasEnemyInRange` 프로퍼티가 이미 존재함 (`enemiesInRange` 리스트에 살아있는 대상이 하나라도 있으면
`true`). 참고로 적(`EnemyUnitController`) 쪽은 애초에 `IsAttack()`을 상태값이 아니라 이 패턴으로
정의해서 (`attackRange.HasTargetInAttackRange`) 이 문제가 없음 - 아군(`UnitController`) 쪽만 상태
머신 기반이라 어긋나 있었음.

## 제안하는 수정

`UnitController.IsAttack()` 한 곳만 고쳐서 모든 호출부(idle 두리번 2곳 + `UnitAnimatorDriver`의 Fire
애니메이션 파라미터 + `AttackRange.Update()` 자기 자신)에 한 번에 반영되게 함 (root-cause 수정 -
각 호출부를 따로 패치하지 않음):

```csharp
public bool IsAttack() => UnitcurrentState == UnitState.Attack || (attackRange != null && attackRange.HasEnemyInRange);
```

- 지정 공격 명령으로 추격 중(아직 사거리 밖, 실제 교전 전)이어도 기존처럼 `UnitState.Attack`으로
  `true` 유지.
- 자동교전 중(사거리 안에 적이 실제로 있어서 `AttackRange`가 매 프레임 쏘고 있는 상태)에도 이제
  `true` - 두리번 모션이 억제됨.
- `AttackRange.Update()`의 `if (unitController.IsAttack() || unitController.IsIdle())` 조건에 미치는
  영향 확인: 이 함수 안에서 `target`이 non-null인 시점은 이미 `enemiesInRange`에서 뽑아온 대상이라
  `HasEnemyInRange`도 자연히 `true`가 되므로, 기존에 이 분기를 타던 케이스에서 동작이 달라지지 않음.
- 부수 효과(의도한 개선): `UnitAnimatorDriver`의 `Fire` 애니메이터 파라미터도 같은 `IsAttack()`을
  쓰므로, 지금까지 자동교전 중엔 Fire 애니메이션이 안 켜지고 있었다면 이제 자동교전 중에도 정상적으로
  켜짐 - 별도 수정 없이 같은 근본 원인 수정으로 함께 해결됨.

### 변경 파일
- `Assets/Scripts/Unit/UnitController.cs` (`IsAttack()` 한 줄)

## 적용한 변경

사용자 확인 후 `Assets/Scripts/Unit/UnitController.cs`의 `IsAttack()`을 제안대로 수정:

**Before:**
```csharp
public bool IsAttack() => UnitcurrentState == UnitState.Attack;
```

**After:**
```csharp
// 자동교전(AttackMoveTo/FollowUnit/FollowBuilding/패시브 대기 중 사거리 내 적 발견)은 UnitcurrentState를
// 계속 Idle로 유지하므로(각 명령 지점 주석 참고), 상태값만으로는 "실제로 쏘는 중"을 놓친다 - AttackRange의
// 실시간 교전 여부도 함께 확인해야 두리번 애니메이션 등이 전투 중을 정확히 인식한다 (doc/0451).
public bool IsAttack() => UnitcurrentState == UnitState.Attack || (attackRange != null && attackRange.HasEnemyInRange);
```

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 37`(기존 베이스라인과 동일 - 새 경고 없음).

## 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs` (`IsAttack()` 한 줄)
