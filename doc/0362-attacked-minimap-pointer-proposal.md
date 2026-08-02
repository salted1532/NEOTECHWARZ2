# 0362 — 제안: 아군/건물이 공격받으면 Attacked_MiniMapPointer를 공격받은 위치 Y=40에 스폰

**날짜:** 2026-08-02

## 요청

"아군, 건물 공격 받았을 시 미니맵에 표시를 추가할건데 공격 받은 위치를 알아내서 그 위치의 y값 40에다가
내가 만든 Attacked_MiniMapPointer이 생성 되도록 해줘 그리고 한 3초 있다가 사라지도록"

## 조사

`Assets/prefabs/UI/Attacked_MiniMapPointer.prefab` 확인 — 레이어 12(Indicators), X축 -90도로 눕혀서
탑다운으로 보이게 되어 있고, 자식으로 빨간색 파티클 링 이펙트(`Order Select` 프리팹을 붉게 재활용)가
붙어있다. 지금까지 만든 미니맵 마커(Circle/MiniMapIcon, y40/50)와 같은 "탑다운에서만 보이는 3D 오브젝트"
패턴이라 자연스럽게 어울림.

"아군/건물이 [적에게] 공격받으면 미니맵에 알린다"는 기능은 이미 있다(doc/0349) —
`HealthManager.OnDamaged` → `UnitAudio.HandleDamaged`/`BuildingAudio.HandleDamaged` →
`isEnemyAttacker`(아군사격 제외)이면 `MinimapAlertController.Instance.ShowAttackPing(transform)`으로
미니맵 UI에 반투명 핑을 띄운다. **이번 요청은 그 핑과 별개로, 3D 월드스페이스 프리팹을 추가로 스폰하는
것**이므로 같은 자리(같은 이벤트, 같은 `isEnemyAttacker` 필터)에 새 호출만 얹으면 된다.

`transform.position`(공격받은 유닛/건물 자기 자신의 위치)을 쓴다 - `ShowAttackPing(transform)`도 같은
값을 쓰고 있고, "공격받은 위치"는 공격이 날아온 진원지(공격자 위치, `attackerPosition` 파라미터)가
아니라 맞은 대상의 위치를 뜻하는 게 자연스럽다.

기존 핑/경고음은 `!SoundManager.IsWorldPositionOnScreen(...)`(화면 밖일 때만)이라는 조건이 더 붙어있는데,
이번 3D 마커는 미니맵 전용 표시라 화면에 보이고 있어도 상관없이 매번 띄우는 게 맞다고 판단해서 그
조건은 빼고 `isEnemyAttacker`만 검사하도록 제안한다.

## 제안

**`Assets/Scripts/Camera/MinimapAlertController.cs`** — 기존 핑 시스템과 나란히 새 스폰 메서드 추가:

```diff
     [SerializeField] private RectTransform minimapRect;
     [SerializeField] private Camera minimapCamera;
     [SerializeField] private Color pingColor = Color.red;
     [SerializeField] private float pingSize = 18f;
     [SerializeField] private float pingDuration = 2.5f; // 핑이 갱신 없이 유지되는 시간(초)
+
+    [Header("공격받은 위치 3D 마커 (미니맵 카메라 전용, doc/0362)")]
+    [SerializeField] private GameObject attackedPointerPrefab;
+    [SerializeField] private float attackedPointerHeight = 40f;
+    [SerializeField] private float attackedPointerLifetime = 3f;
```

```diff
+    // 공격받은 위치(대상 자신의 위치) Y=40에 3D 마커를 스폰하고 일정 시간 뒤 자동 파괴한다.
+    // UI 핑(ShowAttackPing)과 별개 - 화면 안/밖 여부와 무관하게 항상 뜬다.
+    public void SpawnAttackedPointer(Vector3 attackedPosition)
+    {
+        if (attackedPointerPrefab == null)
+            return;
+
+        Vector3 pos = new Vector3(attackedPosition.x, attackedPointerHeight, attackedPosition.z);
+        GameObject instance = Instantiate(attackedPointerPrefab, pos, attackedPointerPrefab.transform.rotation);
+        Destroy(instance, attackedPointerLifetime);
+    }
```

**`Assets/Scripts/Audio/UnitAudio.cs`** / **`Assets/Scripts/Audio/BuildingAudio.cs`** — 기존
`HandleDamaged`에 한 줄만 추가(둘 다 동일):

```diff
     private void HandleDamaged(int amount, Vector3 attackerPosition, AttackEffectType attackType, bool isEnemyAttacker)
     {
+        if (isEnemyAttacker)
+            MinimapAlertController.Instance?.SpawnAttackedPointer(transform.position); // doc/0362
+
         if (isEnemyAttacker && !SoundManager.IsWorldPositionOnScreen(transform.position))
         {
             SoundManager.Instance?.PlayUnitUnderAttackWarning();
             MinimapAlertController.Instance?.ShowAttackPing(transform); // doc/0349
         }
     }
```

## 확인 필요 사항

- `MinimapAlertController`(씬에 이미 있는 싱글턴)의 인스펙터에 `attackedPointerPrefab`으로
  `Attacked_MiniMapPointer.prefab`을 연결하는 작업 — 제가 씬 파일을 직접 찾아 연결할지, 사용자가
  직접 하실지
- 화면 안에 있어도(이미 보고 있는 전투) 매번 뜨게 하는 것으로 이해했는데 맞는지, 아니면 기존 핑처럼
  화면 밖일 때만 뜨길 원하시는지
- "공격받은 위치" = 공격받은 대상 자신의 위치로 해석했는데 맞는지 (공격자 위치가 아님)

## 발견: MinimapAlertController가 씬 어디에도 부착돼 있지 않았음

사용자 질문("MinimapAlertController이거 어디 존재해?")에 답하려고 스크립트 GUID
(`6d02d80aa352d8f4993f067aa1335c5c`)로 전체 프로젝트를 검색했으나 자기 `.meta` 파일 외엔 어디서도
안 나옴 — **doc/0349가 만든 이 컴포넌트가 씬/프리팹 어디에도 실제로 붙어있지 않아서, `Instance`가
항상 null이고 공격 핑 기능 자체가 지금까지 런타임에서 동작한 적이 없었음**을 발견. 같은 패턴을 쓰는
`MinimapController`가 `Assets/prefabs/Game/GameManager.prefab`의 `MiniMap_image`(미니맵 RawImage)
오브젝트에 붙어있는 걸 확인하고, 같은 자리에 `MinimapAlertController`도 추가하기로 함.

## 적용 (2026-08-02)

"연결해줘" — 위에서 결정 못했던 세부사항(화면 안/밖 무관하게 항상 표시, 공격받은 대상 자신의 위치 사용)은
제안한 기본값 그대로 진행.

1. **`Assets/Scripts/Camera/MinimapAlertController.cs`**: `attackedPointerPrefab`/`attackedPointerHeight`
   (기본 40)/`attackedPointerLifetime`(기본 3) 필드와 `SpawnAttackedPointer(Vector3)` 메서드 추가(설계안
   그대로).
2. **`Assets/Scripts/Audio/UnitAudio.cs`**, **`Assets/Scripts/Audio/BuildingAudio.cs`**: `HandleDamaged()`에
   `if (isEnemyAttacker) MinimapAlertController.Instance?.SpawnAttackedPointer(transform.position);`
   한 줄씩 추가.
3. `npx uloop-cli compile` 통과 (에러 0, 경고 33개, 기존과 동일 - 신규 경고 없음).
4. **`Assets/prefabs/Game/GameManager.prefab`**: Unity 에디터를 `execute-dynamic-code`로 직접 조작해서
   (`PrefabUtility.LoadPrefabContents` → `AddComponent<MinimapAlertController>()` → `SerializedObject`로
   필드 설정 → `PrefabUtility.SaveAsPrefabAsset`) `MiniMap_image` 오브젝트에 `MinimapAlertController`를
   신규 추가하고, `minimapRect`/`minimapCamera`는 같은 오브젝트의 `MinimapController`가 쓰는 값을 그대로
   복사, `attackedPointerPrefab`은 `Attacked_MiniMapPointer.prefab`으로 연결. 실행 후 로그로 세 필드
   전부 정상 연결된 것 확인:
   `MinimapAlertController added on 'MiniMap_image'. minimapRect=MiniMap_image (RectTransform),
   minimapCamera=MiniMap_Camera (Camera), attackedPointerPrefab=Attacked_MiniMapPointer (GameObject)`

이걸로 doc/0349(공격 핑)와 doc/0362(공격받은 위치 3D 마커) 둘 다 이제 실제로 동작함 - 이전엔 doc/0349도
컴포넌트 미부착으로 죽어있었음.

## 핑 시스템 제거 (2026-08-02)

"ping color나 핑 관련된건 없애도 될거 같아 그냥 공격 받으면 Attacked Pointer Prefab 생기고 3초 있다가
없어지면 될거 같아" — doc/0349가 만든 UI 핑(`ShowAttackPing`, `pingColor`/`pingSize`/`pingDuration`,
`PingEntry`, `activePings`/`expiredBuffer`, `Update()`)을 전부 제거하고 `SpawnAttackedPointer()`만
남김. `minimapRect`/`minimapCamera` 필드도 핑 시스템에서만 쓰이던 값이라 같이 제거(더 이상 쓰는 곳 없음 -
`SpawnAttackedPointer`는 두 값 다 필요 없음).

- **`Assets/Scripts/Camera/MinimapAlertController.cs`**: `Instance`/`Awake()`/`attackedPointerPrefab`
  필드 3개(`attackedPointerPrefab`/`attackedPointerHeight`/`attackedPointerLifetime`)/
  `SpawnAttackedPointer()`만 남기고 전면 재작성. `using System.Collections.Generic;`/
  `using UnityEngine.UI;`도 더 이상 안 써서 제거.
- **`Assets/Scripts/Audio/UnitAudio.cs`**, **`Assets/Scripts/Audio/BuildingAudio.cs`**:
  `MinimapAlertController.Instance?.ShowAttackPing(transform);` 호출 라인만 제거 - 경고음
  (`PlayUnitUnderAttackWarning`/`PlayBuildingUnderAttackWarning`)과 `SpawnAttackedPointer` 호출은
  그대로 유지.
- `GameManager.prefab`의 `MinimapAlertController` 컴포넌트 인스턴스는 그대로 둠 - 삭제된 필드
  (`minimapRect`/`minimapCamera`/`pingColor`/`pingSize`/`pingDuration`) 값은 프리팹 YAML에 죽은
  데이터로 남지만 Unity가 역직렬화 시 조용히 무시하므로 동작에 영향 없음(다음에 에디터에서 프리팹을
  저장하면 자동으로 정리됨).

`npx uloop-cli compile` 통과 (에러 0, 경고 33개 - 기존과 동일, 신규 경고 없음).

## 스폰 조건을 경고음 재생 조건과 통일 (2026-08-02)

"공격 받을때 계속 나타나는게 아니라 공격 받았다고 음성이 나오는 그 부분에만 포인터가 생성 되었으면
좋겠어" — 지금까진 `isEnemyAttacker`만 만족하면(화면 안이든 밖이든) 매번 스폰했는데, 이걸 경고음
(`PlayUnitUnderAttackWarning`/`PlayBuildingUnderAttackWarning`)이 재생되는 조건과 완전히 같게
맞춤 - `!SoundManager.IsWorldPositionOnScreen(transform.position)`(화면 밖일 때만) 블록 안으로
`SpawnAttackedPointer` 호출을 옮김.

```diff
-        if (isEnemyAttacker)
-            MinimapAlertController.Instance?.SpawnAttackedPointer(transform.position); // doc/0362
-
         if (isEnemyAttacker && !SoundManager.IsWorldPositionOnScreen(transform.position))
         {
             SoundManager.Instance?.PlayUnitUnderAttackWarning();
+            MinimapAlertController.Instance?.SpawnAttackedPointer(transform.position); // doc/0362
         }
```

(`BuildingAudio.cs`도 동일하게, `PlayBuildingUnderAttackWarning()` 옆으로 이동.)

`npx uloop-cli compile` 통과 (에러 0, 경고 33개 - 기존과 동일, 신규 경고 없음).

## 여전히 매번 뜨는 문제 — 경고음의 실제 재생 여부로 게이팅 (2026-08-02)

"아직도 음성재생 이후 다시 재생될때 그때 나와야하는데 현재 공격 받을때마다 계속 나오고 있어 내가 10초
간격으로 음성 재생하라고 시키지 않았나? 그부분에 핑도 생성되도록 하면 될거 같아"

원인: 화면 밖(`!IsWorldPositionOnScreen`)이라는 조건은 계속 얻어맞는 동안 매 프레임 계속 참이지만,
`PlayUnitUnderAttackWarning()`(`SoundManager.cs:297`, `underAttackWarningCooldown` 기본 10초)은
내부적으로 쿨다운 때문에 실제로는 10초에 한 번만 소리가 난다 - 그런데 예전 코드는 이 메서드를 그냥
"호출"만 하고 반환값을 안 보고 있어서, `SpawnAttackedPointer`는 쿨다운과 무관하게 화면 밖에서 맞을
때마다 매번 실행되고 있었다. 즉 "경고음이 실제로 재생된 순간"과 무관하게 동작했던 게 원인.

**`Assets/Scripts/Audio/SoundManager.cs`**: `PlayGlobalVoice`/`PlayUnitUnderAttackWarning`/
`PlayBuildingUnderAttackWarning`을 `void` → `bool`로 변경 - 겹침 방지/쿨다운으로 조용히 씹혔으면
`false`, 실제로 새로 재생을 시작했으면 `true`를 반환하도록.

```diff
-    public void PlayGlobalVoice(SoundClipSet set, float minInterval = 0f)
+    public bool PlayGlobalVoice(SoundClipSet set, float minInterval = 0f)
     {
-        if (set == null || !set.HasClips) return;
-        if (겹침) return;
-        if (쿨다운 안 지남) return;
+        if (set == null || !set.HasClips) return false;
+        if (겹침) return false;
+        if (쿨다운 안 지남) return false;

         AudioSource source = PlayFromPool(...);
-        if (source != null) { ...등록... }
+        if (source == null) return false;
+        ...등록...
+        return true;
     }
```

`PlayUnitUnderAttackWarning()`/`PlayBuildingUnderAttackWarning()`도 그 결과를 그대로 반환하도록 변경.
(다른 호출부 `PlayInsufficientResourcesWarning`/`PlayInsufficientPopulationWarning`/
`PlayUpgradeCompleteVoice`는 반환값을 안 쓰던 곳이라 `void`→`bool` 변경에 영향 없음 - C#에서 반환값
무시는 그냥 문(statement)으로 그대로 컴파일됨.)

**`Assets/Scripts/Audio/UnitAudio.cs`**, **`Assets/Scripts/Audio/BuildingAudio.cs`**: 이제 반환값을
확인해서, 실제로 경고음이 재생된 경우에만 마커를 띄운다.

```diff
         if (isEnemyAttacker && !SoundManager.IsWorldPositionOnScreen(transform.position))
         {
-            SoundManager.Instance?.PlayUnitUnderAttackWarning();
-            MinimapAlertController.Instance?.SpawnAttackedPointer(transform.position); // doc/0362
+            // 쿨다운에 안 걸리고 실제로 경고음이 새로 재생된 순간에만 미니맵 마커도 같이 띄운다 -
+            // 계속 얻어맞아도 경고음처럼 10초 간격으로만 나오게.
+            if (SoundManager.Instance != null && SoundManager.Instance.PlayUnitUnderAttackWarning())
+                MinimapAlertController.Instance?.SpawnAttackedPointer(transform.position);
         }
```

`npx uloop-cli compile` 통과 (에러 0, 경고 33개 - 기존과 동일, 신규 경고 없음).
