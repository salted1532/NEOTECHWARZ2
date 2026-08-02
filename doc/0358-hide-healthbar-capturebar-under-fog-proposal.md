# 0358 — 제안: 체력바 / 점령지 타이머 슬라이더가 Fog of War로 가려진 곳에서는 안 보이게

**날짜:** 2026-08-02

## 요청

"유닛의 체력바, 점령지 타이머 슬라이더 등이 fog of war로 인해 가려진 경우 안보이도록 해줘"

## 원인

`doc/0356`(미니맵 마커)에서 확인한 것과 완전히 같은 원인이다. 이 프로젝트의 안개는 지면 바로 위
(Y ≈ `levelMidPoint.y + 1`, `GameManager.prefab`의 `fogPlaneHeight: 1`)에 있는 **실제 3D Plane**
이라서, 카메라(위쪽 어딘가)에서 어떤 지점까지 일직선으로 봤을 때 그 지점의 Y가 안개 평면 Y보다
**높으면** 시선이 안개 평면 아래로 내려갈 일이 없어 절대 가려지지 않는다 — 이건 미니맵(탑다운
오소그래픽)만의 특성이 아니라 메인 카메라(원근 투영, 각도 있음)에도 그대로 적용되는 순수 기하학적
사실이다(카메라가 안개 평면보다 높이 있는 한, 평면보다 높은 지점까지의 직선 시야는 평면과 절대
안 만난다).

- 체력바(`Assets/prefabs/UI/HealthBar.prefab`)는 유닛 로컬 Y=2에 World Space Canvas로 떠 있음 →
  안개 평면(Y≈1)보다 높아서 절대 안 가려짐.
- 점령지 타이머(`CaptureSystem.captureBar`)도 마찬가지로 지면보다 띄워서 배치되므로 동일하게 안 가려짐.

즉 유닛 메쉬 몸체(지면에 붙어 있어 Y가 낮음)만 우연히 안개 평면보다 낮아서 가려지는 것이고, 그 위에
뜬 UI는 전부 구조적으로 가려질 수 없다. 미니맵 마커와 동일하게, 물리적 가림 대신 **안개 상태를 직접
조회해서 UI를 켜고 끄는 방식**으로 가야 한다.

## 제안: 안개 조회 로직을 공용 헬퍼로 뽑아서 재사용

지금 이 로직(안개 상태 조회)이 이미 세 곳에 사실상 같은 형태로 있다: `UserControl.IsRevealedByFog()`,
`EnemyUnitController.IsRevealedByFog()`(doc/0356에서 추가). 체력바/점령 타이머까지 추가하면 네 번째,
다섯 번째 중복이 생기므로, 이번엔 공용 정적 헬퍼로 뽑아서 재사용한다.

### 신규 파일: `Assets/Scripts/FogOfWar/FogVisibility.cs`

```csharp
using UnityEngine;
using FischlWorks_FogWar;

// 월드 좌표가 지금 안개에 가려져 있는지(Revealed/PreviouslyRevealed면 보임) 조회하는 공용 헬퍼.
// UserControl.IsRevealedByFog()/EnemyUnitController의 동일 로직을 한 곳으로 모음 (doc/0356/0358).
public static class FogVisibility
{
    public static bool IsRevealed(csFogWar fogWar, Vector3 worldPosition, int margin = 1)
    {
        if (fogWar == null) return true; // 안개가 없는 씬에서는 항상 보이는 것으로 취급

        Vector2Int center = fogWar.WorldToLevel(worldPosition);

        for (int x = -margin; x <= margin; x++)
        {
            for (int y = -margin; y <= margin; y++)
            {
                Vector2Int cell = new Vector2Int(center.x + x, center.y + y);
                if (!fogWar.CheckLevelGridRange(cell)) continue;

                var visibility = fogWar.shadowcaster.fogField[cell.x][cell.y];
                if (visibility == Shadowcaster.LevelColumn.ETileVisibility.Revealed ||
                    visibility == Shadowcaster.LevelColumn.ETileVisibility.PreviouslyRevealed)
                    return true;
            }
        }
        return false;
    }
}
```

`EnemyUnitController.IsRevealedByFog()`(private, 이번에 새로 추가했던 것)는 이 헬퍼 호출로 교체한다
(동작 동일, 중복 제거). `UserControl.IsRevealedByFog()`는 안 건드림 — 이미 잘 동작 중인 클릭/커서 판정
로직이라 이번 범위에서 굳이 손 안 댐(리스크 대비 이득이 적음).

### `Assets/Scripts/Unit/HealthManager.cs` — 아군/적 구분 없이 공통 적용

아군 유닛은 자기 시야가 항상 자기 위치를 밝히므로 이 체크를 넣어도 실질적으로 계속 보임 — 아군/적을
구분하는 별도 분기 없이 `HealthManager` 한 곳에만 넣으면 유닛/건물 전부(아군 포함) 자동으로 처리됨.

```diff
+    private csFogWar fogWar;
+
     private void Awake()
     {
         currentHp = maxHealth;
+        fogWar = FindFirstObjectByType<csFogWar>();

         OnHealthChanged += UpdateHealthSlider;
         UpdateHealthSlider(currentHp, maxHealth);
     }
+
+    // 체력 변화가 없어도 안개 상태는 계속 바뀌므로(유닛이 안 움직여도 다른 아군이 시야를 뺏어가는 등)
+    // 매 프레임 다시 확인한다 - 체력 조건(풀피면 원래도 숨김)과 안개 조건을 함께 만족해야 보임.
+    private void Update()
+    {
+        if (healthSlider == null) return;
+        if (currentHp >= maxHealth) return; // 풀피면 기존 규칙대로 숨김 - 안개 체크할 필요도 없음
+
+        healthSlider.gameObject.SetActive(FogVisibility.IsRevealed(fogWar, transform.position));
+    }
```

(`SetHealthBarVisible(false)`로 강제로 숨기는 프리뷰/고스트는 항상 풀피로 시작하므로 `currentHp >=
maxHealth`에서 바로 걸러져 이 Update()가 그걸 다시 켜는 일은 없음.)

### `Assets/Scripts/CaptureSystem/CaptureSystem.cs` — `progressing` 조건에 추가

```diff
+    private csFogWar fogWar;
+
     private void Awake()
     {
         if (territoryZone == null) territoryZone = GetComponentInChildren<TerritoryZone>(true);
+        fogWar = FindFirstObjectByType<csFogWar>();
         ...
     }
```

```diff
         bool progressing = !contested && (alliesPresent || enemiesPresent || returningToRest)
             && !(alliesPresent && controlValue >= captureDuration)
-            && !(enemiesPresent && controlValue <= -captureDuration);
+            && !(enemiesPresent && controlValue <= -captureDuration)
+            && FogVisibility.IsRevealed(fogWar, transform.position);
```

## 확인 필요 사항

- 위 방식(공용 `FogVisibility` 헬퍼 + `HealthManager`/`CaptureSystem`에 매 프레임 안개 체크 추가)으로
  진행해도 되는지
- 체력바는 아군/적 구분 없이 `HealthManager` 한 곳에만 적용(아군은 자기 시야로 항상 보임) — 이 방식이
  맞는지, 아니면 혹시 "아군 체력바는 절대 안개 체크 없이 무조건 항상 보이게"처럼 명시적으로 분리하고
  싶으신지
- 점령지 타이머는 거점 오브젝트 자체의 `transform.position` 기준으로 안개를 조회함(거점이 넓으면
  중심점 기준) — 이걸로 충분한지

## 적용 (2026-08-02)

"둘다 적용되도 좋을거 같아 안개 조회 로직을 둘다 추가해줘" — 위 설계 그대로 진행.

- **`Assets/Scripts/FogOfWar/FogVisibility.cs`**(신규): 설계안 그대로 `IsRevealed(fogWar, worldPosition,
  margin=1)` 정적 헬퍼 추가.
- **`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`**: 자체 `IsRevealedByFog()` private 메서드를
  삭제하고 `UpdateMinimapIconVisibility()`가 `FogVisibility.IsRevealed(fogWar, transform.position,
  minimapFogVisibilityMargin)`를 직접 호출하도록 교체(동작 동일, 중복 제거).
- **`Assets/Scripts/Unit/HealthManager.cs`**: `fogWar` 필드 추가(`Awake()`에서 캐싱), `Update()`
  신규 추가 — `healthSlider`가 있고 풀피가 아닐 때만 `FogVisibility.IsRevealed(fogWar,
  transform.position)`로 표시 여부를 매 프레임 갱신. 아군/적 구분 없이 적용(설계안대로 아군은 자기
  시야로 항상 보임).
- **`Assets/Scripts/CaptureSystem/CaptureSystem.cs`**: `fogWar` 필드 추가(`Awake()`에서 캐싱),
  `UpdateCaptureBar()`의 `progressing` 조건에 `&& FogVisibility.IsRevealed(fogWar,
  transform.position)` 추가.

`npx uloop-cli compile` 통과 (에러 0, 경고 30개 — 신규 2건은 `HealthManager`/`CaptureSystem`에 새로
추가된 `FindFirstObjectByType` 호출의 obsolete 경고로, 프로젝트 전역에 이미 있던 동일 패턴과 같은
종류 - 새로운 문제 아님).
