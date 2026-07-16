# 0147. DOTween 기반 자동 회전 스크립트 (레이더/포탑 등)

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **제안 코드**를 담고 있다. 새 파일
> 하나만 추가하는 격리된 작업이라 바로 이어서 생성했다 — 마음에 안 들면 파라미터/방식을 알려주면 바로 고친다.

## 날짜
2026-07-16

## 요청
`Assets/Scripts/Animation` 폴더 안에, 레이더나 포탑같은 건물이 자동으로 회전하는 스크립트를 DOTween으로 만들어달라.

## 조사
- `Assets/Scripts/Animation/`에 이미 `HoverBob.cs`(`doc/0119`), `VehicleShake.cs`(`doc/0120`)가 있음 — 둘 다 "루트가 아니라 비주얼 자식 오브젝트에 부착, 특정 상태(공중/이동 중)일 때만 재생" 패턴.
- 이번 대상(레이더 접시, 포탑 머리)은 그 둘과 달리 **조건 없이 항상 도는 아이돌 애니메이션**이라 `UnitController`/`BuildingController` 상태를 폴링할 필요가 없음 — 붙인 오브젝트가 계속 자전하기만 하면 됨. 건물은 유닛과 달리 루트 트랜스폼을 매 프레임 덮어쓰는 이동 로직이 없으므로, 회전시키고 싶은 파츠(레이더 접시/포탑 머리 자체)에 바로 붙여도 다른 로직과 충돌하지 않음.

## 제안 코드

### 신규 파일: `Assets/Scripts/Animation/AutoRotate.cs`
```csharp
using UnityEngine;
using DG.Tweening;

// 레이더 접시, 포탑 머리처럼 계속 제자리에서 자전하는 파츠에 부착한다. 건물은 유닛과 달리 루트
// 트랜스폼을 매 프레임 덮어쓰는 이동 로직이 없으므로, 회전시키고 싶은 오브젝트에 직접 붙이면 된다
// (HoverBob/VehicleShake처럼 별도 자식으로 피할 필요 없음).
public class AutoRotate : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // 회전축 (레이더/포탑 대부분 Y축)
    [SerializeField] private float secondsPerRotation = 2f;     // 한 바퀴(360도) 도는 데 걸리는 시간
    [SerializeField] private bool clockwise = true;

    private Tween rotateTween;

    private void Start()
    {
        Vector3 axis = (clockwise ? 1f : -1f) * rotationAxis.normalized * 360f;

        rotateTween = transform.DOLocalRotate(axis, secondsPerRotation, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental);
    }

    private void OnDestroy() => rotateTween?.Kill();
}
```

- `rotationAxis`: 기본 `Vector3.up`(Y축) — 레이더 접시나 포탑 머리 대부분 이 축으로 돎. 다른 축으로 돌아야 하면 인스펙터에서 바꾸면 됨.
- `secondsPerRotation`: 한 바퀴 도는 데 걸리는 시간(초) — 값이 작을수록 빨리 돎.
- `clockwise`: 방향 반전용 체크박스.
- `RotateMode.FastBeyond360` + `LoopType.Incremental`: DOTween에서 "한쪽 방향으로 계속 이어서 도는" 무한 회전을 만들 때 쓰는 표준 조합(각도가 계속 커지기만 하고 절대 리셋/왕복하지 않음 — 값이 누적되며 계속 회전).

## 사용법 (에디터에서 직접)
회전시키고 싶은 오브젝트(레이더 접시, 포탑 머리 등)에 `AutoRotate` 컴포넌트를 Add Component로 붙이면 된다. 포탑처럼 "머리만" 돌아야 하는 경우, 회전할 부분이 별도 자식 오브젝트로 분리돼 있어야 한다(포탑 받침대까지 같이 돌면 안 되므로) — 지금 어떤 프리팹에 붙일지는 알려주면 그 프리팹 구조를 보고 정확한 부착 위치를 같이 확인해줄 수 있음.

## 변경 파일
- `Assets/Scripts/Animation/AutoRotate.cs` (신규)
