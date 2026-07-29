# BuildingSoundBankSO

`Assets/Scripts/ScriptableObject/BuildingSoundBankSO.cs`

## 개요

건물 "종류" 하나에 대응하는 사운드 묶음 에셋(`[CreateAssetMenu(menuName = "Sound/Building Sound Bank")]`). `UnitSoundBankSO`와 동일한 목적(doc/0255).

## 필드 (전부 `SoundClipSet`)

| 필드 | 설명 |
|---|---|
| `constructLoopSFX` | 건설 진행 중 루프음 |
| `constructCompleteSFX` | 건설 완료음 |
| `destroySFX` | 파괴음(전투로 파괴됐을 때만) |
| `selectVoice` | "건물 음성" — 선택 시 재생 |

## 연관 컴포넌트

- **BuildingAudio**: 이 에셋의 필드를 `SoundManager` 재생 API에 그대로 전달
- **BuildingDataSO**: `BuildingData.soundBank` 필드로 건물 종류마다 이 에셋 하나씩 연결
- **SoundClipSet**: 각 필드의 타입
