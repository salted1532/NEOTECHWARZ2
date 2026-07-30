# 0317. 사운드 기본 볼륨값 변경(doc/0315) 되돌림

날짜: 2026-07-30

## 요청 내용

> > 이제 연결 다 완료했고 마스터 볼륨은 100이 기본 나머지는 50이 기본으로 해줘
> 이거 명령 내리기 전으로 돌려줘

`doc/0315`(마스터 100%/나머지 50% 기본값 변경) 명령 자체를 취소하고 그 이전 상태로 되돌려달라는 요청.

## 확인한 되돌리기 범위

되돌릴 범위를 사용자에게 확인: `SoundManager.cs` 기본값만 되돌리고, 그 사이에 별개로 발견/수정한
슬라이더 0~100% 범위 통일 버그 수정(`doc/0316`)은 그대로 유지하기로 함 - 그건 "50% 기본값"과 무관하게
그 자체로 필요한 수정이었기 때문(슬라이더 Min/Max가 0~100인데 코드가 0~1을 넘겨서 위치가 안 맞던
문제).

## 코드 변경

### `Assets/Scripts/Audio/SoundManager.cs`

**기존 코드(doc/0315 적용 상태)**:
```csharp
    [Header("볼륨/뮤트 (기본값 - 마스터 100%/나머지 50%, PlayerPrefs에 저장된 값이 있으면 그게 우선)")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.5f;
```

**변경 코드(되돌린 상태, doc/0315 이전과 동일)**:
```csharp
    [Header("볼륨/뮤트 (임시 - 실제 설정 UI가 붙기 전까지 인스펙터에서 직접 조절/테스트용, doc/0288)")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
```

## 참고

- `doc/0315` 진행 중 삭제했던 `PlayerPrefs`의 `Sound_MasterVolume`/`Sound_BGMVolume`/`Sound_SFXVolume`/
  `Sound_VoiceVolume` 키는 그대로 비어있는 상태다. 기본값이 다시 전부 `1f`로 돌아왔으므로, 저장된
  값이 없으면 `LoadVolumePrefs()`가 그냥 이 기본값(`1f`)을 그대로 쓰게 되어 별도 조치 없이 되돌리기
  전과 동일한 결과가 된다.
- `doc/0316`(슬라이더 0~100% 통일)은 되돌리지 않고 그대로 유지.

## 영향받는 파일

- `Assets/Scripts/Audio/SoundManager.cs` (수정 - doc/0315 되돌림)
- `doc/0315-sound-default-volumes-proposal.md` (되돌림 결과 기록 추가)
