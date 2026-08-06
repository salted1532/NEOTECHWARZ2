# 0442. 5스테이지 OC 부재 처리 (제안)

**날짜:** 2026-08-06

## 요청 내용
> 현재 캠페인에서 4스테이지만 OC와 협력하고 5스테이지는 NTA혼자서 외계종족과 싸우는거로 변경하고
> 싶어 정확히는 OC는 다른 측면에서 공격하러 갔기 때문에 스테이지 전장에는 없는거로 그래서
> 5스테이지 서브 목표인 OC사령부 생존은 빼주고 브리핑 내용도 변경해줘

## 현재 상태

`Docs/Campaign.md` 미션 5("최후의 원정")는 4스테이지와 동일하게 "OC 사령부 생존" 서브 목표를
갖고 있고, 브리핑에도 OC(셀레나 카터)가 같은 전장에 있는 것처럼 등장하지 않지만 서브목표로만
남아있어 앞뒤가 안 맞음. `Assets/Scripts/System/Stage5Objectives.cs`도
[[0438-stage1-5-objectives-proposal]] / [[0440-stage-objective-reflection-auto-wiring-proposal]]에서
Stage4와 동일한 패턴(`ocCommandCenter` 필드 → 파괴되면 영구 실패 고정)으로 구현돼 있음.

## 제안하는 변경

### 1) `Docs/Campaign.md` — 미션 5 브리핑 + 서브 목표 삭제

**Before:**
```markdown
## 미션 5 : 최후의 원정 (Final Offensive) — 5막

**브리핑**

> **부관**: "외계 함대는 후퇴했지만, 거대한 워프 게이트가 열렸습니다. 그 너머에서 놈들의 모행성이 확인되었습니다."
>
> **셀레나**: "여기서 끝내지 못하면 인류에게 미래는 없다."
>
> **아드리안**: "모든 함대. 워프 게이트로 진입한다."
>
> **부관**: "이번 작전의 목표는 외계 지휘 체계의 완전한 파괴입니다."
>
> **아드리안**: "오늘, 이 전쟁을 끝낸다."

**메인 목표**
- 에너지 코어 3개 파괴
- 외계 지휘 코어 제거

**서브 목표**
- OC 사령부 생존
```

**After:**
```markdown
## 미션 5 : 최후의 원정 (Final Offensive) — 5막

**브리핑**

> **부관**: "외계 함대는 후퇴했지만, 거대한 워프 게이트가 열렸습니다. 그 너머에서 놈들의 모행성이 확인되었습니다."
>
> **셀레나 카터 (통신)**: "OC 함대는 별도 경로로 모행성 후방을 친다. 정면은 NTA가 맡아라."
>
> **아드리안**: "알겠다. 이번엔 우리 단독이다."
>
> **부관**: "OC 지상 병력의 지원은 없습니다. 이번 작전은 NTA 단독으로 진행됩니다."
>
> **아드리안**: "상관없다. 모든 함대, 워프 게이트로 진입한다."
>
> **부관**: "이번 작전의 목표는 외계 지휘 체계의 완전한 파괴입니다."
>
> **아드리안**: "오늘, 이 전쟁을 끝낸다."

**메인 목표**
- 에너지 코어 3개 파괴
- 외계 지휘 코어 제거
```
(서브 목표 섹션 자체를 삭제 — OC가 이 전장에 없으므로 "OC 사령부 생존"이라는 목표가 성립하지 않음)

### 2) `Assets/Scripts/System/Stage5Objectives.cs` — OC 사령부 서브목표 제거

**Before:**
```csharp
public class Stage5Objectives : MonoBehaviour
{
    [Header("주목표 - 에너지 코어")]
    [SerializeField] private List<GameObject> energyCores;
    [SerializeField] private TextMeshProUGUI destroyEnergyCoresText;

    [Header("주목표 - 외계 지휘 코어")]
    [SerializeField] private GameObject alienCommandCore;
    [SerializeField] private TextMeshProUGUI destroyCommandCoreText;

    [Header("서브목표")]
    [SerializeField] private GameObject ocCommandCenter;
    [SerializeField] private TextMeshProUGUI survivalOcCommandText;

    private List<GameObject> trackedEnergyCores;
    private bool alienCommandCoreAssigned;
    private bool ocCommandCenterAssigned;
    private bool ocCommandCenterDestroyedPermanently;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        trackedEnergyCores = energyCores.FindAll(core => core != null);
        alienCommandCoreAssigned = alienCommandCore != null;
        ocCommandCenterAssigned = ocCommandCenter != null;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        int destroyedCoreCount = trackedEnergyCores.FindAll(core => core == null).Count;
        bool allCoresDestroyed = trackedEnergyCores.Count > 0 && destroyedCoreCount == trackedEnergyCores.Count;

        bool commandCoreDestroyed = alienCommandCoreAssigned && alienCommandCore == null;

        if (!ocCommandCenterDestroyedPermanently && ocCommandCenterAssigned && ocCommandCenter == null)
            ocCommandCenterDestroyedPermanently = true;

        ObjectiveTextUtil.SetObjectiveText(destroyEnergyCoresText, "(주목표) 에너지 코어 파괴", destroyedCoreCount, trackedEnergyCores.Count);
        ObjectiveTextUtil.SetObjectiveText(destroyCommandCoreText, "(주목표) 외계 지휘 코어 제거", commandCoreDestroyed);
        ObjectiveTextUtil.SetSurvivalObjectiveText(survivalOcCommandText, "(서브) OC 사령부 생존", ocCommandCenterDestroyedPermanently);

        if (allCoresDestroyed && commandCoreDestroyed)
            StageManager.Instance?.ReportVictory();
    }
}
```

**After:**
```csharp
public class Stage5Objectives : MonoBehaviour
{
    [Header("주목표 - 에너지 코어")]
    [SerializeField] private List<GameObject> energyCores;
    [SerializeField] private TextMeshProUGUI destroyEnergyCoresText;

    [Header("주목표 - 외계 지휘 코어")]
    [SerializeField] private GameObject alienCommandCore;
    [SerializeField] private TextMeshProUGUI destroyCommandCoreText;

    private List<GameObject> trackedEnergyCores;
    private bool alienCommandCoreAssigned;

    private void Start()
    {
        StageManager.Instance.WireObjectiveTexts(this);
        trackedEnergyCores = energyCores.FindAll(core => core != null);
        alienCommandCoreAssigned = alienCommandCore != null;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        int destroyedCoreCount = trackedEnergyCores.FindAll(core => core == null).Count;
        bool allCoresDestroyed = trackedEnergyCores.Count > 0 && destroyedCoreCount == trackedEnergyCores.Count;

        bool commandCoreDestroyed = alienCommandCoreAssigned && alienCommandCore == null;

        ObjectiveTextUtil.SetObjectiveText(destroyEnergyCoresText, "(주목표) 에너지 코어 파괴", destroyedCoreCount, trackedEnergyCores.Count);
        ObjectiveTextUtil.SetObjectiveText(destroyCommandCoreText, "(주목표) 외계 지휘 코어 제거", commandCoreDestroyed);

        if (allCoresDestroyed && commandCoreDestroyed)
            StageManager.Instance?.ReportVictory();
    }
}
```

`ObjectiveTextUtil.SetSurvivalObjectiveText()`는 Stage4Objectives가 계속 쓰므로 그대로 둠(4스테이지는
요청대로 OC 협력 유지, "OC 사령부 생존" 서브목표도 그대로).

## 변경 없음
- 미션 4(공동 전선) — OC 협력/서브목표 그대로.
- 캠페인 개요 표(1~5막 요약) — 5막 설명이 이미 "외계종족의 모행성으로 진격, 최종 격퇴"로 OC를
  언급하지 않아 그대로 둬도 모순 없음.

## 영향받는 파일
- `Docs/Campaign.md` — 미션 5 브리핑 교체, 서브 목표 섹션 삭제
- `Assets/Scripts/System/Stage5Objectives.cs` — `ocCommandCenter`/`survivalOcCommandText` 필드와
  관련 로직 삭제

## 확인 필요 사항
- 씬의 Stage5Objectives 컴포넌트 인스펙터에 연결돼 있던 `OC 사령부`/`서브목표 텍스트` 참조는 필드
  삭제 후 자동으로 사라짐(Unity가 알아서 무시) — 별도 정리 불필요.
