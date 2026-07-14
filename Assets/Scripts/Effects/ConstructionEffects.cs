using System.Collections.Generic;
using UnityEngine;

// BaseStructure(건설중 파운데이션)에 같이 부착하는 이펙트 전담 컴포넌트.
// 건설 진행 중 지속 이펙트와 완공 순간 이펙트를 담당한다(doc/0105 3.8절).
public class ConstructionEffects : MonoBehaviour
{
    [Header("건설 중 지속 (비워두면 건물 중심 1곳)")]
    [SerializeField] private GameObject constructionLoopPrefab;
    [SerializeField] private List<Transform> constructionPoints = new(); // 비계/모서리 등 여러 지점
    private List<GameObject> activeLoops = new();

    [Header("완공 순간")]
    [SerializeField] private GameObject completePrefab;
    [SerializeField] private List<Transform> completePoints = new();

    public void StartLoop()
    {
        if (activeLoops.Count > 0)
            return; // 이미 재생 중

        activeLoops = EffectPlayer.SpawnPersistentAtPoints(constructionLoopPrefab, constructionPoints, transform);
    }

    public void StopLoopAndPlayComplete()
    {
        foreach (GameObject loop in activeLoops)
            if (loop != null) Destroy(loop);
        activeLoops.Clear();

        EffectPlayer.SpawnAtPoints(completePrefab, completePoints, transform);
    }
}
