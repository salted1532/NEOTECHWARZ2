# 0599 - ProductionSlot 단축키 텍스트 표시

날짜: 2026-08-17

**상태: 구현 완료** (사용자 확인 후 그대로 적용, Unity 컴파일 0 에러 확인)

## 요청 내용

> OrderButtons 아래 panel 안에 slot버튼들 안에 shortcut_key_Text를 각각 넣었거든 해당하는 버튼이 활성화 되고 추가되었을때 해당하는 버튼의 단축키를 표시하도록 해줘

## 조사 내용

- `Assets/prefabs/Game/GameManager.prefab`에서 `OrderButtons` → `Panel` 아래 `Slot0`~`Slot8` 9개 슬롯을 확인. 각 슬롯 GameObject에는 `RectTransform`, `Image`(Button 타깃 그래픽), `Button`, `ProductionSlot` 컴포넌트가 붙어 있고, 자식으로 `shortcut_key_Text`(TextMeshProUGUI) 오브젝트가 하나씩 이미 존재함.
- 예: Slot0의 `shortcut_key_Text`는 `m_text: w`처럼 에디터에서 손으로 박아넣은 고정 문자열 상태 — 실제 단축키와 무관하게 항상 같은 글자가 보임.
- `Assets/Scripts/UI/ProductionSlot.cs`를 확인하니 이미 `KeyCode shortcut` 필드가 있고 `SetData(UIController.CommandButtonData data)`에서 `shortcut = data.Shortcut;`으로 받아 `Update()`에서 단축키 입력을 감지해 클릭을 시뮬레이션하는 로직까지 있음. 다만 이 값을 화면에 텍스트로 보여주는 코드는 없음.
- `UIController.CommandButtonData.Shortcut` (`Assets/Scripts/UI/UIController.cs`)이 `RTSUnitController`에서 유닛/건물마다 다른 `KeyCode`(예: `data.shortcutKey`, `BuildPanelShortcuts` 딕셔너리, `KeyCode.Y`/`KeyCode.M` 등)로 채워져 `ShowUnitTierPanel`/`ShowBuildModePanel`/이동·공격 패널 등에서 슬롯마다 동적으로 달라짐. 즉 슬롯이 재사용될 때마다 실제 단축키가 바뀌므로, 프리팹에 박아둔 고정 텍스트로는 맞지 않음.
- 툴팁 설명 문구를 만들 때 이미 `KeyCode.ToString()`(예: `ShortcutTag(KeyCode.Y)` → `<color=yellow>Y</color>`)을 그대로 쓰는 관례가 있음 (`RTSUnitController.cs:1649`) — 별도 매핑 테이블 없이 `KeyCode` enum 이름을 그대로 표시하면 충분.
- `iconImage` 필드와 동일하게 `Awake()`에서 `[SerializeField]`가 비어 있으면 `GetComponentInChildren<>`로 자동 연결하는 기존 패턴이 있음 (`ProductionSlot.cs:46-47`). 슬롯 하나당 TMP 텍스트 자식이 `shortcut_key_Text` 하나뿐이므로 같은 방식을 쓰면 9개 슬롯 프리팹을 일일이 인스펙터에서 필드 연결(에셋 YAML 수정)할 필요가 없음.

## 계획된 코드 변경

`Assets/Scripts/UI/ProductionSlot.cs`

### 기존 코드

```csharp
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

...

public class ProductionSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;

    ...

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (button == null)
            button = GetComponent<Button>();

        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);

        // 오버레이가 클릭을 가로채 버튼이 안 눌리는 사고를 막기 위한 방어 처리 (인스펙터에서 깜빡 안 꺼도 항상 안전).
        if (cooldownOverlayImage != null)
            cooldownOverlayImage.raycastTarget = false;
    }

    public void SetData(UIController.CommandButtonData data)
    {
        gameObject.SetActive(true);

        this.data = data;
        hasData = true;
        callback = data.Callback;
        shortcut = data.Shortcut;

        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.enabled = data.Icon != null;
        }

        if (button != null)
        {
            button.interactable =
                data.Interactable &&
                data.Callback != null;
        }
    }

    public void Clear()
    {
        callback = null;
        hasData = false;
        shortcut = KeyCode.None;

        ...

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (button != null)
            button.interactable = false;

        SetCooldownFill(0f);

        gameObject.SetActive(false);
    }
```

### 변경 코드

```csharp
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

...

public class ProductionSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text shortcutKeyText; // 슬롯 단축키 표시용 (예: KeyCode.Y → "Y") - 비워두면 자식에서 자동 탐색

    ...

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (button == null)
            button = GetComponent<Button>();

        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);

        if (shortcutKeyText == null)
            shortcutKeyText = GetComponentInChildren<TMP_Text>(true);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);

        // 오버레이가 클릭을 가로채 버튼이 안 눌리는 사고를 막기 위한 방어 처리 (인스펙터에서 깜빡 안 꺼도 항상 안전).
        if (cooldownOverlayImage != null)
            cooldownOverlayImage.raycastTarget = false;
    }

    public void SetData(UIController.CommandButtonData data)
    {
        gameObject.SetActive(true);

        this.data = data;
        hasData = true;
        callback = data.Callback;
        shortcut = data.Shortcut;

        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.enabled = data.Icon != null;
        }

        if (shortcutKeyText != null)
            shortcutKeyText.text = shortcut != KeyCode.None ? shortcut.ToString() : string.Empty;

        if (button != null)
        {
            button.interactable =
                data.Interactable &&
                data.Callback != null;
        }
    }

    public void Clear()
    {
        callback = null;
        hasData = false;
        shortcut = KeyCode.None;

        ...

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (shortcutKeyText != null)
            shortcutKeyText.text = string.Empty;

        if (button != null)
            button.interactable = false;

        SetCooldownFill(0f);

        gameObject.SetActive(false);
    }
```

## 요약

- `ProductionSlot`에 `shortcutKeyText` 필드를 추가하고, `iconImage`와 동일한 패턴으로 `Awake()`에서 비어 있으면 자식의 `TMP_Text`(`shortcut_key_Text`)를 자동으로 찾아 연결한다.
- `SetData()`(슬롯 활성화/데이터 채움 시점)에서 `data.Shortcut`(`KeyCode`) 값을 `KeyCode.ToString()`으로 텍스트에 반영하고, 단축키가 없으면(`KeyCode.None`) 빈 문자열로 비운다.
- `Clear()`(슬롯 비활성화 시점)에서도 텍스트를 비워, 다른 커맨드로 재사용될 때 이전 단축키 글자가 잠깐 남아있는 것을 방지한다.
- 프리팹(`GameManager.prefab`)의 9개 `shortcut_key_Text`에 손으로 박아둔 고정 문자(`w` 등)는 `Awake()` 자동 연결 + 런타임 갱신으로 대체되므로, 프리팹 YAML 자체는 건드릴 필요 없음(필드 연결도 자동 탐색이라 인스펙터 수동 연결 불필요).

## 영향받는 파일

- `Assets/Scripts/UI/ProductionSlot.cs` (변경 예정, 아직 미적용)
