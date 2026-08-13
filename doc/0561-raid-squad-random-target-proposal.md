# 0561 - 점령 별동대 목표를 우선순위 없이 무작위 선정

## 날짜
2026-08-13

## 요청 내용
"점령 별동대는 점령지 리스트에서 랜덤으로 하나 정해서 점령하러 가도록 하면돼"

→ 현재 `EnemyAIDirector.PickRaidTarget()`은 "Ally(플레이어) 소유 우선, 없으면 Neutral"(doc/0532) 순서로
고정 우선순위를 쓰는데, 이걸 우선순위 없이 후보 중 무작위로 하나 고르도록 바꾼다.

```csharp
// 현재 (Ally가 뺏어간 곳을 Neutral보다 우선, doc/0532)
private CaptureSystem PickRaidTarget()
{
    CaptureSystem allyOwned = raidTargets.Find(t => t != null && t.CurrentOwner == CaptureOwner.Ally);
    if (allyOwned != null)
        return allyOwned;

    return raidTargets.Find(t => t != null && t.CurrentOwner == CaptureOwner.Neutral);
}
```

## 설계안
```csharp
// 점령지 후보(자신 소유가 아닌 곳) 중 우선순위 없이 무작위로 하나(doc/0561 - 기존 Ally 우선 → Neutral
// 순 우선순위를 없앰).
private CaptureSystem PickRaidTarget()
{
    List<CaptureSystem> candidates = raidTargets.FindAll(t => t != null && t.CurrentOwner != CaptureOwner.Enemy);
    return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
}
```

이미 자기 진영(`CaptureOwner.Enemy`) 소유인 점령지는 후보에서 계속 제외한다 - 별동대를 보내도 이미
점령된 곳이라 아무 효과가 없기 때문(요청에 명시되진 않았으나 기존 로직도 이 전제였음). Ally 소유든
Neutral이든 구분 없이 하나의 무작위 풀에서 뽑는다.

## 확인 결과
사용자에게 물어본 결과 "자신 소유지는 제외" 선택 - 위 설계안대로 그대로 적용.

## 변경 상세
- `Assets/Scripts/System/EnemyAIDirector.cs` - `PickRaidTarget()` 본문을 위 설계안대로 교체(Ally 우선
  → Neutral 순 우선순위 로직 삭제, `FindAll` + `Random.Range` 무작위 선택으로 대체).
- `Docs/EnemyAIDirector.md` - "점령지 탈환 별동대" 절 설명과 메소드 표에 `PickRaidTarget()` 행 갱신.

## 컴파일 확인
`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
`WarningCount: 40`(기존 베이스라인과 동일 - 새 경고 없음).
