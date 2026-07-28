## 날짜
2026-07-29

## 요청 내용
doc/0277 제안(SFX 3D 롤오프 거리 조정 vs 전부 2D 전환) 중 사용자가 절충안 선택: "이동, 선택은 2D로 수정해주고 공격이나 사망, Gather등은 가까이 있을때만 들려야하니깐 3D로 남겨줘."

## 조사 내용
doc/0275~0277에서 이어진 결론: Voice는 항상 2D(`spatialBlend=0`)라 거리 무관 최대 볼륨, SFX는 전부 3D(`spatialBlend=1`)인데 `SoundManager.BuildPool()`이 롤오프를 전혀 설정 안 해 유니티 기본값(Logarithmic, minDistance=1)을 썼다 - 카메라 거리(대략 10~45유닛)에서 지나치게 일찍 감쇠되어 거의 안 들림.

사용자 의도: "명령 확인음"(이동/선택 삑 소리)은 카메라 거리와 무관하게 항상 또렷하게 들려야 하고, "전투/근접 효과음"(공격/사망/채취 등)은 실제로 가까이 있을 때만 들리는 게 맞는 디자인 - 완전히 안 들리는 게 아니라 카메라를 바짝 당겼을 때는 들려야 함.

## 코드 변경 (적용 완료)

### Assets/Scripts/Audio/SoundManager.cs
SFX 풀에만 Linear 롤오프 + 게임 카메라 스케일에 맞춘 거리값 적용 (voicePool은 항상 2D라 무의미하므로 건드리지 않음).

**기존 코드**
```csharp
        LoadVolumePrefs();
        BuildPool(sfxPool, sfxSourcePrefab, sfxPoolSize, "SFXSource");
        BuildPool(voicePool, voiceSourcePrefab, voicePoolSize, "VoiceSource");
```
```csharp
    private void BuildPool(List<PooledSource> pool, AudioSource prefab, int size, string namePrefix)
    {
        for (int i = 0; i < size; ++i)
        {
            AudioSource source = prefab != null
                ? Instantiate(prefab, transform)
                : new GameObject($"{namePrefix}_{i}").AddComponent<AudioSource>();

            source.transform.SetParent(transform);
            source.playOnAwake = false;
            pool.Add(new PooledSource { Source = source });
        }
    }
```

**변경 코드**
```csharp
        LoadVolumePrefs();
        BuildPool(sfxPool, sfxSourcePrefab, sfxPoolSize, "SFXSource", configureSpatialRolloff: true);
        BuildPool(voicePool, voiceSourcePrefab, voicePoolSize, "VoiceSource", configureSpatialRolloff: false); // voicePool은 항상 spatialBlend=0(2D)로만 재생되므로 롤오프 설정이 무의미하다
```
```csharp
    private void BuildPool(List<PooledSource> pool, AudioSource prefab, int size, string namePrefix, bool configureSpatialRolloff)
    {
        for (int i = 0; i < size; ++i)
        {
            AudioSource source = prefab != null
                ? Instantiate(prefab, transform)
                : new GameObject($"{namePrefix}_{i}").AddComponent<AudioSource>();

            source.transform.SetParent(transform);
            source.playOnAwake = false;

            // SFX 풀만: 유니티 기본 롤오프(Logarithmic, minDistance=1)는 카메라 거리(대략 10~45유닛,
            // doc/0277)에서 너무 일찍 감쇠를 시작해 근접 전투에서도 거의 안 들렸다. 실제로 카메라를
            // 바짝 당겨야(가까이 있을 때만) 들리도록 거리값을 게임 카메라 스케일에 맞춘다.
            if (configureSpatialRolloff)
            {
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 15f;
                source.maxDistance = 80f;
            }

            pool.Add(new PooledSource { Source = source });
        }
    }
```

### Assets/Scripts/Audio/UnitAudio.cs
`PlaySelectSFX`/`PlayMoveSFX`만 3D(`PlaySFX`)에서 2D(`PlaySFX2D`)로 전환. 나머지(`PlayAttackSFX`/`PlaySpawnSound`의 spawnSFX/`PlayGatherSFX`/`PlaySkillSFX`/`HandleDeath`의 deathSFX)는 3D 그대로 유지 - 위 롤오프 조정의 혜택을 그대로 받음.

**기존 코드**
```csharp
    public void PlaySelectSFX()
    {
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.selectSFX, transform.position);
    }

    public void PlayMoveSFX()
    {
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.moveSFX, transform.position);
    }
```

**변경 코드**
```csharp
    public void PlaySelectSFX()
    {
        if (bank != null)
            SoundManager.Instance?.PlaySFX2D(bank.selectSFX);
    }

    public void PlayMoveSFX()
    {
        if (bank != null)
            SoundManager.Instance?.PlaySFX2D(bank.moveSFX);
    }
```

(`BuildingAudio.cs`의 `constructLoopSFX`/`constructCompleteSFX`/`destroySFX`는 요청 범위 밖이라 3D 그대로 유지 - 필요하면 별도로 확인.)

## 요약/남은 작업
적용 완료. `minDistance=15`/`maxDistance=80` 수치는 카메라 줌 범위(`CameraControl.minZoom=8`~`maxZoom=35`) 추정 기반 초안이라, 실제 플레이해서 "가까이 있을 때만 들린다"는 체감이 원하는 정도인지 확인 후 필요하면 조정.

## 변경된 파일
- `Assets/Scripts/Audio/SoundManager.cs`
- `Assets/Scripts/Audio/UnitAudio.cs`
