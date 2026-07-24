# 0226 — BigExplosionEffect의 Debris(파편)가 적 유닛(TestEnemy)에서만 안 보이는 원인

## 1. 요청
"근데 아군 유닛에게 쏘는건 잘 작동하는데 왜 적 유닛에게 쏘는거만 그 Devris 이부분이 안보이는거임?"
([[0225-bigexplosioneffect-partial-materials-broken-in-urp]]의 후속 질문 — 그 문서에서 Debris는 이미
URP 셰이더로 정상 변환돼 있다고 확인했으므로, 이번 증상은 셰이더 문제가 아니라 다른 원인)

## 2. 조사 내용

- `BigExplosionEffect.prefab`의 `Debris` 자식은 (StoneSurface.mat, Mesh 렌더 모드 — 실제 돌덩이 메쉬를
  파티클로 흩뿌리는 방식) `CollisionModule`이 켜져 있음:
  - `type: 1` (World collision — 씬의 실제 콜라이더와 물리적으로 충돌)
  - `collidesWith.m_Bits: 4294967295` (전체 레이어와 충돌)
  - `interiorCollisions: 1` (파티클이 콜라이더 내부/표면에서 시작해도 충돌 감지)
  - Unity의 World Collision 모듈은 **Trigger로 설정된 콜라이더와는 충돌하지 않고, 실제(Trigger가 아닌)
    콜라이더에만 반응**함(엔진 사양).
- 피격 이펙트 스폰 위치는 `EffectPlayer.PlayHit()`에서 `bodyCollider.ClosestPoint(attackerPosition)`로
  계산됨 — 즉 **맞은 대상 자신의 바디 콜라이더 표면**에 정확히 스폰됨(`Assets/Scripts/Effects/EffectPlayer.cs:51-61`).
- 여기서 아군(플레이어) 유닛과 TestEnemy의 바디 콜라이더 설정이 다름:
  - `Assets/prefabs/NTA/Unit/Tier1/Assault Trooper.prefab`의 `CapsuleCollider` → `m_IsTrigger: 1` (Trigger).
    같은 패턴이 프로젝트의 다른 Unit 프리팹들에도 공통 적용됨 — 클릭 선택 레이캐스트용으로만 쓰고, 이동/충돌은
    NavMeshAgent가 전담하므로 물리적으로는 통과되게 만들어둔 것.
  - `Assets/prefabs/Test/TestEnemy.prefab`의 `CapsuleCollider` → `m_IsTrigger: 0` (Trigger 아님, 실제
    물리 콜라이더) + `Rigidbody`까지 붙어있음(`m_UseGravity: 0`, `m_IsKinematic: 0`).
- 그래서 아군 유닛을 맞혔을 때는 Debris 파티클이 (Trigger라서) 대상 콜라이더와 물리적으로 충돌하지 않고
  스폰 지점에서 그대로 사방으로 튀어나가 정상적으로 보이는데, **TestEnemy를 맞혔을 때는 Debris가 스폰되자마자
  TestEnemy 자신의 실제(비-Trigger) 콜라이더 표면/내부에서 바로 충돌 판정을 받아** `m_Dampen`/`m_Bounce`/
  `m_EnergyLossOnCollision` 커브에 의해 튀어나가지 못하고 그 자리에 붙잡히거나 즉시 감속돼버림 — 화면상으로는
  "그 부분만 안 보인다"로 느껴지는 것.

## 3. 결론
셰이더 문제(0225)와는 별개로, **TestEnemy 프리팹의 `CapsuleCollider`가 다른 모든 Unit 프리팹과 달리
`m_IsTrigger: 0`(실제 물리 콜라이더)로 되어 있어서** BigExplosionEffect의 Debris 파티클(World Collision
모듈 사용)이 TestEnemy 자신의 몸에 부딪혀 튕겨나가지 못하고 즉시 멈춰버리는 것이 원인. 아군 유닛은 전부
콜라이더가 Trigger라 이 충돌이 아예 발생하지 않아서 정상으로 보인 것.

## 4. 제안하는 수정 (아직 적용 안 함 — 승인 필요)
`Assets/prefabs/Test/TestEnemy.prefab`의 `CapsuleCollider`를 다른 Unit 프리팹들과 동일하게
`m_IsTrigger: 0` → `1`로 변경 (클릭 판정용 콜라이더로만 쓰고 물리 충돌은 만들지 않는 프로젝트 공통 컨벤션에 맞춤).

```yaml
# Assets/prefabs/Test/TestEnemy.prefab, CapsuleCollider (fileID 5932759624869339708)
# 기존
  m_IsTrigger: 0
# 변경
  m_IsTrigger: 1
```

- 참고: TestEnemy에는 다른 Unit 프리팹에는 없는 `Rigidbody`(`m_UseGravity: 0`, `m_IsKinematic: 0`)도 붙어있음.
  콜라이더를 Trigger로 바꾸면 이 Rigidbody가 굳이 있을 이유가 없어지지만(물리 충돌 대상이 없어짐), 지금
  당장 문제를 일으키는 건 아니라서 이번 수정 범위에는 포함하지 않음 — 필요하면 별도로 정리 여부 확인 요청.

진행할지 확인 부탁.
