using UnityEngine;
using UnityEngine.Serialization;

// 유닛 "종류" 하나에 대응하는 사운드 묶음 에셋. 유닛이 늘어나도 코드 수정 없이 이 에셋을 새로 만들어
// UnitData.soundBank에 연결하기만 하면 된다 (doc/0255, UnitTraitOption과 동일한 "코드 밖에서 유닛별로
// 관리" 철학).
[CreateAssetMenu(menuName = "Sound/Unit Sound Bank")]
public class UnitSoundBankSO : ScriptableObject
{
    [Header("SFX (효과음 - 음성 제외)")]
    [field: SerializeField]
    public SoundClipSet attackSFX { get; private set; }
    [field: SerializeField]
    public SoundClipSet spawnSFX { get; private set; } // 이륙음/엔진음 등, 유닛마다 다르게 채움
    [field: SerializeField]
    public SoundClipSet deathSFX { get; private set; }
    [field: SerializeField]
    public SoundClipSet skillSFX { get; private set; } // 고급유닛 액티브 스킬용
    [field: SerializeField]
    public SoundClipSet gatherSFX { get; private set; } // 워커 전용, 나머지 유닛은 비워둠
    [field: SerializeField]
    public SoundClipSet selectSFX { get; private set; } // 선택 시 대사와 별개로 같이 나는 효과음(삑 소리 등)
    // 이동/공격/순찰 등 유닛에게 내리는 모든 명령 시 대사와 별개로 같이 나는 확인음 (구 moveSFX,
    // doc/0279 - 이동 전용에서 명령 전반으로 범위 확대). FormerlySerializedAs로 기존에 채워둔
    // moveSFX 클립 데이터를 그대로 승계한다.
    [field: SerializeField, FormerlySerializedAs("<moveSFX>k__BackingField")]
    public SoundClipSet orderSFX { get; private set; }

    [Header("Voice (음성)")]
    [field: SerializeField]
    public SoundClipSet selectVoice { get; private set; } // 3~4개 권장
    // 이동/순찰 명령 시 대사 (구 moveVoice, doc/0289 - 순찰 명령까지 범위 확대). FormerlySerializedAs로
    // 기존에 채워둔 moveVoice 클립 데이터를 그대로 승계한다.
    [field: SerializeField, FormerlySerializedAs("<moveVoice>k__BackingField")]
    public SoundClipSet orderVoice { get; private set; } // 3~4개 권장
    [field: SerializeField]
    public SoundClipSet attackOrderVoice { get; private set; } // 1~2개 권장
    [field: SerializeField]
    public SoundClipSet spawnVoice { get; private set; }
    [field: SerializeField]
    public SoundClipSet deathVoice { get; private set; }

    [Header("Voice (워커 전용 - 다른 유닛은 비워둠)")]
    [field: SerializeField]
    public SoundClipSet buildCompleteVoice { get; private set; }
    [field: SerializeField]
    public SoundClipSet buildFailVoice { get; private set; }
}
