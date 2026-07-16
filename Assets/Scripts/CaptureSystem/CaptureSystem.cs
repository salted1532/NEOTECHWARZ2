using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 거점(비콘)의 소유 상태. Neutral(흰색) -> Ally(초록) -> (추후) Enemy(빨강)로 전환된다.
public enum CaptureOwner { Neutral, Ally, Enemy }

// 거점 포인트에 부착한다. 트리거 콜라이더 안에 아군 유닛(UnitController)이 있으면 점령 시간을 누적하고,
// 다 채워지면 점령 완료 처리 및 상태별 이펙트를 전환한다. 콜라이더는 Is Trigger가 켜져 있어야 한다.
public class CaptureSystem : MonoBehaviour
{
    [SerializeField] private float captureDuration = 30f;

    [Header("상태별 이펙트 (흰색/초록/빨강)")]
    [SerializeField] private GameObject neutralEffect;
    [SerializeField] private GameObject allyEffect;
    [SerializeField] private GameObject enemyEffect;

    [Header("UI")]
    [SerializeField] private Slider captureBar; // 점령 진행도 UI (프리팹에서 직접 연결) - captureTimer에 맞춰 값만 자동 갱신됨

    [Header("이 거점이 관리하는 영토 (비워두면 같은 오브젝트에서 자동으로 찾음)")]
    [SerializeField] private TerritoryZone territoryZone;

    public CaptureOwner CurrentOwner { get; private set; } = CaptureOwner.Neutral;

    // 콜라이더 트리거 범위 안에 들어와 있는 아군 유닛 목록
    private readonly List<UnitController> alliesInRange = new List<UnitController>();

    private float captureTimer;

    private void Awake()
    {
        if (territoryZone == null) territoryZone = GetComponent<TerritoryZone>();

        ApplyEffect(CurrentOwner);

        if (captureBar != null)
        {
            captureBar.maxValue = captureDuration;
            captureBar.gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        UnitController unit = other.GetComponent<UnitController>();
        if (unit != null && !alliesInRange.Contains(unit))
            alliesInRange.Add(unit);
    }

    private void OnTriggerExit(Collider other)
    {
        UnitController unit = other.GetComponent<UnitController>();
        if (unit != null)
            alliesInRange.Remove(unit);
    }

    private void Update()
    {
        alliesInRange.RemoveAll(unit => unit == null); // 파괴된 유닛 정리

        if (CurrentOwner == CaptureOwner.Ally)
        {
            SetCaptureBarVisible(false); // 이미 아군이 점령한 상태면 더 진행할 것이 없다
            return;
        }

        bool capturing = alliesInRange.Count > 0;
        SetCaptureBarVisible(capturing);

        if (!capturing)
            return; // 범위 안에 아군이 없으면 타이머는 그 자리에서 멈춘다 (리셋하지 않음)

        captureTimer += Time.deltaTime;

        if (captureBar != null)
            captureBar.value = captureTimer;

        if (captureTimer >= captureDuration)
            CompleteCapture(CaptureOwner.Ally);
    }

    private void CompleteCapture(CaptureOwner newOwner)
    {
        CurrentOwner = newOwner;
        ApplyEffect(newOwner);
        SetCaptureBarVisible(false); // 완료된 뒤에는 진행 바를 볼 필요 없음

        Debug.Log("점령이 되었다");
    }

    private void SetCaptureBarVisible(bool visible)
    {
        if (captureBar != null)
            captureBar.gameObject.SetActive(visible);
    }

    // 현재 소유 상태에 해당하는 이펙트만 켜고 나머지는 끈다.
    private void ApplyEffect(CaptureOwner owner)
    {
        if (neutralEffect != null) neutralEffect.SetActive(owner == CaptureOwner.Neutral);
        if (allyEffect != null) allyEffect.SetActive(owner == CaptureOwner.Ally);
        if (enemyEffect != null) enemyEffect.SetActive(owner == CaptureOwner.Enemy);

        if (territoryZone != null) territoryZone.Owner = owner;
    }
}
