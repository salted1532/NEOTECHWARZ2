# 0503 - 해상도 드롭다운, 씬 전환 후 640x480으로 돌아가는 버그 수정

## 요청 내용
"원래 유니티 에디터 상에선 해상도 변경을 알수 없는거야? 드롭다운 선택하고 나서 바로 갱신이
안되는건가 해상도 적용이 안되는거 같아 선택하더라도 다른씬 가거나 갔다오면 640 x 480 로
돌아가있어"

## 원인
Unity 에디터에서는 씬이 막 로드된 직후(Awake/Start 시점)에 `Screen.width`/`Screen.height`가
아직 Game View가 한 프레임도 렌더링되기 전이라 예전 기본값인 `640x480`을 반환하는 경우가 있음 -
에디터에서만 나타나는 잘 알려진 특성(실제 빌드에서는 발생하지 않음).

`GraphicsSettingsPanel.RefreshSelectedIndex()`가 드롭다운의 "현재 선택값"을 고를 때 이
`Screen.width`/`height`를 기준으로 찾다 보니, 씬을 전환하고 돌아왔을 때 실제로 저장된 해상도
(`PlayerPrefs`)와 무관하게 640x480에 해당하는 옵션이 선택된 것처럼 보이는 문제였음. (doc/0502에서
설명한 "Screen.SetResolution이 에디터에서 창 크기를 안 바꾼다"는 현상과는 별개의, 드롭다운 표시
로직 자체의 버그.)

## 수정
`Assets/Scripts/UI/GraphicsSettingsPanel.cs`의 `RefreshSelectedIndex()`가 `Screen.width`/
`height` 대신 저장된 `PlayerPrefs` 값을 우선 사용하도록 변경. 아직 한 번도 선택/저장한 적이 없을
때만 `Screen.width`/`height`를 폴백으로 사용.

**Before**
```csharp
private void RefreshSelectedIndex()
{
    if (resolutionDropdown == null) return;

    int index = options.FindIndex(r => r.width == Screen.width && r.height == Screen.height);
    if (index >= 0)
        resolutionDropdown.SetValueWithoutNotify(index);
}
```

**After**
```csharp
private void RefreshSelectedIndex()
{
    if (resolutionDropdown == null) return;

    int targetWidth = PlayerPrefs.HasKey(PrefWidth) ? PlayerPrefs.GetInt(PrefWidth) : Screen.width;
    int targetHeight = PlayerPrefs.HasKey(PrefHeight) ? PlayerPrefs.GetInt(PrefHeight) : Screen.height;

    int index = options.FindIndex(r => r.width == targetWidth && r.height == targetHeight);
    if (index >= 0)
        resolutionDropdown.SetValueWithoutNotify(index);
}
```

## 적용 결과
Unity 에디터에서 컴파일 확인 완료: 에러 0, 경고 0, 콘솔 클린. 수정된 `RefreshSelectedIndex()`
내용도 의도대로 반영됐음을 재확인함. (Play Mode는 권한 분류기 문제로 계속 사용 불가 - 정적
컴파일 확인까지만 진행. 실제 씬 전환 후 드롭다운이 저장된 값을 제대로 보여주는지는 에디터에서
직접 확인 필요.)
