# 0111 - 공격 취소 시 공격 이펙트 즉시 정지

## 1. 요청
"공격 하는 와중엔 이펙트가 나오다가 공격을 멈추면 바로 없어지도록 해줘. 이동명령이나 다른 명령을 내려서
공격이 취소되면 바로 이펙트도 없어지도록."

## 2. 설계
공격 이펙트는 지금까지 "발사 후 잊기"(자기 재생시간 다 되면 스스로 파괴)로만 동작했다. 이번 요청은 그 자연
종료를 기다리지 않고, **공격 명령이 취소되는 시점에 강제로 즉시 파괴**하는 경로를 추가하는 것.

`UnitController.CancelAttackOrder()`가 이동(`MoveTo`)/정지(`StopUnit`)/순찰(`PatrolUnit`)/따라가기
(`FollowUnit`)/건설이동(`GoBuild`)/대기(`HoldUnit`) 등 **공격이 아닌 다른 명령이 들어올 때마다 이미 호출되는
공통 지점**이라, 여기 한 곳에만 훅을 걸면 요청한 모든 케이스("이동명령이나 다른 명령")를 커버한다.

- `UnitEffects`가 `PlayAttack()`으로 스폰한 인스턴스들을 `activeAttackEffects` 목록에 계속 추적.
- `StopAttackEffects()`: 그 목록에 남아있는(아직 자기 수명 안 끝난) 인스턴스를 전부 즉시 `Destroy`.
- `UnitController.CancelAttackOrder()`에서 `GetComponent<UnitEffects>()?.StopAttackEffects()` 호출.

이미 자기 수명이 다 돼서 스스로 파괴된 인스턴스는 리스트에서 매 `PlayAttack()`마다 걸러내(`RemoveAll(null)`)
무한정 커지지 않게 했다.

## 3. 변경 내용

### `Assets/Scripts/Effects/EffectPlayer.cs`
`SpawnAtPoints`가 `void` 대신 스폰한 인스턴스 목록(`List<GameObject>`)을 반환하도록 변경 — 호출자가 필요하면
그 인스턴스들을 붙잡아뒀다가 나중에 조기 종료(`Destroy`)할 수 있게. 기존 호출부(`BuildingEffects`,
`ConstructionEffects`, `UnitEffects.HandleDeath`)는 반환값을 그냥 무시하면 되므로 수정 불필요(C#에서
`void` 메서드 본문이 값을 반환하는 표현식이어도 컴파일됨).

### `Assets/Scripts/Effects/UnitEffects.cs`
```csharp
private List<GameObject> activeAttackEffects = new();

public void PlayAttack()
{
    activeAttackEffects.RemoveAll(effect => effect == null);
    activeAttackEffects.AddRange(EffectPlayer.SpawnAtPoints(muzzlePrefab, firePoints, transform));
}

public void StopAttackEffects()
{
    foreach (GameObject effect in activeAttackEffects)
        if (effect != null) Destroy(effect);

    activeAttackEffects.Clear();
}
```

### `Assets/Scripts/Unit/UnitController.cs` — `CancelAttackOrder()`
```csharp
attackMoveDestination = null;
followTarget = null;
hasFollowOrder = false;

GetComponent<UnitEffects>()?.StopAttackEffects(); // 추가

CancelBuildOrder();
```

## 4. 범위 밖(의도적으로 손 안 댄 것)
적/대상이 죽어서 자연스럽게 교전이 끝나는 경우(`FriendlyAttackTick`/`AttackOrderTick`이 대상 null을 감지해
Idle로 전환하는 경로)는 `CancelAttackOrder()`를 거치지 않아서 이번 수정의 영향을 받지 않는다 — 마지막 한 발의
이펙트는 원래대로 자기 수명만큼 재생을 끝낸다. 요청이 명시한 건 "이동명령이나 다른 명령으로 취소"였고, 대상이
죽어서 끝나는 건 다른 시나리오라 범위에서 제외했다.
