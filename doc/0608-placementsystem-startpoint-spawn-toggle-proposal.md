# 0608. PlacementSystem StartPoint 메인기지 스폰 온오프 필드 (제안)

**날짜:** 2026-08-18

## 요청 내용
> PlacementSystem에서 StartPoint의 MainBase 스폰에 관해서 온오프 할 수 있도록 bool 형태로 인스펙터로 필드를 빼줘. 유닛 조종만으로 구성된 미션에선 StartPoint가 필요가 없어서 메인기지가 스폰할 필요가 없어.

## 조사 결과 (현재 코드 상태)
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`의 `Start()`가 매번 `SpawnStartingMainBase()`를 호출한다 (line 60-66).
- `SpawnStartingMainBase()`는 이미 `startPoint == null`이면 아무 일도 하지 않고 조용히 리턴한다 ([[0055-startpoint-mainbase-spawn]] 참고, line 72-73).
- 즉 지금도 `startPoint`를 씬에서 비워두면 메인기지가 스폰되지 않는다. 다만 이번 요청은 "필드를 비워서 우회"가 아니라, **명시적인 온오프 스위치**를 인스펙터에 두고 싶다는 것으로 이해했다 — `startPoint`는 Sub_Mission 씬들의 `Mission_Sub*` 프리팹에서 리프트 착륙 위치 등 다른 용도로도 참조될 수 있으니, "연결 여부"와 "스폰 여부"를 분리해두는 편이 향후 더 안전함.

## 설계안

### `Assets/Scripts/BuildSystem/PlacementSystem.cs`

**필드 추가** (`startPoint` 바로 아래):
```csharp
// 기존 코드
    [Header("시작 위치")]
    [Tooltip("게임 시작 시 메인기지(커맨드센터)를 그리드에 맞춰 즉시 생성할 위치. 빈 오브젝트를 씬에 배치해서 연결.")]
    [SerializeField] private GameObject startPoint;
```
```csharp
// 변경 코드
    [Header("시작 위치")]
    [Tooltip("게임 시작 시 메인기지(커맨드센터)를 그리드에 맞춰 즉시 생성할 위치. 빈 오브젝트를 씬에 배치해서 연결.")]
    [SerializeField] private GameObject startPoint;
    [Tooltip("게임 시작 시 startPoint 위치에 메인기지를 자동 스폰할지 여부. 유닛 조종만 있는 미션 등에서는 꺼둔다.")]
    [SerializeField] private bool spawnStartingMainBase = true;
```

**`SpawnStartingMainBase()` 가드에 조건 추가**:
```csharp
// 기존 코드
    private void SpawnStartingMainBase()
    {
        if (startPoint == null)
            return;
```
```csharp
// 변경 코드
    private void SpawnStartingMainBase()
    {
        if (!spawnStartingMainBase || startPoint == null)
            return;
```

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **기본값 `true`**: 기존 씬들(`MissionSelect` 등 이미 `startPoint`가 연결된 프리팹)의 동작이 바뀌지 않도록 기본값을 켜진 상태로 둔다. 유닛 조종 전용 Sub_Mission 씬에서만 인스펙터에서 체크 해제하면 됨.
- **`startPoint` 필드 자체는 유지**: 다른 로직(리프트 이동 등)이 `startPoint.transform.position`을 참조할 가능성을 배제하지 않기 위해 필드는 그대로 두고, 스폰 동작만 별도 bool로 분리.
- **변경 범위**: 필드 1개 추가 + 가드 조건 1줄 수정, 총 2곳.

## 변경 예정 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음). 컴파일 확인 완료 (에러 0).

적용 후 유니티 에디터에서 필요 시: 유닛 조종만 있는 미션(Sub_Mission 등) 프리팹의 `Placement System` 컴포넌트에서 `Spawn Starting Main Base` 체크 해제.
