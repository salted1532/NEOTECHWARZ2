# 0567 - 반쯤 밝혀진 지역에서 미발견 적 유닛 숨기기 (제안)

## 요청 내용

> Fog of war에서 반쯤 밝혀진 곳(유닛이 한번 지나간곳)에서 이전에 발견된 적 유닛 말고 새로 등장하는
> 유닛은 안보이도록 할수 없나? 한번 유닛들에게 밝혀짐된 적 유닛에 경우 반쯤 밝혀진 곳에서 보이지만
> 만약 다시 어두운곳으로 가게 되면 밝혀짐이 풀리는 식으로 하면 어떻까 그러면 반쯤 밝혀진 곳에서 안
> 밝혀진 유닛에 경우 안보이도록 하는거야

정리: 안개는 "완전히 밝음(지금 시야 안)/반쯤 밝음(예전에 밝혀졌던 적 있음)/완전히 어두움(한번도
안 밝혀짐)" 3단계인데, 지금은 적 유닛이 "직접 발견된 적 있는지"와 무관하게 반쯤 밝은 지역에 있기만
하면 (다들 흐릿하게) 보인다. 원하는 동작:
- 완전히 밝은 곳: 항상 보임 (지금과 동일) + "발견됨" 기록.
- 반쯤 밝은 곳: **발견된 적 있는 유닛만** 보임. 발견된 적 없는 유닛(반쯤 밝은 지역으로 새로 들어온
  적)은 안 보임.
- 완전히 어두운 곳으로 나가면 "발견됨" 기록이 풀림 - 나중에 다시 반쯤 밝은 지역에 들어와도 실제로
  다시 눈에 띄기(완전히 밝은 곳에 서기) 전까진 안 보임.

## 조사 결과

### 안개는 이미 3단계 데이터를 갖고 있음 (지금은 2단계로 뭉뚱그려 쓰는 중)

`Assets/AssetFolder/AOSFogWar/Shadowcaster.cs:131-136`:
```csharp
public enum ETileVisibility { Hidden, Revealed, PreviouslyRevealed }
```
`GameManager.prefab`에 `keepRevealedTiles: 1`이 켜져 있어 `PreviouslyRevealed`(반쯤 밝음, 알파
`revealedTileOpacity`로 반투명 렌더)가 실제로 쓰이고 있음.

`Assets/Scripts/FogOfWar/FogVisibility.cs`(공용 헬퍼)의 `IsRevealed()`는 `Revealed`와
`PreviouslyRevealed`를 **똑같이 "보임"으로 취급**하는 단일 bool만 반환한다. 이 프로젝트 안의 모든
소비처(체력바/미니맵 마커/이펙트/선택 해제/클릭 판정 등, `doc/0356/0358/0359/0360` 등)가 전부 이
2단계 bool 하나만 쓴다.

### 적 유닛의 3D 모델 자체는 지금 스크립트로 켜고 끄지 않는다

안개는 실제 3D Plane 메시(Y≈1)라서, `Hidden`은 완전 불투명(모델을 가림), `PreviouslyRevealed`는
반투명(모델이 흐릿하게 비쳐 보임), `Revealed`는 완전 투명 - 이 3단계가 지금은 순전히 "안개 판의
알파값"만으로 자동 표현되고 있다 (`doc/0356`에 설명됨). 즉 반쯤 밝은 지역에 있는 적은 "발견 여부"와
무관하게 전부 흐릿하게 보이는 게 현재 동작 - 요청하신 문제와 정확히 일치.

`EnemyUnitController.cs:225-234` (`UpdateFogVisibility()`, 매 프레임 `Update()`에서 호출)는 지금
미니맵 마커(`minimapIcon.enabled`)와 선택 해제만 처리하고, 몸체 렌더러는 건드리지 않는다.

### "발견됨" 개인별 기록은 현재 전혀 없음

코드 전체에 `discovered`/`revealed`/`hasBeenSeen` 류의 유닛별 플래그가 없음 - 완전히 새로 추가해야
하는 개념. (참고: 초기 설계 문서 `doc/0069`는 "잔상/최근 위치 기억"은 오히려 혼란을 준다고 명시적으로
제외했었는데, 이번 요청은 그것과 달리 "잔상"이 아니라 "발견된 적만 반쯤 밝은 곳에서 계속 보임"이라
다른 기능임.)

## 제안하는 수정

**범위: `EnemyUnitController`(적대 유닛)만.** `AllyController`(아군 편입 OC)와 건물
(`EnemyBuildingController`)은 이번 요청("새로 등장하는 유닛") 밖이라 그대로 둠 - 건물은 안 움직여서
"새로 등장" 자체가 없고, 아군 유닛은 적이 아니라서 숨길 이유가 없음.

### 1. `Assets/Scripts/FogOfWar/FogVisibility.cs` - "지금 완전히 밝은지"만 보는 조회 추가

기존 `IsRevealed`(Revealed든 PreviouslyRevealed든 하나라도 있으면 true) 옆에, `Revealed`만 인정하는
버전을 추가 (margin 루프는 기존과 동일한 관례 그대로):

```csharp
// "지금 실제로 시야 안(Revealed)"인지만 본다 - IsRevealed와 달리 PreviouslyRevealed(반쯤 밝음)는 false.
public static bool IsCurrentlyVisible(csFogWar fogWar, Vector3 worldPosition, int margin = 1)
{
    if (fogWar == null) return true;

    Vector2Int center = fogWar.WorldToLevel(worldPosition);

    for (int x = -margin; x <= margin; x++)
    {
        for (int y = -margin; y <= margin; y++)
        {
            Vector2Int cell = new Vector2Int(center.x + x, center.y + y);
            if (!fogWar.CheckLevelGridRange(cell)) continue;

            if (fogWar.shadowcaster.fogField[cell.x][cell.y] == Shadowcaster.LevelColumn.ETileVisibility.Revealed)
                return true;
        }
    }
    return false;
}
```

### 2. `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

**필드 추가** (`fogWar` 근처):
```csharp
// 이 유닛이 "완전히 밝은 곳(Revealed)"에 한 번이라도 서서 실제로 발견된 적이 있는지. 반쯤 밝은 곳
// (PreviouslyRevealed)에서는 이게 true인 유닛만 계속 보이게 한다. 완전히 어두운 곳(Hidden)으로
// 나가면 다시 false로 풀린다 (doc/0567).
private bool discovered;
```

**몸체 렌더러 캐싱** (`Awake()`에 한 줄, 기존 `turretController = GetComponentInChildren<...>()` 등과
동일한 자리):
```csharp
private Renderer[] bodyRenderers;
...
bodyRenderers = GetComponentsInChildren<Renderer>();
```

**`UpdateFogVisibility()` 교체**:
```csharp
private void UpdateFogVisibility()
{
    bool currentlyVisible = FogVisibility.IsCurrentlyVisible(fogWar, transform.position, minimapFogVisibilityMargin);
    bool exploredAtAll = FogVisibility.IsRevealed(fogWar, transform.position, minimapFogVisibilityMargin);

    if (currentlyVisible)
        discovered = true;
    else if (!exploredAtAll) // 완전히 어두운 곳으로 나가면 발견 기록을 잃는다
        discovered = false;

    // 반쯤 밝은 곳(exploredAtAll && !currentlyVisible)에서는 discovered인 유닛만 보임
    bool effectivelyVisible = currentlyVisible || (exploredAtAll && discovered);

    if (minimapIcon != null)
        minimapIcon.enabled = effectivelyVisible;

    foreach (Renderer r in bodyRenderers)
        if (r != null && r != minimapIcon)
            r.enabled = effectivelyVisible;

    if (!effectivelyVisible)
        rtsController?.ClearSelectedEnemyIfMatches(this);
}
```

## 영향 범위 / 한계

- `fogWar == null`인 씬(안개 없는 테스트 씬 등)에서는 `IsCurrentlyVisible`도 `true`를 반환하므로
  `discovered`가 항상 true로 유지돼 기존과 동일하게 항상 보임 - 회귀 없음.
- 체력바(`HealthManager`)/이펙트(`UnitEffects`/`EffectPlayer`)/클릭 판정(`UserControl`)은 여전히
  기존의 `FogVisibility.IsRevealed`(2단계) 기준을 그대로 씀 - "모델은 안 보이는데 클릭은 되거나
  이펙트는 재생되는" 불일치가 생길 수 있음. 이번 요청은 "안 보이게"가 핵심이라 몸체/미니맵/선택
  해제만 우선 맞추고, 나머지(체력바 등)까지 통일할지는 별도 확인 필요 - 원하시면 같은 패턴으로
  추가 확장 가능.
- `bodyRenderers`는 `Awake()` 시점에 존재하는 자식만 캐싱한다 - 공격 이펙트 등이 나중에 이 유닛의
  자식으로 동적 생성되는 구조라면 그건 포함되지 않음(현재 구조상 공격 이펙트는 별도 재생 방식이라
  문제 없을 것으로 보이나, 확인 필요).
- 몸체 렌더러를 꺼도 NavMeshAgent 이동/AI 로직 자체는 계속 동작함(순수 렌더링만 제어) - 기존
  `Hidden`(완전 어둠) 상태에서 안개 판이 가리는 것과 동일한 성격.

## 확인 요청

이 방향(파일 2개, `EnemyUnitController`만 대상)으로 구현해도 될지 확인 부탁드립니다. 체력바 등
나머지 UI도 같이 "미발견 시 숨김"으로 맞출지 여부도 알려주시면 반영하겠습니다.

## 범위 확정 (사용자 추가 지시)

> 미발견시 클릭X 체력바,이펙트 등 그런것도 안보이도록 해줘 반쯤 밝혀졌는데 발견된 유닛에 경우에만
> 계속 보이도록 하지만 어둠으로 가게 되면 밝혀짐이 풀려져서 다시 안보이게 되도록

→ 체력바(`HealthManager`)/이펙트(`UnitEffects`: 이동 트레일, 공격/피격/사망 이펙트)/클릭·호버 판정
(`UserControl`)까지 전부 `EnemyUnitController.IsEffectivelyVisible()`을 따르도록 확장.

## 구현 결과

컴파일 성공(`npx uloop-cli compile --wait-for-domain-reload true` → `Success: true, ErrorCount: 0`,
기존 경고 40개만 그대로).

### `Assets/Scripts/FogOfWar/FogVisibility.cs`
제안한 `IsCurrentlyVisible()` 그대로 추가 (Revealed만 인정, PreviouslyRevealed는 false).

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
제안대로 `discovered`/`effectivelyVisible`/`bodyRenderers` 추가하고, 다른 컴포넌트가 읽을 수 있게
`public bool IsEffectivelyVisible() => effectivelyVisible;` 게터를 추가로 노출. `UpdateFogVisibility()`는
제안한 로직 그대로 구현.

### `Assets/Scripts/Unit/HealthManager.cs`
`Awake()`에서 `enemyUnitController = GetComponent<EnemyUnitController>();` 캐싱(적이 아니면 null로
남아 기존 동작 그대로). `Update()`의 체력바 표시 조건을 다음으로 교체:
```csharp
bool visible = enemyUnitController != null
    ? enemyUnitController.IsEffectivelyVisible()
    : FogVisibility.IsRevealed(fogWar, transform.position);
healthSlider.gameObject.SetActive(visible);
```

### `Assets/Scripts/Effects/UnitEffects.cs`
이미 `Awake()`에서 `enemyUnitController`를 캐싱해두고 있었음(doc/0233) - 그대로 재사용.
- `Update()`의 이동 트레일 표시 조건을 `HealthManager`와 동일한 패턴으로 교체.
- `PlayAttack()`/`HandleDamaged()`/`HandleDeath()` 맨 앞에
  `if (enemyUnitController != null && !enemyUnitController.IsEffectivelyVisible()) return;` 가드 추가 -
  발사/피격/사망 이펙트가 아예 스폰되지 않음.

### `Assets/Scripts/UserControl/UserControl.cs`
적 유닛 대상 클릭/추격/호버 판정 3곳을 `IsRevealedByFog(position)` 대신 `enemy.IsEffectivelyVisible()`로
교체 (좌클릭 선택·공격 doc `line 358`대, 우클릭 추격 공격 `line 619`대, `GetHoveredTarget()`의 커서
판정 `line 1147`대 - 이곳은 `layerEnemy`에 적 유닛/건물이 섞여있어 `EnemyUnitController`가 있으면
그쪽을, 없으면(건물) 기존 위치 기반 판정을 쓰도록 분기). 적 건물/자원 노드 관련 호출(`IsRevealedByFog`
직접 사용)은 그대로 둠 - 이번 요청 범위(적 유닛) 밖.

## 최종 요약

- 완전히 밝은 곳: 항상 보임 + `discovered = true` 기록.
- 반쯤 밝은 곳: `discovered`인 유닛만 몸체/미니맵/체력바/이펙트/클릭·호버 전부 보임·가능, 아니면 전부 숨김.
- 완전히 어두운 곳으로 나가면 `discovered = false`로 풀림 - 이후 반쯤 밝은 곳에 다시 들어와도 실제로
  다시 완전히 밝은 곳에서 눈에 띄기 전까진 계속 숨김.
