using System.Collections.Generic;
using UnityEngine;

// 건물 프리팹에 BuildingController와 같이 부착하는 이펙트 전담 컴포넌트. 이륙/착륙 이펙트를 담당한다(doc/0105 3.8절).
public class BuildingEffects : MonoBehaviour
{
    [Header("이륙 (비워두면 건물 자신의 위치에서 재생)")]
    [SerializeField] private GameObject takeoffPrefab;
    [SerializeField] private List<Transform> takeoffPoints = new(); // 추진구/랜딩기어 등 여러 지점

    [Header("착륙 (비워두면 건물 자신의 위치에서 재생)")]
    [SerializeField] private GameObject landingPrefab;
    [SerializeField] private List<Transform> landingPoints = new();

    public void PlayTakeoff() => EffectPlayer.SpawnAtPoints(takeoffPrefab, takeoffPoints, transform);
    public void PlayLanding() => EffectPlayer.SpawnAtPoints(landingPrefab, landingPoints, transform);
}
