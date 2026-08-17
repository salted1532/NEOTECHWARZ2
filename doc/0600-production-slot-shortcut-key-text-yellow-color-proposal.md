# 0600 - ProductionSlot 단축키 텍스트 노란색 표시

날짜: 2026-08-17

**상태: 구현 완료** (사용자 확인 후 그대로 적용, Unity 컴파일 0 에러 확인)

## 요청 내용

> 글자를 노란색으로 나오도록해줘

[[0599]]에서 추가한 `shortcut_key_Text`(단축키 표시) 글자색을 노란색으로 바꿔달라는 요청.

## 조사 내용

- 현재 프리팹(`GameManager.prefab`)의 `shortcut_key_Text` 9개 모두 `m_fontColor: {r: 1, g: 1, b: 1, a: 1}`(흰색)로 되어 있음.
- 툴팁 설명 문구에서 단축키를 강조할 때 이미 `<color=yellow>`(Unity 기본 yellow) 관례가 있음 (`RTSUnitController.cs:1649` `ShortcutTag`).
- 슬롯마다 프리팹 YAML을 9번 손으로 고치는 대신, `ProductionSlot.cs`의 `Awake()`에서 `shortcutKeyText`를 찾아 연결하는 시점에 `color = Color.yellow`를 한 번 지정하면 9개 슬롯 전부에 동일하게 적용되고 프리팹은 건드릴 필요 없음 (doc/0599와 동일한 이유).

## 계획된 코드 변경

`Assets/Scripts/UI/ProductionSlot.cs`

### 기존 코드

```csharp
        if (shortcutKeyText == null)
            shortcutKeyText = GetComponentInChildren<TMP_Text>(true);
```

### 변경 코드

```csharp
        if (shortcutKeyText == null)
            shortcutKeyText = GetComponentInChildren<TMP_Text>(true);

        if (shortcutKeyText != null)
            shortcutKeyText.color = Color.yellow;
```

## 요약

- `Awake()`에서 `shortcutKeyText`를 찾은 직후 색을 노란색(`Color.yellow`)으로 고정 지정한다. `SetData`/`Clear`에서 텍스트 내용만 바뀌고 색은 그대로 유지되므로 한 번만 지정하면 충분.
- 프리팹의 `m_fontColor`(흰색)는 그대로 두되, 런타임에 코드가 덮어씀.

## 영향받는 파일

- `Assets/Scripts/UI/ProductionSlot.cs` (변경 예정, 아직 미적용)
