# 0638 - 미션 경과시간 타이머 표시 (제안)

## 요청
미션 시작부터 흐른 시간을 "시:분:초"로 표시. GameManager 프리팹의 MiniMap 밑에 이미 만들어둔 `Timer_Text`가 대상. 날짜 개념 없이 시(時)만 계속 늘어나며, 시가 3자리를 넘어가면(1000시간 이상) 그 시점부터 초과 자릿수만큼 폰트 크기가 1씩 줄어든다.

## 현재 상태
- `Assets/prefabs/Game/GameManager.prefab`의 `MiniMap` GameObject 바로 밑에 `Timer_Text`(TextMeshProUGUI) 확인됨. 지금은 플레이스홀더 텍스트 `"10:10:10"`만 들어있고 갱신 로직 없음.
- 현재 `m_fontSize: 8`, `m_enableAutoSizing: 0`(오토사이징 꺼짐) - 폰트 크기를 코드로 직접 바꿔도 됨.
- 이 오브젝트를 갱신하는 스크립트/컴포넌트는 아직 없음.

## 제안 설계
`Assets/Scripts/UI/MissionTimerDisplay.cs` 새로 작성해서 `Timer_Text` GameObject에 직접 붙임(자기 자신의 `TextMeshProUGUI`를 `GetComponent`로 참조 - 인스펙터 배선 불필요):

```csharp
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class MissionTimerDisplay : MonoBehaviour
{
    private TextMeshProUGUI timerText;
    private float baseFontSize;
    private float startTime;

    private void Awake()
    {
        timerText = GetComponent<TextMeshProUGUI>();
        baseFontSize = timerText.fontSize; // 인스펙터에 넣어둔 현재 크기를 기준값으로 사용
        startTime = Time.time;
    }

    private void Update()
    {
        int totalSeconds = Mathf.FloorToInt(Time.time - startTime);
        int hours = totalSeconds / 3600;
        int minutes = totalSeconds / 60 % 60;
        int seconds = totalSeconds % 60;

        string hourText = hours.ToString("D2");
        timerText.text = $"{hourText}:{minutes:D2}:{seconds:D2}";

        int extraDigits = Mathf.Max(0, hourText.Length - 3);
        timerText.fontSize = baseFontSize - extraDigits;
    }
}
```

- 경과시간은 `Time.time - startTime`으로 계산 - `Time.time`은 스케일드 타임이라 승리 패널 등에서 `Time.timeScale = 0`이 되면 자동으로 멈춘다(별도 일시정지 처리 불필요, `UserControl.IsPaused`와 자연히 맞음).
- 시는 `D2`로 최소 2자리(`00~99`) 표시, 100시간부터는 자연히 3자리(`100`)로 늘어남 - 별도 자리수 고정 없음.
- 폰트 크기: 시 문자열 길이가 3자리를 넘는 순간(4자리, 1000시간)부터 초과 자릿수만큼 `baseFontSize`에서 1씩 차감. 3자리 이하는 기준 크기 그대로.
- 씬 전환/재시작 시 `Awake`가 다시 불려 `startTime`이 리셋되므로 "미션 시작부터"라는 요구를 그대로 만족(별도 리셋 로직 불필요).

## 배선 방법
스크립트는 텍스트 편집으로 만들지만, 프리팹에 컴포넌트를 추가하는 작업은 YAML을 직접 손으로 편집하지 않고 Unity 에디터 API(`uloop-execute-dynamic-code`)로 `Timer_Text`를 찾아 `AddComponent<MissionTimerDisplay>()` 후 프리팹을 저장하는 방식으로 진행 - fileID/guid를 손으로 잘못 적어 프리팹이 깨질 위험을 없앤다.

## 범위 밖
- 타이머 일시정지/재개를 별도로 제어하는 기능 - `Time.timeScale` 연동만으로 충분하다고 판단.
- 미션 외 화면(메인 메뉴 등)에서의 타이머 표시나 저장/기록 - 요청 범위 밖.

## 진행 여부
위 스크립트를 추가하고 `Timer_Text`에 붙여도 될까요?

**사용자 승인**: "진행해줘" (채팅에서 확인, 2026-08-20).

## 구현 완료
- `Assets/Scripts/UI/MissionTimerDisplay.cs` 작성. 컴파일 성공(에러 0).
- `Assets/prefabs/Game/GameManager.prefab`의 `Timer_Text`(fileID 287251321762647327)에 `MissionTimerDisplay` 컴포넌트(fileID 6821985940377600359, script guid `2cb48a7e1df557f41be644b2d57adffc`) 추가 후 저장 - `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`로 진행해서 YAML 손상 위험 없음. 저장 후 재컴파일도 에러 0 확인.

## 상태
완료. 미션 씬 진입 시 `Timer_Text`가 경과시간을 "시:분:초"로 표시하고, 시가 1000시간을 넘으면 자릿수만큼 폰트가 줄어든다.
