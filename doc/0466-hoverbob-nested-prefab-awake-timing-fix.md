# 0466. HoverBob이 공중 유닛에서 안 켜지던 문제 - EnemyController 때문이 아니라 중첩 프리팹 Awake 타이밍 문제

**날짜:** 2026-08-08

## 질문
> 현재 적 OC경우 Hover Bob이 작동안하는거 같은데 이건 EnemyController라서 그런거야?

## 조사

`HoverBob.Awake()`가 `GetComponentInParent<UnitController>()`/`GetComponentInParent<EnemyUnitController>()`로
루트의 컨트롤러를 찾아 캐싱해두고, `Update()`는 그 캐시된 참조만 본다.

`Strike Drone`/`Raven`(OC) 등 공중 유닛 프리팹을 열어보면, HoverBob이 붙어있는 메쉬 오브젝트
(`unit_Ornithopter_...`)는 루트 아래 직속 자식이 아니라 **중첩된 프리팹 인스턴스**(공유 메쉬
프리팹)이고, HoverBob/TurretController는 그 중첩 인스턴스 위에 "추가된 컴포넌트"로 얹혀있음.

실제로 프리팹을 인스턴스화해서 확인해보니:
- `Object.Instantiate` 직후 곧바로 확인하면 `GetComponentInParent<EnemyUnitController>()`가
  `null`을 캐싱함(Awake 시점 문제).
- 그런데 인스턴스화 직후 **다시** `GetComponentInParent<EnemyUnitController>()`를 호출하면 정상적으로
  찾아짐 - 즉 컴포넌트 자체는 있는데, `Awake()`가 실행되는 바로 그 시점에는 아직 못 찾는 것.
- 원인: 중첩 프리팹 인스턴스는 바깥쪽 루트에 최종적으로 재부모(reparent)되기 **전에** 자체적으로
  먼저 인스턴스화되면서 그 위에 얹힌 컴포넌트들의 `Awake()`가 먼저 호출됨 - 그 순간엔 아직 바깥쪽
  루트(`EnemyUnitController`/`UnitController`가 있는 오브젝트)에 붙어있지 않아서
  `GetComponentInParent`가 못 찾음.
- **같은 문제를 NTA(아군) 쪽 `Guardian Drone`/`Firehawk`에서도 동일하게 재현** - `unitController`도
  똑같이 `null`로 캐싱됨. 즉 `EnemyController`라서가 아니라 "HoverBob이 중첩 프리팹 위에 얹힌
  공중 유닛 전부"에 해당하는 일반적인 문제였음 (아군/적 둘 다 사실상 안 떠 있었을 가능성이 높음).

## 적용

- `HoverBob.cs`: 조회 로직을 `Awake()` → `Start()`로 이동. `Start()`는 그 프레임의 모든 오브젝트가
  최종 계층(재부모 포함)에 다 붙은 뒤에 호출되므로, 이 시점엔 중첩 프리팹도 이미 바깥쪽 루트 밑에
  자리 잡은 상태라 `GetComponentInParent`가 안정적으로 찾음.

## 검증 (Play Mode)

- `Strike Drone` 프리팹을 실제로 씬에 스폰한 뒤 한 프레임 대기 후 확인:
  `cached enemyUnitController != null: True`, `bobbing: True` (수정 전엔 `Awake()` 시점 캐시가
  계속 `null`이라 `Update()`의 `shouldBob`이 항상 `false`였음).
- Unity 콘솔: 새로 발생한 Error 없음(기존 Editor Inspector 관련 에러 6건은 무관하게 그대로).
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- `git status`: `HoverBob.cs`만 이 세션이 만든 변경. `Striker`/`Brute Mech`/`Heavy Assault
  Tank`/`Ironhawk`/`Raven` 프리팹에 잡힌 변경(`EnemyAttackRange.targetTags`,
  `VehicleIdleAnimation` 추가)은 이 세션이 만든 게 아니라 동시에 진행 중인 다른 세션(`doc/0460`)의
  작업 - 건드리지 않음.

## 답변 요약

아니, `EnemyController`라서가 아니라 **HoverBob이 붙어있는 메쉬가 중첩 프리팹이라 `Awake()` 시점에
아직 바깥쪽 루트에 재부모되기 전이라 컨트롤러를 못 찾던 문제**였음(아군 NTA 공중 유닛도 동일하게
영향받고 있었음). `Awake()` → `Start()`로 옮겨서 해결.

## 변경된 파일

- `Assets/Scripts/Animation/HoverBob.cs`
