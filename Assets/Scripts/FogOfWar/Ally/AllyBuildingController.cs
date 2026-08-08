// 아군 OC 건물 컨트롤러. EnemyBuildingController는 애초에 이동/전투 AI가 전혀 없는 순수 껍데기라(체력/
// 선택/사망 처리만) "AI를 따로 조종"할 대상 자체가 없다 - 그래서 AllyController(유닛)와 달리 로직을
// 복제하지 않고 그대로 상속한다. 이름만 다른 타입으로 두는 이유는 AllyAttackRange(doc/0448)와 동일:
// 아군 OC 프리팹에 "EnemyBuildingController"라는 이름이 붙어 있으면 헷갈리기 때문 (doc/0452).
public class AllyBuildingController : EnemyBuildingController
{
}
