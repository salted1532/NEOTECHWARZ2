# SoundSettingsPanel

`Assets/Scripts/UI/SoundSettingsPanel.cs`

## 개요

볼륨 설정 패널 — 슬라이더 4개(주음량/배경음악/효과음/음성) + 토글 3개(배경음악/효과음/음성)를 `SoundManager`의 볼륨/뮤트 API에 연결한다(doc/0255 Phase 4). 주음량은 요청 원문에 토글 언급이 없어 슬라이더만 둔다.

> **이 스크립트는 로직만 담당한다.** 실제 Canvas/슬라이더/토글 GameObject 배치(레이아웃)는 유니티 에디터에서 직접 만들고, 이 컴포넌트의 인스펙터 필드에 각 UI 요소를 연결해야 동작한다. 2026-07-29 기준 실제 씬 배치는 아직 안 된 상태(doc/0288) — 그동안은 `SoundManager` 인스펙터에서 직접 볼륨 필드를 조절해 테스트한다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `masterSlider`, `bgmSlider`, `sfxSlider`, `voiceSlider` | 0~1 범위 슬라이더 4개 |
| `bgmToggle`, `sfxToggle`, `voiceToggle` | 뮤트 토글 3개(켜짐 = 소리 남) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 슬라이더/토글에 `onValueChanged` 리스너 등록 후 `RefreshDisplayedValues()` |
| `OnEnable()` | 패널이 다시 열릴 때마다(예: ESC 메뉴 재진입) `SoundManager`의 현재 값으로 다시 맞춤 |
| `RefreshDisplayedValues()` (private) | `SetValueWithoutNotify`/`SetIsOnWithoutNotify`로 UI를 `SoundManager` 현재 값에 맞춤(순환 호출 방지) |
| `OnMasterVolumeChanged` 등 4개 (private) | 슬라이더 값 변경 시 `SoundManager.SetXxxVolume` 호출 |
| `OnBGMToggleChanged` 등 3개 (private) | 토글 값 변경 시 `SoundManager.SetXxxMuted`(!isOn) 호출 — 토글 "켜짐" = 소리 남 쪽이 직관적이라 `Muted`와는 반전해서 넘김 |

## 연관 컴포넌트

- **SoundManager**: 모든 볼륨/뮤트 상태의 실제 소유자, 이 패널은 UI ↔ 값 동기화만 담당
