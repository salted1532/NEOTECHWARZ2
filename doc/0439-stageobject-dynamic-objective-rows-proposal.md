# 0439. StageObject 목표 텍스트 동적 생성 (제안 — [[0440-stage-objective-reflection-auto-wiring]]로 대체됨)

> **상태: 대체됨.** 이 문서의 방식(스테이지 스크립트마다 `CreateObjectiveRow()`를 필드 개수만큼
> 손으로 호출)은 적용 전에 요청이 바뀌어서 실제로는 적용 안 됨. Stage1~5Objectives까지 이미
> 추가된 상태에서 "컴포넌트만 연결하면 인스펙터 필드도 자동으로 채워지도록" 요구사항이 추가돼서,
> 리플렉션 기반의 범용 자동 연결 방식인 [[0440-stage-objective-reflection-auto-wiring]]으로
> 대체함. 아래 내용은 그 이전 설계 기록으로만 남겨둠.

## 날짜
2026-08-05

## 요청
"canvas의 StageObject라는 오브젝트에 vertical Layout Group을 추가했거든 이건 캠페인의 주목표와
서브목표 달성 유무의 텍스트를 자동생성해서 수직으로 나열시키려고 만든거야 이걸 각
Stage0Objectives 와 같은 스크립트를 읽어와서 게임 시작시 스테이지 목표를 입력할 텍스트를
개수에 맞게 생성해서 나열하고 목표 클리어시 -선이 생기도록 stage Manager를 수정하든 병합하든
해서 새로 만들어줘"

## 조사

### 현재 상태 (작업 중, 미커밋)
`Assets/prefabs/Game/GameManager.prefab`의 Canvas 밑 `StageObject`(`StageManager` 컴포넌트가
붙어있는 그 오브젝트) — 사용자가 이미 직접:
- 기존에 있던 고정 텍스트 6개(`Label`, `Main1/2/3`, `sub1/2`)를 삭제
- `StageObject`에 `VerticalLayoutGroup` 추가(`childControlWidth/Height=0`, `forceExpand=1`)

한 상태(`git status`상 `Assets/prefabs/Game/GameManager.prefab`가 `M`). 즉 지금은 목표 텍스트를
표시할 자식이 하나도 없는 빈 컨테이너 상태.

### `Stage0Objectives.cs` (현재 구조)
`Assets/Scripts/System/Stage0Objectives.cs` — 주목표 3개(`captureZoneText`,
`produceTroopersText`, `buildBarracksText`) + 서브목표 2개(`clearEnemiesText`,
`secureOreText`)를 **개별 `[SerializeField] TextMeshProUGUI` 필드**로 들고 있고, 지금까지는
이 필드들을 인스펙터에서 씬의 `Main1/2/3`, `sub1/2`에 수동으로 연결해서 썼음. `Update()`에서 매
프레임 조건을 재평가해서 `SetObjectiveText(...)`로 완료 시 `<s>`(취소선) 태그를 씌우는 로직은
이미 있고 그대로 재사용 가능([[0322]] 참고, [[0438-stage1-5-objectives-proposal]]에서
`ObjectiveTextUtil.SetObjectiveText`로 분리됨). 문제는 딱 하나 — **텍스트를 받을 대상(TMP
오브젝트)을 더 이상 인스펙터에서 미리 만들어 연결할 수 없음** (그 방식을 버리고 자동 생성으로
바꾸는 게 이번 요청의 핵심).

참고: `doc/0438`에서 `Stage1Objectives.cs`~`Stage5Objectives.cs`도 설계/승인까지 됐었는데, 현재
디스크에는 파일이 없음(`ObjectiveTextUtil.cs`/`Stage0Objectives.cs` 리팩터링만 적용된 상태) —
아마 텍스트 필드를 5칸씩 인스펙터에서 일일이 연결하는 게 번거로워서 지우고 이번 자동 생성
방식으로 다시 하려는 것으로 보임. **이번 변경 범위는 메커니즘(StageManager + Stage0Objectives)
까지만이고, Stage1~5Objectives 재작성은 포함하지 않음** — 필요하면 이 패턴 확정 후 별도 요청으로.

### 폰트/스타일
삭제되기 전 `Main1`의 `TextMeshProUGUI` 설정(git HEAD 기준) 확인: 폰트 에셋
`82cbbef41c7b30a49a2ed4607e4eec4e`(한글 지원 폰트로 추정 - 목표 텍스트가 전부 한글), 크기 28,
색상 `(0.54, 0.75, 1, 1)`, `richText: 1`(취소선 태그 렌더링에 필요), `raycastTarget: 0`. 코드로
새 텍스트를 만들 때 이 값을 기본값으로 사용.

### 기존 코드 패턴 확인
`Assets/Scripts/UI/Tooltip/TooltipContentFitter.cs`가 이미 `gameObject.AddComponent<...>()`로
런타임에 UI 컴포넌트를 코드로 붙이는 패턴을 쓰고 있음 — 프리팹 계층을 직접 손으로 편집하는 대신
코드에서 `new GameObject(...)`로 행(row)을 만드는 이번 방식이 이 프로젝트 관례와 맞음. 프리팹
YAML을 손으로 편집하지 않아도 됨.

## 제안하는 변경

### 1) `StageManager.cs` — 목표 행(row) 생성 기능 추가
`StageObject`(=이 컴포넌트가 붙은 오브젝트, 이미 `VerticalLayoutGroup`을 가지고 있음) 밑에 목표
1개당 텍스트 오브젝트 1개를 코드로 만들어서 반환하는 `CreateObjectiveRow()`를 추가. 폰트/크기/색은
인스펙터에서 한 번만 연결(한글 폰트가 필요하므로 필드로 노출 — 기존 `Main1` 등에 쓰던 폰트
에셋을 그대로 연결하면 됨).

**Before:**
```csharp
using System;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public enum StageResult { InProgress, Victory, Defeat }

    public StageResult Result { get; private set; } = StageResult.InProgress;

    public event Action OnVictory;
    public event Action OnDefeat;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ReportVictory() { ... }
    public void ReportDefeat() { ... }
}
```

**After:**
```csharp
using System;
using TMPro;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public enum StageResult { InProgress, Victory, Defeat }

    public StageResult Result { get; private set; } = StageResult.InProgress;

    public event Action OnVictory;
    public event Action OnDefeat;

    [Header("목표 체크리스트 UI")]
    [SerializeField] private TMP_FontAsset objectiveFont; // 한글 지원 폰트 연결 (예: 기존 Main1이 쓰던 폰트)
    [SerializeField] private int objectiveFontSize = 28;
    [SerializeField] private Color objectiveColor = new Color(0.5424528f, 0.7460864f, 1f, 1f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 스테이지 목표 스크립트(Stage0Objectives 등)가 Start()에서 목표 개수만큼 호출한다.
    // 이 오브젝트(StageObject) 밑에 텍스트 한 줄을 새로 만들어 자식으로 붙이면, 이미 붙어있는
    // VerticalLayoutGroup이 생성 순서대로 수직 나열해준다.
    public TextMeshProUGUI CreateObjectiveRow()
    {
        var row = new GameObject("ObjectiveRow", typeof(RectTransform));
        row.transform.SetParent(transform, false);
        ((RectTransform)row.transform).sizeDelta = new Vector2(500, 50);

        var text = row.AddComponent<TextMeshProUGUI>();
        text.font = objectiveFont;
        text.fontSize = objectiveFontSize;
        text.color = objectiveColor;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.richText = true; // <s> 취소선 태그용

        return text;
    }

    public void ReportVictory() { ... }
    public void ReportDefeat() { ... }
}
```

### 2) `Stage0Objectives.cs` — 텍스트 필드를 인스펙터 연결 대신 자동 생성으로 전환
5개 `[SerializeField] TextMeshProUGUI` 필드를 private 필드로 바꾸고, `Start()`에서
`StageManager.Instance.CreateObjectiveRow()`를 목표 개수(5개: 주목표 3 + 서브목표 2)만큼 호출해서
채운다. 호출 순서 = 화면에 나열되는 순서(주목표 3개 먼저, 서브목표 2개 나중 — 기존 `Main1/2/3`,
`sub1/2` 순서와 동일). `Update()`의 취소선 로직은 완전히 그대로.

**Before:**
```csharp
    [Header("주목표")]
    [SerializeField] private TerritoryZone targetZone; // 점령해야 할 거점 (씬의 TerritoryZone 오브젝트를 연결)
    [SerializeField] private TextMeshProUGUI captureZoneText;
    [SerializeField] private TextMeshProUGUI produceTroopersText;
    [SerializeField] private TextMeshProUGUI buildBarracksText;

    [Header("서브목표")]
    [SerializeField] private TextMeshProUGUI clearEnemiesText;
    [SerializeField] private TextMeshProUGUI secureOreText;

    private RTSUnitController rtsController;

    private float enemyScanTimer;
    private bool enemiesCleared;

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }
```

**After:**
```csharp
    [Header("주목표")]
    [SerializeField] private TerritoryZone targetZone; // 점령해야 할 거점 (씬의 TerritoryZone 오브젝트를 연결)

    private TextMeshProUGUI captureZoneText;
    private TextMeshProUGUI produceTroopersText;
    private TextMeshProUGUI buildBarracksText;
    private TextMeshProUGUI clearEnemiesText;
    private TextMeshProUGUI secureOreText;

    private RTSUnitController rtsController;

    private float enemyScanTimer;
    private bool enemiesCleared;

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();

        // StageObject(VerticalLayoutGroup)에 목표 개수만큼 텍스트 행을 생성 - 이 순서가 화면에
        // 나열되는 순서(주목표 3개 → 서브목표 2개)가 된다.
        captureZoneText = StageManager.Instance.CreateObjectiveRow();
        produceTroopersText = StageManager.Instance.CreateObjectiveRow();
        buildBarracksText = StageManager.Instance.CreateObjectiveRow();
        clearEnemiesText = StageManager.Instance.CreateObjectiveRow();
        secureOreText = StageManager.Instance.CreateObjectiveRow();
    }
```
(나머지 `Update()`/`SetObjectiveText` 호출부는 필드 이름이 그대로라 수정 없음)

## 확인 필요 사항
- `StageManager`(`StageObject` 오브젝트)의 `objectiveFont`에 한글 지원 폰트 에셋을 인스펙터에서
  한 번 연결해줘야 함(기존 `Main1` 등이 쓰던 폰트 에셋을 그대로 넣으면 동일하게 보임). **이 연결은
  요청하신 대로 직접 하실 것으로 가정** — 원하면 알려주면 코드로도 자동 연결 가능(예: 다른 씬
  UI 텍스트에서 폰트를 찾아오는 방식) — 되지만 인스펙터에서 드래그 한 번이 제일 간단하고 확실함.
- `Stage1~5Objectives.cs`는 이번 범위에 포함 안 함(위 "조사" 참고) — 이 방식이 마음에 들면 같은
  패턴으로 이어서 만들어줄 수 있음.
- Unity 에디터에서 실제 컴파일/재생 확인은 못 했음 — 적용 후 uloop-compile로 에러 확인 예정.

## 영향받는 파일
- `Assets/Scripts/System/StageManager.cs` — `CreateObjectiveRow()` 추가
- `Assets/Scripts/System/Stage0Objectives.cs` — 텍스트 필드를 자동 생성 방식으로 전환
