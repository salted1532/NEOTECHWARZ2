# MeleeBodySlamAttack

`Assets/Scripts/Animation/MeleeBodySlamAttack.cs`

## 개요

근접 공격 유닛(예: Ripfang)의 몸 모델 오브젝트에 붙이는 DOTween 기반 공격 연출 컴포넌트(doc/0553).
공격 판정 순간 몸이 대상 쪽(로컬 정면, +Z)으로 짧게 튀어나갔다가 되돌아와 "몸통박치기" 느낌을 낸다.
`TurretController.FireRecoil()`과 동일한 훅 지점/구조 — 데미지 적용 순간 컨트롤러가 `Slam()`을 호출하는
방식이며, 이 컴포넌트 자체는 데미지 계산에 관여하지 않고 순수 시각 연출만 담당한다.

몸은 이미 대상을 향해 회전한 뒤(`EnemyUnitController.Attack()`의 `RotateYOnly`가 먼저 실행됨) 이 훅이
불리므로, 로컬 정면으로 튀어나갔다 돌아오기만 해도 그대로 몸통박치기로 보인다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `bodyPart` | `Transform` (SerializeField) | 튀어나갈 파츠. 비우면 이 오브젝트 자신 |
| `lungeDistance` | `float` (기본 0.6) | 로컬 정면으로 튀어나가는 거리 |
| `lungeDuration` / `lungeReturnDuration` | `float` (기본 0.08 / 0.15) | 튀어나가는 시간 / 되돌아오는 시간 |
| `lungeEase` / `lungeReturnEase` | `Ease` (기본 OutQuad / OutBack) | DOTween 이징 곡선 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | `bodyPart`가 비어있으면 자기 자신으로 대체, 원래 로컬 위치(`restLocalPosition`) 저장 |
| `Slam()` (public) | 기존 트윈이 있으면 즉시 종료 후 위치 복원, 로컬 정면으로 `lungeDuration` 동안 튀어나갔다가 완료 시 `lungeReturnDuration` 동안 원위치로 복귀 — 연속 공격 중 재호출돼도 끊기지 않고 자연스럽게 이어짐 |
| `OnDestroy()` | 진행 중인 트윈 정리(메모리 누수 방지) |

## 연관 컴포넌트

- **[`EnemyUnitController`](EnemyUnitController.md)**: `Awake()`에서 `GetComponentInChildren<MeleeBodySlamAttack>()`으로 자식에서 탐색해 캐싱, `Attack()`이 데미지를 입히는 순간 `meleeBodySlamAttack?.Slam()` 호출(없으면 조용히 무시)
- **[`TurretController`](TurretController.md)**: 동일한 "공격 순간 훅 호출" 구조를 쓰는 참고 사례(`FireRecoil()`)
