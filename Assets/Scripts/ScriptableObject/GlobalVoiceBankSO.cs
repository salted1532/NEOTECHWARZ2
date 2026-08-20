using UnityEngine;

// 특정 유닛/건물에 묶이지 않는 게임 나레이션 음성 (자원/인구 부족, 화면 밖 피격 경고, 업그레이드 완료 등).
// SoundManager가 이 에셋 1개를 참조해서 재생한다 (doc/0255).
[CreateAssetMenu(menuName = "Sound/Global Voice Bank")]
public class GlobalVoiceBankSO : ScriptableObject
{
    [field: SerializeField]
    public SoundClipSet insufficientResources { get; private set; }
    [field: SerializeField]
    public SoundClipSet insufficientPopulation { get; private set; }
    [field: SerializeField]
    public SoundClipSet unitUnderAttackWarning { get; private set; } // 화면 밖에서 아군 유닛이 공격받았을 때
    [field: SerializeField]
    public SoundClipSet buildingUnderAttackWarning { get; private set; } // 화면 밖에서 아군 건물이 공격받았을 때
    [field: SerializeField]
    public SoundClipSet upgradeComplete { get; private set; }
    [field: SerializeField]
    public SoundClipSet missionSuccess { get; private set; } // 임무(스테이지) 목표 달성 시(doc/0464)
    [field: SerializeField]
    public SoundClipSet actionFailed { get; private set; } // 행동 실패 공통 SFX(doc/0524) - 자원/인구부족, 건설실패, 이륙불가 등 ShowWarning()이 뜨는 모든 경우에 공통 재생
    [field: SerializeField]
    public SoundClipSet territoryCaptured { get; private set; } // 거점 점령 완료 시(doc/0642) - 아군이 거점을 점령했을 때만 재생
    [field: SerializeField]
    public SoundClipSet victoryScreen { get; private set; } // 승리화면이 실제로 표시되는 순간(doc/0645)
}
