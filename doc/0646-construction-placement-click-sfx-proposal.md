# 0646 - 건설모드 배치 클릭 시 프리뷰 설치 사운드 추가 (제안)

## 요청
건설모드에서 건물 건설 위치를 클릭해 확정하는 순간(고스트/프리뷰가 그 지점에 고정되는 순간) 사운드 추가. 건물 사운드 뱅크에 넣으면 되는지 확인 요청.

## 현재 상태
- `PlacementSystem.PlaceStructure()`(`Assets/Scripts/BuildSystem/PlacementSystem.cs:145`)가 배치 클릭을 처리한다: 유효성 검사 → 자원 차감(`TryConstructBuilding`) → `preview.SpawnConstructionGhost(data.Prefab, spawnPos)`(182줄, 클릭한 자리에 고정 고스트 생성) → 일꾼을 그 자리로 이동(`GoBuild`). 지금은 이 시점에 사운드가 없고, 이동 명령 음성/효과음(`PlayOrderVoice`/`PlayOrderSFX`, 184~185줄)만 재생된다.
- 사운드는 `BuildingAudio`(건물 오브젝트에 붙는 컴포넌트)가 `constructLoopSFX`(일꾼 도착 후 실제 건설 시작)/`constructCompleteSFX`(완공) 등을 재생하지만, 전부 "실제 건물/파운데이션이 생긴 뒤" 시점이라 지금 요청한 "클릭 즉시 프리뷰 설치 시점"과는 다르다. 이 시점엔 아직 건물 오브젝트 자체가 없고(고스트만 있음) 그리드만 예약된 상태라, `BuildingAudio`가 재생할 수 없다 - `PlacementSystem`이 직접 재생해야 함.
- 건물 종류별 사운드는 관례대로 `BuildingSoundBankSO`(`Assets/Scripts/ScriptableObject/BuildingSoundBankSO.cs`)에 모여있고, `BuildingData.soundBank`로 건물 종류마다 하나씩 연결돼 있다(비워두면 무음). 말씀하신 대로 건물 사운드 뱅크에 추가하는 게 기존 컨벤션과 맞다.

## 제안 설계
1. `BuildingSoundBankSO`에 필드 추가:
```csharp
[field: SerializeField]
public SoundClipSet placementSFX { get; private set; } // 배치 클릭으로 프리뷰가 그 자리에 고정되는 순간
```
2. `PlacementSystem.PlaceStructure()`에서 고스트 생성 직후(182줄 다음 줄)에 재생:
```csharp
SoundManager.Instance?.PlaySFX(data.soundBank?.placementSFX, spawnPos);
```
- `PlacementSystem`은 이미 `data`(`BuildingData`, `soundBank` 프로퍼티 포함)를 들고 있어 별도 조회 없이 바로 재생 가능.
- `data.soundBank`가 비어있거나 `placementSFX`가 비어있으면 `SoundManager.PlaySFX`가 조용히 무시하는 기존 관례 그대로(다른 SFX와 동일).
- 3D 위치 기반(`PlaySFX`)으로 재생 - 다른 건설/파괴 SFX와 동일한 감쇠 규칙 적용.

## 범위 밖
- 건물 리프트 재배치(착륙 위치 선택, `PlaceRelocatedBuilding()`)의 고스트 배치 사운드 - 요청은 "건설모드" 한정이라 별도 확인 없이는 포함하지 않음. 필요하면 같은 패턴으로 후속 추가 가능.
- 각 건물 사운드뱅크 에셋에 실제 클립 연결 - 클립 파일이 없으면 빈 채로 둬도 무음으로 안전하게 동작.

## 구현 완료
- `BuildingSoundBankSO.cs`: `placementSFX`(SoundClipSet) 필드 추가.
- `PlacementSystem.cs`의 `PlaceStructure()`: 고스트 생성(`preview.SpawnConstructionGhost`) 바로 다음 줄에 `SoundManager.Instance?.PlaySFX(data.soundBank?.placementSFX, spawnPos)` 추가. `data.soundBank`나 `placementSFX`가 비어있으면 기존 관례대로 조용히 무시됨.
- 컴파일 성공(에러 0, 경고 0).

## 상태
완료. 각 건물 종류의 `BuildingSoundBankSO` 에셋에 `placementSFX` 클립을 연결하면 건설모드 배치 클릭 즉시 그 자리에서 재생된다. 클립을 아직 안 넣은 건물은 지금처럼 무음.

## 후속 - 클립 연결 (2026-08-20)
`Assets/Sound/NTA/Building/BuildingPlace_Sound.mp3`를 `NTA Building Sound Bank SO.asset`의 `placementSFX`에 연결(건물별 뱅크가 아니라 NTA 전체가 공유하는 단일 뱅크라 모든 NTA 건물에 공통 적용됨). `AssetDatabase.LoadAssetAtPath` + 리플렉션으로 재로드해 클립이 실제로 물렸는지 확인 완료(`clipCount: 1`). OC 건물 사운드 뱅크(`OC Building Sound Bank SO.asset`)는 클립이 없어 요청 범위 밖으로 비워둠 - 필요하면 같은 방식으로 후속 추가.
