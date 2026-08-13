# AllyAttackRange

`Assets/Scripts/FogOfWar/Ally/AllyAttackRange.cs`

## 개요

아군 OC 유닛의 자식 오브젝트에 부착되어 사거리 내 "적대 세력"(외계종족 Spore Brood 등, Tag=`"Enemy"`)을 자동 감지/교전한다. `EnemyAttackRange`를 그대로 상속해서 로직(추격/교전/거리 판정)은 완전히 동일하게 재사용하되, 대상 태그 기본값만 플레이어 진영이 아니라 `"Enemy"` 하나로 바꾼다. `EnemyAttackRange` 컴포넌트 자체를 재설정해서 쓰지 않고 별도 클래스로 두는 이유는, 아군 OC 프리팹에 "EnemyAttackRange"라는 이름이 붙어 있으면 헷갈리기 때문(doc/0448).

건물보다 적 유닛을 항상 우선하는 `PrioritizeUnitTargets`도 `true`로 재정의한다(doc/0565) - 건물을
공격 중이던 아군 OC 유닛도 사거리 안에 적 유닛이 들어오면 즉시 그쪽으로 교전 대상을 바꾼다(플레이어
쪽 `AttackRange.cs`와 동일한 doc/0460 패턴). 적 AI(`EnemyUnitController`)는 이 우선순위를 켜지
않아 기존 동작(건물/유닛 동일 우선순위) 그대로다.

## 구현

```csharp
public class AllyAttackRange : EnemyAttackRange
{
    private void Reset()
    {
        targetTags = new[] { "Enemy" };
    }

    protected override bool PrioritizeUnitTargets => true;
}
```

`Reset()`은 에디터에서 컴포넌트를 처음 추가할 때 호출돼, 새로 만드는 아군 OC Variant 프리팹마다 매번 손으로 Tag 목록을 고칠 필요가 없게 한다.

## 연관 컴포넌트

- **EnemyAttackRange**: 상속 원본 — 실제 감지/교전 로직 전부
- **AllyController**: 이 컴포넌트가 부모로 삼는 `IAttackRangeUnit` 구현체
