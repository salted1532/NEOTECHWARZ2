## 날짜
2026-07-29

## 요청 내용
doc/0276(sfxBoost 1.5배), doc/0281(sfx3DBoost 1.5배, 최종 2.25배)로 SFX 볼륨을 계속 키워왔는데, 사용자가 진짜 원인을 찾음: `GameManager` 프리팹에 `AudioListener`가 박혀있었고, 메인 카메라의 `AudioListener`는 작동 안 하고 GameManager 쪽 리스너만 작동하고 있었음. 이 때문에 3D SFX의 거리 감쇠가 카메라 기준이 아니라 GameManager 위치 기준으로 계산되어 실제보다 훨씬 조용하게 들렸던 것 - doc/0276~0281에서 "볼륨을 계속 키워야 했던" 근본 원인이었음. 이제 원인을 알았으니 **전체 SFX 소리 크기를 원래대로(부스트 적용 전)** 되돌려달라는 요청.

## 조사 내용
`Assets/prefabs/Game/GameManager.prefab`에 실제로 `AudioListener` 컴포넌트가 2개 존재하는 것을 확인(`fileID 3750800321157420721`, `fileID 6662348182529360506`) - 사용자 설명과 일치. Unity는 씬에 `AudioListener`가 여러 개 있으면 경고를 띄우고 그중 하나만 실제로 동작하는데, 그게 메인 카메라가 아니라 GameManager 쪽이었던 것으로 보임.

`SoundManager.cs`의 볼륨 계산에서 부스트가 곱해지는 지점은 2곳:
- `PlaySFX`(3D): `sfxVolume * sfxBoost * sfx3DBoost`
- `PlaySFX2D`(2D): `sfxVolume * sfxBoost`

이번 요청 범위는 "전체 SFX 소리 크기"이므로 `sfxBoost`/`sfx3DBoost` 두 부스트 필드를 모두 제거하고, doc/0276 이전 상태(부스트 없이 `sfxVolume` 그대로 사용)로 되돌리는 것을 제안.

**범위에서 제외한 것 (요청받지 않음, 필요하면 별도 확인 후 진행)**
- doc/0277~0278의 롤오프 조정(`Linear`, `minDistance=15`/`maxDistance=80`)과 이동/선택음의 2D 전환: 이것도 같은 근본 원인(잘못된 리스너 위치 기준 거리 계산)의 영향을 받았을 가능성이 있어서, 리스너 문제가 실제로 고쳐지면 재검토할 가치가 있어 보이지만 이번 요청은 볼륨 크기만 언급했으므로 그대로 둠.
- `GameManager.prefab`의 중복 `AudioListener` 자체를 제거/이동하는 작업: 사용자가 원인을 직접 파악한 상태라 이미 처리했거나 처리할 예정일 수 있어, 이번엔 건드리지 않음.

## 코드 변경 (제안 - 아직 미적용)

### Assets/Scripts/Audio/SoundManager.cs

**기존 코드**
```csharp
    [Header("SFX 카테고리 전용 부스트 (사용자 볼륨 슬라이더와 별개 - 클립 자체 음량이 작아 체감 볼륨을 전반적으로 키우고 싶을 때)")]
    [SerializeField, Range(1f, 2f)] private float sfxBoost = 1.5f;

    [Header("3D 포지셔널 SFX 전용 추가 부스트 (공격/사망/건설/파괴/채취 등 - 위 sfxBoost 위에 곱해짐, 2D SFX/선택/명령음에는 영향 없음)")]
    [SerializeField, Range(1f, 2f)] private float sfx3DBoost = 1.5f;
```
```csharp
    // 위치가 있는 3D 효과음 (공격/사망/건설/파괴/채취 등 유닛·건물이 내는 소리).
    public void PlaySFX(SoundClipSet set, Vector3 worldPos) =>
        PlayFromPool(sfxPool, set, sfxVolume * sfxBoost * sfx3DBoost, sfxMuted, spatialBlend: 1f, worldPos);

    // 위치가 없는 2D 효과음 (인터페이스 소리 등) - sfx3DBoost 영향 없음.
    public void PlaySFX2D(SoundClipSet set) =>
        PlayFromPool(sfxPool, set, sfxVolume * sfxBoost, sfxMuted, spatialBlend: 0f, transform.position);
```

**변경 코드 (제안)**
```csharp
    // (doc/0276 sfxBoost, doc/0281 sfx3DBoost 필드 제거 - GameManager 프리팹의 중복 AudioListener가
    // 원인이었던 "SFX가 너무 조용함" 문제를 볼륨 부스트로 우회했었으나, 근본 원인이 밝혀져 되돌림)
```
```csharp
    // 위치가 있는 3D 효과음 (공격/사망/건설/파괴/채취 등 유닛·건물이 내는 소리).
    public void PlaySFX(SoundClipSet set, Vector3 worldPos) =>
        PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 1f, worldPos);

    // 위치가 없는 2D 효과음 (인터페이스 소리 등).
    public void PlaySFX2D(SoundClipSet set) =>
        PlayFromPool(sfxPool, set, sfxVolume, sfxMuted, spatialBlend: 0f, transform.position);
```

## 확인 결과
사용자가 "볼륨 부스트만 제거" 선택 - 롤오프 조정(doc/0277~0278)과 이동/선택음 2D 전환은 그대로 유지.

## 코드 변경 (적용 완료)
위 "제안" 코드 그대로 적용 - `sfxBoost`/`sfx3DBoost` 필드 삭제, `PlaySFX`/`PlaySFX2D` 모두 `sfxVolume` 그대로 사용.

## 요약/남은 작업
적용 완료. `GameManager.prefab`의 중복 `AudioListener` 자체를 정리하는 작업은 이번 요청 범위 밖 - 사용자가 직접 처리 중인 것으로 보임. 리스너 문제가 실제로 고쳐진 뒤 플레이해보고, doc/0277~0278의 롤오프/2D 전환도 여전히 맞는지(혹시 그것도 리스너 버그의 영향을 받아 과도하게 튜닝된 건 아닌지) 확인 필요.

## 변경된 파일
- `Assets/Scripts/Audio/SoundManager.cs`
