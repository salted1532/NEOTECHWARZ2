# 0369 — 버그수정: MainScene BGM이 재생되지 않는 문제

**날짜:** 2026-08-02

## 질문

> MainScene에 사운드 매니저에다가 BGM추가했는데 왜 재생이 안되는거 같지

## 원인 확인

`Assets/Scripts/Audio/SoundManager.cs`의 `Update()`는 매 프레임
`if (bgmTracks.Count > 0 && bgmSource != null && !bgmSource.isPlaying) PlayRandomBGMTrack();`로
BGM 자동 재생을 트리거한다.

`Assets/Scenes/MainScene.unity`에서 `SoundManager` 게임오브젝트(`GameManager.prefab`과는 무관하게 씬에
직접 배치된 별도 오브젝트, `fileID: 1336732491`)를 확인한 결과:

- `bgmTracks`에 클립이 1개 들어있음(`guid: 004718f5dd6ae22428098d35f138f937`) — BGM 추가 자체는 정상적으로 함.
- 하지만 `bgmSource: {fileID: 0}` — **AudioSource 참조가 비어있음**. 이 오브젝트의 컴포넌트 목록에
  Transform과 `SoundManager` 스크립트 딱 둘뿐이라, **AudioSource 컴포넌트 자체가 없음**.

`bgmSource == null`이라 위 `Update()`의 재생 조건이 항상 거짓이 되어 `PlayRandomBGMTrack()`이 한 번도
호출되지 않는다. 예외/에러 없이 조용히 재생만 안 되는 상태.

(참고: `Assets/prefabs/Game/GameManager.prefab`에 들어있는 SoundManager는 AudioSource가 정상 연결돼
있지만, MainScene은 이 프리팹을 사용하지 않고 별도로 만든 SoundManager를 쓰고 있어서 프리팹 쪽 설정은
MainScene에 영향이 없음.)

## 수정 (사용자 승인 후 적용 예정)

코드 수정이 아니라 **씬 오브젝트 설정**: MainScene의 `SoundManager` 게임오브젝트에 AudioSource
컴포넌트를 추가하고, `SoundManager` 컴포넌트의 `bgmSource` 필드에 그 AudioSource를 연결한다.
(Unity 에디터 스크립트로 씬을 열어 `GameObject.AddComponent<AudioSource>()` → `SerializedObject`로
`bgmSource` 필드 할당 → 씬 저장. 인스펙터에서 수동으로 Add Component + 드래그하는 것과 동일한 결과.)

AudioSource 설정값은 `GameManager.prefab`의 기존 BGM용 AudioSource(`playOnAwake=0`, `volume=1`,
`loop=0`, `spatialBlend`는 기본 2D)를 그대로 참고해서 맞춘다 — `SoundManager.PlayRandomBGMTrack()`이
매번 `clip`을 새로 지정하고 `Play()`하는 방식이라 `loop`은 꺼둬도 무방(대신 `Update()`가 곡이 끝나면
알아서 다음 곡을 이어붙임).

## 적용 결과 (2026-08-02)

에디터 스크립트 실행(`npx uloop-cli execute-dynamic-code`)이 auto mode 권한 분류기에 의해 차단되어,
사용자가 Unity 에디터에서 직접 처리함: MainScene의 `SoundManager` 오브젝트에 AudioSource 컴포넌트를
추가하고 `SoundManager.bgmSource` 필드에 연결. `Assets/Scenes/MainScene.unity` 확인 결과
`bgmSource: {fileID: 1336732494}`로 정상 연결됨.

## 영향받는 파일

- `Assets/Scenes/MainScene.unity`
