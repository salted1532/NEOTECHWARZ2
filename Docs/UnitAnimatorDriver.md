# UnitAnimatorDriver

`Assets/Scripts/Animation/UnitAnimatorDriver.cs`

## 개요

유닛의 이동/공격 상태를 Animator 파라미터(`IsMoving`/`Fire`)에 반영하는 컴포넌트. 비주얼 모델에 Animator가 없는 유닛(정적 메쉬만 쓰는 유닛 등)도 있으므로, Animator를 못 찾으면 아무 동작도 하지 않고 조용히 넘어간다 — 모든 유닛이 애니메이션을 갖는 것은 아니기 때문.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `IsMovingParam` / `FireParam` | 캐싱된 Animator 파라미터 해시(static readonly) |
| `unitController` / `enemyUnitController` / `allyController` | 아군/적(doc/0242)/아군 OC(doc/0469) 유닛 중 붙어있는 컨트롤러(하나만 세팅됨) |
| `animator` | 비주얼 모델 자식에 붙어있는 Animator |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 컨트롤러 3종 + `Animator`(자식에서 탐색) 캐싱 |
| `Update()` | Animator나 컨트롤러가 없으면 스킵. 붙어있는 컨트롤러의 `IsCurrentlyMoving()`/`IsAttack()`을 각각 `IsMoving`/`Fire` 파라미터에 반영 — 공격 중인 동안은 계속 true를 흘려보내 Fire 상태에 머무르게 하고(doc/0225), 끝나면 false가 되어 애니메이터가 자체적으로 idle로 복귀 |

## 연관 컴포넌트

- **UnitController / EnemyUnitController / AllyController**: 이동/공격 상태 조회 대상
- **Animator**: 비주얼 모델 자식에 붙은 실제 애니메이션 재생 컴포넌트
