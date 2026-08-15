# 0583. 자원부족 실패 시 Action Failed SFX가 안 들리는 문제 - 제안

**날짜:** 2026-08-16

## 요청 내용

> 자원 부족에서도 실패시 사운드 나오도록 해줘

후속 확인: 유닛/건물을 자원부족으로 생산·건설하지 못할 때 "자원부족" 내레이션(목소리)은 들리는데,
doc/0524에서 추가한 공통 "Action Failed" SFX(`Access_denied.mp3`)는 안 들린다는 문제였다. 사용자
확인으로는 인구수부족/건설 위치 실패에서는 내레이션과 Action Failed SFX가 같이 잘 들린다고 함.

## 조사 내용 (1차 - 기각됨)

처음엔 "볼륨이 낮아서(≈0.203) 내레이션 목소리에 묻힌다"고 추정했으나, 사용자가 "인구수부족/건설
실패에서는 내레이션과 같이 잘 들린다"고 확인하면서 기각됨 - 볼륨 문제라면 그 경우들도 똑같이 안
들려야 하는데 아니었음.

## 조사 내용 (2차 - 실제 원인)

Unity 에디터 Play Mode에서 `PlayInsufficientResourcesWarning()` + `ShowWarning()` 시퀀스와
`PlayInsufficientPopulationWarning()` + `ShowWarning()` 시퀀스를 직접 재현해 `SoundManager`의
`voicePool`/`activeGlobalVoiceSources` 상태를 리플렉션으로 찍어본 결과, 두 시퀀스 모두 `actionFailed`
(`Access_denied`) 클립이 최종적으로 재생되지 않는 것을 확인했다 (풀 슬롯이 다른 카테고리에 의해 이미
"재생 중"으로 점유된 stale 상태였기 때문). 실제 게임에서 "자원부족에서만 유독 안 들림"으로 보이는
것은, 그 순간 voicePool의 어떤 슬롯이 stale 상태였는지에 따라 갈리는 **타이밍 의존적 버그**였다.

### 근본 원인

`SoundManager.cs`의 `PlayGlobalVoice()`(266~287번 줄)는 카테고리(`SoundClipSet`)별 중복 재생 방지를
위해 `activeGlobalVoiceSources[set] = 재생에 쓴 AudioSource`를 기억해두고, 다음 요청 때
"그 AudioSource가 지금 재생 중이면 건너뛴다"고 판단한다:

```csharp
if (activeGlobalVoiceSources.TryGetValue(set, out AudioSource activeSource)
    && activeSource != null && activeSource.isPlaying)
    return false;
```

문제는 이 `AudioSource`가 `voicePool`(크기 4)에서 **여러 카테고리가 돌려쓰는 공유 소스**라는 점이다.
예를 들어 `actionFailed`가 `voicePool[0]`을 써서 재생했다고 기록해둔 뒤, 나중에 완전히 다른 카테고리
(`insufficientResources` 등)가 같은 `voicePool[0]`을 재사용해 다른 클립을 재생하면, `actionFailed`의
다음 재생 요청은 "저장해둔 소스가 지금 재생 중이네 → 이미 재생 중이구나"라고 **잘못 판단**해서 새
재생을 조용히 스킵해버린다. 즉 "이 소스가 재생 중인가"만 볼 뿐 "이 소스가 **여전히 이 카테고리를**
재생 중인가"는 확인하지 않는다.

한편 `PlayFromPool()`(335~377번 줄)에는 이미 "이 소스가 지금 어떤 `SoundClipSet`을 재생 중인지"를
기록하는 `sourceCurrentSet` 딕셔너리가 있지만, `limitSpam=true`인 SFX 경로(`PlaySFX`/`PlaySFX2D`/
`PlayVoice`)에서만 채워지고, `PlayGlobalVoice`가 쓰는 경로(`limitSpam` 기본값 `false`)에서는 전혀
채워지지 않는다 - 그래서 `PlayGlobalVoice`의 dedup 체크가 이 정보를 활용할 수 없었다.

## 변경 계획

`sourceCurrentSet` 기록을 `limitSpam` 여부와 무관하게 항상 채우도록 하고, `PlayGlobalVoice`의 dedup
체크에서 "저장해둔 소스가 지금도 같은 카테고리를 재생 중인지"까지 함께 확인한다.

### `Assets/Scripts/Audio/SoundManager.cs`

```diff
     public bool PlayGlobalVoice(SoundClipSet set, float minInterval = 0f)
     {
         if (set == null || !set.HasClips)
             return false;

         if (activeGlobalVoiceSources.TryGetValue(set, out AudioSource activeSource)
-            && activeSource != null && activeSource.isPlaying)
+            && activeSource != null && activeSource.isPlaying
+            && sourceCurrentSet.TryGetValue(activeSource, out SoundClipSet currentOwner) && currentOwner == set)
             return false;
```

```diff
         pooled.StartedAt = Time.time;
         source.Play();

+        sourceCurrentSet[source] = set; // 이 소스가 지금 재생 중인 카테고리를 항상 기록 (풀 재사용 시
+        // PlayGlobalVoice의 dedup 오판 방지 - 예전엔 limitSpam=true일 때만 기록해서 나레이션류에는
+        // 반영이 안 됐음)
+
         if (limitSpam)
         {
             lastSfxStartTime[set] = Time.time;
-            sourceCurrentSet[source] = set;
         }
```

이 수정은 `actionFailed`뿐 아니라 `voicePool`을 공유하는 다른 모든 나레이션 카테고리
(`missionSuccess`, `upgradeComplete`, `unitUnderAttackWarning` 등)에 잠재해있던 같은 종류의
오판 버그도 함께 해결한다(근본 원인을 공유 함수 한 곳에서 고치는 방식).

## 변경 예정 파일
- `Assets/Scripts/Audio/SoundManager.cs`

---

## 적용 (사용자 승인 후)

제안대로 `Assets/Scripts/Audio/SoundManager.cs`의 `PlayGlobalVoice`/`PlayFromPool` 두 곳에 위 diff
그대로 적용함. `npx uloop-cli compile` 성공 확인(Error 0개, Warning은 기존에 있던 무관한 것들뿐).
실제 게임 Play Mode에서 자원부족으로 생산/건설 실패시켜 확인 - 사용자가 "잘 작동하네" 확인함.

## 변경된 파일
- `Assets/Scripts/Audio/SoundManager.cs`
