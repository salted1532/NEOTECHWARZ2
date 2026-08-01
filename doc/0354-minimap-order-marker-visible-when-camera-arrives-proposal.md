# 0354 — 제안: 미니맵 명령 마커가 카메라를 옮겨서 확인할 때까지 사라지지 않게

**날짜:** 2026-08-01

## 요청

"이제 잘 작동은 하는데 미니맵으로 유닛에게 명령을 내리면 그에 맞는 마커가 위치 땅에 스폰되도록 해줘 명령을 내리고
직접 확인하면 어디로 이동명령이 내려졌는지 보이도록"

## 원인 확인

이동/공격 마커(`movePointer`/`attackPointer`)는 이미 `ConfirmPendingOrderAt()`/`IssueRightClickMoveAt()`
(`Assets/Scripts/UserControl/UserControl.cs:657-728`) 안에서 `ShowMovePointer()`/`ShowAttackPointer()`를 호출해
정확한 지면 위치에 스폰된다 — 메인 화면 클릭과 미니맵 클릭이 같은 코드를 타므로([[0349-minimap-commands-mission-markers-attack-pings-proposal]]) 이미 요청한 "마커가 그 위치에 뜨는" 동작 자체는 되고 있음.

문제는 **자동 소멸 타이머**다. `ShowMovePointer()`/`ShowAttackPointer()`(`UserControl.cs:155-169`)는 마커를 켜는
즉시 `movePointerHideTime = Time.time + PointerAutoHideDuration(3초)`를 잡고, `UpdatePointerAutoHide()`(171-179줄)가
매 프레임 이 시각을 넘었으면 무조건 꺼버린다 — **화면에 실제로 보였는지 여부와 무관하게** 켜진 시점 기준 3초 뒤 소멸.

메인 화면에서 클릭할 땐 클릭한 자리가 곧 지금 보고 있는 화면 안이라 문제가 없지만, 미니맵으로 명령을 내리면 보통
카메라가 다른 곳을 보고 있는 상태다. 플레이어가 "직접 확인"하려고 카메라를 그 지점으로 옮기는 데 3초 이상 걸리면
도착했을 땐 이미 마커가 사라진 뒤라 요청한 "어디로 명령이 내려졌는지 보이는" 효과를 못 봄.

## 제안 수정

`Assets/Scripts/UserControl/UserControl.cs`: 마커가 화면에 처음 들어오기 전까지는 소멸 타이머가 흐르지 않도록,
"한 번이라도 화면에 보인 적 있는지" 플래그를 추가하고 `UpdatePointerAutoHide()`에서 화면 밖인 동안은 타이머를
계속 뒤로 미룬다. 화면 노출 여부 판정은 이미 있는 `SoundManager.IsWorldPositionOnScreen()`(doc/0292, 피격 경고에
쓰던 것과 동일 헬퍼)을 재사용한다.

```diff
     private const float PointerAutoHideDuration = 3f;
     private float movePointerHideTime = float.NegativeInfinity;
     private float attackPointerHideTime = float.NegativeInfinity;
+    private bool movePointerSeen; // 켜진 뒤 화면에 한 번이라도 들어온 적 있는지 - 아직이면 소멸 타이머를 미룸
+    private bool attackPointerSeen;

     private void ShowMovePointer(Vector3 position)
     {
         movePointer.transform.position = position;
         movePointer.SetActive(true);
         attackPointer.SetActive(false);
         movePointerHideTime = Time.time + PointerAutoHideDuration;
+        movePointerSeen = false;
     }

     private void ShowAttackPointer(Vector3 position)
     {
         attackPointer.transform.position = position;
         attackPointer.SetActive(true);
         movePointer.SetActive(false);
         attackPointerHideTime = Time.time + PointerAutoHideDuration;
+        attackPointerSeen = false;
     }

     private void UpdatePointerAutoHide()
     {
-        if (movePointer.activeSelf && Time.time >= movePointerHideTime)
-            movePointer.SetActive(false);
+        if (movePointer.activeSelf)
+        {
+            if (!movePointerSeen)
+            {
+                if (SoundManager.IsWorldPositionOnScreen(movePointer.transform.position))
+                    movePointerSeen = true;
+                else
+                    movePointerHideTime = Time.time + PointerAutoHideDuration; // 화면 밖인 동안은 계속 미룸
+            }
+
+            if (Time.time >= movePointerHideTime)
+                movePointer.SetActive(false);
+        }

-        if (attackPointer.activeSelf && Time.time >= attackPointerHideTime)
-            attackPointer.SetActive(false);
+        if (attackPointer.activeSelf)
+        {
+            if (!attackPointerSeen)
+            {
+                if (SoundManager.IsWorldPositionOnScreen(attackPointer.transform.position))
+                    attackPointerSeen = true;
+                else
+                    attackPointerHideTime = Time.time + PointerAutoHideDuration;
+            }
+
+            if (Time.time >= attackPointerHideTime)
+                attackPointer.SetActive(false);
+        }
     }
```

### 동작 확인

- **메인 화면 클릭(기존 동작)**: 클릭한 자리가 이미 화면 안이므로, 켜진 바로 다음 프레임에 `movePointerSeen=true`가
  되고 원래처럼 3초 뒤 사라짐 — 기존 체감과 사실상 동일(프레임 하나 차이).
- **조준 중(A공격 등 대기 상태에서 마우스로 조준, `UpdatePointer()`가 매 프레임 위치/타이머 갱신)**: 마우스가 가리키는
  자리는 항상 메인 카메라 화면 안이므로 곧바로 `seen=true`가 되고, 매 프레임 타이머가 갱신되는 기존 동작과 다르지 않음
  — `UpdatePointer()`는 수정하지 않음.
- **미니맵 명령(이번 요청)**: 카메라가 다른 곳을 보고 있으면 화면 밖이라 `movePointerSeen`이 계속 `false`로 남고,
  `UpdatePointerAutoHide()`가 매 프레임 소멸 시각을 뒤로 미뤄서 마커가 꺼지지 않는다. 플레이어가 카메라를 그 지점으로
  옮겨서 마커가 화면에 들어오는 순간부터 정확히 3초 카운트다운이 시작돼 사라진다 — 요청한 "직접 확인하면 보이도록"을
  그대로 만족.

## 확인 필요 사항

이 문서는 제안까지만 진행했고 아직 코드에 손대지 않았습니다. 진행해도 될지 확인 부탁드립니다.
