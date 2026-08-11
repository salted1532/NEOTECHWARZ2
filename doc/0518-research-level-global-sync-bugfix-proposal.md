# 0518. 연구소(Lab) 공격력/방어력 연구 레벨이 연구소별로 따로 노는 버그 - 조사/수정 제안

**날짜:** 2026-08-11

## 요청 내용

> 연구소 연구 관련한 버그가 많아
> 연구소끼리 공격력, 방어력 업그레이드 수치가 따로놈
> 연구소가 2개일때 공격력 업글시
> 다른 연구소에선 공격력 업글 가능하고(다른곳에서 공격력 업글시 공격력 업글은 다른 연구소에선 막혀있어야함)
> 공격력 업글중일떄 다른 연구소에선 방어력이 2레벨 업글로 바뀌어있음(버그)
> 이를 확인해줘

> (추가) 공격력,방어력 업그레이드 수치는 1,2,3으로 다른 연구소에서도 똑같은 수치를 받아야하고
> 다른곳에서 연구중일땐 다른 연구소에선 해당 연구는 연구할수 없게 막혀있어야해

## 원인

`Assets/Scripts/Building/ResearchQueue.cs`는 연구소(Lab) 건물 하나하나에 개별 부착되는 `MonoBehaviour`이고,
`attackLevel`/`armorLevel`(현재 레벨)과 `researchQueue`(대기열 - "이미 연구 중인지" 판정용)가 전부
**그 연구소 인스턴스만의 로컬 상태**다.

반면 실제 전투에 적용되는 보너스(`UpgradeManager.attackBonus/armorBonus`)는 이미 싱글턴처럼 하나만
존재해서 전역으로 공유된다 (`RTSUnitController.AddGlobalBonus`를 거쳐서만 접근). **"레벨"만 전역이 아니고
"보너스"만 전역인 불일치**가 이번 버그들의 공통 원인이다.

- UI가 연구 레벨/버튼 상태를 읽을 때는 `RTSUnitController.GetRepresentativeBuilding()`(선택된 건물 중
  우선순위로 대표 하나만 고름)으로 대표 연구소 하나만 골라 그 건물의 로컬 레벨/큐를 보여준다
  (`ResearchButtonAction`/`CanResearch`/`TryResearch`/`GetResearchQueue` 전부 이 경유,
  `RTSUnitController.cs:1491~1650`). → **연구소 A에서 공격력을 올려도 연구소 B의 `attackLevel`은
  그대로라 서로 수치가 따로 논다.**
- `CanEnqueue()`의 "이미 연구 중이면 막기" 판정(`IsQueued`, `ResearchQueue.cs:68~76`)도 자기 자신의
  로컬 큐만 본다. → **연구소 A가 공격력을 연구 중이어도 연구소 B는 전혀 모르고 공격력을 또 큐잉할 수
  있다** (신고하신 버그). 이 상태로 둘 다 완료되면 `Complete()`가 각자 `AddGlobalBonus`를 호출해서
  **보너스가 두 번 적용되는 밸런스 버그**로도 이어진다.
- "공격력 업글 중일 때 다른 연구소에서 방어력이 2레벨로 바뀌어 보임"도 같은 원인이다 - 코드에 레벨을
  뒤섞는 별도 버그가 있는 게 아니라, 여러 연구소를 선택/전환할 때마다 "대표 건물"이 바뀌면서 그 순간
  대표가 된 다른 연구소의 (서로 다른) 로컬 레벨이 그대로 표시되는 것이다.

참고로 `ResearchQueue.cs`의 기존 주석("공격/방어 각각 '다음 레벨' 1개씩만 의미가 있으므로 동시에
최대 2개까지만 허용")은 이미 "전역으로 하나씩만"이라는 설계 의도를 담고 있다 - 그걸 실제로 전역
동기화하지 않은 게 버그다.

## 수정 방향

레벨/진행 중 여부를 이미 전역 싱글턴인 `UpgradeManager`로 옮겨서, 보너스와 동일하게 전역 상태로
만든다. 각 연구소(Lab)의 로컬 큐(타이머, 영토 이탈 시 정지)는 그대로 유지 - "연구를 물리적으로 어느
건물이 수행 중인가"는 여전히 그 건물 하나지만, "레벨이 몇인지" / "이 종류가 지금 어딘가에서 연구
중인지"는 전역으로 공유한다.

### `UpgradeManager.cs`
- `attackLevel`/`armorLevel`(0~3) 필드 추가 + `GetLevel(type)`/`AddLevel(type)`
- `attackInProgress`/`armorInProgress`(bool) 필드 추가 + `IsInProgress(type)`/`SetInProgress(type, bool)`

### `RTSUnitController.cs`
기존 `AddGlobalBonus`와 동일한 패턴으로 얇은 위임 메서드 3개 추가 (ResearchQueue가 UpgradeManager를
직접 참조하지 않는 기존 관례 유지):
- `GetGlobalResearchLevel(type)` → `upgradeManager.GetLevel(type)`
- `IsResearchInProgressAnywhere(type)` → `upgradeManager.IsInProgress(type)`
- `SetResearchInProgress(type, bool)` → `upgradeManager.SetInProgress(type, value)`

### `ResearchQueue.cs`
- `attackLevel`/`armorLevel` 로컬 필드 제거, `GetLevel()`은 `rtsController.GetGlobalResearchLevel()`로 위임
- `IsQueued()`(로컬 큐 검사) 제거, `CanEnqueue()`는 `rtsController.IsResearchInProgressAnywhere(type)`로 전역 판정
- `Enqueue()`: 로컬 큐에 추가 후 `rtsController.SetResearchInProgress(type, true)` 호출
- `Complete()`: 로컬 `attackLevel++` 대신 전역 레벨 증가 위임 + 기존 `AddGlobalBonus` 호출 + `SetResearchInProgress(type, false)`
- `Cancel(index)`: 취소된 항목의 타입에 대해서도 `SetResearchInProgress(type, false)` 호출 (취소해도 "연구 중" 플래그가 안 풀리는 걸 막기 위함)
- `ClearQueue()`(건물 파괴 시): 남아있던 각 항목의 타입에 대해서도 `SetResearchInProgress(type, false)` 호출

### 효과
- 연구소가 몇 개든 레벨 표시가 항상 전역으로 일치 (증상 1, 3 해결)
- 한 연구소가 특정 타입을 연구 중이면 다른 모든 연구소에서 그 타입 버튼이 비활성화됨 (증상 2 해결)
- 중복 큐잉으로 인한 보너스 이중 적용도 구조적으로 불가능해짐 (부수 효과)

### 범위 밖(이번엔 안 건드림, 필요하면 별도 진행)
- 연구소 A에서 진행 중인 연구의 진행률 바(프로그레스)는, 다른 연구소 B의 패널을 보고 있을 때는
  B의 로컬 큐가 비어있어서 표시되지 않는다(버튼은 정상적으로 비활성화되지만 "왜 막혔는지" 진행률까지
  보여주진 않음). 필요하면 후속 작업으로 "현재 전역으로 진행 중인 연구"를 어느 연구소를 보든 진행률
  바에 표시하도록 추가 가능.
- 연구소 건물이 영토 밖으로 나가면 그 건물이 실제로 들고 있는 로컬 큐만 타이머가 멈춘다 (기존 규칙
  그대로 유지 - 레벨/잠금만 전역화하고 타이머 자체의 물리적 위치·영토 판정은 안 건드림).

## 변경 예정 파일
- `Assets/Scripts/Upgrade/UpgradeManager.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/Building/ResearchQueue.cs`

---

## 적용 (사용자 승인 후)

> 진행시켜줘

제안대로 적용함.

### `UpgradeManager.cs`
`attackLevel`/`armorLevel`(전역 레벨) + `attackInProgress`/`armorInProgress`(전역 진행중 플래그) 필드와
`GetLevel`/`AddLevel`/`IsInProgress`/`SetInProgress` 추가.

### `RTSUnitController.cs`
`AddGlobalBonus` 바로 옆에 위임 메서드 4개 추가:
```csharp
public int GetGlobalResearchLevel(ResearchType type) => upgradeManager.GetLevel(type);
public void AddGlobalResearchLevel(ResearchType type) => upgradeManager.AddLevel(type);
public bool IsResearchInProgressAnywhere(ResearchType type) => upgradeManager.IsInProgress(type);
public void SetResearchInProgress(ResearchType type, bool inProgress) => upgradeManager.SetInProgress(type, inProgress);
```

### `ResearchQueue.cs`
```diff
-    private int attackLevel; // 0~3
-    private int armorLevel;  // 0~3
-
-    public int GetLevel(ResearchType type) => type == ResearchType.Attack ? attackLevel : armorLevel;
-
-    private bool IsQueued(ResearchType type)
-    {
-        foreach (var r in researchQueue)
-            if (r.Type == type) return true;
-        return false;
-    }
-
-    public bool CanEnqueue(ResearchType type) =>
-        GetLevel(type) < MaxLevel && !IsQueued(type) && researchQueue.Count < MaxQueueSize;
+    public int GetLevel(ResearchType type) => rtsController != null ? rtsController.GetGlobalResearchLevel(type) : 0;
+
+    public bool CanEnqueue(ResearchType type) =>
+        GetLevel(type) < MaxLevel
+        && (rtsController == null || !rtsController.IsResearchInProgressAnywhere(type))
+        && researchQueue.Count < MaxQueueSize;
```
- `Enqueue()`: `researchQueue.Add(...)` 뒤에 `rtsController?.SetResearchInProgress(type, true)` 추가
- `Complete()`: `attackLevel++`/`armorLevel++` 대신 `rtsController.AddGlobalResearchLevel(type)` 호출 + 끝에 `rtsController.SetResearchInProgress(type, false)` 추가
- `Cancel(index)`: `researchQueue.RemoveAt(index)` 뒤에 `rtsController?.SetResearchInProgress(type, false)` 추가
- `ClearQueue()`(건물 파괴 시): 제거된 각 항목의 타입에 대해 `rtsController?.SetResearchInProgress(item.Type, false)` 추가

`npx uloop-cli compile` 성공 확인 (Error 0개, 경고는 이 변경과 무관한 기존 obsolete API 경고만 있음).

## 변경된 파일
- `Assets/Scripts/Upgrade/UpgradeManager.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/Building/ResearchQueue.cs`
