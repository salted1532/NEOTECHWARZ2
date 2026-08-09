# 0504 - 해상도 드롭다운 내림차순 정렬

## 요청 내용
"빌드 완료해서 테스트했고 해상도 정상적으로 변경되는데 드롭다운안에 해상도를 높은 해상도부터
나오도록 순서를 반전시켜줘"

빌드에서 `Screen.SetResolution`이 정상 동작함을 사용자가 직접 확인함 (doc/0501~0503에서 만든
기능이 실제로 동작). 이제 드롭다운 옵션 순서만 높은 해상도 → 낮은 해상도로 바꿔달라는 요청.

## 변경 내용
`Assets/Scripts/UI/GraphicsSettingsPanel.cs`의 `BuildOptions()`에서 중복 제거한 해상도 목록을
가로x세로(픽셀 수) 기준 내림차순으로 정렬하는 한 줄 추가.

```csharp
options.Clear();
options.AddRange(seen.Values);
options.Sort((a, b) => (b.width * b.height) - (a.width * a.height)); // 높은 해상도부터

var labels = new List<string>(options.Count);
```

## 적용 결과
컴파일 확인 완료: 에러 0, 경고 0, 콘솔 클린.
