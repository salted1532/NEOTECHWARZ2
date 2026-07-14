# 0118 - 이동 이펙트 회전 추적 완화 (구현 완료)

## 날짜
2026-07-14

## 요청
"이동 이펙트가 만약 유닛이 회전한다고 하면 조금 느리게 회전을 따라가게 할수 있어? 지금 이펙트가 너무
유닛에 딱 붙어있어서 회전할때 부자연스럽게 움직이네 이펙트는 유닛의 회전값 만큼 따라갈때 느리게 따라가거나
회전할떄는 이펙트가 꺼지거나 좀 줄어들었으면 좋겠어"

## 조사
이동 트레일(발밑/바퀴 이펙트)은 `Assets/Scripts/Effects/UnitEffects.cs`의 `SetMoveTrail()`이
`EffectPlayer.SpawnPersistentAtPoints(moveTrailPrefab, moveTrailPoints, transform)`로 스폰한다.

`EffectPlayer.SpawnPersistentAtPoints()` (Assets/Scripts/Effects/EffectPlayer.cs:102-123)는 스폰된
인스턴스를 지점(`moveTrailPoints`의 각 Transform, 없으면 유닛 자신)에 **직접 부모로 붙인다**
(`Object.Instantiate(effectPrefab, point.position, point.rotation, point)`). Unity에서 부모-자식 관계는
매 프레임 즉시(hard) 동기화되므로, 유닛이 회전하면 이펙트의 월드 회전값도 같은 프레임에 똑같이 즉시
바뀐다 — 이것이 "이펙트가 유닛에 딱 붙어서 회전할 때 부자연스럽다"는 현상의 원인이다.

같은 함수(`SpawnPersistentAtPoints`)는 건설 중 이펙트(`ConstructionEffects.cs`)에도 쓰이는데, 건물은
회전하지 않으므로 그쪽은 영향이 없다 — 유닛 이동 트레일에만 해당하는 변경이다.

## 제안하는 두 가지 방향
사용자가 메시지에서 두 방향을 모두 제시했으므로, 택1 또는 병행 여부를 확인 후 구현하고자 함.

**A안 — 회전을 느리게 따라가기 (Slerp)**
트레일 인스턴스를 지점에 직접 부모로 붙이는 대신, 별도의 작은 컴포넌트가 위치는 매 프레임 그대로
따라가되(발밑에서 안 떨어지도록) 회전만 `Quaternion.Slerp`로 서서히 뒤쫓게 한다. 급격한 방향 전환 시
이펙트가 유닛보다 살짝 늦게 돌아 "관성"처럼 보인다.

**B안 — 회전 중 축소/페이드**
매 프레임 유닛의 회전 변화량(각속도)을 측정해서, 일정 각속도 이상으로 회전 중이면 트레일의 스케일이나
파티클 방출량을 줄였다가 회전이 끝나면 원래대로 복구한다.

두 방향은 서로 배타적이지 않지만(A만 해도 상당히 자연스러워질 것으로 예상), 처음엔 A안만 구현하고
필요하면 나중에 B안을 추가하는 것을 추천함 — B안은 "회전 중 감지" 임계값 튜닝이 더 필요해서 결과가
바로 눈에 안 들어올 수 있음.

## 결정
사용자가 "A+B 둘 다" 선택 → 아래처럼 한 컴포넌트(`TrailRotationFollower`)에 두 기능을 함께 구현함.

## 실제 구현

### 신규 파일: `Assets/Scripts/Effects/TrailRotationFollower.cs`
A안(회전 Slerp 추적)과 B안(급회전 중 축소)을 한 컴포넌트에 함께 구현. `Init()`에서 부모 부착을 끊고
(`transform.SetParent(null)`), 자식 ParticleSystem들의 기준 `startSizeMultiplier`/`rateOverTimeMultiplier`를
저장해둔다. 매 `LateUpdate()`마다:
1. 지점의 각속도(`Quaternion.Angle(직전 회전, 현재 회전) / Time.deltaTime`)를 측정
2. 임계값(`fastRotationThreshold`, 도/초) 초과 시 목표 축소율을 `shrinkScale`로, 아니면 1로 두고 `Mathf.Lerp`로 서서히 접근
3. 그 축소율을 각 ParticleSystem의 크기/방출량 배율에 곱해서 적용
4. 위치는 그대로 스냅, 회전만 `Quaternion.Slerp(..., rotationFollowSpeed * Time.deltaTime)`으로 서서히 따라감

### `Assets/Scripts/Effects/EffectPlayer.cs` (기존 → 변경)
```csharp
// 기존
public static List<GameObject> SpawnPersistentAtPoints(GameObject effectPrefab, IReadOnlyList<Transform> points, Transform fallback)
{
    ...
    instances.Add(ForceLooping(Object.Instantiate(effectPrefab, point.position, point.rotation, point)));
    ...
}
```
```csharp
// 변경 (rotationFollowSpeed > 0이면 부모 대신 TrailRotationFollower로 위치/회전/크기를 직접 제어)
public static List<GameObject> SpawnPersistentAtPoints(
    GameObject effectPrefab, IReadOnlyList<Transform> points, Transform fallback,
    float rotationFollowSpeed = 0f, float fastRotationThreshold = 90f, float shrinkScale = 1f, float shrinkLerpSpeed = 8f)
{
    ...
    instances.Add(SpawnPersistentAt(effectPrefab, point, rotationFollowSpeed, fastRotationThreshold, shrinkScale, shrinkLerpSpeed));
    ...
}

private static GameObject SpawnPersistentAt(GameObject effectPrefab, Transform point,
    float rotationFollowSpeed, float fastRotationThreshold, float shrinkScale, float shrinkLerpSpeed)
{
    GameObject instance = ForceLooping(Object.Instantiate(effectPrefab, point.position, point.rotation, rotationFollowSpeed > 0f ? null : point));
    if (rotationFollowSpeed > 0f)
        instance.AddComponent<TrailRotationFollower>().Init(point, rotationFollowSpeed, fastRotationThreshold, shrinkScale, shrinkLerpSpeed);
    return instance;
}
```

### `Assets/Scripts/Effects/UnitEffects.cs` (기존 → 변경)
```csharp
// 기존
[SerializeField] private GameObject moveTrailPrefab;
[SerializeField] private List<Transform> moveTrailPoints = new();
...
activeTrails = EffectPlayer.SpawnPersistentAtPoints(moveTrailPrefab, moveTrailPoints, transform);
```
```csharp
// 변경
[SerializeField] private GameObject moveTrailPrefab;
[SerializeField] private List<Transform> moveTrailPoints = new();
[SerializeField] private float moveTrailRotationFollowSpeed = 6f; // 낮을수록 더 느리게(관성감 크게), 0이면 기존처럼 즉시 부착
[SerializeField] private float moveTrailFastRotationThreshold = 90f; // 이 각속도(도/초) 초과 급회전 중엔 축소
[SerializeField] private float moveTrailShrinkScale = 0.4f; // 급회전 중 목표 크기/방출량 배율 - 1이면 축소 없음
[SerializeField] private float moveTrailShrinkLerpSpeed = 8f;
...
activeTrails = EffectPlayer.SpawnPersistentAtPoints(
    moveTrailPrefab, moveTrailPoints, transform,
    moveTrailRotationFollowSpeed, moveTrailFastRotationThreshold, moveTrailShrinkScale, moveTrailShrinkLerpSpeed);
```

`ConstructionEffects.cs`는 새 매개변수를 안 넘기므로 기본값(`rotationFollowSpeed = 0f`)으로 기존 동작
(즉시 부착) 그대로 유지된다.

## 튜닝 안내
4개 값 모두 `UnitEffects` 인스펙터에 노출되어 있어 유닛/프리팹별로 인게임에서 보면서 조정 가능:
- `moveTrailRotationFollowSpeed`: 작을수록 회전을 더 느리게 따라감(관성감↑), 0이면 예전처럼 즉시 부착
- `moveTrailFastRotationThreshold`(도/초), `moveTrailShrinkScale`, `moveTrailShrinkLerpSpeed`: 급회전 감지 임계값/축소율/축소-복구 속도

## 변경 파일
- `Assets/Scripts/Effects/TrailRotationFollower.cs` (신규)
- `Assets/Scripts/Effects/EffectPlayer.cs` (수정)
- `Assets/Scripts/Effects/UnitEffects.cs` (수정)
