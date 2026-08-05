using TMPro;
using UnityEngine;

// 스테이지 목표 체크리스트 텍스트 표시 공통 헬퍼(Stage0~5Objectives 공유). 완료 시 <s>(취소선)로
// 감싸고, 미완료면 그대로 표시한다. 매 프레임 다시 호출되는 것을 전제로 하므로 "한 번 완료되면
// 고정"하지 않는다 - 조건이 다시 깨지면 취소선도 자동으로 사라진다.
public static class ObjectiveTextUtil
{
    public static void SetObjectiveText(TextMeshProUGUI text, string description, bool complete)
    {
        if (text == null) return;
        text.text = complete ? $"<s>{description}</s>" : description;
    }

    // 개수 비교형 목표용 오버로드 - "설명 (현재/목표)" 형식으로 표시(요청사항: 9/10 형식).
    // 현재값이 목표를 넘어도 표시는 목표치에서 고정(예: 1050/1000이 아니라 1000/1000으로 표시).
    public static void SetObjectiveText(TextMeshProUGUI text, string description, int current, int target)
    {
        if (text == null) return;
        bool complete = current >= target;
        string content = $"{description} ({Mathf.Min(current, target)}/{target})";
        text.text = complete ? $"<s>{content}</s>" : content;
    }

    // 생존형 목표용 오버로드(예: OC 사령부 생존) - 살아있는 동안은 취소선 없이 그대로 표시하다가,
    // 파괴되면 실패로 확정되므로 취소선을 긋고 "(실패)"를 덧붙인다(한 번 실패하면 되돌아가지 않음).
    public static void SetSurvivalObjectiveText(TextMeshProUGUI text, string description, bool failed)
    {
        if (text == null) return;
        text.text = failed ? $"<s>{description}</s> (실패)" : description;
    }
}
