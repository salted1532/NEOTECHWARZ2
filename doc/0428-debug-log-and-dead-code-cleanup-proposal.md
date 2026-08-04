# 0428. 디버그 로그/데드 코드 정리 및 내부 리팩토링 제안

- 날짜: 2026-08-05
- 상태: **A그룹 적용 완료** (컴파일 확인: 에러 0개, 경고는 기존부터 있던 것(FindFirstObjectByType obsolete)만 남음, B그룹은 미적용)

## 요청 내용

> 현재 발견된 버그나 그런건 다 발견한거 같고 전체 코드를 다 읽어보고 불필요한 디버그 로그나 불필요한
> 코드가 있는지 확인해보고 내부 리팩토링을 해줬으면 좋겠어. 그리고 전체 코드에서 불필요한 부분이
> 있으면 최적화 해줬으면 좋겠어 하지만 현재 기능적인건 그대로 작동했으면 좋겠어

요약: 기능은 그대로 유지한 채, ① 안 쓰는 디버그 로그 ② 데드 코드 ③ 불필요하게 장황한 부분을 찾아
정리해달라는 요청. 코드 동작을 바꾸는 변경이므로 [[confirm-before-implementing-rule]]에 따라 적용
전에 먼저 이 제안서로 검토를 받는다.

## 조사 내용

`Assets/Scripts` 전체(80개 파일, 약 15,800줄)를 서브에이전트로 전수 조사했다. 특히 큰 파일
(UnitController 2147줄, RTSUnitController 2081줄, UIController 1324줄, UserControl 1096줄,
EnemyUnitController 742줄 등)은 전체를 읽었고, 나머지는 `Debug.Log`/주석 처리된 코드/안 쓰는
`using` 패턴으로 훑었다. 각 발견 사항은 `doc/` 기록과 대조해서 "그 당시엔 필요했지만 원인이 이미
확정되어 지금은 죽은 코드인지"를 확인했다.

결과는 확신도에 따라 두 그룹으로 나눴다:
- **A. 바로 제거해도 안전 (doc 기록상 근본 원인이 이미 확정/수정됨)**
- **B. 확인이 필요 (애매하거나, 의도적으로 남겨뒀을 수 있음)** — 이 제안서에는 포함하되 사용자
  선택에 따라 빼거나 넣는다.

동작(게임플레이) 변경은 전혀 없고, 전부 로그 출력/죽은 코드/안 쓰는 import 제거뿐이다.

---

## A그룹: 바로 제거 제안 (근본 원인 확정됨, 안전)

### 1) `Assets/Scripts/Unit/UnitController.cs` — 스폰 진단 로그 (doc/0345 원인 확정됨)

`doc/0345`에서 "헤비탱크 등이 땅속에 박힌 것처럼 보이는" 원인이 testmap 아래 겹쳐진 지형
잔재임이 최종 확정됐고, 코드 자체에도 "원인이 확정되면 삭제해도 되는 임시 코드"라고 명시돼 있다.

**기존 코드** (304~369번 줄, `LogSpawnDiagnostics()`/`DescribeMaterial()` 정의 + 306번 줄 호출):
```csharp
        // doc/0345 "헤비탱크/브루트메크/스카이랜서가 땅속에 박힌 것 같다" 조사용 진단 로그.
        // EnemyUnitController에 이미 있는 것과 동일한 로직 - 원인이 확정되면 삭제해도 되는 임시 코드.
        LogSpawnDiagnostics();
    }

    // 진단용: ... (LogSpawnDiagnostics 전체 본문, Physics.Raycast + Debug.Log/LogWarning 다수)
    private void LogSpawnDiagnostics() { ... }

    // 셰이더 이름/지원 여부뿐 아니라 ...
    private static string DescribeMaterial(Material m) { ... }
```

**변경 코드**: `LogSpawnDiagnostics()` 호출과 두 메서드 정의를 통째로 삭제.

### 2) `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` — 동일 진단 로그 (같은 원인)

UnitController와 완전히 같은 코드가 적 유닛 쪽에도 복사돼 있다. 155~156번 줄의 호출과
163~223번 줄의 `LogSpawnDiagnostics()`/`DescribeMaterial()` 정의를 동일하게 삭제.

### 3) `Assets/Scripts/Unit/UnitController.cs` / `EnemyUnitController.cs` — 추격 재탐색 로그 (매 프레임 콘솔 스팸)

`doc/0416`에 "도달 가능 모드는 게이트 없이 매 프레임 실행되므로 콘솔에 로그가 초당 수십 줄씩
쌓인다"고 문제로 명시돼 있고, 특정 버그 조사용이 아니라 순수 매 프레임 스팸이다.

**기존 코드** (UnitController.cs 671번 줄, EnemyUnitController.cs 356번 줄 — 도달불가 모드 재탐색 시 1회성):
```csharp
            bool reachableOnArrival = IsPositionReachable(targetPos);
            Debug.Log($"{name}: [도달 불가 추격] 재탐색 결과 - {(reachableOnArrival ? "도달 가능" : "도달 불가")}");
            if (reachableOnArrival)
```
**변경 코드**:
```csharp
            bool reachableOnArrival = IsPositionReachable(targetPos);
            if (reachableOnArrival)
```

**기존 코드** (UnitController.cs 693번 줄, EnemyUnitController.cs 376번 줄 — 도달가능 모드, 매 프레임):
```csharp
        bool reachableNow = IsPositionReachable(targetPos);
        Debug.Log($"{name}: [추격] 재탐색 결과 - {(reachableNow ? "도달 가능" : "도달 불가")}");
        if (!reachableNow)
```
**변경 코드**:
```csharp
        bool reachableNow = IsPositionReachable(targetPos);
        if (!reachableNow)
```
(EnemyUnitController.cs는 변수명이 `targetPos` 대신 `pos`라는 점만 다르고 나머지는 동일)

### 4) `Assets/Scripts/Unit/UnitController.cs` — 자원 반납 진단 로그 `[GatherDiag]` (doc/0345 "버그 7" 확정됨)

**기존 코드** (1630~1633번 줄):
```csharp
        // 진단용(doc/0345 "일꾼이 쌓이면 리턴을 아예 안 함" 조사) - 반납 시도 시점마다 대상/상태를 기록.
        // 원인이 확정되면 삭제해도 되는 임시 코드.
        Debug.Log($"[GatherDiag] {gameObject.name}: 반납 시작 target={depositBuilding?.name ?? depositTargetTransform.name} " +
            $"lifted={lifted} targetPos={depositTargetTransform.position} myPos={transform.position}", this);
```
**변경 코드**: 위 3줄(주석 2줄 + Debug.Log) 삭제.

**기존 코드** (1875~1876번 줄):
```csharp
        // 진단용(doc/0345) - 반납이 실제로 완료되는지 확인. 원인이 확정되면 삭제해도 되는 임시 코드.
        Debug.Log($"[GatherDiag] {gameObject.name}: 반납 완료 amount={carryingAmount} type={carryingType}", this);
```
**변경 코드**: 위 2줄 삭제.

(1711번·1852번 줄의 `[GatherDiag]` `Debug.LogWarning`은 실제 실패 상황에서만 찍히는 가드성 로그라
B그룹으로 분류 — 아래 참고)

### 5) `Assets/Scripts/UI/ControlGroupPanel.cs` — 클릭 조사용 로그 전체 (doc/0427에서 진짜 원인이 프리팹 설정임이 확인·수정됨)

`doc/0427`에서 "부대선택 버튼이 안 눌리는" 진짜 원인이 `DragRectangle`의 `Raycast Target`
프리팹 설정이었고 이미 껐다고 기록돼 있다. 이 파일의 클릭/누름/뗌/이탈 로그는 그 조사를 위해
`doc/0426`에서 추가한 것으로, 원인이 이미 다른 곳(프리팹)에서 해결돼 이제 아무 목적이 없다.

**기존 코드** (68~91번 줄):
```csharp
        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() =>
            {
                Debug.Log($"[ControlGroupPanel] 부대 {DisplayNumber(groupIndex)}번 버튼 클릭됨 (frame {Time.frameCount})");
                rtsController.SelectControlGroup(groupIndex);
            });

        // 눌림(PointerDown)/뗌(PointerUp)/이탈(PointerExit) 시점을 각각 로그로 남긴다 - 실제 손으로
        // 누를 때만 실패한다면, 누르고 있는 동안 커서가 버튼 밖으로 벗어났다가(EXIT) 밖에서 손을 떼는
        // 상황일 가능성이 높다(그 경우 UNPRESSED는 찍히지만 그 뒤에 "클릭됨" 로그가 안 따라온다).
        EventTrigger trigger = buttonObj.AddComponent<EventTrigger>();
        AddTriggerLog(trigger, EventTriggerType.PointerDown, groupIndex, "PRESSED");
        AddTriggerLog(trigger, EventTriggerType.PointerUp, groupIndex, "UNPRESSED");
        AddTriggerLog(trigger, EventTriggerType.PointerExit, groupIndex, "POINTER EXIT(누른 채 벗어남)");

        groupButtons[groupIndex] = buttonObj;
    }

    private void AddTriggerLog(EventTrigger trigger, EventTriggerType type, int groupIndex, string label)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ =>
            Debug.Log($"[ControlGroupPanel] 부대 {DisplayNumber(groupIndex)}번 버튼 {label} (frame {Time.frameCount})"));
        trigger.triggers.Add(entry);
    }
```
**변경 코드**:
```csharp
        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() => rtsController.SelectControlGroup(groupIndex));

        groupButtons[groupIndex] = buttonObj;
    }
```
(`EventTrigger`를 조사 목적으로만 추가했던 것이라 `AddTriggerLog` 메서드 자체도 함께 제거,
`using UnityEngine.EventSystems;` 도 이 파일에서 더 이상 쓰이지 않으면 같이 제거)

### 6) `Assets/Scripts/Building/BuildingController.cs` — 주석 처리된 죽은 로그 (인코딩 깨진 채 방치)

**기존 코드** (392번, 399번 줄):
```csharp
        //Debug.Log(name + " ????");
        buildingMarker.SetActive(true);
```
```csharp
        //Debug.Log(name + " ???? ????");
        buildingMarker.SetActive(false);
```
**변경 코드**: 두 주석 줄 삭제 (기능에 영향 없음, 이미 비활성화된 코드).

### 7) `Assets/Scripts/UnitSpawner/UnitSpawner.cs` — 안 쓰는 using + 큐 추가마다 찍히는 로그

**기존 코드** (1~3번 줄):
```csharp
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
```
**변경 코드**:
```csharp
using System.Collections.Generic;
using UnityEngine;
```
(`Unity.VisualScripting` 네임스페이스의 타입을 이 파일에서 하나도 쓰지 않음)

**기존 코드** (49번 줄):
```csharp
    public void Enqueue(int unitID)
    {
        Debug.Log($"database : {database}");

        if (database == null)
```
**변경 코드**:
```csharp
    public void Enqueue(int unitID)
    {
        if (database == null)
```
(바로 아래 null 체크가 이미 있어서 이 로그는 정보 가치가 없음 - 유닛을 큐에 넣을 때마다 찍힘)

---

## B그룹: 확인 필요 (애매함 — 원하는 항목만 골라서 알려주면 같이 처리)

- **`RTSUnitController.cs` 187/556/710/783번 줄**: `"유닛 선택"`/`"건물 선택"`/`"적 선택"`/
  `"자원 선택"` — 클릭할 때마다 찍히는, 아무 정보도 없는 로그. 초기 개발용으로 보이지만 플레이
  중 확인용으로 일부러 남겨뒀을 수도 있음.
- **`RTSUnitController.cs` 929/941번 줄**: `[SelectControlGroup]` 그룹 인원수/프레임 로그 —
  ControlGroupPanel과 같은 조사(doc/0427, 이미 해결)에서 추가된 것으로 보이나, 그룹 인원수 등
  약간의 정보를 담고 있어 A그룹과 분리.
- **`RTSUnitController.cs` 1287/1292/1304/1320번 줄**: `"자원부족!"`/`"인구수부족!"`/`"생산 불가..."`
  — 바로 옆에 `SoundManager.Instance?.PlayInsufficientResourcesWarning()`으로 이미 오디오
  피드백이 있어서 콘솔 로그는 중복이지만, 텍스트라서 틀린 건 아님.
- **`UnitSpawner.cs`의 `PrintQueue()`** (161~183번 줄, `Enqueue`/`Spawn`/`Cancel`마다 호출) —
  코드 주석에 "콘솔 디버그용"이라고 명시돼 있어 의도적인 개발 도구일 수 있음.
- **`CaptureSystem.cs` 154번 줄**: `"점령 상태 변경: {newOwner}"` — 소유권이 실제로 바뀔 때만
  찍혀서 빈도는 낮음. 게임 이벤트 로그로 의도된 것일 수도, leftover일 수도 있음.
- **`UnitController.cs` 1711/1852번 줄**: `[GatherDiag]` `Debug.LogWarning` 2건 — doc/0345
  조사용으로 추가됐지만, 실제로 "반납 대상을 못 찾음"/"길을 못 찾음" 같은 진짜 실패 상황에서만
  찍히는 가드성 경고라 계속 남겨두는 게 나을 수 있음 (A그룹의 정상 흐름 로그와는 성격이 다름).
- **`RTSUnitController.cs` 1663~1670번 줄**: 유닛 스킬 슬롯 충돌 경고 — 주석에 "진단용(doc/0368)"
  이라 적혀 있지만, 실제로 뭔가 잘못됐을 때만 찍히는 불변조건 가드라 유지를 권장.

이 제안서에서는 B그룹을 기본적으로 **건드리지 않음**으로 두고, A그룹만 적용을 제안한다.
B그룹 중 원하는 항목이 있으면 알려주면 이번에 같이 처리한다.

## 그 외 조사 결과

이번 패스에서 읽은 파일 범위에서는 A/B그룹 외에 안 쓰는 private 메서드, 도달 불가능한 분기,
그 밖의 안 쓰는 `using`은 발견되지 않았다. "리팩토링"이라 부를 만한 구조적 변경(메서드 추출,
클래스 분리 등)은 이번 조사에서 발견한 항목이 전부 로그/데드 코드 제거 수준이라 별도로 제안하지
않는다 — 기능 변경 없이 "불필요한 것 삭제"에 집중했다.

## 요약 / 영향받는 파일

- **A그룹 적용 시 영향받는 파일** (게임플레이 동작 변화 없음, 콘솔 로그/데드 코드만 감소):
  - `Assets/Scripts/Unit/UnitController.cs`
  - `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
  - `Assets/Scripts/UI/ControlGroupPanel.cs`
  - `Assets/Scripts/Building/BuildingController.cs`
  - `Assets/Scripts/UnitSpawner/UnitSpawner.cs`
- **B그룹**: 위 목록 + `Assets/Scripts/System/RTSUnitController.cs`, `Assets/Scripts/CaptureSystem/CaptureSystem.cs` — 사용자 확인 후 선택 적용.
- 아직 프로젝트 파일에는 아무것도 적용하지 않음 (제안 단계).
