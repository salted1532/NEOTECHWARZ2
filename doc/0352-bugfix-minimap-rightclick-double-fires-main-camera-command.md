# 0352 — 버그수정(제안): 미니맵 우클릭 시 메인 카메라 기준 명령이 같이 발행되는 문제

**날짜:** 2026-08-01

## 요청

"유닛,건물 미니맵 지도에다가 명령 내릴수 있게 우클릭, A공격등 이걸 좀 수정해야하는게 미니맵 카메라를 기준으로
월드에 좌표를 계산해서 그곳으로 이동하거나 공격하라고 명령을 내려달라는거지 그냥 지도를 무시하고 우클릭이나
공격명령이 들어가도록 하는게 아니라"

[[0349-minimap-commands-mission-markers-attack-pings-proposal]]에서 설계·구현한 미니맵 명령 자체(`MinimapController`가
`minimapCamera.ViewportPointToRay`로 미니맵 기준 월드 좌표를 구해 `UserControl.IssueRightClickMoveAt`/`ConfirmPendingOrderAt`을
호출하는 부분)는 정상 구현돼 있음. 이번 보고는 그와 별개로, 우클릭 시 **그 미니맵 명령과 동시에 메인 카메라 기준의
엉뚱한 명령이 하나 더 나가는** 문제로 확인됨.

## 원인 확인

`Assets/Scripts/UserControl/UserControl.cs`의 `HandleMouse()`(199-240줄):

- **좌클릭**(203-213줄)은 `EventSystem.current.IsPointerOverGameObject()`가 참이면(마우스가 UI 위에 있으면) `HandleLeftClick()`을 호출하지 않고 그냥 `return`한다 — 그래서 미니맵(UI)을 좌클릭하면 메인 화면 클릭 로직은 아예 안 타고, `MinimapController.OnPointerClick()`(UI 이벤트)만 단독으로 처리된다.
- **우클릭**(236-239줄)은 이 UI 오버 체크가 **없다**:
  ```csharp
  if (Input.GetMouseButtonDown(1))
  {
      HandleRightClick();
  }
  ```
  `HandleRightClick()`은 곧바로 `mainCamera.ScreenPointToRay(Input.mousePosition)`(475줄)으로 **메인 카메라 기준** 레이캐스트를 쏜다. 미니맵 위에서 우클릭하면 `Input.mousePosition`은 화면 구석(미니맵이 그려진 자리)을 가리키는데, 이 좌표를 메인 카메라가 그대로 자기 시야로 해석해버려서 실제로 보고 있는 화면과 무관한 지점으로 이동/공격 명령이 나간다.

즉 미니맵을 우클릭하면 **두 번** 명령이 나간다: (1) `MinimapController.OnPointerClick`(UI 이벤트) → `IssueRightClickMoveAt(정확한 미니맵 기준 좌표)`, (2) `UserControl.HandleMouse()`(매 프레임 `Input.GetMouseButtonDown` 폴링) → `HandleRightClick()` → 메인 카메라 기준의 엉뚱한 좌표로 또 명령. 실행 순서(둘 중 어느 게 나중에 적용되는지)는 Unity의 스크립트 실행 순서에 따라 달라서, 사용자 입장에선 "미니맵에서 우클릭했는데 엉뚱한 곳으로 간다"로 보임 — 요청 문구의 "그냥 지도를 무시하고 우클릭 명령이 들어가는" 증상과 정확히 일치.

좌클릭 A공격 확정은 이미 209줄의 UI 오버 체크 덕분에 이 문제가 없다(그래서 요청에서 우클릭 쪽을 콕 집어 말한 것으로 보임 — "A공격 등"은 좌클릭 확정 경로라 이미 안전).

## 제안 수정

`Assets/Scripts/UserControl/UserControl.cs`의 우클릭 분기에 좌클릭과 동일한 UI 오버 체크를 추가:

```diff
         // 우클릭 시
         if (Input.GetMouseButtonDown(1))
         {
+            if (EventSystem.current.IsPointerOverGameObject())
+                return;
+
             HandleRightClick();
         }
```

좌클릭에서 이미 검증된 동일한 패턴(`EventSystem.current.IsPointerOverGameObject()`)을 그대로 재사용하는 최소 diff.
`HandleMouse()`에서 이 블록이 마지막 문장이라 `return`이 다른 로직을 건너뛰지 않음.

## 적용 결과 (2026-08-01)

사용자 확인 후 제안한 diff 그대로 적용.

- **`Assets/Scripts/UserControl/UserControl.cs`**: `HandleMouse()`의 우클릭 분기에 좌클릭과 동일한
  `EventSystem.current.IsPointerOverGameObject()` 체크를 추가해, 마우스가 UI(미니맵 포함) 위에 있으면
  `HandleRightClick()`(메인 카메라 기준 레이캐스트)을 아예 호출하지 않도록 함.
- `npx uloop-cli compile` 통과 (에러 0, 경고 27개 — 전부 이번 변경과 무관한 기존 경고, 신규 경고 없음).

**확인 필요 사항**: Unity 에디터에서: (1) 미니맵 우클릭 시 미니맵 기준 좌표로만 이동/추적 명령이 나가는지, (2) 메인
게임 화면(3D 뷰) 우클릭은 기존과 동일하게 정상 동작하는지, (3) 다른 UI(생산 패널 등) 위에서 우클릭했을 때도 메인
화면 명령이 안 나가는지(부수 효과로 함께 좋아짐) 확인 부탁.
