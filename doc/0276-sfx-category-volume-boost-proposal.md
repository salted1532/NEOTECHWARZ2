## 날짜
2026-07-29

## 요청 내용
SFX 소리가 전반적으로 너무 작아서 키우고 싶다는 요청. 적용 범위 확인 결과 "SkyLancer moveSFX만"이 아니라 **전체 SFX 카테고리**(공격/사망/건설/이동 등 모든 효과음)를 대상으로 함.

## 조사 내용
`SoundManager.cs`의 볼륨 슬라이더는 0~1 범위이고 기본값이 이미 1(최대)이라(`sfxVolume = 1f`, `SetSFXVolume`도 `Mathf.Clamp01`로 1 이상 못 올라감), 지금 구조로는 "슬라이더를 더 올려서" 키울 여지가 없음 - 이미 천장에 닿아있는 상태. 개별 클립마다 `SoundClipSet.volumeScale`(0~1.5)이 있긴 하지만, 모든 SoundBank 에셋(유닛별로 여러 개)마다 일일이 손대야 해서 "전체 SFX 카테고리"라는 요청 범위에는 비효율적.

가장 깔끔한 방법: SFX 카테고리 전용 boost 배율을 `SoundManager`에 추가해서, 사용자가 만지는 볼륨 슬라이더(0~1, 기존 의미 그대로 유지)와 별개로 SFX 재생시에만 곱해준다. BGM/Voice/Master에는 영향 없음.

## 코드 변경 (제안 - 아직 미적용)

### Assets/Scripts/Audio/SoundManager.cs

**기존 코드**
```csharp
    [Header("SFX/Voice 오디오소스 풀 (비워두면 코드에서 자동 생성)")]
    [SerializeField] private AudioSource sfxSourcePrefab;
    [SerializeField] private AudioSource voiceSourcePrefab;
    [SerializeField] private int sfxPoolSize = 16;
    [SerializeField] private int voicePoolSize = 4;
```
```csharp
    // 위치가 있는 3D 효과음 (공격/사망/건설/파괴/채취 등 유닛·건물이 내는 소리).
    public void PlaySFX(SoundClipSet set, Vector3 worldPos) =>
        PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 1f, worldPos);

    // 위치가 없는 2D 효과음 (인터페이스 소리 등).
    public void PlaySFX2D(SoundClipSet set) =>
        PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 0f, transform.position);
```

**변경 코드**
```csharp
    [Header("SFX/Voice 오디오소스 풀 (비워두면 코드에서 자동 생성)")]
    [SerializeField] private AudioSource sfxSourcePrefab;
    [SerializeField] private AudioSource voiceSourcePrefab;
    [SerializeField] private int sfxPoolSize = 16;
    [SerializeField] private int voicePoolSize = 4;

    [Header("SFX 카테고리 전용 부스트 (사용자 볼륨 슬라이더와 별개, 클립 자체 음량이 작아서 체감 볼륨을 전반적으로 키우고 싶을 때)")]
    [SerializeField, Range(1f, 2f)] private float sfxBoost = 1.3f;
```
```csharp
    // 위치가 있는 3D 효과음 (공격/사망/건설/파괴/채취 등 유닛·건물이 내는 소리).
    public void PlaySFX(SoundClipSet set, Vector3 worldPos) =>
        PlayFromPool(sfxPool, set, sfxVolume * sfxBoost, sfxMuted, spatialBlend: 1f, worldPos);

    // 위치가 없는 2D 효과음 (인터페이스 소리 등).
    public void PlaySFX2D(SoundClipSet set) =>
        PlayFromPool(sfxPool, set, sfxVolume * sfxBoost, sfxMuted, spatialBlend: 0f, transform.position);
```

`PlayFromPool` 내부에서 최종 볼륨은 `EffectiveVolume(categoryVolume, muted) * set.volumeScale`로 계산되는데, `EffectiveVolume`이 `masterVolume * categoryVolume`이라 슬라이더가 1일 때 `sfxBoost`만큼 그대로 더 커지고, 슬라이더를 낮추면 그 비율만큼 같이 줄어들어 기존 사용자 조작감은 유지됨.

## 확인 결과
사용자가 배율로 **1.5배(50% 증가)** 선택. 인스펙터에서 나중에 미세조정 가능하도록 `Range(1f, 2f)`는 그대로 유지.

## 코드 변경 (적용 완료)

### Assets/Scripts/Audio/SoundManager.cs

**기존 코드**
```csharp
    [Header("SFX/Voice 오디오소스 풀 (비워두면 코드에서 자동 생성)")]
    [SerializeField] private AudioSource sfxSourcePrefab;
    [SerializeField] private AudioSource voiceSourcePrefab;
    [SerializeField] private int sfxPoolSize = 16;
    [SerializeField] private int voicePoolSize = 4;
```
```csharp
    public void PlaySFX(SoundClipSet set, Vector3 worldPos) =>
        PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 1f, worldPos);

    public void PlaySFX2D(SoundClipSet set) =>
        PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 0f, transform.position);
```

**변경 코드**
```csharp
    [Header("SFX/Voice 오디오소스 풀 (비워두면 코드에서 자동 생성)")]
    [SerializeField] private AudioSource sfxSourcePrefab;
    [SerializeField] private AudioSource voiceSourcePrefab;
    [SerializeField] private int sfxPoolSize = 16;
    [SerializeField] private int voicePoolSize = 4;

    [Header("SFX 카테고리 전용 부스트 (사용자 볼륨 슬라이더와 별개 - 클립 자체 음량이 작아 체감 볼륨을 전반적으로 키우고 싶을 때)")]
    [SerializeField, Range(1f, 2f)] private float sfxBoost = 1.5f;
```
```csharp
    public void PlaySFX(SoundClipSet set, Vector3 worldPos) =>
        PlayFromPool(sfxPool, set, sfxVolume * sfxBoost, sfxMuted, spatialBlend: 1f, worldPos);

    public void PlaySFX2D(SoundClipSet set) =>
        PlayFromPool(sfxPool, set, sfxVolume * sfxBoost, sfxMuted, spatialBlend: 0f, transform.position);
```

## 요약/남은 작업
적용 완료. 인스펙터에서 SoundManager의 `Sfx Boost` 필드로 1~2 사이 미세조정 가능. 참고: 원본 클립이 이미 노멀라이즈된 상태라 1.5배 정도는 대체로 찌그러짐 없이 커진 걸로 들리지만, 특정 클립이 이미 큰 편이었다면 부분적으로 클리핑될 수 있음 - 실제 플레이해서 귀로 확인 필요.

## 변경된 파일
- `Assets/Scripts/Audio/SoundManager.cs`
