# TurretController

`Assets/Scripts/Unit/TurretController.cs`

## 개요

차량형 유닛의 포탑 오브젝트에 직접 부착한다(예: `unit_Tank_Heavy_A_Turret_yup`). 몸체(`UnitController.RotateYOnly`)와 별개로 `AttackRange`가 감지한 대상을 향해 몸체보다 빠르게 Y축 회전하고, 이동 명령 중이라 자동 공격이 안 나가는 상태(`UnitState.Move`)에도 상관없이 계속 조준만 한다(doc/0219) — `AttackRange.GetTrackingTarget()`이 공격 가능 여부와 무관하게 사거리 내 우선순위 대상을 그대로 돌려주기 때문이다. `UnitController.Attack()`은 이 컴포넌트가 붙어있는 유닛에 한해 몸체 `RotateYOnly`를 건너뛴다("공격 중엔 몸체가 안 돌고 포탑만 돈다"는 요구사항). 반동(recoil)도 DOTween으로 함께 처리한다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `rotationSpeed` | 초당 조준 회전각(도) — 몸체보다 빠름 |
| `recoilPart` | 뒤로 빠질 파츠(포신). 비우면 이 오브젝트 자신 |
| `recoilLocalOffset` / `recoilDuration` / `recoilReturnDuration` / `recoilEase` / `recoilReturnEase` | 반동 이동량, 뒤로 빠지는/복귀 시간, 각각의 이징 |
| `attackRange` / `enemyAttackRange` | 아군/적(및 아군 OC) 유닛 중 하나만 세팅되는 사거리 판정 참조 |
| `restLocalRotation` | 조준 대상이 없을 때 되돌아갈 "정면" 로컬 회전값(보통 몸체 정면) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | `recoilPart` 기본값 설정, 반동 원위치/정면 회전값 캐싱 |
| `Start()` | `AttackRange`/`EnemyAttackRange` 조회 — `Awake`가 아니라 `Start`에서 하는 이유는 Unity가 서로 다른 GameObject 컴포넌트들의 `Awake` 호출 순서를 보장하지 않기 때문(자식 Turret의 Awake가 부모 UnitController.Awake보다 먼저 실행되면 참조가 비어있는 채로 캐싱되는 문제가 있었음). `UnitController` → `EnemyUnitController` → `AllyController` 순으로 부모를 탐색하며, `AllyController`(아군 OC, doc/0452)도 `EnemyAttackRange` 타입을 그대로 재사용한다(doc/0469에서 누락돼있던 것을 추가) |
| `Update()` | 대상이 있으면 그쪽으로, 없으면 부모(몸체)의 현재 회전에 `restLocalRotation`을 다시 얹은 "몸체 기준 정면"으로 매 프레임 `RotateTowards`로 수렴 — 별도의 "복귀 중" 상태 관리 없이 끊김 없이 이어짐 |
| `FireRecoil()` | `UnitController.Attack()`이 데미지를 실제로 입힌 순간 호출 — 반동 트윈(뒤로 빠짐 → 복귀) 재생 |
| `OnDestroy()` | 반동 트윈 정리 |

## 연관 컴포넌트

- **UnitController / EnemyUnitController / AllyController**: 몸체 컨트롤러 — `Attack()`이 이 컴포넌트 존재 여부로 몸체 회전을 건너뛸지 결정, `GetAttackRange()` 제공
- **AttackRange / EnemyAttackRange**: `GetTrackingTarget()`으로 조준 대상 제공
- **VehicleIdleAnimation**: idle 상태에서 포탑을 잠시 방황시킬 때 이 컴포넌트를 껐다 켬(`enabled`)
