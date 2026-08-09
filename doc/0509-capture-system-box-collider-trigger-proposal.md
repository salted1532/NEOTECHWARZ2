# 0509. 점령 시스템이 생성된 박스 콜리더를 트리거로 사용하도록 연결

**날짜:** 2026-08-09

## 요청 내용

> 점령시스템이 해당 생성된 박스 콜리더들을 거점 점령 트리거 콜리더로 사용하도록해줘

([[0508-cylinder-box-collider-generator-proposal]]에서 만든 생성기로, `Capture_Point` 프리팹에 이미 `CylinderBoxColliderGenerator`를 붙이고 `BoxSegment_0`~`BoxSegment_7` 8개를 생성해둔 상태 — Unity 에디터에서 직접 작업하신 것으로 보임.)

## 조사 내용

- `Assets/prefabs/Capture_Point/Capture_Point.prefab` 확인 결과, 기존에는 `Capture_Point` 오브젝트 **자기 자신**에 `SphereCollider`(Is Trigger, radius 10)가 직접 붙어 있었고, `CaptureSystem.OnTriggerEnter/OnTriggerExit`가 이 콜라이더로 유닛 출입을 감지했다.
- 지금은 이 `SphereCollider`가 지워지고 대신 `BoxSegment_0`~`_7` **자식 오브젝트**들에 각각 `BoxCollider`(Is Trigger)가 생성되어 있다.
- 문제: Unity 물리 엔진은 트리거 콜라이더가 **부모 오브젝트의 Rigidbody 없이 자식에 붙어 있으면**, `OnTriggerEnter/Exit`를 그 자식 오브젝트 자신의 스크립트로만 보낸다. `Capture_Point`(부모, `CaptureSystem`이 붙어있는 곳)에는 전달되지 않는다.
  - 자식 콜라이더들을 "부모의 복합 콜라이더(compound collider)"로 묶어서 이벤트가 부모(`CaptureSystem`)로 전달되게 하려면, **부모 오브젝트에 `Rigidbody`가 있어야 한다.** (물리적으로 움직일 필요는 없으므로 `Kinematic`으로 고정.)
  - 현재 `Capture_Point`에는 `Rigidbody`가 없음 → 지금 상태로는 유닛이 박스 안에 들어와도 `CaptureSystem`이 전혀 감지하지 못함 (점령이 진행되지 않는 버그).
- 근본 원인은 "생성기로 만든 자식 콜라이더에는 Rigidbody가 없다"는 것이므로, `CaptureSystem` 쪽을 고치는 대신 **생성기(`CylinderBoxColliderGenerator`) 자체가 생성 시 자동으로 부모에 Kinematic Rigidbody를 붙이도록** 고치는 게 근본적인 수정이다. 이렇게 하면 이 생성기를 다른 오브젝트에 써도 항상 트리거가 정상 동작한다.

## 계획된 코드 변경

파일: `Assets/Scripts/Utility/CylinderBoxColliderGenerator.cs`

**기존 코드:**
```csharp
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
```

**변경 코드:**
```csharp
    [ContextMenu("Generate Cylinder Collider")]
    private void Generate()
    {
        Clear();
        EnsureRigidbody();

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

    // 자식 BoxCollider의 트리거 이벤트가 부모(이 오브젝트)의 스크립트(CaptureSystem 등)로 전달되려면
    // 부모에 Rigidbody가 있어야 한다 (Unity 복합 콜라이더 규칙). 움직일 필요는 없으니 Kinematic으로 고정.
    private void EnsureRigidbody()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }
```

## 적용 대상: `Capture_Point` 프리팹

코드 변경 후, `Capture_Point` 프리팹에서 **Generate Cylinder Collider를 한 번 더 실행**하면(기존 8개 박스를 지우고 같은 설정으로 재생성 + 이번엔 Rigidbody도 자동으로 붙음) 별도 수작업 없이 바로 반영된다. (에디터가 열려 있으므로 확인 주시면 제가 직접 재실행해서 프리팹까지 반영하겠습니다.)

## 요약 / 영향받는 파일

- `Assets/Scripts/Utility/CylinderBoxColliderGenerator.cs`: `EnsureRigidbody()` 추가, `Generate()`에서 호출.
- `Assets/prefabs/Capture_Point/Capture_Point.prefab`: 위 재생성을 통해 `Capture_Point`에 `Rigidbody`(Kinematic) 컴포넌트 추가됨 (박스 8개는 동일하게 재생성).
- **스킵한 것:** `CaptureSystem.cs` 자체는 변경 없음 (이미 `OnTriggerEnter/Exit` 로직은 올바르게 되어 있었고, 문제는 콜라이더 쪽 설정이었음).

---

## 확인 요청

위 코드 변경을 적용하고, `Capture_Point` 프리팹에서 재생성해서 Rigidbody를 붙여도 될까요?
