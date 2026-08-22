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
    [field: SerializeField]
    public SoundClipSet selectSFX { get; private set; } // 선택 대사와 별개로 같이 나는 확인음(삑 소리 등) - UnitSoundBankSO.selectSFX와 동일 패턴 (doc/0660)
    [field: SerializeField]
    public SoundClipSet placementSFX { get; private set; } // 건설모드 배치 클릭으로 프리뷰(고스트)가 그 자리에 고정되는 순간 (doc/0646)
    [field: SerializeField]
    public SoundClipSet repairTickSFX { get; private set; } // 일꾼이 수리 중일 때 회복 틱마다 재생 (doc/0658)
}
