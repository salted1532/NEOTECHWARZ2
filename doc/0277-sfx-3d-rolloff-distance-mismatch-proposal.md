## 날짜
2026-07-29

## 요청 내용
doc/0276에서 sfxBoost(1.5배)를 적용했는데도 "음성은 크게 잘 들리는데 SFX는 왜 이러냐"는 재문의.

## 조사 내용
`Voice`(`PlayVoice`/`PlayOrderVoice`)는 `spatialBlend: 0f`로 항상 2D 재생 - 거리와 무관하게 항상 최대 볼륨.
`SFX`(`PlaySFX`)는 `spatialBlend: 1f`로 완전 3D 포지셔널 재생인데, `SoundManager.BuildPool()`(`SoundManager.cs:112-124`)이 AudioSource를 만들 때 `minDistance`/`maxDistance`/`rolloffMode`를 전혀 설정하지 않아 유니티 기본값을 그대로 씀:
- `rolloffMode = Logarithmic`
- `minDistance = 1`
- `maxDistance = 500`

Logarithmic 롤오프는 `minDistance` 바로 지나서부터 볼륨이 급격히 떨어지는 커브라, 카메라가 유닛에서 10~40+ 유닛 떨어진 일반적인 RTS 플레이 거리에서는 원본 볼륨의 극히 일부만 남는다. doc/0276에서 추가한 `sfxBoost`는 이 감쇠가 적용되기 **전** 기준 볼륨에 곱해지므로, 감쇠 자체를 상쇄하지 못해 체감 변화가 거의 없었던 것으로 보임.

카메라 거리 추정(`CameraControl.cs`): `minZoom=8`~`maxZoom=35`(카메라 높이), pitch 약 55도 → 화면 중앙 기준 실제 거리 대략 10~45 유닛, 화면 가장자리 유닛은 그보다 더 멀 수 있음.

## 코드 변경 (제안 - 아직 미적용)

### Assets/Scripts/Audio/SoundManager.cs

**기존 코드**
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

**변경 코드 (제안)**
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

            // SFX 풀만: 카메라 거리(대략 10~45유닛)에서 Logarithmic 기본 롤오프(minDistance=1)가
            // 너무 일찍 감쇠를 시작해 거의 안 들리던 문제(doc/0277) - 카메라 거리 안에서는 거의
            // 감쇠 없이 들리고, 그 밖에서 서서히 줄어들도록 Linear 롤오프 + 거리값을 조정한다.
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

호출부(`Awake()`)도 맞춰서 수정:
```csharp
BuildPool(sfxPool, sfxSourcePrefab, sfxPoolSize, "SFXSource", configureSpatialRolloff: true);
BuildPool(voicePool, voiceSourcePrefab, voicePoolSize, "VoiceSource", configureSpatialRolloff: false); // voicePool은 항상 spatialBlend=0(2D)로만 재생되므로 롤오프 설정 자체가 의미 없음 - 굳이 건드릴 필요 없음
```

## 확인 필요
- `minDistance = 15`, `maxDistance = 80` 수치가 적당한지 (카메라 거리 추정치 기반 초안). 더 넓게/좁게 들리길 원하면 조정 가능.
- 아니면 위치감(3D positional) 자체를 포기하고 SFX도 전부 Voice처럼 2D로 재생하는 더 단순한 대안도 있음 - 다만 이 경우 "왼쪽에서 공격받는 소리는 왼쪽 스피커에서" 같은 방향감이 사라짐.

## 요약/남은 작업
코드 변경 아직 미적용 - 수치/방향(3D 유지 vs 2D 전환) 확인되면 반영.

## 변경된 파일
없음 (제안 단계).
