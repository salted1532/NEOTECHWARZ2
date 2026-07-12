# 0082 — 유닛 따라가기(Follow)가 대상과 겹칠 때까지 계속 밀어붙이던 문제

## 질문
"유닛을 선택한 상태에서 유닛 우클릭해서 따라가게 만든거에서 유닛에 어느정도 도착하면 정지하도록해줘 지상유닛의 경우는
계속 따라가는 유닛을 밀어버리네 그리고 공중유닛은 이동모드이기 때문에 계속해서 겹치기 상태로 유지하네 유닛을
따라가는데 따라가는 유닛이 멈춰서 서로 같은 위치에 도착하면 멈추도록 해줘"

## 원인

`Assets/Scripts/Unit/UnitController.cs`의 `FollowTick()`이 거리 조건 없이 **매 프레임** `MoveAgentTo(followTarget.transform.position)`를 호출하고 있었음. 즉 따라가는 유닛은 대상의 "정확한 좌표"를 목적지로 매 프레임 계속 재지정받는 구조였음.

- **지상 유닛**: `navMeshAgent.SetDestination(대상의 정확한 좌표)`를 매 프레임 다시 호출 → NavMeshAgent가 대상이 서 있는
  바로 그 자리를 점유하려고 계속 시도해서, 대상 유닛의 콜라이더를 계속 밀어붙임(NavMeshAgent의 충돌 회피가 서로
  밀어내는 것처럼 보임).
- **공중 유닛**: `MoveAgentTo`의 공중 분기가 매 프레임 `isMovingAirUnit = true`로 계속 세팅됨. 그런데
  `SeparateFromOverlappingAirUnits()`(공중 유닛 겹침 분리)는 `if (isMovingAirUnit) return;`로 "이동 중엔 서로
  통과 허용"하는 규칙이라, 따라가기가 계속 "이동 중" 상태를 유지하는 한 분리 로직이 영원히 실행되지 않아서 두 유닛이
  같은 자리에 겹친 채로 멈추지도 분리되지도 않음.

## 수정

`FollowTick()`에 거리 체크를 추가: 대상과의 거리가 `followStopDistance`(신규 `[SerializeField]`, 기본값 2) 이내면
더 이상 `MoveAgentTo`를 호출하지 않고 그 자리에 정지한다.
- 지상 유닛: `navMeshAgent.isStopped = true`.
- 공중 유닛: `isMovingAirUnit = false` — 이렇게 하면 다음 프레임부터 `SeparateFromOverlappingAirUnits()`가 정상
  동작해서 겹친 상태면 [[0074-air-unit-separation-scaled-by-size]]에서 만든 반경 기반 분리 로직으로 서로 밀려 벌어짐.

대상이 다시 멀어지면(다음 프레임에 거리가 `followStopDistance`를 넘으면) 자동으로 다시 `MoveAgentTo`가 호출돼서
쫓아가기를 재개한다 - 별도의 "재개" 로직 없이 매 프레임 거리 재확인만으로 자연스럽게 처리됨.

## 확인 필요 사항
지상 유닛으로 다른 유닛을 우클릭해서 따라가게 한 뒤, 가까워지면 더 이상 밀어붙이지 않고 적당한 거리에서 멈추는지,
대상이 다시 움직이면 다시 따라가는지 확인. 공중 유닛도 동일하게 확인하되, 멈춘 뒤 다른 공중 유닛과 겹쳐 있으면
서로 벌어지는지(분리되는지)까지 확인 부탁. `followStopDistance` 값(기본 2)이 체감상 너무 가깝거나 멀면 인스펙터에서
조정하면 됨.

## 후속 수정 (2026-07-12): 공중 유닛만 별도 정지 거리로 분리

사용자 확인 결과, 공중 유닛은 겹침 문제는 해결됐지만 followStopDistance(2) 문턱 부근에서 대상 위치가 미세하게
흔들릴 때(예: 대상이 다른 유닛과의 분리 로직으로 조금씩 밀릴 때) 정지↔재이동을 반복하며 대상 쪽으로 조금씩
튕겨 들어가는 현상이 있었음 - 지상 유닛의 "밀치기"와 비슷하게 보임. 공중 유닛은 지상처럼 NavMeshAgent 감속이 없고
`MoveTowards` 기반이라 문턱을 넘을 때마다 즉시 최고속도로 한 스텝씩 튀어나가서 이 진동이 더 눈에 띔.

- `airFollowStopDistance` 필드 추가(기본 4, 기존 지상용 `followStopDistance`(2)의 약 2배).
- `FollowTick()`에서 `isAirUnit ? airFollowStopDistance : followStopDistance`로 분기해서 사용.

여유 거리를 넉넉하게 둬서 문턱 진동 자체를 줄이는 방식 - 필요하면 인스펙터에서 `Air Follow Stop Distance` 값을
더 조정하면 됨.

## 후속 수정 2 (2026-07-12): 정지 거리를 고정값이 아니라 유닛 크기(반경)에 맞게 계산

"2배(4)로 늘렸더니 작은 공중유닛은 잘 멈추는데 큰 공중유닛은 여전히 서로 밀어낸다"는 후속 리포트.

원인: 고정된 `airFollowStopDistance`(4)는 [[0074-air-unit-separation-scaled-by-size]]에서 도입한 유닛별
`airUnitRadius`(몸집이 클수록 큰 값)와 아무 연관이 없었음. 큰 유닛 두 기가 만나면 실제 겹침 분리에 필요한 거리
(`airUnitRadius` 합)가 고정 정지거리(4)보다 커지는 경우가 생기는데, 그러면:
1. 따라가기(FollowTick)가 거리 4에서 "도착"으로 판단해 정지(`isMovingAirUnit = false`)
2. 그런데 실제 필요한 분리거리(예: 반경 합 5)보다 가까우므로 `SeparateFromOverlappingAirUnits()`가 겹침으로 감지해서 서로 밀어냄(거리가 다시 벌어짐)
3. 벌어진 거리가 다시 followStopDistance(4)보다 커지면 FollowTick이 재이동을 시작해서 다시 다가감
4. 1~3이 반복 → 계속 밀고 당기는 것처럼 보임(사용자가 본 "밀어내기")

수정: `airFollowStopDistance`(고정값)를 없애고, 정지 거리를 **매번 두 유닛의 반경 합으로 동적 계산**하도록 변경.

```csharp
float combinedRadius = airUnitRadius + (followTarget.isAirUnit ? followTarget.airUnitRadius : 0f);
stopDistance = combinedRadius + airFollowStopMargin; // airFollowStopMargin 기본값 1
```

이제 정지 거리가 항상 "실제 겹침 분리에 필요한 거리보다 최소 `airFollowStopMargin`만큼 더 멀리"로 보장되므로,
정지한 뒤에 분리 로직이 다시 끼어들어 밀어낼 일이 없다 - 유닛 크기(작든 크든)와 무관하게 항상 성립하는 조건.

## 확인 필요 사항 (추가)
큰 공중유닛끼리(높은 `Air Unit Radius` 값) 서로 따라가게 시켜서, 이제 적당한 거리에서 멈추고 더 이상 밀고
당기지 않는지 확인 부탁. 여유 간격이 너무 좁거나 넓으면 `Air Follow Stop Margin` 값을 조정하면 됨.

## 후속 수정 3 (2026-07-12): 지상 유닛도 크기(NavMeshAgent 반경)에 맞게 정지 거리 계산

공중 유닛과 같은 이유로 지상 유닛도 손봄. 지상 유닛은 `airUnitRadius` 같은 별도 필드가 없지만, 이미
`NavMeshAgent.radius`가 유닛마다 다르게 설정돼 있음(예: Assault Trooper/Worker Drone `0.5`, Ranger IFV/Pulsar
Tank/Scout Drone `1`) - 이 값을 그대로 "지상 유닛의 크기"로 재사용함.

- 고정값이었던 `followStopDistance`(2)를 없애고, `followStopMargin`(신규, 기본값 1 - 공중과 동일한 여유값)으로 교체.
- `FollowTick()`에서 지상 분기: `combinedRadius = navMeshAgent.radius + (대상이 지상유닛이면 대상의 navMeshAgent.radius, 공중유닛이면 0)`,
  `stopDistance = combinedRadius + followStopMargin`. 공중 유닛과 완전히 동일한 패턴(반경 합 + 여유값)으로 통일됨.

이제 지상 유닛도 몸집이 큰 유닛일수록(NavMeshAgent 반경이 클수록) 더 멀리서 멈추므로, NavMeshAgent끼리 서로의
반경 안쪽을 점유하려고 밀어붙이는 문제가 유닛 크기와 무관하게 해결됨.

## 확인 필요 사항 (추가 2)
반경이 큰 지상 유닛(예: Ranger IFV, Pulsar Tank)끼리 서로 따라가게 시켜서 적당히 넉넉한 거리에서 멈추고 밀어붙이지
않는지, 반경이 작은 유닛(Assault Trooper, Worker Drone)은 예전처럼 가깝게 붙어 멈추는지 확인 부탁.
