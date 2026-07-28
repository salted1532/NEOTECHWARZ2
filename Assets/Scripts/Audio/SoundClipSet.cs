using System.Collections.Generic;
using UnityEngine;

// 랜덤 재생용 오디오 클립 묶음 - "선택 시 대사 3~4개", "공격명령 대사 1~2개"처럼 카테고리 하나에
// 해당하는 클립 여러 개를 담아두고, 재생할 때마다 그중 하나를 무작위로 고른다 (doc/0255).
[System.Serializable]
public class SoundClipSet
{
    [field: SerializeField]
    public List<AudioClip> clips { get; private set; } = new List<AudioClip>();

    // 이 카테고리만 살짝 더 크게/작게 재생하고 싶을 때 (최종 볼륨 = 카테고리 볼륨 슬라이더 값 × 이 값)
    [field: SerializeField, Range(0f, 1.5f)]
    public float volumeScale { get; private set; } = 1f;

    // 같은 클립이 반복 재생돼도 덜 기계적으로 들리게 하는 피치 변동 폭 (0이면 변동 없음)
    [field: SerializeField, Range(0f, 0.3f)]
    public float pitchVariance { get; private set; } = 0f;

    public bool HasClips => clips != null && clips.Count > 0;

    // clips가 비어있으면 null 반환 - 호출부(SoundManager)는 null이면 재생을 그냥 스킵한다.
    public AudioClip GetRandomClip()
    {
        if (!HasClips)
            return null;

        return clips[Random.Range(0, clips.Count)];
    }

    public float GetRandomPitch() => 1f + Random.Range(-pitchVariance, pitchVariance);
}
