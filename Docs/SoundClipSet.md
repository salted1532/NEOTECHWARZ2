# SoundClipSet

`Assets/Scripts/Audio/SoundClipSet.cs`

## 개요

랜덤 재생용 오디오 클립 묶음을 표현하는 직렬화 클래스(`[System.Serializable]`, MonoBehaviour 아님). "선택 시 대사 3~4개", "공격명령 대사 1~2개"처럼 카테고리 하나에 해당하는 클립 여러 개를 담아두고, 재생할 때마다 그중 하나를 무작위로 고른다(doc/0255). `UnitSoundBankSO`/`BuildingSoundBankSO`/`GlobalVoiceBankSO`/`SoundManager`(`uiClickSFX`)의 모든 사운드 슬롯이 이 타입이다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `clips` | `List<AudioClip>` — 이 카테고리에 속한 클립 목록 |
| `volumeScale` (0~1.5) | 이 카테고리만 살짝 더 크게/작게 재생하고 싶을 때 — 최종 볼륨 = 카테고리 볼륨 슬라이더 값 × 이 값 |
| `pitchVariance` (0~0.3) | 같은 클립이 반복 재생돼도 덜 기계적으로 들리게 하는 피치 변동 폭(0이면 변동 없음) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `HasClips` | `clips`가 비어있지 않은지 |
| `GetRandomClip()` | `clips` 중 무작위 하나 반환, 비어있으면 `null`(호출부 `SoundManager`는 `null`이면 재생을 그냥 스킵) |
| `GetRandomPitch()` | `1 ± pitchVariance` 범위의 무작위 피치값 반환 |

## 연관 컴포넌트

- **SoundManager**: `PlayFromPool`/`PlaySingleChannel`/`PlayOrderVoice` 등 모든 재생 API가 이 타입을 받아 클립/피치/볼륨배율을 조회
- **UnitSoundBankSO / BuildingSoundBankSO / GlobalVoiceBankSO**: 모든 사운드 슬롯이 이 타입의 필드
