# 0468. 차량/보병 idle 애니메이션 - AllyController(아군 OC) 지원 추가

**날짜:** 2026-08-08

## 요청 내용
> 차량 idle 애니메이션 + 유닛 idle 애니메이션도 현재 컨트롤러에서 모두 작동하도록 확인하고 수정해줘

`doc/0467`에서 `HoverBob`이 `AllyController`를 빼먹고 있던 걸 고친 것과 같은 종류의 문제가
`VehicleIdleAnimation`(차량 엔진 떨림/포탑 방황)과 `InfantryIdleLookAround`(보병 제자리 주변 경계)에도
있는지 점검.

## 조사 및 적용

### VehicleIdleAnimation.cs
- `UnitController`/`EnemyUnitController`만 검사하고 `AllyController`는 없었음 - `allyController`
  필드 추가, `IsIdle()`에 `allyController.IsCurrentlyMoving()`/`IsAttack()` 분기 추가.
- `HasTrackingTarget()`이 참조하는 `attackRange`/`enemyAttackRange`도 `AllyController` 쪽 경로가
  없었음 - `AllyController.GetAttackRange()`가 `EnemyAttackRange` 타입을 그대로 반환하므로(doc/0448,
  "실제로는 AllyAttackRange"), 별도 필드 없이 기존 `enemyAttackRange`에 그대로 대입해서 재사용.
- 이 컴포넌트도 `HoverBob`과 마찬가지로 차량 메쉬(중첩 프리팹 인스턴스)의 자식으로 얹히는 패턴이라
  `doc/0466`과 동일한 Awake 타이밍 문제가 있었음 - `Awake()`에 있던 조회 로직(컨트롤러 3종 +
  `turretController` + `attackRange`)을 전부 `Start()`로 합쳐서 이동.

### InfantryIdleLookAround.cs
- 보병 유닛 루트에 직접 붙는 컴포넌트라 `GetComponent`(부모 탐색 아님)를 쓰고 있어서 Awake 타이밍
  문제는 없었음 - `allyController` 필드만 추가하고 `IsIdle()`에 분기 추가.

## 검증 (Play Mode)

- `Heavy Assault Tank (Ally).prefab`을 스폰한 뒤 자식 오브젝트에 `VehicleIdleAnimation`을 런타임으로
  추가하고 한 프레임 대기 후 확인: `cached allyController != null: True`, `IsIdle()=True`.
- `Cyborg Soldier (Ally).prefab`을 스폰한 뒤 루트에 `InfantryIdleLookAround`를 런타임으로 추가하고
  확인: `cached allyController != null: True`, `IsIdle()=True`.
- (참고: 현재 프로젝트의 실제 Ally 프리팹들에는 이 두 컴포넌트가 아직 하나도 안 붙어있음(적/NTA
  프리팹에만 존재) - 이번 수정은 "붙이면 정상 작동하는지"의 코드 레벨 수정이고, 실제 Ally 프리팹에
  컴포넌트를 붙이는 건 별도 작업임.)
- Unity 콘솔: 새로 발생한 Error 없음(기존 Editor Inspector 관련 에러 6건은 무관하게 그대로).
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- `git status`: 물 메쉬 애셋 노이즈(`Ocean50x50W750H750.asset`) 재발 → `git checkout --`로 되돌림.
  `Cyborg Soldier `/`Railgunner`/`Striker`/`Brute Mech`/`Heavy Assault Tank`/`Ironhawk`/`Raven`
  프리팹 변경은 이 세션이 만든 게 아니라 동시에 진행 중인 다른 세션(`doc/0460`)의 작업 - 건드리지 않음.

## 변경된 파일

- `Assets/Scripts/Animation/VehicleIdleAnimation.cs`
- `Assets/Scripts/Animation/InfantryIdleLookAround.cs`
