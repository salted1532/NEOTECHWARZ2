# 0329. 샤프슈터 은신 시 프리뷰 흰색 머티리얼 적용

**날짜:** 2026-07-31

## 요청

> 사프슈터 클로킹시 프리뷰처럼 그 흰색 메테리얼로 변경되도록 하는거, 풀리면 다시 원래대로 돌아가도록

## 조사 내용

- 이 기능은 이미 [[advanced-unit-active-passive-skill-effects-design]](doc/0323)에서 설계·구현
  완료된 상태였음:
  - `Assets/Scripts/Unit/StealthVisual.cs` — `EnterStealth()`(모든 `Renderer`의 머티리얼을
    `stealthMaterial`로 교체, 원본은 딕셔너리에 보관) / `ExitStealth()`(원본 복원). "PreviewSystem의
    건물 배치 고스트 머티리얼과 같은 기법"이라고 코드 주석에 이미 명시돼 있음.
  - `Assets/Scripts/Unit/Skills/SharpshooterSkill.cs` — 은신 스킬 발동 시
    `unit.GetComponent<StealthVisual>()?.EnterStealth()`, 지속시간(15초) 종료 시 `ExitStealth()`를
    이미 호출하고 있음.
- 하지만 **`Sharpshooter.prefab`에 `StealthVisual` 컴포넌트 자체가 붙어있지 않았음**
  (`SharpshooterSkill`은 붙어있음) — doc/0323에서 "유닛 프리팹에 스킬 스크립트 부착"은 에디터에서
  직접 하는 수동 작업으로 남겨뒀는데, `StealthVisual`은 그 목록에서 빠져 있었음. `stealthMaterial`
  필드가 비어있으면 `EnterStealth()`가 아무것도 안 하고 조용히 리턴하므로(가드 있음), 지금까지는
  은신해도 겉보기 변화가 전혀 없었던 것.
- "프리뷰처럼 그 흰색 머티리얼"이 정확히 어떤 에셋인지 확인: `PreviewSystem.previewMaterialPrefab`이
  `Assets/prefabs/Game/GameManager.prefab`에서 GUID `416a36b76ed514b4882827efeb9dc850`로 연결돼
  있고, 이 GUID는 `Assets/Shader/TransparentMaterial.mat`(반투명 흰색 고스트 머티리얼, 건물 배치
  프리뷰에 쓰는 바로 그 에셋)과 일치함.

## 적용한 변경

`Assets/prefabs/NTA/Unit/Tier1/Sharpshooter.prefab` — 루트 오브젝트(`SharpshooterSkill`이 붙은
동일 오브젝트)에 `StealthVisual` 컴포넌트를 새로 추가하고, `stealthMaterial`을
`Assets/Shader/TransparentMaterial.mat`(PreviewSystem이 쓰는 것과 동일한 에셋)로 연결.
코드 변경 없음 — 이미 완성된 시스템을 프리팹에 마저 연결하는 작업만 필요했음.

`npx uloop-cli get-logs --log-type Error`로 콘솔 에러 0개 확인.

## 결과

샤프슈터가 은신(자기 자신 대상 액티브 스킬) 발동 시 전신이 건물 배치 프리뷰와 동일한 반투명 흰색으로
바뀌고, 15초 지속시간이 끝나면 자동으로 원래 머티리얼로 복원됨(`SharpshooterSkill.StealthRoutine`이
이미 그렇게 되어 있었음 - 이번 변경으로 실제로 눈에 보이게 됨).

## 후속: 은신 시 선택 마커까지 같이 하얗게 변하는 문제 (같은 날 추가 요청)

**요청**: "유닛은 잘 적용되었는데 밑에 마커까지 머티리얼까지 바뀌네 이거좀 수정해줘"

- 원인: `StealthVisual.EnterStealth()`가 `GetComponentsInChildren<Renderer>()`로 자식 전체를
  순회하는데, 여기에 유닛 발밑의 선택 마커(`Marker` 오브젝트, `UnitController.unitMarker`가 참조하는
  바로 그 오브젝트)의 `Renderer`도 포함돼 있어서 마커까지 같이 흰색으로 바뀌었음.
- 수정: `StealthVisual.cs`에 `[SerializeField] private GameObject[] excludeFromStealth` 추가 —
  `EnterStealth()`가 이 목록 아래의 `Renderer`는 건드리지 않도록 스킵. `ExitStealth()`는 원래
  `originalMaterials`에 저장된 것만 복원하므로 별도 수정 불필요(제외된 렌더러는애초에 안 들어감).
  `Sharpshooter.prefab`의 `StealthVisual`에 `excludeFromStealth = [Marker]`로 연결.
- **부수적으로 발견한 무관한 버그**: 위 수정 검증을 위해 Play Mode 콘솔을 확인하던 중,
  `RTSUnitController.PurgeAndCountControlGroup`(부대 버튼 기능, doc/0327/0328)에서 매 프레임
  `NullReferenceException`이 스팸되고 있는 걸 발견함 — `controlGroupUnits`/`controlGroupBuildings`
  배열의 각 슬롯이 `Awake()`에서만 초기화돼서, `ControlGroupPanel`이 같은 프레임에 `RTSUnitController`의
  `Awake()`보다 먼저 `Update()`를 도는 실행 순서일 때 슬롯이 아직 `null`이라 터짐. 필드 초기화 시점(생성자
  타이밍, `Awake`보다 항상 먼저)에 슬롯까지 채우도록 고쳐서 실행 순서에 의존하지 않게 함. Play Mode를
  1분 이상 돌려 에러 0개로 확인.
- `Assets/AssetFolder/AOSFogWar/Shadowcaster.cs`의 `ProcessLevelData`에서도 별도의
  `NullReferenceException`이 관찰됐으나(에셋 코드, 이번 변경과 무관), 이번 범위 밖이라 손대지 않음
  — 필요하면 별도로 조사 요청.

## 후속2: 은신 발동 시 효과음 추가

**요청**: "은신 했을때 소리 나오도록 해주고 연결할수 있게 해줘"

- 같은 스크립트의 `Sniper()`가 이미 쓰던 패턴(`[SerializeField] private AudioClip sniperShotSfx` +
  `AudioSource.PlayClipAtPoint`)을 그대로 재사용 — `UnitAudio`/`UnitSoundBankSO` 경유 방식(`bank.skillSFX`
  용 `PlaySkillSFX()`가 이미 있지만 어떤 스킬도 아직 안 씀)보다, 같은 스크립트 안에서 이미 검증된 로컬
  패턴을 따르는 게 일관적이라 그쪽으로 통일.
- `SharpshooterSkill.cs`에 `[SerializeField] private AudioClip stealthSfx` 추가, `StealthRoutine()`이
  은신 진입(`EnterStealth` 호출 직후) 시점에 `null` 체크 후 재생.
- 코드만 추가함 — 인스펙터에서 `Sharpshooter` 프리팹의 `Sharpshooter Skill` 컴포넌트에 있는
  `Stealth Sfx` 필드에 원하는 오디오 클립을 직접 드래그해서 연결하면 됨(프리팹은 건드리지 않음,
  값이 비어있으면 기존처럼 조용히 무음).
