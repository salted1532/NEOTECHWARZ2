## 날짜
2026-07-29

## 요청 내용
3D SFX(공격/사망/스킬/채취/건설/파괴 등)의 "거리별 감소가 좀 더 컸으면 좋겠다" - 즉 카메라 거리에 따라 소리가 더 강하게/더 빨리 작아지길 원함.

## 조사 내용
현재 `SoundManager.BuildPool()`(`Assets/Scripts/Audio/SoundManager.cs`)에서 SFX 풀에 설정된 값(doc/0277~0278):
- `rolloffMode = Linear`
- `minDistance = 15` (이 거리 안에서는 감쇠 없이 최대 볼륨)
- `maxDistance = 80` (이 거리를 넘으면 완전 무음)

`Linear` 롤오프는 `minDistance`~`maxDistance` 구간에서 거리에 정비례해 볼륨이 줄어드는 방식이라, "감소가 더 크게" 느껴지려면 이 구간을 좁히면 됨 - 같은 거리를 이동해도 볼륨이 더 빨리 떨어짐.

카메라 거리 추정(doc/0277, `CameraControl.cs` 기준): 화면 중앙 기준 대략 10~45유닛 범위(줌 아웃하면 그 이상). 지금 `15~80` 구간은 이 범위보다 넓어서, 일반적인 줌 상태에서는 아직 크게 안 줄어든 상태로 들림.

## 코드 변경 (제안 - 아직 미적용)

### Assets/Scripts/Audio/SoundManager.cs
```csharp
if (configureSpatialRolloff)
{
    source.rolloffMode = AudioRolloffMode.Linear;
    source.minDistance = ?; // 현재 15
    source.maxDistance = ?; // 현재 80
}
```

구간을 좁혀서(`minDistance`를 낮추고 `maxDistance`를 낮추면) 카메라 거리 변화에 훨씬 민감하게 반응 - 가까이 있으면 크게, 조금만 멀어져도 빠르게 작아짐.

## 확인 결과
사용자가 "안 A: 10~45" 선택.

## 코드 변경 (적용 완료)

### Assets/Scripts/Audio/SoundManager.cs
`BuildPool()`의 `configureSpatialRolloff` 분기에서 `minDistance`/`maxDistance`를 `15`/`80` → `10`/`45`로 변경.

## 요약/남은 작업
적용 완료. 실제 플레이해서 줌 레벨별로 소리가 원하는 만큼 빨리 작아지는지 확인 필요 - 여전히 완만하면 구간을 더 좁히면 됨(안 B: 6~30 등).

## 변경된 파일
- `Assets/Scripts/Audio/SoundManager.cs`
