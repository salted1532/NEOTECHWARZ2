using FischlWorks_FogWar;
using UnityEngine;

// 유닛/건물에 부착해 csFogWar에 자신을 시야 소스(FogRevealer)로 등록/해제하는 어댑터.
// UnitController/BuildingController는 전혀 건드리지 않고, 같은 오브젝트에 이 컴포넌트만 추가하면 된다.
public class FogRevealerAgent : MonoBehaviour
{
    [Header("시야 범위 (월드 단위, csFogWar가 내부에서 unitScale로 나눠 셀 단위로 변환)")]
    [SerializeField] private int sightRange = 10;
    [SerializeField] private bool updateOnlyOnMove = true;

    private csFogWar fogWar;
    private csFogWar.FogRevealer fogRevealer;

    private void Start()
    {
        // 이름 문자열(GameObject.Find)에 의존하지 않고, 이 프로젝트의 다른 컨트롤러들과 동일하게
        // FindFirstObjectByType으로 찾는다(BuildingController/EnemyController와 동일 패턴).
        fogWar = FindFirstObjectByType<csFogWar>();

        if (fogWar == null)
        {
            Debug.LogWarning($"{name}: csFogWar를 씬에서 찾지 못해 시야를 등록하지 못했습니다.", this);
            return;
        }

        fogRevealer = new csFogWar.FogRevealer(transform, sightRange, updateOnlyOnMove);
        fogWar.AddFogRevealer(fogRevealer);
    }

    private void OnDestroy()
    {
        if (fogWar == null || fogRevealer == null)
            return;

        // AddFogRevealer/RemoveFogRevealer는 리스트 인덱스 기반이라, 다른 유닛이 먼저 죽어서
        // 목록이 앞으로 당겨지면 등록 당시 캐싱해둔 인덱스가 어긋난다. 그래서 인덱스를 미리 저장해두지
        // 않고, 제거 직전에 내 FogRevealer 인스턴스의 "현재" 인덱스를 다시 찾아서 그 값으로 제거한다.
        int currentIndex = fogWar._FogRevealers.IndexOf(fogRevealer);

        if (currentIndex >= 0)
            fogWar.RemoveFogRevealer(currentIndex);
    }
}
