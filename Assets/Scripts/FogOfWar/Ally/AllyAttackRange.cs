using UnityEngine;

// 아군 OC(플레이어에게 적대적이지 않은 진영) 유닛의 자식 오브젝트에 부착되어 사거리 내 "적대 세력"
// (외계종족 Spore Brood 등, Tag="Enemy")을 자동 감지/교전한다. EnemyAttackRange를 그대로 상속해서
// 로직(추격/교전/거리 판정)은 완전히 동일하게 재사용하되, 대상 Tag 기본값만 플레이어 진영이 아니라
// "Enemy" 하나로 바꾼다 - EnemyAttackRange 컴포넌트 자체를 재설정해서 쓰지 않고 별도 클래스로 두는
// 이유는, 아군 OC 프리팹에 "EnemyAttackRange"라는 이름이 붙어 있으면(설정값과 무관하게) 헷갈리기
// 때문이다(doc/0448).
public class AllyAttackRange : EnemyAttackRange
{
    // 컴포넌트를 처음 추가할 때(에디터에서 Add Component) 기본값을 곧바로 "외계종족만" 감지하도록
    // 맞춰준다 - 새로 만드는 아군 OC Variant 프리팹마다 매번 손으로 Tag 목록을 고칠 필요가 없다.
    private void Reset()
    {
        targetTags = new[] { "Enemy" };
    }
}
