using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 부대(컨트롤 그룹) 선택 버튼을 그룹이 생기고/전멸할 때마다 생성/파괴한다.
// buttonContainer에 HorizontalLayoutGroup을 둬서(씬에서 직접 구성) sibling index만 그룹 번호 오름차순으로
// 맞추면, "왼쪽부터 그룹번호 순" 배치와 "하나 없어지면 나머지가 왼쪽으로 당겨지는" 동작이 레이아웃
// 그룹에 의해 공짜로 처리된다 - 좌표를 직접 계산하지 않는다.
public class ControlGroupPanel : MonoBehaviour
{
    [SerializeField] private GameObject buttonPrefab; // Button + 자식에 TextMeshProUGUI 하나 필요
    [SerializeField] private Transform buttonContainer; // HorizontalLayoutGroup이 달린 부모 (Info_panel 위)

    private RTSUnitController rtsController;
    private readonly GameObject[] groupButtons = new GameObject[10];

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    private void Update()
    {
        if (rtsController == null || buttonPrefab == null || buttonContainer == null)
            return;

        // 좌클릭 중(다운~업)에는 버튼 생성/파괴/재배치를 미룬다 - 클릭 도중 다른 부대가 전멸/생성돼
        // 버튼이 옆으로 밀리거나 통째로 교체되면, 마우스를 뗄 때 그 자리에 클릭 대상이 없어 선택이
        // 씹히는 문제가 있었다(부대선택 버튼이 "가끔" 안 눌리던 원인).
        if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
            return;

        bool changed = false;

        for (int i = 0; i < groupButtons.Length; i++)
        {
            bool hasMembers = rtsController.PurgeAndCountControlGroup(i) > 0;
            bool hasButton = groupButtons[i] != null;

            if (hasMembers && !hasButton)
            {
                CreateButton(i);
                changed = true;
            }
            else if (!hasMembers && hasButton)
            {
                Destroy(groupButtons[i]);
                groupButtons[i] = null;
                changed = true;
            }
        }

        if (changed)
            ReorderButtons();
    }

    private void CreateButton(int groupIndex)
    {
        GameObject buttonObj = Instantiate(buttonPrefab, buttonContainer);

        TextMeshProUGUI label = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = DisplayNumber(groupIndex);

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

    // 그룹번호 오름차순으로 sibling index를 다시 매긴다 - HorizontalLayoutGroup이 그 순서대로 왼쪽부터 배치.
    private void ReorderButtons()
    {
        int siblingIndex = 0;

        for (int i = 0; i < groupButtons.Length; i++)
        {
            if (groupButtons[i] != null)
                groupButtons[i].transform.SetSiblingIndex(siblingIndex++);
        }
    }

    // 인덱스 0~8은 키보드 1~9 그대로, 인덱스 9는 키보드 0으로 지정하는 그룹이라 "0"으로 표시.
    private static string DisplayNumber(int groupIndex) => groupIndex == 9 ? "0" : (groupIndex + 1).ToString();
}
