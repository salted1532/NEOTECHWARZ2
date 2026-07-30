# 0318. TestScene BGM 무음 + 인풋필드 동작 확인 조사

날짜: 2026-07-31

## 요청 내용

> 아니 이거 1~100안으로 수치 조정할수 있어야하고 testscene에서 bgm 소리가 안나 확인좀해줘
> '인풋필드로 직접 수치 조정할수 있도록 연결할수 있게 해줘' 이부분 까지 내용으로 작동하게 해줘

## 조사 내용 - 코드가 아니라 씬 배치가 원인

- **`Assets/Scripts/UI/SoundSettingsPanel.cs`는 되돌리기(doc/0317) 이후에도 전혀 손대지 않았고, 지금도
  doc/0314+doc/0316 그대로 온전함** - 슬라이더/인풋필드 둘 다 0~100 값을 주고받고, 서로/PlayerPrefs와
  정상적으로 동기화되도록 되어 있음. 즉 "인풋필드로 직접 수치 조정" 연결 코드 자체는 망가지지 않았음.
- 진짜 원인은 씬 배치: **`TestScene.unity`에는 `SoundManager`가 아예 배치되어 있지 않음**
  (`MainScene.unity`엔 2건 매치, `TestScene.unity`엔 0건). `SoundManager`는 정적 싱글턴(`Instance`)이고
  씬이 바뀌면 이전 씬의 GameObject는 파괴되는데, TestScene에 자기 자신의 `SoundManager`가 없으니
  `SoundManager.Instance`가 **null**이 됨. `SoundSettingsPanel`의 모든 로직(`RefreshDisplayedValues()`,
  슬라이더/인풋필드 변경 핸들러)이 `SoundManager.Instance` null 체크로 시작하거나 `?.`로 감싸여 있어서,
  TestScene에서는 슬라이더든 인풋필드든 조작해도 조용히 아무 일도 안 일어남 - "인풋필드가 안 먹는다"로
  보이는 것도, "BGM이 안 난다"도 같은 원인.
- **덤으로 발견한 문제**: `MainScene.unity`에 배치된 `SoundManager`도 확인해보니
  ```yaml
  bgmSource: {fileID: 0}
  bgmTracks: []
  ```
  로 **BGM용 AudioSource 미할당 + BGM 트랙 리스트가 비어있음**. `SoundManager.Update()`의 BGM 재생 로직은
  `bgmTracks.Count > 0 && bgmSource != null`일 때만 동작하므로, MainScene에 SoundManager는 있지만 이
  두 필드를 채우지 않으면 MainScene에서도 BGM은 재생되지 않는다(SFX/Voice는 풀을 코드에서 자동
  생성하므로 별도 할당 없이도 동작하지만, BGM만 예외적으로 `bgmSource`를 직접 만들어 연결해야 함 -
  `SoundManager.cs` 상단 주석 참고).

## 결론 - 코드 수정 없음, 필요한 건 씬 작업

1. **TestScene에 `SoundManager` 배치** (MainScene의 SoundManager를 참고해서 새로 만들거나 복제).
2. **MainScene/TestScene 양쪽 SoundManager의 `Bgm Source` 필드에 AudioSource 컴포넌트를 연결**
   (빈 GameObject에 AudioSource 추가해서 연결하면 됨).
3. **`Bgm Tracks` 리스트에 BGM으로 쓸 AudioClip을 최소 1개 이상 추가**.

이 세 가지를 마치면 BGM도 재생되고, 옵션 패널의 슬라이더/인풋필드도 (SoundManager.Instance가 더 이상
null이 아니게 되므로) TestScene에서 정상적으로 값 조정이 될 것으로 예상됨.

## 영향받는 파일

없음 (코드 변경 없음 - 조사 결과 원인이 씬 배치 누락으로 확인됨).
