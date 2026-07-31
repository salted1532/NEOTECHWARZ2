# 0325. 승리창 안 뜨는 문제 수정 + 개수형 목표 진행도(N/M) 표시

**날짜:** 2026-07-31

## 요청 내용

> 주 목표 클리어시 승리창이 뜨도록 해줘 그리고 자원이나 병력처럼 개수를 나타내는 미션에 경우 9/10 이런식으로 현재 몇마리나 자원얼마 있고 목표치랑 비교해서 보이도록

## 조사 내용

### 1) "승리창이 안 뜨는" 원인 확인
`uloop find-game-objects`로 현재 열려있는 씬을 확인함:
- `StageObject` GameObject에 `Stage0Objectives`와 `VictoryPanelController`가 이미 붙어있고, `targetZone`(`Capture_territory`), 목표 텍스트 5개(`Main1/2/3`, `sub1/2`), `victoryPanel`(`VictoryPanel`), `mainMenuButton`(`BackToMainMenu`)까지 전부 인스펙터에 연결되어 있음 — 씬 작업은 이미 다 되어 있었음.
- 하지만 **씬에 `StageManager` 컴포넌트 자체가 없음**(`find-game-objects --required-components StageManager` 결과 0건). `Stage0Objectives.Update()`의 `StageManager.Instance?.ReportVictory()`와 `VictoryPanelController.Start()`의 `StageManager.Instance.OnVictory += ...` 둘 다 `StageManager.Instance`가 `null`이면 조용히 아무 일도 안 함(`?.` 널 조건 연산자라 예외도 안 남) — 그래서 주목표를 다 채워도 승리창이 뜨지 않았던 것.
- **수정**: `StageObject`에 `StageManager` 컴포넌트를 추가하면 됨(코드 변경 없이 씬 편집만으로 해결). 이미 붙어있는 `Stage0Objectives`/`VictoryPanelController`는 `Start()`에서 같은 프레임에 `StageManager.Instance`를 조회하므로, `StageManager`가 씬에 존재하기만 하면(어떤 오브젝트에 있든 상관없음, 스크립트 실행 순서에 의존하지 않음 — `Awake`가 전체 오브젝트에 대해 먼저 다 끝난 뒤 `Start`가 실행되는 유니티 기본 순서를 그대로 이용) 자동으로 연결됨.

### 2) 개수형 목표 진행도(N/M) 표시
- 대상: "자원이나 병력처럼 개수를 나타내는" 목표 → 트루퍼 생산(`produceTroopersText`, 목표 10기)과 광물 확보(`secureOreText`, 목표 1000) 2개가 정확히 여기 해당함.
- 거점 점령/병영 건설(존재 여부만 판정, 목표 수량 없음)과 적 전멸(목표 수량이 애초에 정해져 있지 않음 - "전부"가 목표)은 대상에서 제외 — 지금처럼 완료/미완료 텍스트만 표시.
- `Stage0Objectives.Update()`가 이미 매 프레임 `CountAliveUnits(...)`와 `rtsController.GetOre()`로 현재값을 계산하고 있으므로, 그 값을 지역 변수로 꺼내 텍스트에 같이 넘기기만 하면 됨 — 새 계산 로직 불필요.

## 설계안

### 씬 수정
- `StageObject`(또는 씬의 다른 아무 GameObject)에 `StageManager` 컴포넌트 추가

### `Assets/Scripts/System/Stage0Objectives.cs` 수정

#### 기존 코드
```csharp
        bool zoneCaptured = targetZone != null && targetZone.Owner == CaptureOwner.Ally;
        bool troopersReady = CountAliveUnits(AssaultTrooperUnitID) >= RequiredTrooperCount;
        bool barracksBuilt = rtsController != null && rtsController.HasCompletedBuilding(BarracksBuildingID);
        bool enemiesCleared = FindObjectsByType<EnemyUnitController>(FindObjectsSortMode.None).Length == 0;
        bool oreSecured = rtsController != null && rtsController.GetOre() >= RequiredOre;

        SetObjectiveText(captureZoneText, "거점 1개 점령하기", zoneCaptured);
        SetObjectiveText(produceTroopersText, $"어썰트 트루퍼 {RequiredTrooperCount}기 생산하기", troopersReady);
        SetObjectiveText(buildBarracksText, "병영 건설하기", barracksBuilt);
        SetObjectiveText(clearEnemiesText, "(서브) 주변 적 유닛 모두 제거", enemiesCleared);
        SetObjectiveText(secureOreText, $"(서브) 광물 {RequiredOre} 확보", oreSecured);
```

```csharp
    // 완료 시 텍스트를 <s>(취소선)로 감싸고, 미완료면 그대로 표시 - 매 프레임 다시 호출되므로
    // 조건이 다시 깨지면 취소선도 자동으로 사라진다.
    private static void SetObjectiveText(TextMeshProUGUI text, string description, bool complete)
    {
        if (text == null) return;
        text.text = complete ? $"<s>{description}</s>" : description;
    }
```

#### 변경 코드
```csharp
        int trooperCount = CountAliveUnits(AssaultTrooperUnitID);
        int oreAmount = rtsController != null ? rtsController.GetOre() : 0;

        bool zoneCaptured = targetZone != null && targetZone.Owner == CaptureOwner.Ally;
        bool troopersReady = trooperCount >= RequiredTrooperCount;
        bool barracksBuilt = rtsController != null && rtsController.HasCompletedBuilding(BarracksBuildingID);
        bool enemiesCleared = FindObjectsByType<EnemyUnitController>(FindObjectsSortMode.None).Length == 0;
        bool oreSecured = oreAmount >= RequiredOre;

        SetObjectiveText(captureZoneText, "거점 1개 점령하기", zoneCaptured);
        SetObjectiveText(produceTroopersText, "어썰트 트루퍼 생산하기", trooperCount, RequiredTrooperCount);
        SetObjectiveText(buildBarracksText, "병영 건설하기", barracksBuilt);
        SetObjectiveText(clearEnemiesText, "(서브) 주변 적 유닛 모두 제거", enemiesCleared);
        SetObjectiveText(secureOreText, "(서브) 광물 확보", oreAmount, RequiredOre);
```

```csharp
    // 완료 시 텍스트를 <s>(취소선)로 감싸고, 미완료면 그대로 표시 - 매 프레임 다시 호출되므로
    // 조건이 다시 깨지면 취소선도 자동으로 사라진다.
    private static void SetObjectiveText(TextMeshProUGUI text, string description, bool complete)
    {
        if (text == null) return;
        text.text = complete ? $"<s>{description}</s>" : description;
    }

    // 개수 비교형 목표용 오버로드 - "설명 (현재/목표)" 형식으로 표시(요청사항: 9/10 형식).
    // 현재값이 목표를 넘어도 표시는 목표치에서 고정(예: 1050/1000이 아니라 1000/1000으로 표시).
    private static void SetObjectiveText(TextMeshProUGUI text, string description, int current, int target)
    {
        if (text == null) return;
        bool complete = current >= target;
        string content = $"{description} ({Mathf.Min(current, target)}/{target})";
        text.text = complete ? $"<s>{content}</s>" : content;
    }
```

## 검증

- 사용자 확인 후 `Stage0Objectives.cs`를 설계안 그대로 수정
- `uloop execute-dynamic-code`로 `StageObject`에 `StageManager` 추가 후 씬 저장 — 콘솔 로그로 확인: `StageManager added to StageObject.` / `Scene saved: True, has StageManager: True`
- **주의**: 이번 프로젝트 전체 컴파일에는 이 작업과 무관한 에러가 남아있음 — `Assets/Scripts/Unit/UnitController.cs`, `Assets/Scripts/System/RTSUnitController.cs`에서 `SkillActivationContext` 관련 에러(`git status`로 확인한 결과 `UnitController.cs`가 이미 수정된 상태 - 동시에 진행 중인 다른 세션의 "고급유닛 액티브/패시브 스킬" 작업([[advanced-unit-active-passive-skill-effects-design|doc/0323]])이 원인, 이번 작업과 무관). `Stage0Objectives.cs`/`VictoryPanelController.cs`/`StageManager.cs` 자체에서 발생한 에러는 없음(경고만 있음 - `FindFirstObjectByType`/`FindObjectsByType` obsolete, 기존 코드 전반에 있는 것과 동일한 종류).

## 영향받는 파일

- `Assets/Scripts/System/Stage0Objectives.cs` (수정 완료)
- 씬(`StageObject`에 `StageManager` 컴포넌트 추가, 저장 완료)
