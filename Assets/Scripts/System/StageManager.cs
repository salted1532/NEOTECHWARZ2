using System;
using UnityEngine;

// 스테이지(미션)의 승리/패배 "결과"만 담당하는 최소 골격.
// 어떤 조건이 목표 달성/패배인지는 이 매니저가 판단하지 않는다 - 각 시스템(적 전멸 판정,
// BaseStructure 파괴 감지 등)에서 조건을 직접 확인한 뒤 ReportVictory()/ReportDefeat()를
// 호출해서 결과만 보고하면, 여기서 상태를 한 번만 고정하고 이벤트로 알린다.
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public enum StageResult { InProgress, Victory, Defeat }

    public StageResult Result { get; private set; } = StageResult.InProgress;

    public event Action OnVictory;
    public event Action OnDefeat;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 임무 목표 달성 시 호출 (예: 적 기지 파괴 등 - 조건 판단은 호출부 책임).
    public void ReportVictory()
    {
        if (Result != StageResult.InProgress) return;
        Result = StageResult.Victory;
        OnVictory?.Invoke();
    }

    // 패배 조건 충족 시 호출 (예: 아군 본진 파괴 등 - 조건 판단은 호출부 책임).
    public void ReportDefeat()
    {
        if (Result != StageResult.InProgress) return;
        Result = StageResult.Defeat;
        OnDefeat?.Invoke();
    }
}
