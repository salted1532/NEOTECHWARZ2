# 0463. 비콘 트리거 오판 수정 - AttackRange 사거리도 "닿음"으로 잘못 인식되던 문제

**날짜:** 2026-08-08

## 요청 내용
> 현재 비콘이 유닛이 닿았다를 인식하는게 AttackRange도 인식해버려서 사실 유닛이 비콘에 가지도
> 않았는데 구조가 되어버리네 이거 확인해줘

## 원인

`UnitController`의 `OnTriggerEnter/OnTriggerExit`는 루트 오브젝트(Rigidbody 보유)에 붙어있는데,
자식인 `AttackRange`의 감지용 `CapsuleCollider`(예: 반경 17, `UnitRange + DetectionRangeMargin`)는
자체 Rigidbody가 없어서 이 콜라이더의 트리거 이벤트도 부모(유닛 루트)의 `OnTriggerEnter`로 함께
전달됨. 그 결과 실제 몸통(반경 0.5짜리 루트 `CapsuleCollider`)이 비콘 근처에도 안 갔는데, 사거리
17짜리 AttackRange 콜라이더만 비콘(반경 10)에 닿아도 `overlappingTriggers`에 비콘이 추가되어
`IsTouching(beacon)`이 `true`로 잘못 판정됨 - 구조 조건이 위치와 무관하게 너무 일찍 충족되던 원인.

## 적용

- `UnitController.cs`에 `bodyCollider`(루트 자신의 `Collider`, `Awake()`에서 `GetComponent<Collider>()`로
  캐시) 필드 추가.
- `OnTriggerEnter`에서 `bodyCollider.bounds.Intersects(other.bounds)`를 만족할 때만
  `overlappingTriggers`에 추가하도록 변경 - AttackRange 콜라이더가 유발한 이벤트는 몸통 Bounds와
  안 겹치므로 걸러짐. `OnTriggerExit`는 그대로 무조건 제거(포함 안 돼 있으면 no-op이라 안전).

## 검증 (Play Mode)

- `Cyborg Soldier (Rescue)`를 비콘 중심에서 20 떨어진 지점(AttackRange 반경 17+비콘 반경 10=27
  이내라 AttackRange는 닿지만, 몸통 반경 0.5+비콘 반경 10=10.5 이내는 아님)으로 이동 후 확인:
  `IsTouching(beacon)=False` (수정 전이었다면 `True`).
- 같은 유닛을 비콘 중심에서 5 떨어진 지점(몸통이 실제로 닿는 거리)으로 이동 후 확인:
  `IsTouching(beacon)=True`.
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- Unity 콘솔: 새로 발생한 Error 없음(기존 Editor Inspector 관련 에러 6건은 무관하게 그대로).
- `git status` 확인: water-mesh 애셋 노이즈 없음. `AttackRange.cs` 변경은 이 세션이 만든 게
  아니라 동시에 작업 중인 다른 세션의 `doc/0460` 관련 변경(건드리지 않음).

## 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs` (`bodyCollider` 캐시 추가, `OnTriggerEnter` 판정 수정)
