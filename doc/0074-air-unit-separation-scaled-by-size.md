# 0074 — 공중 유닛 겹치기 풀림 거리를 유닛 크기에 비례하도록 변경

## 질문
"공중 유닛 서로 멀어지는거 겹치기 풀기. 공중유닛의 크기에 따라서 멀어지는정도를 정해졌으면 좋겠어 크기가 큰 유닛은 좀더 더 멀리 겹치기 풀리도록"

## 기존 동작

`Assets/Scripts/Unit/UnitController.cs`의 `SeparateFromOverlappingAirUnits()`가 이동 중이 아닌 공중 유닛끼리
겹치면 서로 밀어내는데, 판정 기준 거리(`airSeparationRadius`, 기본 1.2)가 **모든 유닛에 동일한 고정값**이었음.
즉 유닛 크기(예: Guardian Drone vs Firehawk)와 무관하게 항상 같은 거리로만 분리됨.

## 조사

- `airSeparationRadius`는 `[SerializeField]`로 각 유닛 프리팹마다 값을 가질 수 있는 필드였지만, 실제로는
  모든 유닛 프리팹(공중/지상 통틀어)이 전부 기본값 1.2를 그대로 쓰고 있었음(`Guardian Drone.prefab`,
  `Firehawk.prefab` 등 확인).
- 계산 로직도 `dist < airSeparationRadius`처럼 **자기 자신의 값만** 기준으로 판정해서, 애초에 "상대방 크기"를
  전혀 반영하지 않는 구조였음(페어 관계가 아니라 단방향 판정).
- 실제 유닛 "크기" 근거: 각 공중 유닛 루트 오브젝트에 달린 몸체 `CapsuleCollider`(트리거 아님, `AttackRange`의
  큰 트리거 콜라이더와는 별개) 반경 × 루트 트랜스폼 스케일로 비교해보면,
  Guardian Drone은 반경 1 × 스케일 1.5 = 실효 반경 1.5, Firehawk는 반경 1 × 스케일 1.25 = 실효 반경 1.25로
  Guardian Drone이 확실히 더 큼. 이 비율을 새 값 산정에 참고함.

## 변경 내용

물리 엔진의 콜라이더 반경 합 방식과 동일한 아이디어로, **각 유닛이 자신의 "절반 분리 반경"을 갖고, 두 유닛
사이의 필요 분리 거리는 두 반경의 합**으로 계산하도록 바꿈. 이렇게 하면 큰 유닛이 낀 모든 페어가 자동으로
더 멀리 떨어져서 풀린다(둘 다 크면 훨씬 더, 큰 유닛+작은 유닛이면 중간 정도로).

- `Assets/Scripts/Unit/UnitController.cs`
  - `airSeparationRadius` (고정 임계값) → `airUnitRadius` (이 유닛 자신의 절반 반경, 기본값 `0.6`)로 이름/의미 변경.
    기본값 0.6은 기존 고정값 1.2와 동일한 결과(0.6+0.6=1.2)를 내도록 골라서, 값을 따로 세팅하지 않은
    유닛들은 이전과 동일하게 동작함(하위 호환).
  - `SeparateFromOverlappingAirUnits()`: `requiredDist = airUnitRadius + other.airUnitRadius`로 페어별
    필요 거리를 계산하도록 수정. 이제 상대방 크기도 함께 반영됨.
- `Assets/prefabs/NTA/Unit/Tier3/Guardian Drone.prefab`: `airUnitRadius: 0.75` (실제 몸체 크기가
  Firehawk보다 약 1.2배 커서 그 비율만큼 키움).
- `Assets/prefabs/NTA/Unit/Tier3/Firehawk.prefab`: `airUnitRadius: 0.6` (기존과 동일, 기준점 역할).
- `Assets/prefabs/Test/TestAirUnit.prefab`: 원래 이 필드를 커스텀 오버라이드하지 않고 있었으므로 그대로 둠
  (자동으로 새 기본값 0.6 적용됨).
- 필드 이름이 바뀌면서 지상 유닛 프리팹들(Scout Drone, Assault Trooper 등)에 남아있던 옛
  `airSeparationRadius: 1.2` 값은 더 이상 어떤 필드와도 매칭되지 않는 죽은 키가 됨 — 어차피
  `isAirUnit`이 꺼져 있어 이 로직 자체가 실행되지 않으므로 기능상 영향은 없음. Unity 에디터에서 해당
  프리팹을 한 번이라도 열고 저장하면 자동으로 정리됨(급한 건 아님).

결과적으로 Guardian Drone(0.75)과 Firehawk(0.6)가 섞여 있으면 필요 분리 거리는 `0.75+0.6=1.35`,
Guardian Drone 둘이 겹치면 `0.75+0.75=1.5`, Firehawk 둘이면 `0.6+0.6=1.2`(기존과 동일)로 자동 차등 적용됨.

## 확인 필요 사항
Unity 에디터에서 Guardian Drone과 Firehawk를 섞어서 여러 기 배치/정지시켜보고, Guardian Drone이 낀 조합이
Firehawk끼리보다 확실히 더 멀리 떨어지는지 확인 부탁. 체감 간격이 부족하거나 과하면 각 프리팹의
`Air Unit Radius` 값을 인스펙터에서 직접 조정하면 됨.
