using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 비콘에 (등록되지 않은) 아군 유닛이 닿으면, 그 비콘에 미리 등록해둔 조종불가(isRescueUnit) 유닛들을
// 한꺼번에 조종 가능하게 전환한다. Mission3 "생존자 구조" 메커니즘(Stage3Objectives, doc/0458/0459/0461)을
// 여러 비콘에 대해 동시에 지원하도록 일반화한 것 - 실제 유닛 상태 전환(시야 복원/마커/인구수)은 전부
// UnitController.Rescue()가 담당하므로 여기서는 "비콘 접촉 판정 → Rescue() 호출"만 담당한다 (doc/0609).
public class RescueBeaconSystem : MonoBehaviour
{
    [Serializable]
    private class BeaconEntry
    {
        public Collider beacon; // 비콘의 트리거 콜라이더 - 직접 연결
        public List<UnitController> units; // 이 비콘에 도착하면 조종 가능해질 유닛들 - 직접 연결
        [HideInInspector] public bool triggered;
    }

    [SerializeField] private List<BeaconEntry> beacons;
    [SerializeField] private float rescueStaggerInterval = 0.1f; // Stage3Objectives와 동일한 목적(doc/0466)

    private RTSUnitController rtsController;

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    private void Update()
    {
        if (rtsController == null)
            return;

        foreach (BeaconEntry entry in beacons)
        {
            if (entry.triggered || entry.beacon == null)
                continue;

            if (IsAnyUnitTouchingBeacon(entry))
            {
                entry.triggered = true;
                StartCoroutine(RescueSequence(entry));
            }
        }
    }

    private IEnumerator RescueSequence(BeaconEntry entry)
    {
        foreach (UnitController unit in entry.units)
        {
            unit?.Rescue();
            yield return new WaitForSeconds(rescueStaggerInterval);
        }
    }

    // 그 비콘 자신의 리스트에 속한 유닛은 판정에서 제외한다 - isRescueUnit 유닛도 UnitList엔 등록돼
    // 있어서(doc/0459), 처음부터 비콘 근처에 배치돼 있으면 자기 자신만으로 즉시 완료돼버리는 문제를
    // 막기 위함 (Stage3Objectives.IsAnyUnitTouchingBeacon과 동일한 이유).
    private bool IsAnyUnitTouchingBeacon(BeaconEntry entry)
    {
        foreach (UnitController unit in rtsController.UnitList)
        {
            if (unit == null || entry.units.Contains(unit))
                continue;

            if (unit.IsTouching(entry.beacon))
                return true;
        }
        return false;
    }
}
