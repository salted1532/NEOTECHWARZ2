# 0590. 건설 중인 일꾼이 건물을 바라보게 - 제안

**날짜:** 2026-08-16

## 요청 내용

> 건설중에 건물을 쳐다보도록 할수 있어?

## 변경 계획

`ConstructionWanderTick()`(doc/0589)에 매 프레임 `attachedStructure` 쪽을 바라보게 하는 로직을
추가한다 - 공중 유닛 이동 시 진행 방향을 바라보게 하는 것과 동일한 패턴
(`Quaternion.LookRotation` + `Quaternion.Slerp`, `UnitController.cs:433~434`)을 재사용한다.
배회 이동 중/2초 대기 중 상관없이 항상 적용 - 대기 중에도 계속 건물 쪽을 향하게.

### `Assets/Scripts/Unit/UnitController.cs`

```diff
     private void ConstructionWanderTick()
     {
         if (!isConstructing || attachedStructure == null)
             return;

+        FaceConstructionStructure();
+
         if (constructionWanderWaiting)
         {
             ...
```

```diff
+    // 건설 중인 일꾼이 배회/대기 중이든 항상 건물 쪽을 바라보게 한다 - 공중 유닛 이동 방향 회전과
+    // 동일한 패턴(doc/0589).
+    private void FaceConstructionStructure()
+    {
+        Vector3 dir = attachedStructure.transform.position - transform.position;
+        dir.y = 0f;
+
+        if (dir.sqrMagnitude > 0.001f)
+        {
+            Quaternion rot = Quaternion.LookRotation(dir);
+            transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 10f);
+        }
+    }
```

## 변경 예정 파일
- `Assets/Scripts/Unit/UnitController.cs`

---

## 적용 (사용자 승인 후)

제안대로 `Assets/Scripts/Unit/UnitController.cs`에 위 diff 그대로 적용함. `npx uloop-cli compile`
성공 확인(Error 0개, Warning 0개).

## 변경된 파일
- `Assets/Scripts/Unit/UnitController.cs`
