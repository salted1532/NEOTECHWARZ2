# 0052. 메인기지 자원 이격 거리 4→7칸 + 인스펙터 노출 강화

**날짜:** 2026-07-10

## 요청 내용
> 메인기지 자원에서부터 4칸이라고 했는데 조금더 늘려야겠어 7칸정도 늘려볼게 그리고 이거 내가 직접 수정할수 있게 인스펙터 상으로 보이도록해줘

## 확인
`PlacementSystem.cs`의 `minDistanceFromResource` 필드는 [[0049-building-min-distance-from-resource|0049]]에서 이미 `[SerializeField]`로 선언돼 있어 **원래도 인스펙터에 노출은 되어 있음**(private 필드라도 `[SerializeField]`면 인스펙터에 표시됨). 다만:
- `[Header]`/`[Tooltip]`이 없어서 다른 필드들 사이에 묻혀 눈에 잘 안 띄었을 수 있음.
- [[0050-mainbase-only-resource-distance-rule|0050]]에서 적용 대상을 메인기지로 좁혔는데, 필드 이름/주석은 여전히 "자원(광물/가스)"이라고만 돼 있어 "메인기지에만 적용된다"는 사실이 인스펙터에서 안 보임.

이번 변경은 ① 기본값을 4 → 7로 올리고, ② `[Header]` + `[Tooltip]`을 붙여서 인스펙터에서 이 값이 "메인기지 전용 자원 이격 거리"임을 바로 알아볼 수 있게 한다.

## 설계안

**`PlacementSystem.cs`**
```csharp
// 기존 코드
    // ⭐ 자원(광물/가스) 노드로부터 최소 이격 거리 (그리드 칸 단위, 원형/유클리드 거리)
    [SerializeField]
    private float minDistanceFromResource = 4f;
```
```csharp
// 변경 코드
    [Header("메인기지(커맨드센터) 전용 - 자원 이격 거리")]
    // ⭐ 메인기지(커맨드센터)만 적용받는, 자원(광물/가스) 노드로부터의 최소 이격 거리 (그리드 칸 단위, 원형/유클리드 거리)
    [Tooltip("메인기지(커맨드센터)를 지을 때 광물/가스로부터 최소 이만큼(칸, 원형 거리) 떨어져야 함. 다른 건물에는 적용되지 않음.")]
    [SerializeField]
    private float minDistanceFromResource = 7f;
```

## 참고
- 값 자체(7)와 판정 로직(`IsTooCloseToResource`, 메인기지 전용 적용)은 변경 없음 — 기본값과 인스펙터 표시만 개선.
- 씬/프리팹에 이미 저장된 `PlacementSystem` 컴포넌트가 있다면, Unity는 필드 자체(이름)가 그대로이므로 **직렬화된 값이 있으면 그 값을 그대로 유지**하고 코드의 새 기본값(7)은 "아직 한 번도 저장 안 된 새 컴포넌트"에만 적용됨. 이미 4로 저장돼 있던 씬이라면 인스펙터에서 직접 7로 바꿔줘야 함 (요청하신 대로 인스펙터에서 직접 조정 가능).

## 변경 예정 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`

## 상태
**적용 완료** — 설계안 그대로 `PlacementSystem.cs`에 반영함.
