# 0424. 부대선택 버튼 클릭 디버그 로그 추가

- 날짜: 2026-08-04

## 요청 내용

> HorizontalLayoutGroup에 생성된 버튼들이 클릭시 작동했는지에 대한 디버그좀 남도록 코드좀 추가해줄래

[[0423-controlgrouppanel-click-race-during-reorder-proposal]]에서 수정한 부대선택 버튼 클릭 문제가
실제로 해결됐는지(또는 여전히 씹히는 경우가 있는지) 눈으로 확인할 수 있도록, 버튼 클릭 시 로그를
남긴다.

## 코드 변경 (적용됨)

`onClick` 리스너가 실제로 호출됐는지(= Unity가 클릭을 버튼까지 도달시켰는지)를 그룹 번호, 인원수,
프레임 번호와 함께 남긴다. 클릭이 "씹히면" 애초에 이 로그 자체가 안 찍히므로, 로그가 안 남는 시점을
재현 상황과 대조하면 된다.

**기존 코드** (`Assets/Scripts/UI/ControlGroupPanel.cs`):
```csharp
        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() => rtsController.SelectControlGroup(groupIndex));
```

**변경 코드**:
```csharp
        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() =>
            {
                Debug.Log($"[ControlGroupPanel] 부대 {DisplayNumber(groupIndex)}번 버튼 클릭됨 (frame {Time.frameCount})");
                rtsController.SelectControlGroup(groupIndex);
            });
```

## 요약 / 영향받는 파일

- 클릭이 버튼까지 도달했는지(= onClick이 실제로 발동했는지)를 콘솔에서 확인할 수 있는 로그 한 줄 추가.
- 디버그용 로그이므로, 문제 재현/확인이 끝나면 나중에 지워도 되는 임시 코드다.
- 영향받는 파일: `Assets/Scripts/UI/ControlGroupPanel.cs` (적용 완료, 컴파일 확인 완료 - 0 errors)
