using System;
using System.Reflection;
using TMPro;
using UnityEngine;

// 스테이지(미션)의 승리/패배 "결과"만 담당하는 최소 골격.
// 어떤 조건이 목표 달성/패배인지는 이 매니저가 판단하지 않는다 - 각 시스템(적 전멸 판정,
// BaseStructure 파괴 감지 등)에서 조건을 직접 확인한 뒤 ReportVictory()/ReportDefeat()를
// 호출해서 결과만 보고하면, 여기서 상태를 한 번만 고정하고 이벤트로 알린다.
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public enum StageResult { InProgress, Victory, Defeat }

    public StageResult Result { get; private set; } = StageResult.InProgress;

    public event Action OnVictory;
    public event Action OnDefeat;

    [Header("목표 체크리스트 UI")]
    [SerializeField] private TextMeshProUGUI objectiveRowPrefab; // 목표 텍스트 한 줄 프리팹 연결

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 이 오브젝트(StageObject, VerticalLayoutGroup 있음) 밑에 objectiveRowPrefab을 복제해서
    // 자식으로 붙인다. VerticalLayoutGroup이 생성 순서대로 수직 나열해준다.
    public TextMeshProUGUI CreateObjectiveRow()
    {
        return Instantiate(objectiveRowPrefab, transform);
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

    // 임무 목표 달성 시 호출 (예: 적 기지 파괴 등 - 조건 판단은 호출부 책임).
    public void ReportVictory()
    {
        if (Result != StageResult.InProgress) return;
        Result = StageResult.Victory;
        SoundManager.Instance?.PlayMissionSuccessVoice(); // 주목표 달성 나레이션 - 모든 스테이지가 이 지점을 거쳐가므로 한 곳만 훅(doc/0643)
        OnVictory?.Invoke();
    }

    // 패배 조건 충족 시 호출 (예: 아군 본진 파괴 등 - 조건 판단은 호출부 책임).
    public void ReportDefeat()
    {
        if (Result != StageResult.InProgress) return;
        Result = StageResult.Defeat;
        OnDefeat?.Invoke();
    }
}
