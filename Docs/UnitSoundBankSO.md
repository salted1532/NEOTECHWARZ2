# UnitSoundBankSO

`Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs`

## 개요

유닛 "종류" 하나에 대응하는 사운드 묶음 에셋(`[CreateAssetMenu(menuName = "Sound/Unit Sound Bank")]`). 유닛이 늘어나도 코드 수정 없이 이 에셋을 새로 만들어 `UnitData.soundBank`에 연결하기만 하면 된다(doc/0255, `UnitTraitOption`과 동일한 "코드 밖에서 유닛별로 관리" 철학).

## 필드 (전부 `SoundClipSet`)

### SFX (효과음 - 음성 제외)
| 필드 | 설명 |
|---|---|
| `attackSFX` | 공격 시 |
| `spawnSFX` | 생성 시(이륙음/엔진음 등, 유닛마다 다르게 채움) |
| `deathSFX` | 사망 시 |
| `skillSFX` | 고급유닛 액티브 스킬용 |
| `gatherSFX` | 채취 시(워커 전용, 나머지 유닛은 비워둠) |
| `selectSFX` | 선택 시 대사와 별개로 같이 나는 효과음(삑 소리 등) |
| `orderSFX` | 이동/공격/순찰 등 모든 명령 시 대사와 별개로 같이 나는 확인음(구 `moveSFX`, doc/0279 — 이동 전용에서 명령 전반으로 범위 확대. `FormerlySerializedAs`로 기존 `moveSFX` 클립 데이터 승계) |

### Voice (음성)
| 필드 | 설명 |
|---|---|
| `selectVoice` | 선택 시 대사(3~4개 권장) |
| `orderVoice` | 이동/순찰 명령 시 대사(구 `moveVoice`, doc/0289 — 순찰까지 범위 확대. `FormerlySerializedAs`로 기존 `moveVoice` 클립 데이터 승계, 3~4개 권장) |
| `attackOrderVoice` | 공격명령 시 대사(1~2개 권장) |
| `spawnVoice` | 생성 시 대사 |
| `deathVoice` | 사망 시 대사 |

### Voice (워커 전용 - 다른 유닛은 비워둠)
| 필드 | 설명 |
|---|---|
| `buildCompleteVoice` | 건설 완료 시 |
| `buildFailVoice` | 건설 실패 시 |

## 연관 컴포넌트

- **UnitAudio**: 이 에셋의 필드를 `SoundManager` 재생 API에 그대로 전달
- **UnitDataSO**: `UnitData.soundBank` 필드로 유닛 종류마다 이 에셋 하나씩 연결
- **SoundClipSet**: 각 필드의 타입 — 클립 여러 개 + 랜덤 재생 로직
