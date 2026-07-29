## 날짜
2026-07-29

## 요청 내용
"BGM을 사운드 매니저에 하나 집어넣었는데 작동을 안 한다" - 확인 요청.

## 조사 내용
`Assets/Scenes/TestScene.unity`에서 `GameManager` 프리팹 인스턴스에 대한 오버라이드를 확인한 결과, `bgmTracks`에 클립 1개(`Iron March.mp3`, guid `652501b298d724b4b8d71a54f0ea85f9` - 실제 존재하는 정상 오디오 파일 확인됨)가 정상적으로 등록되어 있음. 여기까진 사용자가 한 작업이 맞음.

문제는 `SoundManager` 컴포넌트의 `bgmSource` 필드가 **비어있음**(`{fileID: 0}`) - `Assets/prefabs/Game/GameManager.prefab`을 확인해보니 `SoundManager`가 붙어있는 GameObject(`SoundManager` 자식 오브젝트)에 **`AudioSource` 컴포넌트 자체가 아예 없음**(`Transform`+`SoundManager` 스크립트 2개 컴포넌트뿐). 씬 오버라이드에도 `bgmSource`를 채운 흔적이 없음.

`SoundManager.cs`의 재생 로직:
```csharp
private void Update()
{
    if (bgmTracks.Count > 0 && bgmSource != null && !bgmSource.isPlaying)
        PlayRandomBGMTrack();
}
```
`bgmSource`가 null이라 `bgmSource != null` 조건에서 항상 걸려 `PlayRandomBGMTrack()`이 절대 호출되지 않음 - 클립을 몇 개를 넣어도 재생시킬 `AudioSource` 자체가 없어서 조용한 것. 에러/예외 없이 그냥 아무 일도 안 일어나는 형태라 원인 파악이 어려웠을 것.

## 해결 방법
`SoundManager` GameObject(프리팹)에 `AudioSource` 컴포넌트를 추가하고, `SoundManager.bgmSource` 필드에 그 컴포넌트를 연결해야 함. 두 가지 방법:

**A) Unity 에디터에서 직접 (권장, 안전)**
1. `Assets/prefabs/Game/GameManager.prefab`을 열고(또는 `TestScene`에서 GameManager > SoundManager 오브젝트 선택) `SoundManager` 자식 오브젝트 선택
2. `Add Component` → `Audio Source` 추가
3. `Play On Awake` 체크 해제(코드가 직접 `Play()` 호출), `Loop` 체크 해제(꺼져있어야 곡이 끝났을 때 `Update()`가 `isPlaying==false`를 감지해서 다음 곡으로 넘어감 - 켜져있으면 한 곡만 무한 반복되고 다음 랜덤 곡으로 안 넘어감)
4. `SoundManager` 컴포넌트의 `Bgm Source` 필드에 방금 추가한 `Audio Source`를 드래그해서 연결
5. 프리팹 저장(Apply)

**B) 이 세션에서 프리팹 파일을 직접 수정**
Unity 에디터 없이도 `.prefab`을 텍스트로 직접 편집해서 `AudioSource` 컴포넌트를 추가하고 `bgmSource`를 연결할 수 있음 - 다른 씬 파일(`Menu.unity`)의 기존 `AudioSource` 직렬화 포맷을 그대로 참고해서 작성.

## 코드/에셋 변경 (제안 - 아직 미적용)

### Assets/prefabs/Game/GameManager.prefab

**`SoundManager` GameObject의 `m_Component` 목록에 추가**
```yaml
  m_Component:
  - component: {fileID: 8732777014121625636}
  - component: {fileID: 3547970380920705843}
  - component: {fileID: 8684094549309555777}
```

**새 `AudioSource` 컴포넌트 블록 추가** (`PlayOnAwake: 0`, `Loop: 0` - 코드가 직접 재생을 제어하므로, `panLevelCustomCurve` value 0 = 2D 그대로 유지)
```yaml
--- !u!82 &8684094549309555777
AudioSource:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 2042123696298826469}
  m_Enabled: 1
  serializedVersion: 4
  OutputAudioMixerGroup: {fileID: 0}
  m_audioClip: {fileID: 0}
  m_Resource: {fileID: 0}
  m_PlayOnAwake: 0
  m_Volume: 1
  m_Pitch: 1
  Loop: 0
  Mute: 0
  Spatialize: 0
  SpatializePostEffects: 0
  Priority: 128
  DopplerLevel: 1
  MinDistance: 1
  MaxDistance: 500
  Pan2D: 0
  rolloffMode: 0
  BypassEffects: 0
  BypassListenerEffects: 0
  BypassReverbZones: 0
  rolloffCustomCurve: (기존 AudioSource와 동일한 기본 커브)
  panLevelCustomCurve: (value 0 = 2D)
  spreadCustomCurve: (기본값)
  reverbZoneMixCustomCurve: (기본값)
```

**`bgmSource` 필드 연결**
```yaml
  bgmSource: {fileID: 8684094549309555777}
```

## 확인 결과
사용자가 "B) 프리팹 파일 텍스트 직접 편집"으로 진행 선택.

## 코드/에셋 변경 (적용 완료)

### Assets/prefabs/Game/GameManager.prefab
- `SoundManager` GameObject(`fileID: 2042123696298826469`)의 `m_Component` 목록에 새 `AudioSource`(`fileID: 8684094549309555777`) 추가.
- 새 `AudioSource` 컴포넌트 블록 추가: `m_PlayOnAwake: 0`(코드가 직접 `Play()` 호출), `Loop: 0`(꺼져있어야 곡이 끝났을 때 `Update()`가 다음 곡으로 자동 전환), `panLevelCustomCurve` value `0`(2D 유지) - 나머지는 위 "Menu.unity"의 기존 `AudioSource` 기본값 그대로.
- `SoundManager` 컴포넌트의 `bgmSource: {fileID: 0}` → `bgmSource: {fileID: 8684094549309555777}`로 연결.

## 요약/남은 작업
적용 완료. Unity를 열어 `GameManager` 프리팹의 `SoundManager` 오브젝트에 `Audio Source` 컴포넌트가 정상 표시되고, `Sound Manager`의 `Bgm Source` 필드가 그 컴포넌트를 가리키는지 확인 필요. 그 후 플레이 모드에서 BGM(`Iron March.mp3`)이 정상 재생되는지 확인.

## 변경된 파일
- `Assets/prefabs/Game/GameManager.prefab`
