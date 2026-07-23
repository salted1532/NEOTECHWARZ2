# 0218 — 레이저 공격 빔 기능 설계 제안

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **설계 제안만** 담고 있고
> 아직 스크립트/프리팹을 만들지 않았다. 검토 후 확인해주면 그때 구현한다.

## 1. 요청
"레이저 공격방식을 가진 유닛의 경우 Effect/Laser 폴더의 `Attack_Laser_Blue_3D`를 이용해서, 유닛이 공격 → firePos에 스폰해서
적 transform 위치로 연결 → 0.2초 유지 후 사라짐. 공격 도중 서로 움직일 수 있으니 사라지기 전까지 매 프레임 위치를 갱신해서
계속 연결되게. 공격자가 회전해도 레이저는 같이 돌지 않고 정확히 적 위치에 연결 유지. 오브젝트 풀링 관점에서 프리팹을
유닛에 미리 붙여놓고(firePos 자식으로) 처음엔 비활성 상태로 두고 필요할 때마다 active/inactive로 재활용."

## 2. 기존 코드 구조 조사

- **공격 트리거**: `AttackRange.Update()`가 사거리 안 적을 매 프레임 감지해서 `UnitController.Attack(Vector3 end, GameObject enemy)`를 호출. 내부에 쿨다운(`alreadyAttacked` + `Invoke(ResetAttack, timeBetweenAttacks)`)이 있어서 실제 데미지/이펙트 로직은 쿨다운 주기당 한 번만 실행됨.
- **기존 공격 이펙트 훅**: `UnitController.Attack()` 안, 데미지 적용 직후 `GetComponent<UnitEffects>()?.PlayAttack();` 한 줄로 총구 이펙트를 재생함(`Assets/Scripts/Unit/UnitController.cs:842`). `UnitEffects`는 옵셔널 컴포넌트라 안 붙어있는 유닛은 그냥 무시됨(`?.` null 조건 호출) — 레이저도 이 패턴을 그대로 따르는 게 기존 코드와 가장 자연스럽게 어울림.
- **회전 문제의 실체 확인**: `UnitController.Attack()`은 매 호출마다 `RotateYOnly(end)`로 공격자를 대상 쪽으로 계속 회전시킴(교전 중엔 매 프레임). 그래서 "공격자가 회전해도 레이저는 안 돈다"는 요구사항이 실제로 의미가 있음 — firePos가 유닛의 자식 Transform이라 유닛이 회전하면 firePos도 같이 회전하기 때문.
- **`AttackEffectType`에 이미 `Laser`가 존재**(`Assets/Scripts/Unit/DamageTypes.cs:13`, `HitEffectSet`에도 `laserHitPrefab` 슬롯 있음) — 피격 이펙트 쪽은 이미 레이저를 고려해 만들어져 있고, 이번에 만드는 건 "발사 중 빔 비주얼" 쪽.
- **`EffectPlayer`/`UnitEffects`의 기존 패턴은 이번 요구사항과 안 맞음**: `EffectPlayer.SpawnAtPoints`는 발사 후 잊기(Instantiate → 자기 수명만큼 있다가 Destroy) 방식이라 매 공격마다 새로 생성/파괴됨 — 사용자가 원하는 "미리 생성해서 풀링"과 다름. 또 매 프레임 두 지점(공격자/적) 위치를 갱신하는 기능도 없음.

## 3. `LaserMachine` 컴포넌트를 그대로 재사용할 수 없는 이유

`Attack_Laser_Blue_3D.prefab`은 `Laser_Blue_3D`(데모용)를 복제해서 이미 손질된 상태(광선 1개, 회전 없음,
프리뷰용 Cylinder 메쉬와 AudioSource는 제거됨)지만, 핵심 로직(`LaserMachine.cs`)은 이번 용도와 근본적으로 안 맞음:

1. **재활성화(pooling)와 충돌** — `LaserMachine.OnEnable()`은 호출될 때마다 `elementsList`에 새 `LineRenderer` 자식을 **append만 하고 기존 걸 정리하지 않음**. `SetActive(false)` → `SetActive(true)`를 반복하면(=풀링) `OnEnable`이 매번 실행되어 자식 LineRenderer가 계속 쌓임(메모리 누수 + 라인 중복).
2. **조준 방식이 다름** — `LaserMachine.Update()`는 `element.transform.forward` 방향으로 `Physics.Linecast`를 쏴서 맞은 지점까지 선을 그림. 즉 "물리 충돌체가 있는 방향"이지 "특정 Transform을 정확히 조준"하는 게 아님. 그래서 공격자가 회전하면 `element.transform`도 같이 회전해(부모-자식 관계) 조준 방향이 바뀌어 버림 — 요구사항과 정반대.

**결론**: `Attack_Laser_Blue_3D` 프리팹의 **비주얼(포인트 라이트 + 발광 구슬 + `Laser_BLUE.mat`)은 그대로 재사용**하되,
`LaserMachine` 컴포넌트는 이 프리팹에서 **비활성화(또는 제거)**하고, 새로 만드는 스크립트가 `LineRenderer`를
직접 두 지점(firePos, 적 위치)의 월드 좌표로만 매 프레임 갱신하는 방식으로 간다. 월드 좌표만 쓰기 때문에
공격자의 회전과 무관하게 항상 정확히 연결된다.

## 4. 제안하는 구현

### 4.1 프리팹 쪽
`Attack_Laser_Blue_3D.prefab`:
- 루트의 `LaserMachine` 컴포넌트 → 비활성화(`m_Enabled: 0`)만 해서 그대로 남겨두거나, 아예 제거. (자산 원본과 비교하기 쉽게 일단 비활성화만 하는 걸 추천 — 나중에 다시 회전형 레이저가 필요해지면 참고하기 쉬움)
- 루트(또는 별도 자식)에 `LineRenderer` 컴포넌트를 새로 추가해서 인스펙터에서 직접 세팅: `material = Laser_BLUE.mat`, `useWorldSpace = true`, `receiveShadows = false`, `shadowCastingMode = Off`, `startWidth`/`endWidth`는 기존 데모 값(`m_rayWidth: 1`)을 참고.

### 4.2 새 스크립트 — `LaserBeamAttack.cs` (위치: `Assets/Scripts/Unit/`, `UnitEffects.cs`와 같은 급)
유닛(예: 레이저 유닛 프리팹)에 부착하는 옵셔널 컴포넌트:

```csharp
[SerializeField] private GameObject laserBeamPrefab;   // Attack_Laser_Blue_3D
[SerializeField] private Transform firePoint;
[SerializeField] private float beamDuration = 0.2f;

private LineRenderer beamLine;
private GameObject beamInstance;
private Coroutine activeBeam;

Awake()  → firePoint 밑에 laserBeamPrefab을 한 번 Instantiate, GetComponentInChildren<LineRenderer>() 캐싱, SetActive(false)
Fire(Transform target) → 이미 실행 중인 코루틴 있으면 정지 후 재시작. beamInstance.SetActive(true) →
    beamDuration 동안 매 프레임 SetPosition(0, firePoint.position) / SetPosition(1, target.position) 갱신
    (target이 코루틴 도중 파괴되면 즉시 종료) → 끝나면 SetActive(false)
```

### 4.3 훅 지점
`UnitController.Attack()`의 기존 줄
```csharp
GetComponent<UnitEffects>()?.PlayAttack();
```
바로 아래에 한 줄 추가:
```csharp
GetComponent<LaserBeamAttack>()?.Fire(enemy.transform);
```
`UnitEffects`와 동일하게 옵셔널 컴포넌트라, 이 컴포넌트가 없는 일반 유닛은 전혀 영향받지 않음.

## 5. 확인 필요 사항 (아래 질문 참고)
1. 빔 종점을 정확히 `enemy.transform.position`으로 할지, 아니면 기존 피격 이펙트처럼 적 콜라이더 표면(`ClosestPoint`)으로 할지.
2. `Attack_Laser_Blue_3D` 프리팹의 `LaserMachine` 컴포넌트를 비활성화만 할지 완전히 제거할지.
3. 이대로 구현 진행해도 될지.

## 6. 적용 완료

질문 답변: 빔 종점 = **적 콜라이더 표면(ClosestPoint)**, `LaserMachine` = **비활성화만**(제거 안 함). 이대로 구현함.

1. **`Attack_Laser_Blue_3D.prefab`**
   - 루트의 `LaserMachine` 컴포넌트 `m_Enabled: 1` → `0` (컴포넌트 자체는 남겨둠, 필요하면 나중에 참고 가능).
   - 루트 GameObject에 `LineRenderer` 컴포넌트를 새로 추가: `material = Laser_BLUE.mat`, `useWorldSpace = true`, `castShadows/receiveShadows = false`(기존 `LaserMachine.cs`가 런타임에 설정하던 것과 동일 값), `widthMultiplier: 1` + 폭 커브 상수 1(기존 `m_rayWidth: 1`과 동일한 굵기), `m_Positions`는 초기값 `(0,0,0)`×2(실제 값은 런타임에 스크립트가 매 프레임 덮어씀).
2. **`Assets/Scripts/Unit/LaserBeamAttack.cs`** 신규 작성 — 3절 설계 그대로: `Awake()`에서 `laserBeamPrefab`을 `firePoint` 밑에 한 번만 `Instantiate` 후 `SetActive(false)`(풀링), `Fire(Transform target)`가 코루틴으로 `beamDuration`(기본 0.2초) 동안 매 프레임 `LineRenderer.SetPosition(0, firePoint.position)` / `SetPosition(1, targetCollider.ClosestPoint(firePoint.position))`를 갱신, 대상이 중간에 파괴되면 즉시 종료.
3. **`UnitController.Attack()`** — `GetComponent<UnitEffects>()?.PlayAttack();` 바로 아래에 `GetComponent<LaserBeamAttack>()?.Fire(enemy.transform);` 한 줄 추가(`Assets/Scripts/Unit/UnitController.cs`). 옵셔널 컴포넌트라 이 컴포넌트가 없는 유닛은 전혀 영향 없음.

### 7. 남은 수동 작업 (Unity 에디터에서)
레이저 공격을 쓸 유닛 프리팹을 아직 특정하지 않아서 자동으로 배선은 못 함. 해당 유닛 프리팹에서:
1. `LaserBeamAttack` 컴포넌트를 추가.
2. `Laser Beam Prefab` 필드에 `Attack_Laser_Blue_3D` 프리팹을 할당.
3. `Fire Point` 필드에 유닛의 총구/발사 위치 Transform을 할당(없으면 새로 만들어서 지정).
4. (선택) `Beam Duration`을 0.2초가 아닌 다른 값으로 쓰고 싶으면 인스펙터에서 조정.

플레이 모드에서 해당 유닛으로 공격해서 빔이 firePoint→적 사이를 정확히 연결하고, 유닛이 회전해도 빔이 안 따라 도는지,
0.2초 후 사라지는지, 다음 공격 때 같은 인스턴스가 재활용(SetActive)되는지 확인 부탁.

## 8. 트러블슈팅 — 빔 굵기가 예측 불가능하게 튐 (너무 큼 → 1로 낮추니 안 보임)

### 질문
"라인 렌더러의 레이저 사이즈가 너무 큰데 Line Renderer의 Size를 2->1로 변경해봤는데 그러면 안보이는데 이거 어떻게함?"

### 원인
`Attack_Laser_Blue_3D` 프리팹은 데모용 `Laser_Blue_3D`를 복제한 거라 루트 Transform에
`localScale: (0.5735887, 0.5735887, 0.5735887)`라는 데모 씬 전용 임의의 값이 그대로 남아 있었음.
Unity의 `LineRenderer`는 `useWorldSpace = true`(포지션은 월드 좌표)여도 **선의 굵기는 오브젝트(및 부모 체인)의
스케일에 그대로 곱해지는** 특성이 있음. 이 빔은 `LaserBeamAttack.cs`가 유닛의 `firePoint` 자식으로 인스턴스화하므로,
최종 굵기 = `widthMultiplier × 프리팹 자체 스케일(0.57) × firePoint 쪽 스케일`이 되어 값이 조금만 바뀌어도
"너무 크다가 갑자기 안 보인다"처럼 튀는 원인이 됨.

### 적용한 수정
1. `Attack_Laser_Blue_3D.prefab` 루트(`LineRenderer`가 붙은 오브젝트)의 `localScale`을 `(1, 1, 1)`로 리셋 — 이제 굵기는 `widthMultiplier`/폭 커브 값만으로 예측 가능하게 결정됨.
2. 루트 스케일을 없앤 대신, 기존 시각적 크기(발광 구슬)가 갑자기 커지지 않도록 `Graphics` 자식 오브젝트(→`center sphere`의 부모)의 `localScale`을 `(0.5735887, ...)`로 옮김 — 구슬 크기는 기존과 동일하게 유지, `LineRenderer`는 루트에 있으므로 이 자식 스케일의 영향을 받지 않음. (Point Light의 `Range`는 애초에 트랜스폼 스케일의 영향을 안 받는 값이라 그대로 둠.)
3. `LineRenderer`의 `widthMultiplier`를 `1` → `0.2`로 변경 — 이제 스케일 왜곡 없이 순수하게 "월드 단위 0.2" 굵기.

### 확인 필요 사항
0.2가 원하는 굵기와 다르면 `Attack_Laser_Blue_3D` 프리팹의 `LineRenderer` → `Width` 필드(현재 `Width Multiplier` 개념, 커브 자체는 1로 고정해둠)를 에디터에서 직접 조정하면 됨 — 이제는 스케일에 왜곡되지 않으니 값과 실제 굵기가 예측한 대로 비례해서 바뀜.
