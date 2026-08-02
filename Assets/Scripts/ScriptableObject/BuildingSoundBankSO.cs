using UnityEngine;

// 건물 "종류" 하나에 대응하는 사운드 묶음 에셋 (UnitSoundBankSO와 동일한 목적, doc/0255).
[CreateAssetMenu(menuName = "Sound/Building Sound Bank")]
public class BuildingSoundBankSO : ScriptableObject
{
    [field: SerializeField]
    public SoundClipSet constructLoopSFX { get; private set; }
    [field: SerializeField]
    public SoundClipSet constructCompleteSFX { get; private set; }
    [field: SerializeField]
    public SoundClipSet destroySFX { get; private set; }
    [field: SerializeField]
    public SoundClipSet takeoffSFX { get; private set; } // 리프트 이륙 시
    [field: SerializeField]
    public SoundClipSet landingSFX { get; private set; } // 착륙 완료 시
    [field: SerializeField]
    public SoundClipSet selectVoice { get; private set; } // "건물 음성" - 선택 시 재생
}
