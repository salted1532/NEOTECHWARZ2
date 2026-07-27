# 0238 - 사거리 확인용 씬 뷰 디버그 선

## 요청

적/아군 사거리(`UnitRange`) 계산이 서로 다른 것 같다는 질문에, 실제로는 감지용 트리거 콜라이더 반경과
`UnitRange` 값이 서로 독립적인 값이라 프리팹마다 격차가 다르게 나 있던 것이라고 설명함(Sharpshooter도
콜라이더 10 vs 사거리 20으로 어긋나 있었음). 이어서 유닛 정면으로 `UnitRange` 길이만큼 뻗어나가는 선을
씬 뷰에서만 보이게 그려서 사거리를 눈으로 바로 확인하고 싶다는 요청.

## 구현

`AttackRange.cs`(아군), `EnemyAttackRange.cs`(적) 둘 다에 `OnDrawGizmos()`를 추가:

```csharp
private void OnDrawGizmos()
{
    Gizmos.color = Color.cyan; // 적은 Color.red
    Gizmos.DrawLine(transform.position, transform.position + transform.forward * UnitRange);
}
```

`Gizmos`는 유니티 에디터의 씬 뷰(및 게임 뷰에서 Gizmos 토글 켰을 때)에서만 그려지고 빌드에는 전혀
포함되지 않아서 "씬에서만 보이는 선" 요구사항을 그대로 만족함. `OnDrawGizmosSelected`가 아니라
`OnDrawGizmos`를 써서 선택 여부와 상관없이 항상 그려지도록 했음 - 여러 유닛의 사거리를 한 번에 비교하기
편하도록. 아군은 청록색, 적은 빨간색으로 구분.

## 변경 파일

- `Assets/Scripts/Unit/AttackRange.cs`
- `Assets/Scripts/Enemy/EnemyAttackRange.cs`
