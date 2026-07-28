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
    public SoundClipSet underAttackWarning { get; private set; }
    [field: SerializeField]
    public SoundClipSet upgradeComplete { get; private set; }
}
