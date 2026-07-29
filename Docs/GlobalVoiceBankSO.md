# GlobalVoiceBankSO

`Assets/Scripts/ScriptableObject/GlobalVoiceBankSO.cs`

## 개요

특정 유닛/건물에 묶이지 않는 게임 나레이션 음성(자원/인구 부족, 화면 밖 피격 경고, 업그레이드 완료 등) 에셋(`[CreateAssetMenu(menuName = "Sound/Global Voice Bank")]`). `SoundManager`가 이 에셋 1개를 인스펙터에서 참조해서 재생한다(doc/0255).

## 필드 (전부 `SoundClipSet`)

| 필드 | 설명 |
|---|---|
| `insufficientResources` | 자원(광물/가스) 부족 경고 |
| `insufficientPopulation` | 인구수 부족 경고 |
| `unitUnderAttackWarning` | 화면 밖에서 아군 유닛이 (적에게) 공격받았을 때 — 아군사격은 제외됨(doc/0292) |
| `buildingUnderAttackWarning` | 화면 밖에서 아군 건물이 (적에게) 공격받았을 때 — 아군사격은 제외됨(doc/0292) |
| `upgradeComplete` | 연구소 업그레이드 완료 시 |

## 연관 컴포넌트

- **SoundManager**: `globalVoiceBank` 필드로 이 에셋을 참조, `PlayGlobalVoice`/`PlayInsufficientResourcesWarning` 등 래퍼 메소드로 재생
- **UnitAudio / BuildingAudio**: `isEnemyAttacker`가 true일 때만 `PlayUnitUnderAttackWarning`/`PlayBuildingUnderAttackWarning`을 호출
