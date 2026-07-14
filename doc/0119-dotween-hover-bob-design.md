# 0119 - DOTween 공중 부양(Hover Bob) 애니메이션 (스크립트 구현 완료 - 프리팹 부착은 사용자가 직접)

## 날짜
2026-07-14

## 요청
"내가 Dotween 에셋을 임포트 했는데 이를 이용해서 공중유닛, 이륙중인 건물의 경우 공중에서 둥둥 떠있는
느낌으로 살짝 아래로 내려갔다가 올라갔다가 하는 식으로 공중에 떠있는 애니메이션을 추가해줄수 있어?"

## 조사
- DOTween은 이미 `Assets/Plugins/Demigiant/DOTween`에 임포트돼 있고, 프로젝트에 asmdef가 하나도 없어
  전부 기본 Assembly-CSharp으로 컴파일되므로 별도 어셈블리 참조 설정 없이 바로 `using DG.Tweening;`
  으로 쓸 수 있음을 확인함.
- 공중유닛(`Assets/Scripts/Unit/UnitController.cs`)은 이동 중일 때만(`isMovingAirUnit`) 루트
  `transform.position.y`를 매 프레임 지형 높이 + `airCruiseAltitude`로 재계산해서 직접 덮어씀 (정지
  상태에서는 아무도 Y를 안 건드림).
- 리프트 중인 건물(`Assets/Scripts/Building/BuildingController.cs`)도 `UpdateLiftedMovement()`가
  상승/수평이동/하강 단계에서 루트 `transform.position`을 직접 계산해 덮어씀. 도착 후 공중에 떠 있기만
  하는 상태(자유이동 도착 후)에는 아무도 안 건드림.
- 따라서 **루트 트랜스폼에 바로 DOTween을 걸면 이동 로직과 매 프레임 충돌**한다 (서로 다른 코드가 같은
  프레임에 `transform.position.y`를 계속 다시 씀 → 이동 중엔 즉시 깨지고, 정지 중에도 다음 이동 명령이
  들어오는 순간 트윈이 남아있던 오프셋 때문에 순간적으로 위치가 튐).
- Explore 조사 결과, 실제 3D 모델은 항상 루트가 아니라 **자식으로 임포트된 FBX 프리팹 인스턴스**로 들어가
  있음 (예: Firehawk → `unit_Ornithopter_Light_B_yup`, Tier1 건물 → `struct_Barracks_A_yup`). 이 자식
  트랜스폼의 `localPosition.y`만 오프셋으로 흔들면 루트가 하는 이동 계산과 절대 충돌하지 않음.
- `UnitController`/`BuildingController` 어디에도 기존에 "비주얼 루트 자식 트랜스폼"을 참조하는 패턴이
  없었음 — 이번에 새로 도입.
- 대상 프리팹 스캔 결과 (`isAirUnit: 1` / `canLift: 1`):
  - 공중유닛: `Guardian Drone.prefab`, `Firehawk.prefab` (+ 테스트용 `TestAirUnit.prefab`)
  - 리프트 건물: `Tier1.prefab`, `Tier2.prefab`, `Tier3.prefab`, `MainBase.prefab`
  - 총 7개 프리팹 — 프리팹 개수가 적어서 에디터에서 각 프리팹의 모델 자식 오브젝트에 수동으로 컴포넌트를
    붙이는 것을 제안함 (자동 일괄 부착용 에디터 스크립트까지는 과할 것으로 판단).

## 제안하는 구현

### 신규 파일: `Assets/Scripts/Effects/HoverBob.cs`
공중유닛/리프트 건물의 **모델(비주얼) 자식 오브젝트**에 부착. 루트가 아니라 이 자식의
`localPosition.y`만 DOTween으로 왕복시킨다.

```csharp
using UnityEngine;
using DG.Tweening;

// 공중 유닛 / 리프트 중인 건물의 비주얼(메쉬) 자식 오브젝트에 부착한다(루트가 아님).
// 루트 트랜스폼은 UnitController(공중유닛 이동)나 BuildingController(리프트 이동)가 매 프레임 좌표를
// 직접 갱신하므로, 같은 트랜스폼을 건드리면 이동 로직과 충돌한다 - 그래서 자식의 localPosition.y만
// DOTween으로 오프셋을 더해 둥실거리게 한다(doc/0119).
public class HoverBob : MonoBehaviour
{
    [SerializeField] private float bobHeight = 0.25f;   // 위/아래 각각 이동하는 폭
    [SerializeField] private float bobDuration = 1.4f;  // 한쪽 방향 이동에 걸리는 시간
    [SerializeField] private Ease bobEase = Ease.InOutSine;

    private UnitController unitController;         // 있으면 "공중유닛" - 항상 공중이므로 항상 재생
    private BuildingController buildingController;  // 있으면 "리프트 건물" - IsLifted()일 때만 재생

    private float baseY;
    private Tween bobTween;
    private bool bobbing;

    private void Awake()
    {
        unitController = GetComponentInParent<UnitController>();
        buildingController = GetComponentInParent<BuildingController>();
        baseY = transform.localPosition.y;
    }

    // UnitEffects/BuildingController를 직접 건드리지 않고 매 프레임 폴링으로 판단한다(doc/0105 패턴과 동일).
    private void Update()
    {
        bool shouldBob = (unitController != null && unitController.IsAirUnit())
            || (buildingController != null && buildingController.IsLifted());

        if (shouldBob && !bobbing)
            StartBob();
        else if (!shouldBob && bobbing)
            StopBob();
    }

    private void StartBob()
    {
        bobbing = true;
        bobTween = transform.DOLocalMoveY(baseY + bobHeight, bobDuration)
            .SetEase(bobEase)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopBob()
    {
        bobbing = false;
        bobTween?.Kill();
        transform.DOLocalMoveY(baseY, 0.3f).SetEase(Ease.OutSine); // 착륙 시 원래 높이로 부드럽게 복귀
    }

    private void OnDestroy() => bobTween?.Kill();
}
```

### `Assets/Scripts/Unit/UnitController.cs` (기존 → 변경)
`isAirUnit`을 외부에서 읽을 방법이 없어서 getter 하나를 추가해야 함 (`IsCurrentlyMoving()` 옆에 같은
스타일로):
```csharp
// 추가
public bool IsAirUnit() => isAirUnit;
```
`BuildingController.IsLifted()`는 이미 공개돼 있어 그대로 재사용.

### 프리팹 작업 (에디터에서 수동, 코드 변경 아님)
아래 7개 프리팹을 열어 모델 자식 오브젝트에 `HoverBob` 컴포넌트를 추가:
- `Assets/prefabs/NTA/Unit/Tier3/Guardian Drone.prefab` → 모델 자식
- `Assets/prefabs/NTA/Unit/Tier3/Firehawk.prefab` → `unit_Ornithopter_Light_B_yup`
- `Assets/prefabs/Test/TestAirUnit.prefab` → `Cube` (테스트용 플레이스홀더)
- `Assets/prefabs/NTA/Building/Tier1.prefab` → `struct_Barracks_A_yup`
- `Assets/prefabs/NTA/Building/Tier2.prefab` → 모델 자식
- `Assets/prefabs/NTA/Building/Tier3.prefab` → 모델 자식
- `Assets/prefabs/NTA/Building/MainBase.prefab` → 모델 자식

## 결정
- 스크립트(`HoverBob.cs`, `IsAirUnit()` getter)는 바로 구현.
- 7개 프리팹에 `HoverBob` 컴포넌트를 붙이는 작업(에디터 GUI 조작)은 사용자가 직접 진행하기로 함 —
  일괄 부착용 에디터 유틸리티는 만들지 않음.

## 사용자가 직접 할 일 (Unity 에디터)
아래 프리팹을 열어 **모델(비주얼) 자식 오브젝트**를 선택하고 `HoverBob` 컴포넌트를 Add Component로
붙이면 된다 (루트 오브젝트가 아니라 모델 자식에 붙여야 함 — 조사 결과 기준):
- `Assets/prefabs/NTA/Unit/Tier3/Guardian Drone.prefab` → 모델 자식
- `Assets/prefabs/NTA/Unit/Tier3/Firehawk.prefab` → `unit_Ornithopter_Light_B_yup`
- `Assets/prefabs/Test/TestAirUnit.prefab` → `Cube` (테스트용 플레이스홀더)
- `Assets/prefabs/NTA/Building/Tier1.prefab` → `struct_Barracks_A_yup`
- `Assets/prefabs/NTA/Building/Tier2.prefab` → 모델 자식
- `Assets/prefabs/NTA/Building/Tier3.prefab` → 모델 자식
- `Assets/prefabs/NTA/Building/MainBase.prefab` → 모델 자식

붙인 뒤 인스펙터에서 `Bob Height`(기본 0.25) / `Bob Duration`(기본 1.4초) / `Bob Ease`(기본
InOutSine)를 프리팹별로 조정 가능. 공중유닛은 항상 재생되고, 건물은 `IsLifted()`가 true인 동안(이륙
직후~착륙 직전)만 재생되며 착륙 시 0.3초 동안 원래 높이로 부드럽게 복귀한다.

## 변경 파일
- `Assets/Scripts/Effects/HoverBob.cs` (신규)
- `Assets/Scripts/Unit/UnitController.cs` (수정 - `IsAirUnit()` getter 추가)
- 프리팹 7종에 `HoverBob` 컴포넌트 부착 — 사용자가 에디터에서 직접 진행 예정 (코드 변경 아님)
