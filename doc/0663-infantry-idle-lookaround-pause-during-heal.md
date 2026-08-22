# 0663 - 치유 중 InfantryIdleLookAround 비활성화

## 요청
힐중일땐 `InfantryIdleLookAround`가 작동 안 하도록 - 공격 유닛이 공격 중일 때 안 도는 것과 동일하게.

## 현재 상태
`InfantryIdleLookAround.IsIdle()`(`Assets/Scripts/Animation/InfantryIdleLookAround.cs:52`)이
`!unitController.IsCurrentlyMoving() && !unitController.IsAttack()`일 때만 랜덤 방향 전환을 허용한다.
치유 중(`isHealing`)은 이동 중도 공격 중도 아니라서 이 조건을 그대로 통과 - 치유하며 `FaceTransform()`으로
대상을 바라보고 있는 도중에 이 컴포넌트가 끼어들어 몸을 다른 방향으로 돌려버릴 수 있었음.

## 수정
- `UnitController.cs`에 `IsAttack()` 바로 아래 접근자 추가: `public bool IsHealing() => isHealing;`
- `InfantryIdleLookAround.IsIdle()`의 `unitController` 분기에 `&& !unitController.IsHealing()` 추가.

`enemyUnitController`/`allyController` 분기는 건드리지 않음 - 치유 유닛은 현재 NTA(`UnitController`)에만
존재(doc/0661).

## 결과
컴파일 에러 0. 치유 중엔 `InfantryIdleLookAround`가 개입하지 않고, 치유가 끝나면(`StopHeal()`) 다시
평소처럼 유휴 시 랜덤하게 방향을 바꾼다.
