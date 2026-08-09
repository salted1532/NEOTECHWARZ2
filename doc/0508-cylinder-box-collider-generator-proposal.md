# 0508. 실린더 콜리더 생성기 (BoxCollider 360도 배치) 제안

**날짜:** 2026-08-09

## 요청 내용

> 실린더 콜리더를 만들려고하는데 박스를 360도 방향으로 박스 콜리더를 배치시켜서 박스의 개수, 박스의 크기를 조절할 수 있는 스크립트를 만들어줘

## 조사 내용

- Unity에는 `CylinderCollider`가 기본 제공되지 않아서, 원기둥 형태를 근사할 때 흔히 쓰는 방법이 `BoxCollider`를 원 둘레에 여러 개 회전 배치하는 방식이다.
- 프로젝트 내에 이미 유사한 콜리더 생성/에디터 유틸리티 스크립트는 없음 (`Assets/Scripts/CaptureSystem` 등 관련 폴더 확인함, 재사용할 기존 코드 없음).
- 요구사항: (1) 박스 개수 조절, (2) 박스 크기 조절, (3) 360도 원형 배치. 별도의 커스텀 에디터 창 없이, Unity 기본 제공 기능인 `[ContextMenu]`(인스펙터 우클릭 메뉴)로 생성/초기화 버튼을 제공하면 충분함 — 커스텀 Editor 클래스 불필요.

## 계획된 코드 변경 (신규 파일)

신규 파일: `Assets/Scripts/Utility/CylinderBoxColliderGenerator.cs`

**신규 코드:**
```csharp
using UnityEngine;

// 실린더 형태의 콜리더를 BoxCollider 여러 개로 근사한다. Unity에는 CylinderCollider가 없어서
// 박스를 360도 방향으로 원형 배치해 대체한다. 인스펙터에서 컴포넌트 우클릭 →
// Generate Cylinder Collider 로 생성, Clear Cylinder Collider 로 제거.
public class CylinderBoxColliderGenerator : MonoBehaviour
{
    [Header("배치")]
    [SerializeField] private float radius = 1f;
    [SerializeField, Min(3)] private int boxCount = 8;

    [Header("박스 크기 (x=접선 방향 폭, y=높이, z=반지름 방향 두께)")]
    [SerializeField] private Vector3 boxSize = new Vector3(0.5f, 2f, 0.3f);

    [SerializeField] private bool isTrigger = false;

    [ContextMenu("Generate Cylinder Collider")]
    private void Generate()
    {
        Clear();

        for (int i = 0; i < boxCount; i++)
        {
            float angle = 360f / boxCount * i;
            var rotation = Quaternion.Euler(0f, angle, 0f);
            var position = rotation * (Vector3.forward * radius);

            var segment = new GameObject($"BoxSegment_{i}");
            segment.transform.SetParent(transform, false);
            segment.transform.localPosition = position;
            segment.transform.localRotation = rotation;

            var box = segment.AddComponent<BoxCollider>();
            box.size = boxSize;
            box.isTrigger = isTrigger;
        }
    }

    [ContextMenu("Clear Cylinder Collider")]
    private void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (!child.name.StartsWith("BoxSegment_")) continue;

            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
```

**동작 방식:**
- 오브젝트에 `CylinderBoxColliderGenerator` 컴포넌트를 붙이고, `radius`(원기둥 반지름) / `boxCount`(박스 개수) / `boxSize`(박스 x=폭, y=높이, z=두께) / `isTrigger`를 인스펙터에서 조절한다.
- 컴포넌트 우클릭(또는 ⋮ 메뉴) → **Generate Cylinder Collider** 실행 시, `boxCount`개의 자식 오브젝트(`BoxSegment_0`, `BoxSegment_1`, ...)가 원 둘레에 균등한 각도로 배치되고 각각 `BoxCollider`가 붙는다. 각 박스는 바깥쪽을 향하도록 자동 회전된다.
- **Clear Cylinder Collider**로 생성된 박스들만 골라서 제거 가능 (재생성 시 자동 호출됨).
- 박스 개수/크기를 바꾼 뒤 다시 Generate를 누르면 기존 것을 지우고 새로 만든다 (덮어쓰기).

## 요약 / 영향받는 파일

- 신규 파일 1개만 추가: `Assets/Scripts/Utility/CylinderBoxColliderGenerator.cs`
- 기존 파일 변경 없음 (프리팹이나 씬에 직접 적용하려면 사용자가 컴포넌트를 붙이고 버튼을 눌러야 함 — 자동 적용 안 함).
- **스킵한 것:** 자동 폭 계산(원 둘레/개수로 박스 폭을 자동 맞추는 로직), 커스텀 에디터 창/버튼 UI, 실린더 캡(위아래 뚜껑) 콜리더. 필요하면 요청 시 추가.

---

## 확인 요청

위 내용대로 `Assets/Scripts/Utility/CylinderBoxColliderGenerator.cs` 파일을 새로 만들어도 될까요?
