# SoundManager

`Assets/Scripts/Audio/SoundManager.cs`

## 개요

사운드 전담 정적 싱글턴(`Instance`). 주음량/배경음악/효과음/음성 4개 카테고리의 볼륨·뮤트를 관리하고, 실제 재생은 미리 만들어둔 `AudioSource` 풀을 순환 재사용해서 처리한다(doc/0255). 유닛/건물 종류별 사운드는 `UnitSoundBankSO`/`BuildingSoundBankSO`에, 유닛에 안 묶이는 나레이션은 `GlobalVoiceBankSO`에 들어있고, 이 매니저는 "재생/볼륨" 로직만 담당한다 — 어떤 클립을 언제 재생할지는 `UnitAudio`/`BuildingAudio` 등 호출부가 결정해서 `SoundClipSet`을 그대로 넘겨준다.

다른 매니저(`ResourceManager` 등)는 인스펙터 직렬화 필드로만 참조를 주고받지만, 사운드는 유닛/건물/UI/`RTSUnitController` 등 정말 많은 곳에서 호출해야 해서 예외적으로 정적 싱글턴을 둔다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `bgmSource`, `bgmTracks` | BGM 재생용 `AudioSource` + 곡 목록(권장 3곡) — 매 판마다 랜덤 1곡, 끝나면 다시 랜덤 무한 재생(직전 곡 연속 방지) |
| `sfxSourcePrefab`, `voiceSourcePrefab`, `sfxPoolSize`(16), `voicePoolSize`(4) | SFX/Voice `AudioSource` 풀 구성 — 프리팹을 비워두면 코드에서 자동 생성 |
| `sfxRetriggerInterval`(0.05초), `sfxMaxConcurrentPerSet`(4) | 동시다발 SFX/Voice 스팸 방지 — 같은 `SoundClipSet`이 이 시간 이내 재요청되면 무시, 동시 재생 개수가 이 값 이상이면 무시(doc/0284) |
| `uiClickSFX` | 인터페이스(버튼 클릭) 소리 — 위치 없는 SFX |
| `globalVoiceBank` | 유닛/건물에 안 묶이는 전역 나레이션 뱅크(`GlobalVoiceBankSO`) |
| `underAttackWarningCooldown`(10초) | 피격 경고음만 재생이 끝나도 곧바로 다시 울리지 않도록 두는 별도 쿨다운(doc/0273) |
| `sfxPool`, `voicePool` | 풀링된 `AudioSource` 목록(`PooledSource` — 소스 + 재생 시작 시각) |
| `activeGlobalVoiceSources`, `lastGlobalVoiceStartTime` | 나레이션 카테고리별 현재 재생 중인 소스 / 마지막 재생 시작 시각(doc/0271, 0273) |
| `lastSfxStartTime`, `sourceCurrentSet` | SFX/Voice 스팸 방지용 — 카테고리별 마지막 재생 시작 시각, 소스별 현재 재생 중인 `SoundClipSet`(doc/0284) |
| `orderVoiceSource`, `currentOrderVoiceUnitType` | 선택/이동/공격명령 음성 전용 단일 채널(doc/0262~0264) |
| `orderSFXSource`, `selectSFXSource` | 명령 확인음/선택 확인음 전용 단일 채널 — 재생 중이면 새 요청 무시, 끝난 뒤부터 재생(doc/0285) |
| `masterVolume`, `bgmVolume`, `sfxVolume`, `voiceVolume`(각 0~1), `bgmMuted`, `sfxMuted`, `voiceMuted` | 카테고리별 볼륨/뮤트 — 실제 설정 UI가 붙기 전까지 인스펙터에서 직접 조절 가능하도록 노출(doc/0288) |

## 메소드

### 생명주기
| 메소드 | 설명 |
|---|---|
| `Awake()` | 싱글턴 중복 파괴, `LoadVolumePrefs()`, SFX/Voice 풀 구성(SFX 풀만 3D 롤오프 설정), 전용 단일 채널(`orderVoiceSource`/`orderSFXSource`/`selectSFXSource`) 생성 |
| `Update()` | 매 프레임 `ApplyBGMVolume()`(인스펙터 값 변경 즉시 반영) 후, 현재 BGM이 끝났으면 `PlayRandomBGMTrack()` |
| `BuildPool(pool, prefab, size, namePrefix, configureSpatialRolloff)` (private) | 풀 생성 — SFX 풀만 `Linear` 롤오프 + `minDistance=10`/`maxDistance=45`(카메라 줌 범위에 맞춘 거리 감쇠, doc/0277·0286) |
| `GetAvailableSource(pool)` (private) | 재생 중이 아닌 소스 우선 반환, 전부 재생 중이면 가장 오래전에 재생 시작한 소스를 가로챔 |

### 재생 API
| 메소드 | 설명 |
|---|---|
| `PlaySFX(set, worldPos)` | 위치가 있는 3D 효과음(공격/사망/건설/파괴/채취 등) — 스팸 방지(`limitSpam`) 적용 |
| `PlaySFX2D(set)` | 위치가 없는 2D 효과음(인터페이스 소리 등) — 스팸 방지 적용 |
| `PlayUIClick()` | `uiClickSFX`를 2D로 재생 |
| `PlaySelectSFX(set)` / `PlayOrderSFX(set)` | 선택/명령 확인음 — 전용 단일 채널(`PlaySingleChannel`)로 재생, 재생 중이면 새 요청 무시(doc/0285) |
| `PlayVoice(set)` | 유닛/건물 음성(스폰/사망 대사 등) — 항상 2D, 스팸 방지 적용 |
| `PlayOrderVoice(set, unitType, category)` | 선택/이동/공격명령 음성 전용 — 다른 종류의 유닛을 새로 선택했을 때만 재생 중인 대사를 끊고 교체, 그 외엔 재생 중이면 요청을 버림(doc/0262~0264) |
| `PlayGlobalVoice(set, minInterval=0)` | 전역 나레이션 — 같은 카테고리가 재생 중이면 겹쳐 재생하지 않음, `minInterval`을 주면 추가로 최소 재재생 간격 적용(doc/0271, 0273) |
| `PlayInsufficientResourcesWarning()` / `PlayInsufficientPopulationWarning()` / `PlayUnitUnderAttackWarning()` / `PlayBuildingUnderAttackWarning()` / `PlayUpgradeCompleteVoice()` | 자주 쓰는 나레이션 카테고리 래퍼 — 호출부가 `globalVoiceBank` null 체크를 안 해도 됨 |
| `PlayFromPool(pool, set, categoryVolume, muted, spatialBlend, worldPos, limitSpam=false)` (private) | 실제 풀 재생 로직 — `limitSpam=true`면 최소 재생 간격 + 동시 재생 개수 제한을 모두 적용(doc/0284) |

### BGM
| 메소드 | 설명 |
|---|---|
| `PlayRandomBGMTrack()` (private) | 직전 곡과 다른 곡을 랜덤 선택해 재생 |
| `ApplyBGMVolume()` (private) | `bgmSource.volume`을 현재 볼륨/뮤트 상태로 갱신 |

### 볼륨/뮤트
| 메소드 | 설명 |
|---|---|
| `EffectiveVolume(categoryVolume, muted)` (private) | `muted`면 0, 아니면 `masterVolume × categoryVolume` |
| `LoadVolumePrefs()` (private) | `PlayerPrefs`에 실제로 저장된 적 있는 키만 덮어씀 — 설정 UI가 아직 없으면 인스펙터 기본값이 그대로 유지됨(doc/0288) |
| `SetMasterVolume/BGMVolume/SFXVolume/VoiceVolume(linear01)`, `SetBGMMuted/SFXMuted/VoiceMuted(muted)` | 설정 UI가 호출하는 세터 — `PlayerPrefs`에 저장, 뮤트는 슬라이더 값 자체는 보존한 채 재생 배율만 0으로 만듦 |
| `GetMasterVolume()` 등 대응 게터 | 현재 값 조회(`SoundSettingsPanel`이 UI 초기화 시 사용) |

### 기타
| 메소드 | 설명 |
|---|---|
| `IsWorldPositionOnScreen(worldPos)` (static) | 카메라 뷰포트 밖 여부 판정 — 화면 밖 피격 경고음 재생 여부 판단에 사용, 카메라를 못 찾으면 항상 화면 안으로 간주 |

## 연관 컴포넌트

- **UnitAudio / BuildingAudio**: 유닛/건물 이벤트마다 이 매니저의 재생 API를 호출
- **UnitSoundBankSO / BuildingSoundBankSO / GlobalVoiceBankSO**: 실제 클립 데이터 제공(`SoundClipSet`)
- **SoundSettingsPanel**: 볼륨 슬라이더/뮤트 토글 UI에서 이 매니저의 Set/Get API를 호출
- **RTSUnitController**: 선택/이동/공격/자원부족/인구부족 등 명령 진입점에서 나레이션 재생을 트리거
