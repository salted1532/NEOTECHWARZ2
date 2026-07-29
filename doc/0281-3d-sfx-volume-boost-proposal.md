## 날짜
2026-07-29

## 요청 내용
"3D 방식으로 작동하는 sfx들의 소리를 키워줘" - 3D 포지셔널로 재생되는 SFX만 콕 집어 볼륨을 키워달라는 요청.

## 조사 내용
현재 `SoundManager.cs`에서 `PlaySFX`(3D, `spatialBlend=1`)와 `PlaySFX2D`(2D, `spatialBlend=0`)는 동일한 `sfxBoost`(doc/0276, 현재 1.5배) 배율을 그대로 공유한다.

- **3D로 재생되는 것들** (`PlaySFX`, 거리 감쇠 있음 - doc/0278에서 의도적으로 근접해야 들리게 설계): `UnitAudio.PlayAttackSFX`(공격), `PlaySpawnSound`(스폰), `PlayGatherSFX`(채취), `PlaySkillSFX`(스킬), `HandleDeath`(사망), `BuildingAudio.PlayConstructLoop`/`PlayConstructComplete`(건설), `HandleDestroyed`(파괴)
- **2D로 재생되는 것들** (`PlaySFX2D`, 거리 무관 항상 최대 볼륨): `UnitAudio.PlaySelectSFX`(선택), `PlayOrderSFX`(명령), `SoundManager.PlayUIClick`

지금 구조로는 `sfxBoost`를 올리면 3D/2D 둘 다 같이 커져서, "3D만" 키우고 싶다는 요청에 정확히 맞추려면 3D 전용 배율을 별도로 추가해야 함.

## 코드 변경 (제안 - 아직 미적용)

### Assets/Scripts/Audio/SoundManager.cs

**기존 코드**
```csharp
    [Header("SFX 카테고리 전용 부스트 (사용자 볼륨 슬라이더와 별개 - 클립 자체 음량이 작아 체감 볼륨을 전반적으로 키우고 싶을 때)")]
    [SerializeField, Range(1f, 2f)] private float sfxBoost = 1.5f;
```
```csharp
    // 위치가 있는 3D 효과음 (공격/사망/건설/파괴/채취 등 유닛·건물이 내는 소리).
    public void PlaySFX(SoundClipSet set, Vector3 worldPos) =>
        PlayFromPool(sfxPool, set, sfxVolume * sfxBoost, sfxMuted, spatialBlend: 1f, worldPos);

    // 위치가 없는 2D 효과음 (인터페이스 소리 등).
    public void PlaySFX2D(SoundClipSet set) =>
        PlayFromPool(sfxPool, set, sfxVolume * sfxBoost, sfxMuted, spatialBlend: 0f, transform.position);
```

**변경 코드 (제안)**
```csharp
    [Header("SFX 카테고리 전용 부스트 (사용자 볼륨 슬라이더와 별개 - 클립 자체 음량이 작아 체감 볼륨을 전반적으로 키우고 싶을 때)")]
    [SerializeField, Range(1f, 2f)] private float sfxBoost = 1.5f;

    [Header("3D 포지셔널 SFX 전용 추가 부스트 (공격/사망/건설/파괴/채취 등 - 위 sfxBoost 위에 곱해짐, 2D SFX/선택/명령음에는 영향 없음)")]
    [SerializeField, Range(1f, 2f)] private float sfx3DBoost = 1.3f;
```
```csharp
    // 위치가 있는 3D 효과음 (공격/사망/건설/파괴/채취 등 유닛·건물이 내는 소리).
    public void PlaySFX(SoundClipSet set, Vector3 worldPos) =>
        PlayFromPool(sfxPool, set, sfxVolume * sfxBoost * sfx3DBoost, sfxMuted, spatialBlend: 1f, worldPos);

    // 위치가 없는 2D 효과음 (인터페이스 소리 등) - sfx3DBoost 영향 없음.
    public void PlaySFX2D(SoundClipSet set) =>
        PlayFromPool(sfxPool, set, sfxVolume * sfxBoost, sfxMuted, spatialBlend: 0f, transform.position);
```

## 확인 결과
사용자가 배율로 **1.5배** 선택. `sfxBoost`(1.5배)와 곱해져 3D SFX 최종 배율은 **2.25배**. 인스펙터에서 나중에 미세조정 가능하도록 `Range(1f, 2f)`는 그대로 유지.

## 코드 변경 (적용 완료)
위 "제안" 코드 그대로 적용 (`sfx3DBoost` 기본값 1.5f).

## 요약/남은 작업
적용 완료. 인스펙터에서 SoundManager의 `Sfx 3D Boost` 필드로 1~2 사이 미세조정 가능. 원본 클립 음량에 따라 일부 클리핑 가능성 있으니 실제 플레이해서 귀로 확인 필요.

## 변경된 파일
- `Assets/Scripts/Audio/SoundManager.cs`
