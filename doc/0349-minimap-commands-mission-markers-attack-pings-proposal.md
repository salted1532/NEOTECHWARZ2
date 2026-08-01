# 0349 — 제안: 미니맵 명령(우클릭/A공격), 미션 오브젝트 표시, 피격 알림

**날짜:** 2026-08-01

## 요청 내용

> 유닛,건물 미니맵 지도에다가 명령 내릴수 있게 우클릭, A공격등
> 미션 오브젝트를 미니맵에 표시
> 아군, 건물 공격 받았을 시 미니맵에 표시
> 이것들 프로토타입하면서 추가했으면 좋겠다는 기능들인데 확인하고 구현 설계안 작성해줘

세 가지 다 조사만 하고 설계안을 정리한다 — **아직 코드는 건드리지 않았음**. 아래 설계로 진행해도 될지 확인 후 구현.

---

## 조사한 현재 구조

- **`Assets/Scripts/Camera/MinimapController.cs`**: 미니맵(RawImage) 클릭/드래그를 처리하는 유일한 진입점. `IPointerClickHandler`/`IDragHandler`만 구현하고 있고, `eventData.button`(좌/우클릭 구분)을 전혀 보지 않는다 — 클릭하면 무조건 `minimapCamera.ViewportPointToRay` + 지면(Y=0) 평면 레이캐스트로 월드 좌표를 구해서 `mainCameraControl.JumpToWorldXZ()`(카메라 이동)만 호출한다. 명령 발행 관련 코드는 전혀 없음.
- **`Assets/Scripts/Camera/MinimapViewIndicator.cs`**: 미니맵 위에 "지금 메인 카메라가 보고 있는 영역"을 사각형으로 표시하는 컴포넌트. **월드 좌표 → 미니맵 UI 로컬 좌표 변환 공식**(`minimapCamera.WorldToViewportPoint(worldPoint)` → `rect.xMin/yMin + viewportPoint.x/y * rect.width/height`)이 이미 여기 있음 — 이번에 추가할 미션 마커/피격 핑 아이콘도 이 공식을 그대로 재사용하면 된다.
- **`Assets/Scripts/UserControl/UserControl.cs`**: 실제 게임 화면(메인 카메라)의 좌/우클릭 명령 처리가 전부 여기 있다. `HandleLeftClick()`의 "4. 땅 클릭 = 명령 처리"(424줄) 부분이 "명령 대기 상태(A공격/이동/순찰/랠리 등)에서 땅을 클릭해 확정"하는 로직이고, `HandleRightClick()`의 "2. 땅 클릭 = 명령 처리"(624줄) 부분이 "그냥 우클릭 = 이동/랠리"하는 로직이다. 미니맵에서도 이 두 로직을 그대로 타야 한다 — 지금은 미니맵이 `UserControl`을 아예 참조하지 않는다.
- **미니맵에 유닛/건물이 보이는 방식**: 별도 아이콘 시스템이 없다. `minimapCamera`가 실제 3D 씬을 위에서 그대로 촬영해서 보여주는 방식(RenderTexture)이라, 렌더러가 있는 오브젝트는 자동으로 보인다. 즉 **아이콘을 얹는 오버레이 시스템 자체가 아직 없음** — 미션 마커/피격 핑 둘 다 이번에 새로 만들어야 한다.
- **미션 목표 시스템**: `Assets/Scripts/System/StageManager.cs`는 승리/패배 "결과"만 관리하는 최소 골격(조건 판단 없음). `Assets/Scripts/System/Stage0Objectives.cs`가 스테이지별로 목표 조건을 하드코딩(예: `targetZone`이라는 `TerritoryZone` 하나를 점령)하는 구조 — **범용 "미션 오브젝트 목록" 같은 건 없다.** 스테이지마다 목표 오브젝트가 다르므로, 새 스테이지가 추가될 때마다 코드를 안 건드려도 되게 하려면 마커를 데이터가 아니라 "씬에 컴포넌트로 붙이는" 방식이 맞다.
- **피격 이벤트**: `Assets/Scripts/Unit/HealthManager.cs`의 `OnDamaged` 이벤트(`(int damage, Vector3 attackerPosition, AttackEffectType attackType, bool isEnemyAttacker)`)가 이미 있고, `UnitAudio.cs`/`BuildingAudio.cs`가 여기 구독해서 "적에게 공격받음" 경고 음성(`SoundManager.PlayUnitUnderAttackWarning()`/`PlayBuildingUnderAttackWarning()`, 화면 밖일 때만, `isEnemyAttacker`일 때만)을 이미 재생하고 있다(doc/0292). **피격 미니맵 핑은 이 기존 구독 지점 바로 옆에 얹으면 된다** — 새로 이벤트/구독 시스템을 만들 필요 없음.

---

## 설계안 1: 미니맵 우클릭/A공격 등 명령

### 방식

`UserControl.cs`의 기존 "땅 클릭 처리" 로직 두 덩어리를 재사용 가능한 public 메서드로 추출하고, `MinimapController`가 이 메서드를 호출하도록 한다. 새 명령 로직을 만드는 게 아니라 **이미 있는 메인 화면 클릭 처리를 그대로 재사용**하는 것 — 그래야 메인 화면에서 되는 모든 명령(이동/A공격-이동/순찰/랠리/건물이동)이 미니맵에서도 자동으로 똑같이 동작하고, 향후 새 명령이 추가돼도 미니맵 쪽을 따로 안 고쳐도 된다.

1. **`UserControl.cs`에 두 개의 public 메서드 추출**:
   - `public bool HasPendingGroundOrder()` — `UsercurrentState`가 Move/Attack/Patrol/Rally/BuildingMove 중 하나인지(=A키 등으로 "위치 지정 대기 중"인지) 반환.
   - `public void ConfirmPendingOrderAt(Vector3 groundPoint)` — 지금 `HandleLeftClick()`의 424줄 "4. 땅 클릭 = 명령 처리" 스위치문 내용을 그대로 옮긴 것(이동/A공격-이동/순찰/랠리/건물이동 확정 + `ShowMovePointer`/`ShowAttackPointer` 호출 + `UsercurrentState = None`).
   - `public void IssueRightClickMoveAt(Vector3 groundPoint)` — 지금 `HandleRightClick()`의 624줄 "2. 땅 클릭 = 명령 처리" 내용을 그대로 옮긴 것(선택된 유닛이면 이동, 선택된 건물이면 랠리/공중이동 지정).
   - 기존 `HandleLeftClick`/`HandleRightClick`은 이 두 메서드를 호출하도록 바꾸기만 하면 되므로(내용 이동), 메인 화면 동작은 전혀 안 바뀜.

2. **`MinimapController.cs` 수정**:
   - `[SerializeField] private UserControl userControl;` 추가(기존 `mainCameraControl` 참조와 같은 방식으로 인스펙터에서 연결).
   - `OnPointerClick`에서 `eventData.button`을 확인:
     - **우클릭(`PointerEventData.InputButton.Right`)** → 월드 좌표 계산 후 `userControl.IssueRightClickMoveAt(groundPoint)` 호출("우클릭" 요청사항).
     - **좌클릭(`PointerEventData.InputButton.Left`)** → `userControl.HasPendingGroundOrder()`가 true면(A키 등으로 명령 대기 중이면) `userControl.ConfirmPendingOrderAt(groundPoint)`만 호출하고 카메라는 움직이지 않음("A공격 등" 요청사항 - A모드에서 미니맵을 클릭하면 그 지점으로 공격-이동). false면(평상시) 기존처럼 카메라만 이동.
   - `OnDrag`(드래그로 계속 카메라 따라가는 동작)는 좌클릭 드래그 전용으로 그대로 유지 — 드래그 중엔 명령 확정을 하지 않음(실수로 드래그하다 명령이 나가는 것 방지, 클릭 확정은 `OnPointerClick` 한 번만).

### 자연히 따라오는 것

`ConfirmPendingOrderAt`/`IssueRightClickMoveAt`이 메인 화면 코드를 그대로 옮긴 것이므로, 이번 세션에 추가한 **이동/공격 마커(3초 후 자동 소멸, 중복 방지)도 미니맵 명령에서 자동으로 동일하게 표시**된다 — 별도 작업 불필요.

### 범위 밖(설계에서 의도적으로 제외)

- **미니맵에서 특정 유닛/건물/자원을 "찍어서" 선택하는 것**(예: 미니맵의 아군 유닛 우클릭 = 그 유닛 따라가기, 적 유닛 우클릭 = 그 적 공격)은 이번 설계에 포함하지 않음. 미니맵 클릭은 순수 지면(Y=0 평면) 레이캐스트라 "그 자리에 뭐가 있는지" 자체를 모른다 — 실제로 무엇을 클릭했는지 알아내려면 메인 카메라 기준 3D 피킹이 별도로 필요해서 범위가 커진다. 대부분의 RTS도 미니맵 명령은 "그 지점으로 이동/공격-이동"까지만 지원하고 특정 대상 지정은 지원하지 않는 경우가 많아서, 우선 이 범위로 제안. 필요하면 후속 작업으로.
- **미니맵 드래그 박스로 유닛 다중 선택**: 요청에 없었고, 범위가 또 하나 커서 제외.

---

## 설계안 2: 미션 오브젝트 미니맵 표시

### 방식

스테이지마다 목표 오브젝트가 다르므로(거점 점령/특정 건물 방어 등), **코드가 아니라 씬에 컴포넌트를 붙이는 방식**으로 확장 가능하게 설계.

1. **`MinimapObjectiveMarker.cs`(신규, 작은 컴포넌트)**: 미션 목표와 관련된 오브젝트(예: `Stage0Objectives`의 `targetZone`)에 그냥 붙이기만 하면 되는 마커. 필드: `Sprite icon`(비워두면 기본 아이콘). `OnEnable`/`OnDisable`에서 아래 오버레이 컨트롤러에 자기 자신을 등록/해제(생성 즉시 자동으로 보이고, 비활성화/파괴되면 자동으로 사라짐 — 예: 거점을 점령해서 더 이상 목표가 아니게 되면 그냥 이 컴포넌트를 꺼주면 끝).
2. **`MinimapObjectiveOverlay.cs`(신규, 미니맵 캔버스에 부착)**: `MinimapViewIndicator`와 같은 위치/공식(월드→미니맵 로컬 좌표 변환)을 재사용. 등록된 `MinimapObjectiveMarker` 각각에 대해 작은 아이콘(UI Image, `raycastTarget = false`로 미니맵 클릭을 가로채지 않게)을 매 프레임 그 좌표로 갱신. 아이콘 풀은 등록 시 생성/해제 시 제거(마커 개수가 프로토타입 규모에서 몇 개 안 되므로 풀링 없이 단순 Instantiate/Destroy로 충분 — 필요해지면 나중에 풀링 추가).
3. **적용**: `Stage0Objectives.cs`는 코드 수정 없음 — 에디터에서 `targetZone` 오브젝트(또는 그 자식)에 `MinimapObjectiveMarker`만 붙이면 끝. 나중에 스테이지 1, 2가 생겨도 마찬가지로 씬에 마커만 붙이면 되고 이 스크립트들은 안 건드림.

---

## 설계안 3: 아군/건물 피격 시 미니맵 표시

### 방식

기존 `HealthManager.OnDamaged` 구독 지점(`UnitAudio.HandleDamaged`/`BuildingAudio.HandleDamaged`, doc/0292)에 그대로 얹는다 — 새 구독 시스템을 만들지 않음.

1. **`MinimapAlertController.cs`(신규, 싱글턴, 미니맵 캔버스에 부착 — `StageManager.Instance`와 동일한 패턴)**: `public void ShowAttackPing(Vector3 worldPosition)` 하나만 노출. 호출되면 그 위치에 짧게(예: 2~3초) 표시됐다가 자동으로 사라지는 핑 아이콘(빨간 점 깜빡임 등)을 띄운다. **같은 대상이 연속으로 맞을 때**(기관총 세례 등) 핑이 계속 새로 쌓이지 않도록, 대상(Transform)별로 이미 떠 있는 핑이 있으면 새로 만들지 않고 위치/타이머만 갱신.
2. **`UnitAudio.cs`/`BuildingAudio.cs`의 기존 `HandleDamaged`에 한 줄 추가**:
   ```csharp
   private void HandleDamaged(int amount, Vector3 attackerPosition, AttackEffectType attackType, bool isEnemyAttacker)
   {
       if (!isEnemyAttacker) return; // 기존과 동일 - 아군사격엔 반응 안 함

       MinimapAlertController.Instance?.ShowAttackPing(transform.position); // 신규
       if (!SoundManager.IsWorldPositionOnScreen(transform.position))
           SoundManager.Instance?.PlayUnitUnderAttackWarning(); // 기존
   }
   ```
   (`BuildingAudio.cs`도 동일한 형태.) 이미 모든 아군 유닛/건물이 `OnEnable`/`OnDisable`로 구독을 관리하고 있으므로, 나중에 생산되는 유닛도 자동으로 커버됨 — 별도 등록 로직 불필요.

### 확인하고 싶은 것

- **화면 안에 있을 때도 핑을 띄울지**: 기존 음성 경고는 "화면 밖일 때만" 울리도록 설계돼 있음(이미 보이는데 또 알려주면 시끄러움). 미니맵 핑은 보통 RTS에서 화면 안/밖 상관없이 항상 표시하는 게 일반적이라(미니맵은 원래 "지금 안 보는 곳"을 보여주는 용도) 위 예시 코드는 화면 여부와 무관하게 항상 핑을 띄우도록 했다 — 이대로 괜찮을지, 아니면 음성 경고와 똑같이 화면 밖일 때만 띄울지 확인 필요.

---

## 요약 / 예상 영향 파일

| 설계안 | 신규 파일 | 수정 파일 |
|---|---|---|
| 1. 미니맵 명령 | 없음 | `UserControl.cs`(메서드 추출), `MinimapController.cs`(버튼 분기 추가) |
| 2. 미션 오브젝트 표시 | `MinimapObjectiveMarker.cs`, `MinimapObjectiveOverlay.cs` | 없음(씬에 컴포넌트 배치만) |
| 3. 피격 알림 | `MinimapAlertController.cs` | `UnitAudio.cs`, `BuildingAudio.cs`(한 줄씩 추가) |

세 개 다 서로 독립적이라 원하는 것만 골라서 먼저 진행해도 된다. 진행 방식(전부/일부, 설계안 3의 화면 안/밖 여부) 확인되면 구현 시작하겠음.
