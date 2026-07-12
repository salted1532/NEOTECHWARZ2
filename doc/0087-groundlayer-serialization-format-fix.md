# 0087 — 건물 이동 중 고도가 지면 높이를 반영하지 못하고 liftHeight로만 고정되던 문제

## 질문
"건물이 처음 이륙할때는 liftHeight만큼 +되서 y값이 적용되는데 이동할때는 그냥 liftHeight값으로 고정되네
지상높이랑 +된 값으로 y값이 되었으면 좋겠어"

## 원인

[[0084-air-unit-terrain-hugging-altitude]]/[[0085-building-terrain-hugging-altitude]]에서 새로 추가한
`groundLayer`(건물)/`airGroundLayer`(유닛) 필드를 프리팹에 **`groundLayer: 128`처럼 단순 정수 스칼라로 직접
써넣었는데, Unity의 실제 `LayerMask` 직렬화 포맷은 그게 아니라**

```yaml
groundLayer:
  serializedVersion: 2
  m_Bits: 128
```

**구조체 형태**임. 스칼라로 잘못 써놓은 값은 Unity가 `LayerMask`로 제대로 역직렬화하지 못해서 사실상 빈 값(0,
Nothing)으로 취급됨 → `SampleGroundHeight()`의 `if (groundLayer == 0) return fallback;`에 걸려서 매번 레이캐스트
없이 `fallback`(= `flightDestination.y - liftHeight`, 즉 명령을 내린 시점에 한 번 계산해둔 값)만 반환 →
그 결과 `desiredY = fallback + liftHeight`가 항상 **명령 시점에 고정된 값**이 되어, 클릭한 지점이 마침 메인
평지(지면 Y=0)였다면 딱 `liftHeight` 숫자 그대로 보이는 것처럼 관찰됨(사용자가 "liftHeight로 고정"이라고
느낀 지점).

씬을 확인해보니 실제 "언덕"은 `Ground` 레이어(레이어 7)의 평면 오브젝트 3개가 서로 다른 높이/기울기로 배치된
구조였음(평지 Y=0, 경사로, 저지대 Y=-2) - `groundLayer`가 제대로 이 레이어를 가리켜야 `SampleGroundHeight`가
실제 지형 높이를 읽어올 수 있음.

일부 프리팹(Guardian Drone, Firehawk, Tier1~3, MainBase)은 그사이 Unity 에디터에서 다시 저장되면서 저절로
올바른 구조체 형태로 고쳐져 있었지만(그래서 유닛 쪽은 "언덕 이동 잘 된다"고 확인됐던 것), 에디터를 거치지 않은
나머지(`SupplyDepot`, `Lab`, `TestAirUnit`)는 여전히 깨진 스칼라 형태로 남아있었음.

## 수정

깨져 있던 3개 프리팹의 `groundLayer`/`airGroundLayer`를 전부 올바른 구조체 형태로 직접 고쳐씀:
- `Assets/prefabs/NTA/Building/SupplyDepot.prefab`
- `Assets/prefabs/NTA/Building/Lab.prefab`
- `Assets/prefabs/Test/TestAirUnit.prefab`

나머지(Guardian Drone/Firehawk/Tier1~3/MainBase)는 이미 올바른 형태였으므로 그대로 둠. 전체 프리팹 9개
(공중유닛 3 + 건물 6) 전부 `groundLayer`/`airGroundLayer`가 `Ground` 레이어(비트값 128)를 올바르게 가리키는
구조체 형태임을 재확인함.

## 확인 필요 사항
`SupplyDepot`이나 `Lab`은 현재 `canLift: 0`이라 바로 테스트는 안 되지만, 다른 이륙 가능 건물(MainBase/Tier1~3)로
평지 → 경사로 → 저지대(Y=-2)로 이동시키면서 고도가 각 지점의 실제 지면 높이 + liftHeight로 계속 따라가는지
확인 부탁. 향후 `canLift`를 켜는 건물이 생기면 `groundLayer` 값도 같이 확인해야 함(인스펙터에서 `Ground`로
제대로 표시되는지).
