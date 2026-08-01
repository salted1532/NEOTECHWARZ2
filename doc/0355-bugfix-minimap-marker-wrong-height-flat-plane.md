# 0355 — 버그수정(제안): 미니맵 명령 마커가 안 보이는 진짜 원인 - Y=0 평면 교차라 지형 높이가 안 맞음

**날짜:** 2026-08-01

## 정정

[[0354-minimap-order-marker-visible-when-camera-arrives-proposal]]에서 "카메라가 도착하기 전에 3초 타이머로
사라진다"고 진단한 건 사용자가 재현한 증상과 다름 — **지금 카메라가 보고 있는 화면 안(이동/카메라 이동 없이 바로
보이는 곳)에 미니맵으로 명령을 내려도 마커가 안 보인다**("아예 생성을 안 하는 것 같다"). 이건 타이밍 문제가 아니라
애초에 마커가 잘못된 위치(높이)에 스폰되는 문제로 재조사함. **0354는 폐기.**

## 원인 확인

`Assets/Scripts/Camera/MinimapController.cs`의 `TryGetGroundPoint()`:

```csharp
Ray ray = minimapCamera.ViewportPointToRay(new Vector3(u, v, 0f));
Plane groundPlane = new Plane(Vector3.up, Vector3.zero);   // ← 딱 Y=0인 수학적 평면

if (!groundPlane.Raycast(ray, out float distance))
    return false;

groundPoint = ray.GetPoint(distance);
```

미니맵 클릭의 월드 좌표를 **실제 지형 콜라이더에 레이캐스트하지 않고, Y=0 평면과의 수학적 교차점**으로 구한다.
반면 메인 화면 클릭(`UserControl.cs`의 `HandleLeftClick()`/`HandleRightClick()`/`UpdatePointer()`, 259/487/949줄)은
전부 `Physics.Raycast(ray, ..., layerGround)`로 **실제 지형 콜라이더**에 레이캐스트해서 진짜 지면 높이를 구한다.

맵 지형이 정확히 Y=0 평면이 아닌 곳(경사로, 언덕, 고저차가 있는 지형 등 이 프로젝트에 이미 흔함 - 램프 관련 문서
[[0031-ramp-unit-jitter-investigation]] 등 참고)에서는 미니맵으로 명령을 내리면 마커가 **실제 지면보다 높거나 낮은
Y좌표**에 스폰된다. 지형보다 낮으면 마커가 땅속에 파묻혀 완전히 안 보이고, 지형보다 높으면 허공에 떠서 눈에 안 띌
수 있다 — 어느 쪽이든 사용자 입장에선 "생성을 안 하는 것 같다"로 보인다. (유닛 자체는 이동 목적지의 XZ만 대략
맞으면 NavMesh가 알아서 실제 지면에 스냅해서 이동하므로, 이동 자체는 정상 동작했던 것 - 마커만 잘못된 높이.)

## 제안 수정

메인 화면과 동일하게 **실제 지형 콜라이더**에 레이캐스트하도록 바꾼다. `UserControl`에 이미 있는 `layerGround`
필드를 재사용해서(새 인스펙터 필드를 또 추가하면 방금 [[0353-bugfix-minimap-click-userControl-unwired]]에서 겪은
"연결 깜빡함" 실수가 반복될 위험이 있음 - 이미 연결돼 있는 `userControl` 참조를 통해 얻는다).

**`Assets/Scripts/UserControl/UserControl.cs`**: `layerGround`를 읽을 수 있는 public 프로퍼티 추가.

```diff
     [SerializeField]
     private LayerMask layerGround;
+
+    public LayerMask GroundLayerMask => layerGround; // 미니맵 클릭의 지형 레이캐스트에서 재사용 (doc/0355)
```

**`Assets/Scripts/Camera/MinimapController.cs`**: `TryGetGroundPoint()`가 평면 교차 대신 실제 지형 레이캐스트를
먼저 시도하고, 혹시 지형 콜라이더가 없는 예외적인 경우에만 기존 Y=0 평면 계산으로 대체(안전망):

```diff
     private bool TryGetGroundPoint(PointerEventData eventData, out Vector3 groundPoint)
     {
         groundPoint = default;

         if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                 minimapRect, eventData.position, null, out Vector2 localPoint))
             return false;

         Rect rect = minimapRect.rect;
         float u = (localPoint.x - rect.xMin) / rect.width;
         float v = (localPoint.y - rect.yMin) / rect.height;

         Ray ray = minimapCamera.ViewportPointToRay(new Vector3(u, v, 0f));
+
+        // 실제 지형 콜라이더에 레이캐스트해서 진짜 지면 높이를 구한다 - 메인 화면 클릭(UserControl)과 동일한 방식.
+        if (userControl != null && Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, userControl.GroundLayerMask))
+        {
+            groundPoint = hit.point;
+            return true;
+        }
+
+        // 지형 콜라이더를 못 맞춘 경우(맵 밖 등)에 대한 안전망
         Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

         if (!groundPlane.Raycast(ray, out float distance))
             return false;

         groundPoint = ray.GetPoint(distance);
         return true;
     }
```

## 적용 결과 (2026-08-01)

사용자 확인 후 제안한 diff 그대로 적용.

- **`Assets/Scripts/UserControl/UserControl.cs`**: `layerGround` 필드 바로 아래에 `public LayerMask GroundLayerMask => layerGround;` 프로퍼티 추가.
- **`Assets/Scripts/Camera/MinimapController.cs`**: `TryGetGroundPoint()`에서 `minimapCamera.ViewportPointToRay()`로 만든 광선을 `userControl.GroundLayerMask`로 실제 지형 콜라이더에 먼저 레이캐스트하도록 변경, 실패 시(맵 밖 등)에만 기존 Y=0 평면 계산으로 대체.
- `npx uloop-cli compile` 통과 (에러 0, 경고 27개 — 전부 이번 변경과 무관한 기존 경고, 신규 경고 없음).

**확인 필요 사항**: Unity 에디터에서 언덕/경사로처럼 Y=0이 아닌 지형 위치를 미니맵으로 우클릭/A공격 지정해서, 마커가
바로 그 지형 표면에 정확히 붙어서 보이는지 확인 부탁.
