# 0273 - 피격 경고음(유닛/건물)에 각각 10초 쿨다운 추가

**날짜:** 2026-07-28

## 요청 내용

> 적 유닛이 아군유닛,건물을 공격해서 공격받은 경고음은 쿨타임을 좀 두는게 좋을거 같아 건물,유닛
> 각각 10초정도 쿨타임을 두고 경고음이 울리면 좋을거같아

doc/0271에서 전역 나레이션의 고정 쿨다운을 없애고 "재생 중이 아니면 곧바로 재생"으로 바꿨는데,
자원/인구부족처럼 플레이어 명령에 따라 발생하는 상황과 달리 **피격 경고음은 전투 중 계속 얻어맞으면
매번(클립이 짧으면 거의 끊임없이) 울려서 오히려 시끄럽다**는 문제가 있어, 피격 경고음만 별도로
쿨다운을 다시 두어달라는 요청.

## 코드 변경

`PlayGlobalVoice`에 선택적 `minInterval` 파라미터를 추가해서, **피격 경고음(유닛/건물)에만** 쿨다운을
적용하고 나머지 나레이션(자원/인구부족, 업그레이드 완료)은 doc/0271 방식(재생 중이 아니면 곧바로
재생) 그대로 유지했다.

### `Assets/Scripts/Audio/SoundManager.cs`

```csharp
[SerializeField] private float underAttackWarningCooldown = 10f;
...
private readonly Dictionary<SoundClipSet, float> lastGlobalVoiceStartTime = new Dictionary<SoundClipSet, float>();

public void PlayGlobalVoice(SoundClipSet set, float minInterval = 0f)
{
    if (set == null || !set.HasClips)
        return;

    if (activeGlobalVoiceSources.TryGetValue(set, out AudioSource activeSource)
        && activeSource != null && activeSource.isPlaying)
        return;

    if (minInterval > 0f
        && lastGlobalVoiceStartTime.TryGetValue(set, out float lastStart)
        && Time.time - lastStart < minInterval)
        return;

    AudioSource source = PlayFromPool(voicePool, set, voiceVolume, voiceMuted, spatialBlend: 0f, transform.position);
    if (source != null)
    {
        activeGlobalVoiceSources[set] = source;
        lastGlobalVoiceStartTime[set] = Time.time;
    }
}

public void PlayUnitUnderAttackWarning()
{
    if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.unitUnderAttackWarning, underAttackWarningCooldown);
}

public void PlayBuildingUnderAttackWarning()
{
    if (globalVoiceBank != null) PlayGlobalVoice(globalVoiceBank.buildingUnderAttackWarning, underAttackWarningCooldown);
}
```

`PlayInsufficientResourcesWarning`/`PlayInsufficientPopulationWarning`/`PlayUpgradeCompleteVoice`는
`minInterval`을 안 넘겨서(기본값 0) doc/0271 동작 그대로 유지된다.

`unitUnderAttackWarning`과 `buildingUnderAttackWarning`은 서로 다른 `SoundClipSet` 객체라서
`lastGlobalVoiceStartTime` 딕셔너리에 각각 독립적인 키로 기록된다 - 유닛이 공격받아 쿨다운이 도는
동안에도 건물이 공격받으면 그와 무관하게 즉시 울린다(요청하신 "건물, 유닛 각각" 그대로).

## 요약/영향받는 파일

`Assets/Scripts/Audio/SoundManager.cs` - `underAttackWarningCooldown`(인스펙터 노출, 기본 10초)
필드 추가, `PlayGlobalVoice`에 `minInterval` 파라미터 추가, `PlayUnitUnderAttackWarning`/
`PlayBuildingUnderAttackWarning`만 이 쿨다운을 사용하도록 연결.
