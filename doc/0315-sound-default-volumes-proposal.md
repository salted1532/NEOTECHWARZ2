# 0315. 사운드 기본 볼륨값 변경 (마스터 100%, 나머지 50%) (제안)

날짜: 2026-07-30

## 결과 (2026-07-30 되돌림)

승인 후 적용했으나, 사용자가 이 명령 자체를 되돌려달라고 요청(`doc/0317`)해서 `SoundManager.cs`의
bgm/sfx/voice 기본값을 다시 `1f`(100%)로 되돌림. 마스터는 원래도 `1f`라 변경 없음. 이후 별개로
진행됐던 슬라이더 0~100% 통일 수정(`doc/0316`)은 그 자체로 유효한 버그 수정이라 되돌리지 않고 그대로 둠.

## 요청 내용

> 이제 연결 다 완료했고 마스터 볼륨은 100이 기본 나머지는 50이 기본으로 해줘

## 조사 내용

- `Assets/Scripts/Audio/SoundManager.cs`의 기본값은 인스펙터 필드 초기값으로 정해짐:
  ```csharp
  [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
  [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
  [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
  [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
  ```
  마스터는 이미 `1f`(100%)라 그대로 두면 되고, BGM/SFX/Voice를 `0.5f`(50%)로 바꾸면 됨.
- **주의**: `LoadVolumePrefs()`는 `PlayerPrefs`에 저장된 값이 있으면 그 값을 덮어써서 우선한다. 이미
  슬라이더를 눌러서 테스트해봤다면(연결 완료 후 확인차 만져봤다면) 그 값이 `PlayerPrefs`에 저장되어
  있어서, 코드의 기본값을 바꿔도 화면에는 이전에 테스트한 값이 그대로 보일 수 있음. 이 경우
  `PlayerPrefs.DeleteAll()`을 한 번 실행하거나(에디터에서 `Edit > Clear All PlayerPrefs`), 각 볼륨의
  `PlayerPrefs` 키(`Sound_MasterVolume` 등)를 개별 삭제해야 새 기본값이 실제로 보임 - 이건 코드
  변경이 아니라 로컬 테스트 데이터 초기화라 원하시면 제가 도와드릴 수 있음.

## 코드 변경

### `Assets/Scripts/Audio/SoundManager.cs`

**기존 코드**:
```csharp
    [Header("볼륨/뮤트 (임시 - 실제 설정 UI가 붙기 전까지 인스펙터에서 직접 조절/테스트용, doc/0288)")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 1f;
```

**변경 코드**:
```csharp
    [Header("볼륨/뮤트 (기본값 - 마스터 100%/나머지 50%, PlayerPrefs에 저장된 값이 있으면 그게 우선)")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.5f;
```

## 영향받는 파일

- `Assets/Scripts/Audio/SoundManager.cs` (수정)

## 다음 단계

1. 이대로 수정해도 될지
2. 테스트하면서 이미 저장된 `PlayerPrefs` 볼륨값을 지금 초기화해드릴지(안 하면 새 기본값이 바로 안
   보일 수 있음)

확인 부탁드립니다.
