## 날짜
2026-08-15

## 요청 내용
"유저 프로필 초기화 이후 씬 다시 로딩해서 초기화 된게 바로바로 갱신되서 보이도록해줘"
(유저 프로필(PlayerPrefs) 초기화 버튼을 누른 뒤, 씬을 다시 로딩해서 초기화 결과가 화면에 즉시 반영되도록 해달라는 요청)

## 조사 내용
"유저 프로필"에 해당하는 저장 데이터는 `PlayerPrefs`이고, 초기화 버튼은
`Assets/Scripts/UI/MainMenuController.cs`의 `ResetPlayerPrefs()`다 (개발자용, doc/0472에서
미션 선택 화면 → 메인 화면으로 이동됨).

```csharp
private void ResetPlayerPrefs()
{
    PlayerPrefs.DeleteAll();
    PlayerPrefs.Save();
}
```

문제: `PlayerPrefs.DeleteAll()`은 저장된 값만 지울 뿐, 이미 `Awake()`에서 그 값을 읽어 메모리에
캐싱해둔 컴포넌트들은 갱신되지 않는다. 이 프로젝트에서 PlayerPrefs를 읽어 필드에 캐싱하는 곳:

- `Assets/Scripts/Audio/SoundManager.cs` (409-420행): `Awake()`에서 마스터/BGM/SFX/음성
  볼륨·뮤트를 `masterVolume` 등 필드로 로드. 리셋 버튼을 눌러도 이 필드들은 그대로 남아있어
  옵션 패널 슬라이더가 즉시 바뀌지 않는다.
- `Assets/Scripts/UI/GraphicsSettingsPanel.cs` (21-24행): `Awake()`에서 해상도(width/height)를 로드.
- `Assets/Scripts/UI/MissionSelectManager.cs` (117-128행): `Awake()`에서 `HighestUnlockedMission`을
  읽어 미션 버튼 잠금 상태(`ApplyLockState()`)를 설정. 이 리셋 버튼은 현재 MainScene에 있어서
  MissionSelectManager는 아직 로드조차 안 된 상태지만, 리셋 후 미션 선택 화면으로 들어가면 잠금
  상태는 정상적으로 새로 읽힌다 - 문제는 지금 씬(MainScene) 안의 옵션 패널(사운드/해상도)이다.

가장 손 적게 가는 해결책은 씬을 다시 로드해서 모든 컴포넌트의 `Awake()`/`Start()`를 재실행시켜
PlayerPrefs 기본값을 새로 읽게 하는 것 - 사용자가 요청한 방식과 정확히 일치한다.

## 계획된 코드 변경

**파일:** `Assets/Scripts/UI/MainMenuController.cs`

### 기존 코드
```csharp
    // 개발자용 - 미션 해금 진행 상황(doc/0472)과 SoundManager 볼륨/뮤트 설정(doc/0288)까지, 이
    // 프로젝트가 PlayerPrefs에 저장하는 값 전부를 지운다. 정식 버전 출시 전 이 버튼/메소드를
    // playerPrefsResetButton과 함께 제거할 것.
    private void ResetPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
```

### 변경 코드
```csharp
    // 개발자용 - 미션 해금 진행 상황(doc/0472)과 SoundManager 볼륨/뮤트 설정(doc/0288)까지, 이
    // 프로젝트가 PlayerPrefs에 저장하는 값 전부를 지운다. 정식 버전 출시 전 이 버튼/메소드를
    // playerPrefsResetButton과 함께 제거할 것.
    //
    // 리셋 직후 씬을 다시 로드하는 이유: SoundManager/GraphicsSettingsPanel 등은 값을 Awake()에서
    // PlayerPrefs로부터 읽어 필드에 캐싱해두므로, DeleteAll()만으로는 이미 로드된 필드(볼륨 슬라이더
    // 등 화면에 보이는 값)가 갱신되지 않는다 - 씬을 재로드해 Awake()를 다시 태워서 기본값을 즉시
    // 반영한다.
    private void ResetPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
```

`using UnityEngine.SceneManagement;`는 이미 파일 상단에 있어 추가 using 불필요.

## 요약/영향받는 파일
- `Assets/Scripts/UI/MainMenuController.cs`의 `ResetPlayerPrefs()`에 현재 씬 재로드 한 줄 추가.
- 개발자용 리셋 버튼 전용 변경이라 다른 곳에 영향 없음 - MainScene뿐 아니라 이 버튼이 나중에
  다른 씬으로 옮겨져도 `GetActiveScene().name`을 쓰므로 그대로 동작한다.

## 확인 필요
이대로 구현해도 될까?
