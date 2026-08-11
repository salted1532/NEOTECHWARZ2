# 0520. 자원 노드 크기 축소에 맞춰 내부 조명도 함께 줄어들도록

**날짜:** 2026-08-11

## 요청 내용

> 자원 작아지면 안에 light도 크기가 줄어서 더 적게 빛나도록
> 그 밝기값인가 그것을 줄이던지 하면 될듯

## 조사 내용

- `ResourceNode.cs`는 채취량이 1/4씩 줄 때마다(`ShrinkByRemainingRatio()`) `visualRoot`(광물 매쉬가
  들어있는 자식 `Transform`)의 스케일을 0.2씩 줄여서 오브젝트가 점점 작아지는 것처럼 보이게 한다.
- `Ore.prefab`/`Gas.prefab`을 열어보니, 광물 안에서 빛나는 `Point Light`(`Light` 컴포넌트, Intensity 5 /
  Range 2)가 **`visualRoot`의 자식이 아니라 형제(sibling)**로 따로 붙어있다:
  ```
  Ore (루트)
  ├─ MiniMapIcon
  ├─ Ore_prefab   ← visualRoot (여기만 스케일이 줄어듦)
  ├─ Marker
  └─ Point Light  ← visualRoot 밖이라 스케일 축소의 영향을 전혀 안 받음
  ```
  그래서 광물 매쉬는 점점 작아지는데 안의 빛은 처음 크기(밝기/범위) 그대로 남아있던 것 - 요청하신
  증상과 정확히 일치.

## 계획된 수정

**`ResourceNode.cs`**
- `resourceMarker`/`minimapIcon`과 같은 패턴으로 `[SerializeField] private Light resourceLight;` 필드
  추가.
- `Awake()`에서 `visualRoot`의 초기 Y 스케일과 `resourceLight`의 초기 `intensity`/`range`를 캐싱.
- `ShrinkByRemainingRatio()`가 매 단계 `visualRoot`를 줄일 때, 그 시점의 "줄어든 비율"(새 Y스케일 ÷
  초기 Y스케일)을 그대로 `resourceLight.intensity`/`range`에도 곱해서 적용 - 광물이 작아지는 만큼 빛도
  약해지고 범위도 좁아짐.

```csharp
[SerializeField] private Light resourceLight; // 자원 안의 조명 - 크기가 줄어드는 비율만큼 밝기/범위도 함께 줄인다

...

private float initialVisualScaleY;
private float initialLightIntensity;
private float initialLightRange;

private void Awake()
{
    initialAmount = remainingAmount;
    transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);

    if (visualRoot != null)
        initialVisualScaleY = visualRoot.localScale.y;

    if (resourceLight != null)
    {
        initialLightIntensity = resourceLight.intensity;
        initialLightRange = resourceLight.range;
    }
}
```

```csharp
// ShrinkByRemainingRatio() while 루프 안, 기존 visualRoot.localPosition 조정 바로 다음
if (resourceLight != null && initialVisualScaleY > 0f)
{
    float ratio = newY / initialVisualScaleY;
    resourceLight.intensity = initialLightIntensity * ratio;
    resourceLight.range = initialLightRange * ratio;
}
```

**`Ore.prefab` / `Gas.prefab`** - `ResourceNode` 컴포넌트의 새 `resourceLight` 필드를 각자의 `Point Light`
컴포넌트에 연결(에셋 값만 추가, 계층 구조는 그대로).

## 변경 예정 파일

- `Assets/Scripts/Resource/ResourceNode.cs`
- `Assets/prefabs/Resource/Ore.prefab`
- `Assets/prefabs/Resource/Gas.prefab`

---

## 적용 (사용자 승인 후)

> 네, 진행

제안대로 적용함.

### `ResourceNode.cs`

```diff
     [SerializeField] private Transform visualRoot; // 축소 애니메이션을 적용할 그래픽 전용 자식 (직접 연결)
+    [SerializeField] private Light resourceLight; // 자원 안의 조명 - 크기가 줄어드는 비율만큼 밝기/범위도 함께 줄인다 (doc/0520)
     ...
     private int initialAmount;
     private int consumedQuarters; // 지금까지 줄어든 구간 수 (0~4)
+
+    private float initialVisualScaleY;
+    private float initialLightIntensity;
+    private float initialLightRange;
     ...
     private void Awake()
     {
         initialAmount = remainingAmount;
         transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);
+
+        if (visualRoot != null)
+            initialVisualScaleY = visualRoot.localScale.y;
+
+        if (resourceLight != null)
+        {
+            initialLightIntensity = resourceLight.intensity;
+            initialLightRange = resourceLight.range;
+        }
     }
     ...
             visualRoot.localPosition -= new Vector3(0f, appliedYShrink, 0f);
+
+            if (resourceLight != null && initialVisualScaleY > 0f)
+            {
+                float ratio = newY / initialVisualScaleY;
+                resourceLight.intensity = initialLightIntensity * ratio;
+                resourceLight.range = initialLightRange * ratio;
+            }
         }
```

### `Ore.prefab` / `Gas.prefab`

각 `ResourceNode` 컴포넌트에 `resourceLight` 필드 한 줄 추가 - 자기 `Point Light` 컴포넌트에 연결.

## 검증

- `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0`.

## 변경된 파일

- `Assets/Scripts/Resource/ResourceNode.cs`
- `Assets/prefabs/Resource/Ore.prefab`
- `Assets/prefabs/Resource/Gas.prefab`
