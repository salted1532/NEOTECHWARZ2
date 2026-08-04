# 0426. 부대선택 버튼 눌림(Pressed)/뗌(Unpressed)/이탈 디버그 로그 추가 제안

- 날짜: 2026-08-04

## 요청 내용

> 버튼이 pressed unpressed에 대해서 디버그 로그 추가해서 테스트를 한번더 진행시켜줘 내가 누를때는 거의 10번 넘게 눌러야 1번 부대 선택 되거든?

## 조사 내용

지난번(0424/0425) `onClick`/`SelectControlGroup` 로그로 자동화 클릭(스크립트로 "누르고 바로 뗌"을
순간적으로 재현)은 100% 성공했다. 반면 사용자가 실제로 마우스로 누르면 10번 넘게 눌러야 1번 성공한다는
건, **누르는 순간(PointerDown)과 떼는 순간(PointerUp) 사이에 실제 물리적인 시간차와 손떨림(마우스
미세 이동)이 있는 "진짜 클릭"에서만 재현된다**는 뜻이다.

Unity UI 클릭 판정 규칙상: `OnPointerUp`은 누른 오브젝트에서 무조건 호출되지만, `onClick`(실제 선택
메소드 호출)은 **뗄 때 그 좌표를 다시 레이캐스트해서 누를 때와 같은 오브젝트여야만** 발동한다. 따라서
"진짜 손으로 누를 때만 실패"하는 건, 누르고 있는 동안 커서가 버튼 밖으로 살짝 벗어났다가(`PointerExit`)
버튼 밖에서 손을 떼는 상황과 정확히 들어맞을 가능성이 크다 - 버튼 크기가 화면상 80px 정도로 작아서
(`Squadbutton.prefab` SizeDelta 80x80) 미세한 손떨림에도 벗어나기 쉽다.

이를 직접 눈으로 확인하기 위해, PointerDown/PointerUp/PointerExit 세 시점에 각각 로그를 남긴다.

## 코드 변경 (제안)

`Button`과 같은 GameObject에 `EventTrigger`를 추가해서, 새 클래스/파일 없이 `ControlGroupPanel.cs`
안에서 PointerDown/PointerUp/PointerExit 세 이벤트를 로그로 남긴다.

**기존 코드** (`Assets/Scripts/UI/ControlGroupPanel.cs`):
```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;
```
```csharp
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

        groupButtons[groupIndex] = buttonObj;
    }
```

**변경 코드**:
```csharp
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
```
```csharp
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
```

## 요약 / 영향받는 파일

- PointerDown("PRESSED")/PointerUp("UNPRESSED")/PointerExit("POINTER EXIT") 세 이벤트에 로그 추가.
  새 파일 없이 `EventTrigger` 컴포넌트를 버튼에 붙여서 처리(Button의 기존 클릭 처리와 공존 가능).
- 다음에 실제로 10번 눌러서 1번만 성공하는 상황이 재현되면, 콘솔에서 실패한 시도들이
  `PRESSED → (EXIT 있음?) → UNPRESSED` 만 찍히고 그 뒤에 `클릭됨`/`SelectControlGroup` 로그가
  없는지 확인하면 원인이 바로 나온다.
- 디버그용 로그이므로 원인 확정 후 지워도 되는 임시 코드다.
- 영향받는 파일: `Assets/Scripts/UI/ControlGroupPanel.cs` (예정, 아직 미적용)
