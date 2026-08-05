# 0440. Stage0~5Objectives 목표 텍스트 리플렉션 기반 자동 생성/연결

**상태: 적용됨** (uloop-compile 에러 0개 확인, 기존 경고 37개는 이번 변경과 무관)

## 날짜
2026-08-05

## 요청
"현재 stage1~5까지 스크립트가 추가되었으니깐 확인해보고 그것까지 포함해서 할수 있도록 해줘
현재 stage objectives 스크립트들이 각 text mesh pro 들이 인스펙터로 연결이 필요한대 각
스테이지마 stage objectives 스크립트 컴포넌트를 연결하면 자동으로 인스펙터 상에도 연결되도록
VerticalLayoutGroup -> 각 미션 오브젝트 개수 만큼 텍스트 생성 -> stage objectives에 텍스트 연결
-> 목표 달성에 따른 -선 변화 -> 목표 완료시 승리화면 출력
이런 로직으로 잘 작동하도록 해줘 저 효율적으로 작동시킬수 있는 방법이 있는지 알아봐줘"

## 조사

### 1) Stage1~5Objectives 확인
[[0438-stage1-5-objectives-proposal]]에서 설계했던 대로 6개 스크립트가 전부 디스크에 있음
(`Assets/Scripts/System/Stage0~5Objectives.cs`, `ObjectiveTextUtil.cs`). 각 스크립트가 가진
`TextMeshProUGUI` 필드 개수(=화면에 나열돼야 할 목표 행 개수):

| 스크립트 | 주목표 텍스트 | 서브목표 텍스트 | 합계 |
|---|---|---|---|
| Stage0Objectives | captureZoneText, produceTroopersText, buildBarracksText (3) | clearEnemiesText, secureOreText (2) | 5 |
| Stage1Objectives | destroyMainBaseText (1) | secureOreText, captureRadarBaseText, destroyAllEnemyBuildingsText (3) | 4 |
| Stage2Objectives | collectArtifactText (1) | collectResearchDataText (1) | 2 |
| Stage3Objectives | destroyOutpostText (1) | rescueSurvivorsText (1) | 2 |
| Stage4Objectives | destroyCommandBaseText (1) | survivalOcCommandText (1) | 2 |
| Stage5Objectives | destroyEnergyCoresText, destroyCommandCoreText (2) | survivalOcCommandText (1) | 3 |

지금 전부 `[SerializeField] private TextMeshProUGUI ...`라 스테이지마다 씬에서 텍스트
오브젝트를 손으로 만들고 하나하나 드래그해서 연결해야 하는 상태(요청하신 "인스펙터로 연결이
필요한" 문제).

### 2) 승리화면은 이미 있음
`Assets/Scripts/UI/VictoryPanelController.cs`가 이미 `StageManager.Instance.OnVictory`를
구독해서 패널을 띄우는 로직을 완성해서 가지고 있음(딜레이 연출, 메인메뉴/다음스테이지/계속하기
버튼 처리까지 전부 구현됨). **"목표 완료시 승리화면 출력" 부분은 이미 작동 중 — 변경 불필요.**
(각 StageXObjectives가 주목표 완료 시 `StageManager.Instance?.ReportVictory()`를 호출 →
`StageManager.OnVictory` 이벤트 발생 → `VictoryPanelController`가 패널 표시)

### 3) 효율적으로 만드는 방법 — 리플렉션 기반 범용 자동 연결
[[0439-stageobject-dynamic-objective-rows-proposal]]에서 제안했던 "스크립트마다
`CreateObjectiveRow()`를 필드 개수만큼 손으로 호출"하는 방식은, 스테이지마다 몇 번 호출해야
하는지(3~5개, 스크립트마다 다름)를 매번 맞춰 적어야 해서 요청하신 "컴포넌트만 연결하면 자동으로"
와는 거리가 있음. 더 효율적인 방법: **`StageManager`가 리플렉션으로 대상 스크립트의
`TextMeshProUGUI` 필드를 전부 찾아서, 비어있는 필드마다 자동으로 행을 만들어 채워준다.**

- 스테이지 스크립트 쪽은 `Start()` 맨 앞에 `StageManager.Instance.WireObjectiveTexts(this);`
  한 줄만 추가하면 끝 — 몇 개 필드가 있는지 알 필요도, 개수를 맞춰 적을 필요도 없음. 새 목표
  텍스트 필드를 나중에 추가해도 이 한 줄은 안 건드려도 됨.
- 이미 인스펙터에서 수동으로 연결해둔 필드가 있으면 건드리지 않음(값이 있으면 건너뜀) — 나중에
  특정 텍스트만 직접 배치하고 싶으면 그렇게 해도 됨.
- Unity의 "파괴된/끊어진 참조"(예: 삭제된 오브젝트를 가리키던 예전 필드)도 `UnityEngine.Object`로
  캐스팅해서 비교하므로 정상적으로 다시 채워짐 — `Stage0Objectives`가 지금 가진, 삭제된
  `Main1` 등을 가리키던 끊어진 참조도 이 방식으로 자동 복구됨.
- `[SerializeField] TextMeshProUGUI` 필드 선언 자체는 그대로 유지되므로, 게임을 플레이해보면
  인스펙터에 실제로 연결된 값이 그대로 보임(요청하신 "인스펙터 상에도 연결되도록"에 해당).

**전제/한계**: 필드를 만드는 순서(=화면에 나열되는 순서)는 클래스에 선언된 순서를 그대로 따름
(`GetFields()`가 반환하는 순서 = 선언 순서, Mono/IL2CPP에서 실질적으로 안정적이지만 C# 스펙이
공식 보장하는 동작은 아님). 지금처럼 각 스크립트가 상속 없는 단일 클래스이고 필드를
`[Header]`로 주목표→서브목표 순서대로 선언해두는 한 문제 없음.

## 제안하는 변경

### 1) `StageManager.cs`
`CreateObjectiveRow()`(행 1개 생성) + `WireObjectiveTexts()`(대상 스크립트의 빈
`TextMeshProUGUI` 필드를 리플렉션으로 찾아 전부 채움) 추가.

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
using System.Reflection;
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
    [SerializeField] private TMP_FontAsset objectiveFont; // 한글 지원 폰트 연결 (예: 기존 목표 텍스트가 쓰던 폰트)
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

    // 이 오브젝트(StageObject, VerticalLayoutGroup 있음) 밑에 텍스트 한 줄을 만들어 반환한다.
    // 자식으로 붙으면 VerticalLayoutGroup이 생성 순서대로 수직 나열해준다.
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

    // stageObjectives(Stage0~5Objectives 등)가 가진 TextMeshProUGUI 필드를 리플렉션으로 전부
    // 찾아서, 아직 비어있는(인스펙터에서 연결 안 했거나 참조가 끊어진) 필드마다 행을 새로 만들어
    // 채워준다. 이미 값이 있는 필드는 그대로 둔다 - 직접 배치하고 싶은 텍스트가 있으면 수동으로
    // 연결해도 덮어쓰지 않는다. 각 스테이지 스크립트는 Start() 맨 앞에서 이거 한 줄만 호출하면 됨.
    public void WireObjectiveTexts(MonoBehaviour stageObjectives)
    {
        FieldInfo[] fields = stageObjectives.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (FieldInfo field in fields)
        {
            if (field.FieldType != typeof(TextMeshProUGUI))
                continue;

            var current = (TextMeshProUGUI)field.GetValue(stageObjectives);
            if (current == null) // Unity의 파괴된/끊어진 참조 판정 포함(캐스팅 후 비교)
                field.SetValue(stageObjectives, CreateObjectiveRow());
        }
    }

    public void ReportVictory() { ... }
    public void ReportDefeat() { ... }
}
```

### 2) `Stage0Objectives.cs` ~ `Stage5Objectives.cs` — `Start()` 맨 앞에 한 줄씩 추가
필드 선언은 전부 그대로. 6개 스크립트 모두 동일하게 `Start()` 첫 줄에
`StageManager.Instance.WireObjectiveTexts(this);` 추가.

예) `Stage0Objectives.cs`:

**Before:**
```csharp
    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }
```

**After:**
```csharp
    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }
```

`Stage1/2/3Objectives`도 기존 `Start()` 첫 줄에 동일하게 추가. `Stage4Objectives`/
`Stage5Objectives`는 지금 `Start()`가 있으니 그 첫 줄에 추가.

### 변경 없음
- `ObjectiveTextUtil.SetObjectiveText` (취소선 로직) — 그대로.
- `VictoryPanelController.cs` — 이미 완성돼 있어서 손댈 필요 없음.

## 확인 필요 사항
- `StageManager`(`StageObject`)의 `objectiveFont`에 한글 지원 폰트를 인스펙터에서 한 번 연결
  필요(요청하신 "직접 하실 것" 범위 — 원래 `Main1` 등이 쓰던 폰트 에셋을 그대로 넣으면 동일하게
  보임). 안 넣으면 TMP 기본 폰트로 나오는데, 한글 문자가 깨져 보일 수 있음.
- 필드 나열 순서는 클래스 내 선언 순서를 따름(위 "전제/한계" 참고) — 지금 6개 스크립트 전부
  주목표를 먼저, 서브목표를 나중에 선언해뒀으므로 화면에도 그 순서로 나열됨.
- Play 모드에서 실제 목표 텍스트가 나열되고 취소선이 정상 동작하는지는 Unity 에디터에서 직접
  플레이해서 확인 부탁(uloop-compile은 컴파일만 확인, 런타임 동작은 미확인).

## 영향받는 파일
- `Assets/Scripts/System/StageManager.cs` — `CreateObjectiveRow()`, `WireObjectiveTexts()` 추가
- `Assets/Scripts/System/Stage0Objectives.cs` ~ `Stage5Objectives.cs` — `Start()`에 한 줄씩 추가

## 후속 변경 — 코드로 스타일 지정 대신 프리팹 사용
사용자가 목표 텍스트 한 줄을 직접 만든 프리팹으로 연결하고 싶다고 해서, `CreateObjectiveRow()`가
`font`/`fontSize`/`color`를 코드로 지정하며 `new GameObject(...)`로 새로 만드는 대신, 인스펙터에
연결한 프리팹을 그대로 `Instantiate()`하도록 변경(스타일은 전부 프리팹 쪽에서 관리 — 코드가 더
짧아지고, 프리팹만 바꾸면 스타일도 자유롭게 바꿀 수 있음).

**Before:**
```csharp
[Header("목표 체크리스트 UI")]
[SerializeField] private TMP_FontAsset objectiveFont; // 한글 지원 폰트 연결 (예: 기존 목표 텍스트가 쓰던 폰트)
[SerializeField] private int objectiveFontSize = 28;
[SerializeField] private Color objectiveColor = new Color(0.5424528f, 0.7460864f, 1f, 1f);

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
```

**After:**
```csharp
[Header("목표 체크리스트 UI")]
[SerializeField] private TextMeshProUGUI objectiveRowPrefab; // 목표 텍스트 한 줄 프리팹 연결

public TextMeshProUGUI CreateObjectiveRow()
{
    return Instantiate(objectiveRowPrefab, transform);
}
```

uloop-compile 에러/경고 0개 확인.

### 확인 필요 사항 (갱신)
- `StageManager`(`StageObject`)의 `Objective Row Prefab`에 텍스트 프리팹을 인스펙터에서 연결
  필요 — 프리팹 자체가 `TextMeshProUGUI` 컴포넌트를 가지고 있고, `richText`가 켜져 있어야
  `<s>` 취소선 태그가 렌더링됨(꺼져 있으면 태그가 글자 그대로 보임 - 프리팹 만들 때 확인 필요).

## 후속 변경 — 주목표에도 "(주목표)" 표시 추가
서브목표는 이미 각 스크립트에서 설명 문자열 앞에 `"(서브) "`를 직접 붙여서 구분해왔는데, 주목표는
접두어가 없었음. 요청대로 주목표 설명 문자열에도 전부 `"(주목표) "`를 붙여서 대칭으로 맞춤(기존
`"(서브) "` 관례를 그대로 따른 것 — 새 로직/필드 없이 문자열 리터럴만 수정).

| 파일 | 대상(주목표) |
|---|---|
| Stage0Objectives.cs | 거점 점령, 트루퍼 생산, 병영 건설 (3개) |
| Stage1Objectives.cs | OC 전초기지 파괴 |
| Stage2Objectives.cs | 외계 유물 확보 |
| Stage3Objectives.cs | 외계 전초기지 제거 |
| Stage4Objectives.cs | 외계 사령기지 파괴 |
| Stage5Objectives.cs | 에너지 코어 파괴, 외계 지휘 코어 제거 (2개) |

uloop-compile 에러 0개 확인(경고 37개는 전부 이번 변경과 무관한 기존 경고).

## 후속 변경 — "OC 사령부 생존" 실패 시 "(실패)" 표시
Stage4/5의 서브목표 "OC 사령부 생존"은 살아있는 동안 계속 완료(취소선)로 표시되다가 파괴되면
영구히 미완료로 고정되는데, 지금까지는 그 미완료 상태가 "아직 안 한 목표"와 똑같이 밋밋한
텍스트로 보여서 실패인지 진행 중인지 구분이 안 됐음. 요청대로 실패 확정 시에도 취소선은 유지한
채 뒤에 "(실패)"를 덧붙이도록 `ObjectiveTextUtil.SetSurvivalObjectiveText()`를 신설:

```csharp
// 생존형 목표용 오버로드(예: OC 사령부 생존) - 살아있는 동안은 계속 완료(취소선)로 표시하다가,
// 파괴되면 실패로 확정되므로 취소선은 유지한 채 "(실패)"를 덧붙여 완료와 구분한다.
public static void SetSurvivalObjectiveText(TextMeshProUGUI text, string description, bool failed)
{
    if (text == null) return;
    text.text = failed ? $"<s>{description}</s> (실패)" : $"<s>{description}</s>";
}
```

`Stage4Objectives.cs`/`Stage5Objectives.cs`의 `survivalOcCommandText` 호출을 이 오버로드로 교체하고,
이제 안 쓰는 `ocCommandSurvived` 로컬 변수는 제거(파괴 여부 `ocCommandCenterDestroyedPermanently`를
직접 넘김).

uloop-compile 에러 0개 확인(경고는 이번 변경과 무관한 기존 경고만 남음).

### 영향받는 파일 (후속)
- `Assets/Scripts/System/ObjectiveTextUtil.cs` — `SetSurvivalObjectiveText()` 추가
- `Assets/Scripts/System/Stage4Objectives.cs`, `Stage5Objectives.cs` — 생존 목표 표시를 새 오버로드로 교체

## 후속 변경 — 생존 중엔 취소선 없이, 파괴 시에만 취소선+"(실패)"
바로 위 버전은 생존 중에도 계속 취소선으로 표시했는데, 요청대로 **생존 중엔 취소선 없이 그대로,
파괴된 순간부터만 취소선 + "(실패)"**로 바꿈 - `SetSurvivalObjectiveText`의 `failed=false` 분기만
`$"<s>{description}</s>"` → `description`(취소선 없음)으로 수정. `failed=true` 분기(취소선+"(실패)")는
그대로. uloop-compile 에러 0개 확인.
