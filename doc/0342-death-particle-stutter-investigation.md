# 0342. 유닛 사망/파티클 관련 순간 렉(스터터) 원인 조사

**날짜:** 2026-07-31

## 요청 내용

> 게임을 빌드해서 플레이 해봤는데 어느 순간 잠깐잠깐 렉 걸리는게 있는데
> 이게 파티클이나 유닛이 사망할때 생기는건가 뭔가 그래픽적이나 그런면에서 생기는 느낌이라서
> 이거 확인해줘

## 조사 내용

사망/피격/이펙트 관련 코드 경로(`HealthManager`, `UnitEffects`, `EffectPlayer`, `UnitController.Die()`,
`EnemyUnitController.Die()`, `BuildingController.Die()`, `Projectile`)를 전부 읽고 비교함.

### 원인 1 (확정) — `UnitController.Die()`가 캐싱된 필드를 두고 매번 씬 전체 검색을 함

```csharp
// Unit/UnitController.cs:1482
public void Die()
{
    ...
    RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
    controller?.UnitList.Remove(this);
    ...
}
```

이 클래스는 이미 `Start()`에서 `rtsController` 필드에 `RTSUnitController`를 캐싱해두고(`:157, :278`)
`Attack()`, `FollowTick()` 등 다른 모든 곳에서 그 캐싱된 필드를 재사용한다. 그런데 유독 `Die()`만
캐싱된 필드를 쓰지 않고 `FindFirstObjectByType<RTSUnitController>()`를 새로 호출한다.

`FindFirstObjectByType`은 로드된 오브젝트를 뒤지는 씬 전역 탐색이라 단발성으로는 크지 않지만, 이
프로젝트에서 유일하게 여기서만 죽을 때마다 재실행된다. 같은 `IDestructible.Die()` 구현체인
`EnemyUnitController.Die()`(`:525`)와 `BuildingController.Die()`(`:491`)는 둘 다 캐싱된
`rtsController` 필드를 그대로 쓴다 — `UnitController.Die()`만 예외.

한 번의 전투에서 아군 유닛 여러 기가 같은 프레임/짧은 구간에 몰려 죽는 상황(다수 병력이 붙어 싸우다
전멸, 광역 공격에 맞아 한꺼번에 사망 등)에서 이 불필요한 탐색이 유닛 수만큼 누적되어 사망 순간에
순간적인 프레임 스파이크로 체감될 수 있음 — 사용자가 보고한 "유닛 사망 시 렉"과 정확히 일치하는
패턴.

### 원인 2 (보조, 확정) — 전투 핫패스에 `Debug.Log` 다수

```csharp
// Unit/HealthManager.cs:77 (GetDamage, 즉 "피격당할 때마다" 매번 호출)
Debug.Log($"{gameObject.name} HP: {currentHp}/{maxHealth}");

// Unit/UnitController.cs:933 (Attack, 즉 "공격 성공할 때마다" 매번 호출)
Debug.Log("공격성공!");
```

`Debug.Log`는 문자열 보간(할당) + 콘솔 기록 비용이 있어 호출 1회는 미미하지만, 전투가 커질수록
(유닛 수 × 초당 공격 횟수만큼) 매 프레임 수십~수백 회씩 쌓일 수 있음. 빌드에서도 `Debug.Log`는
기본적으로 스트립되지 않고 그대로 남아 있어(개발 빌드/`Development Build`가 아니어도 로그 자체는
실행됨), 전투가 격해지는 구간(=유닛이 많이 죽는 구간과 겹침)에서 프레임 스파이크의 일부로 함께
작용했을 가능성이 있음. 사망 자체보다는 "전투가 격해지는 타이밍"과 겹쳐서 같은 증상으로 체감됐을 수
있음.

### 원인 3 (참고용, 구조적 — 당장 손대는 걸 권하지 않음) — 파티클 이펙트가 풀링 없이 즉시 Instantiate/Destroy

`EffectPlayer.Spawn()`(공격/피격/사망 이펙트가 전부 거치는 공용 경로)은 이펙트마다
`Object.Instantiate` → `GetComponentsInChildren<ParticleSystem>()` 스캔 → `Object.Destroy(instance, lifetime)`
예약을 한다. 오브젝트 풀링이 전혀 없어서, 이펙트 하나마다 GC 대상이 되는 새 GameObject가 생기고
사라진다. 유닛 한둘이 죽을 때는 체감이 안 되지만, 광역 공격/대규모 교전으로 사망 이펙트가 같은
프레임에 몰리면 이론상 스파이크 요인이 될 수 있음.

다만 이건 프로젝트 전체에 풀링 인프라 자체가 아예 없어서(코드베이스 검색 결과 풀링 관련 코드
없음) 고치려면 이펙트 재생 경로 전체를 새로 설계해야 하는 큰 작업이고, 원인 1/2보다 실제 기여도가
훨씬 작을 가능성이 높음(유닛 사망 1건당 이펙트 프리팹 Instantiate는 1개뿐이고, 대규모 동시사망이
아니면 몇십 KB 이하 스파이크). 원인 1/2를 먼저 고치고 빌드에서 재현되는지 다시 확인한 뒤에도 렉이
남아있으면 그때 프로파일러로 실측해서 필요성을 판단하는 게 낫다고 봄.

## 적용한 수정

원인 1 + 원인 2 모두 수정 (사용자 확인 후 진행). 원인 3(파티클 풀링)은 보류 — 지금 단계에서는
근거가 약하고 프로젝트에 풀링 인프라 자체가 없어 큰 작업이라, 1/2 적용 후에도 렉이 재현되면 그때
프로파일러로 실측해서 진행 여부 판단하기로 함.

### `Assets/Scripts/Unit/UnitController.cs`

```diff
-        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
-        controller?.UnitList.Remove(this);
-        controller?.selectedUnitList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel/Squad_panel 등)가 유령 참조를 들고 있지 않도록
-        controller?.ReleaseUnitPopulation(unitID); // 죽은 유닛이 차지하던 인구수를 현재 인구수에서 반환
+        rtsController?.UnitList.Remove(this);
+        rtsController?.selectedUnitList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel/Squad_panel 등)가 유령 참조를 들고 있지 않도록
+        rtsController?.ReleaseUnitPopulation(unitID); // 죽은 유닛이 차지하던 인구수를 현재 인구수에서 반환
```

```diff
-        Debug.Log("공격성공!");
         if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
```

### `Assets/Scripts/Unit/HealthManager.cs`

```diff
         OnDamaged?.Invoke(damage, attackerPosition, attackType, isEnemyAttacker);

-        Debug.Log($"{gameObject.name} HP: {currentHp}/{maxHealth}");
-
         if (currentHp <= 0)
```

`npx uloop-cli compile`: 에러 0개 확인 (경고 25개는 전부 이 수정과 무관한 기존 경고, 대부분
프로젝트 전역의 `FindFirstObjectByType` deprecated 경고).

## 영향받는 파일

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Unit/HealthManager.cs`
