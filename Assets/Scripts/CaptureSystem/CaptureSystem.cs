using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 거점(비콘)의 소유 상태. Ally(초록) <-> Neutral(흰색) <-> Enemy(빨강)로, 항상 Neutral을 거쳐서 순환한다.
public enum CaptureOwner { Neutral, Ally, Enemy }

// 거점 포인트에 부착한다. 트리거 콜라이더 안에 있는 아군(UnitController)/적(EnemyController) 유닛 수에 따라
// 부호 있는 점령치(controlValue)를 밀고 당기며, 어느 한쪽이 끝까지 도달하면 그 진영의 소유로 전환한다.
// 양쪽이 동시에 있으면 교착 상태로 진행이 멈춘다. 콜라이더는 Is Trigger가 켜져 있어야 한다.
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

    [Header("디버그 - 인스펙터에서 점령 상태 직접 조종 (테스트용)")]
    [SerializeField] private CaptureOwner debugOwner = CaptureOwner.Neutral;
#if UNITY_EDITOR
    private CaptureOwner lastSyncedDebugOwner = CaptureOwner.Neutral;
#endif

    public CaptureOwner CurrentOwner { get; private set; } = CaptureOwner.Neutral;

    // 콜라이더 트리거 범위 안에 들어와 있는 아군/적 유닛 목록
    private readonly List<UnitController> alliesInRange = new List<UnitController>();
    private readonly List<EnemyController> enemiesInRange = new List<EnemyController>();

    // -captureDuration(완전 적 점령) ~ +captureDuration(완전 아군 점령), 0 = 중립. 0을 반드시 거쳐야
    // 반대 진영으로 넘어갈 수 있다 (Ally <-> Neutral <-> Enemy 순환).
    private float controlValue;

    private void Awake()
    {
        if (territoryZone == null) territoryZone = GetComponentInChildren<TerritoryZone>(true);

        ApplyEffect(CurrentOwner);

        if (captureBar != null)
        {
            captureBar.maxValue = captureDuration;
            captureBar.gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<UnitController>(out var ally))
        {
            if (!alliesInRange.Contains(ally)) alliesInRange.Add(ally);
        }
        else if (other.TryGetComponent<EnemyController>(out var enemy))
        {
            if (!enemiesInRange.Contains(enemy)) enemiesInRange.Add(enemy);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<UnitController>(out var ally))
            alliesInRange.Remove(ally);
        else if (other.TryGetComponent<EnemyController>(out var enemy))
            enemiesInRange.Remove(enemy);
    }

    private void Update()
    {
        alliesInRange.RemoveAll(unit => unit == null); // 파괴된 유닛 정리
        enemiesInRange.RemoveAll(unit => unit == null);

        bool alliesPresent = alliesInRange.Count > 0;
        bool enemiesPresent = enemiesInRange.Count > 0;
        bool contested = alliesPresent && enemiesPresent; // 양쪽 다 있으면 교착 - 진행 정지

        if (!contested)
        {
            if (alliesPresent)
                controlValue = Mathf.Min(controlValue + Time.deltaTime, captureDuration);
            else if (enemiesPresent)
                controlValue = Mathf.Max(controlValue - Time.deltaTime, -captureDuration);
            // 둘 다 없으면 그 자리에서 멈춘다 (리셋하지 않음) - 기존과 동일
        }

        UpdateCaptureBar(alliesPresent, enemiesPresent, contested);
        UpdateOwnerFromControlValue();
    }

    // controlValue가 양 끝(±captureDuration)에 도달했을 때만 소유자가 바뀐다. 그 사이(0 포함)는 전부
    // Neutral 취급 - 기존 "Neutral->Ally"도 원래 이 규칙(완료 전엔 전부 Neutral)이었으므로 대칭 확장일 뿐이다.
    private void UpdateOwnerFromControlValue()
    {
        CaptureOwner newOwner =
            controlValue >= captureDuration ? CaptureOwner.Ally :
            controlValue <= -captureDuration ? CaptureOwner.Enemy :
            CaptureOwner.Neutral;

        if (newOwner == CurrentOwner) return;

        CurrentOwner = newOwner;
        ApplyEffect(newOwner);

        Debug.Log($"점령 상태 변경: {newOwner}");
    }

    // 진행 중인 방향(아군/적) 기준으로 점령 바를 표시한다. 이미 그 방향으로 완전히 점령됐거나,
    // 교착 상태이거나, 아무도 없으면 숨긴다.
    private void UpdateCaptureBar(bool alliesPresent, bool enemiesPresent, bool contested)
    {
        bool progressing = !contested && (alliesPresent || enemiesPresent)
            && !(alliesPresent && controlValue >= captureDuration)
            && !(enemiesPresent && controlValue <= -captureDuration);

        SetCaptureBarVisible(progressing);

        if (!progressing || captureBar == null) return;

        captureBar.value = alliesPresent
            ? Mathf.Clamp(controlValue, 0f, captureDuration)
            : Mathf.Clamp(-controlValue, 0f, captureDuration);
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

#if UNITY_EDITOR
        // 실제 상태(게임 진행에 의한 점령 등)가 바뀔 때마다 디버그 필드도 같이 맞춰서,
        // 인스펙터에 실제 상태가 항상 그대로 보이게 한다 (아래 OnValidate가 이걸 다시 "사용자 조작"으로 오인하지 않도록
        // lastSyncedDebugOwner도 함께 갱신).
        debugOwner = owner;
        lastSyncedDebugOwner = owner;
#endif
    }

#if UNITY_EDITOR
    // 인스펙터에서 debugOwner 드롭다운을 직접 바꿨을 때만(=실제 상태 동기화로 인한 변경이 아닐 때만) 그 값으로 강제 전환한다.
    private void OnValidate()
    {
        if (debugOwner == lastSyncedDebugOwner) return;

        CaptureOwner forced = debugOwner;

        EditorApplication.delayCall += () =>
        {
            if (this == null) return; // 그 사이 오브젝트가 삭제됐을 수 있음

            CurrentOwner = forced;
            controlValue = forced == CaptureOwner.Ally ? captureDuration
                : forced == CaptureOwner.Enemy ? -captureDuration
                : 0f;
            ApplyEffect(forced); // 여기서 debugOwner/lastSyncedDebugOwner도 함께 동기화됨
            SetCaptureBarVisible(false);
        };
    }
#endif
}
