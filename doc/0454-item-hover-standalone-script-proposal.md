# 0454. 조건 없는 독립 Hover 스크립트 신설 - 제안

**날짜:** 2026-08-08

## 요청 내용
> 새로운 Hover 스크립트를 만들어줘 어떠한 조건이나 스크립트도 필요없고 그냥 아이템이 둥둥 뜨는거
> 마냥 Dotween을 이용해서 움직이고 천천히 회전도 하도록

`doc/0453`에서 `HoverBob`을 고쳐서 쓰는 방향(`alwaysBob` 옵션 추가)을 제안했는데, 대신 유닛/건물
상태 판정이 전혀 없는 **완전히 새로운 스크립트**를 원하는 것으로 확인함 - `HoverBob`은 그대로 두고
건드리지 않음.

## 제안하는 스크립트

`Assets/Scripts/Animation/ItemHover.cs` (신규):

```csharp
using UnityEngine;
using DG.Tweening;

// 조건/상태 판정 전혀 없이 붙이기만 하면 계속 위아래로 둥실거리고 천천히 회전하는 순수 장식용
// 컴포넌트. HoverBob(공중 유닛/리프트 건물 상태를 판정해서 재생 여부를 결정)과 달리, 미션 아이템처럼
// "그냥 항상 떠 있어야 하는" 오브젝트에 붙인다.
public class ItemHover : MonoBehaviour
{
    [SerializeField] private float bobHeight = 0.25f;   // 위/아래 각각 이동하는 폭
    [SerializeField] private float bobDuration = 1.4f;  // 한쪽 방향 이동에 걸리는 시간
    [SerializeField] private Ease bobEase = Ease.InOutSine;

    [SerializeField] private float rotationDuration = 8f; // Y축 한 바퀴(360도) 도는 데 걸리는 시간

    private void Start()
    {
        float baseY = transform.localPosition.y;

        transform.DOLocalMoveY(baseY + bobHeight, bobDuration)
            .SetEase(bobEase)
            .SetLoops(-1, LoopType.Yoyo);

        transform.DOLocalRotate(new Vector3(0f, 360f, 0f), rotationDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental);
    }

    private void OnDestroy() => transform.DOKill();
}
```

- 위아래 흔들림은 `HoverBob`과 동일한 필드 구성(`bobHeight`/`bobDuration`/`bobEase`)이라 인스펙터
  값 감이 그대로 통함.
- 회전은 `DOLocalRotate` + `RotateMode.FastBeyond360` + `LoopType.Incremental`로, 매 루프 각도를
  누적시켜 끊김/역회전 없이 계속 같은 방향으로 서서히 도는 표준 DOTween 패턴.
- `Awake`/`Update`도, 다른 컴포넌트 참조도 전혀 없음 - 아무 오브젝트에나 붙이면 그 즉시 독립적으로
  작동.

## 확인하고 싶은 점 (승인됨)

1. 이 스크립트를 새로 만들어도 될까요? (`HoverBob.cs`는 그대로 둠, `doc/0453`의 `alwaysBob` 제안은
   보류)
2. `Artifact.prefab`/`Database.prefab`의 기존 `HoverBob`을 이 `ItemHover`로 교체해드릴까요, 아니면
   스크립트만 만들어두고 프리팹 적용은 나중에 직접 하시겠어요?

사용자가 "HoverBob은 건들지말고 ItemHover만 만들어줘"로 승인함 - 스크립트만 신설, 프리팹 적용 없음.

## 구현 결과

`Assets/Scripts/Animation/ItemHover.cs` 신규 생성(제안 그대로, 수정 없음). `HoverBob.cs`/
`Artifact.prefab`/`Database.prefab`은 전혀 건드리지 않음.

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일 - 새 경고 없음).
- Unity 콘솔 Error 0건.

## 변경된 파일

- `Assets/Scripts/Animation/ItemHover.cs` (신규)
