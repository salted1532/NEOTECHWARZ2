using TMPro;
using UnityEngine;

// 미션 시작부터 흐른 시간을 "시:분:초"로 표시한다 (doc/0638). Time.time - startTime을 쓰므로
// Time.timeScale이 0(승리 패널 일시정지 등)이 되면 자동으로 멈춘다. 날짜 개념 없이 시만 계속
// 늘어나며, 시 자릿수가 3자리를 넘으면(1000시간 이상) 초과 자릿수만큼 폰트 크기를 1씩 줄인다.
[RequireComponent(typeof(TextMeshProUGUI))]
public class MissionTimerDisplay : MonoBehaviour
{
    private TextMeshProUGUI timerText;
    private float baseFontSize;
    private float startTime;

    private void Awake()
    {
        timerText = GetComponent<TextMeshProUGUI>();
        baseFontSize = timerText.fontSize;
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
