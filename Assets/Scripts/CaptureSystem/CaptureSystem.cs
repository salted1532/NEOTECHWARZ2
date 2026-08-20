using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FischlWorks_FogWar;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 거점(비콘)의 소유 상태. Ally(초록) <-> Neutral(흰색) <-> Enemy(빨강)로, 항상 Neutral을 거쳐서 순환한다.
public enum CaptureOwner { Neutral, Ally, Enemy }

// 거점 포인트에 부착한다. 트리거 콜라이더 안에 있는 아군(UnitController)/적(EnemyUnitController) 유닛 수에 따라
// 부호 있는 점령치(controlValue)를 밀고 당기며, 어느 한쪽이 끝까지 도달하면 그 진영의 소유로 전환한다.
// 양쪽이 동시에 있으면 교착 상태로 진행이 멈춘다. 콜라이더는 Is Trigger가 켜져 있어야 한다.
public class CaptureSystem : MonoBehaviour
{
    [SerializeField] private float captureDuration = 30f;

    // 아무도 없을 때(방치) 원래 상태로 되돌아가는 속도, 그리고 아직 한 번도 완전히 점령된 적 없는(Neutral)
    // 상태에서 상대 진행치를 지울 때 점령 속도(1/sec)에 더해지는 보너스 속도. 기본값을 점령 속도와 같은
    // 1로 둬서 "지우는 중엔 2배속"이 정확히 2배가 되도록 함 (doc/0357).
    [SerializeField] private float decayRate = 1f;

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

    // 콜라이더 트리거 범위 안에 들어와 있는 아군/적 유닛 목록. 실린더 콜라이더가 서로 겹치는 여러
    // BoxCollider로 구성되어 있어서, 한 유닛이 동시에 여러 박스 안에 있을 수 있다. 그래서 이 리스트는
    // "겹친 박스 개수만큼" 같은 유닛이 중복으로 들어갈 수 있는 멀티셋으로 쓴다 (Enter마다 추가,
    // Exit마다 하나만 제거) - Count > 0이면 최소 하나의 박스 안에 있다는 뜻. 유닛당 1개로 중복 제거해
    // 버리면, 겹치는 두 박스 사이를 지나갈 때(박스A는 나가지만 여전히 박스B 안인 순간) Exit 하나만으로
    // 유닛이 통째로 빠져버려서 점령 진행이 계속 끊기고 되감기는 버그가 있었다.
    private readonly List<UnitController> alliesInRange = new List<UnitController>();
    private readonly List<EnemyUnitController> enemiesInRange = new List<EnemyUnitController>();

    // -captureDuration(완전 적 점령) ~ +captureDuration(완전 아군 점령), 0 = 중립. 0을 반드시 거쳐야
    // 반대 진영으로 넘어갈 수 있다 (Ally <-> Neutral <-> Enemy 순환).
    private float controlValue;

    // 점령 타이머 슬라이더가 안개(Y≈1의 물리적 Plane)보다 훨씬 높이 떠 있어서 깊이 테스트로는 절대
    // 안 가려지는 문제의 대안 - 안개 상태를 직접 조회해서 UpdateCaptureBar()에서 표시 여부에 반영한다
    // (doc/0358).
    private csFogWar fogWar;

    private void Awake()
    {
        if (territoryZone == null) territoryZone = GetComponentInChildren<TerritoryZone>(true);
        fogWar = FindFirstObjectByType<csFogWar>();

        // 씬에 미리 설정해둔 시작 소유 상태(debugOwner)를 게임 시작 시점(Play 모드/빌드 모두)에 반영한다.
        // 기존엔 OnValidate()가 에디터에서만 이 값을 CurrentOwner/territoryZone에 동기화했기 때문에,
        // 빌드에서는 Awake()가 항상 기본값(Neutral)으로 시작해 에디터에서 저장한 상태를 덮어쓰는 버그가 있었다.
        CurrentOwner = debugOwner;
        controlValue = debugOwner == CaptureOwner.Ally ? captureDuration
            : debugOwner == CaptureOwner.Enemy ? -captureDuration
            : 0f;

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
            alliesInRange.Add(ally);
        else if (other.TryGetComponent<EnemyUnitController>(out var enemy))
            enemiesInRange.Add(enemy);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<UnitController>(out var ally))
            alliesInRange.Remove(ally);
        else if (other.TryGetComponent<EnemyUnitController>(out var enemy))
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
                controlValue = Mathf.Min(controlValue + AllyRate() * Time.deltaTime, captureDuration);
            else if (enemiesPresent)
                controlValue = Mathf.Max(controlValue - EnemyRate() * Time.deltaTime, -captureDuration);
            else
                controlValue = Mathf.MoveTowards(controlValue, RestPoint(), decayRate * Time.deltaTime);
        }

        UpdateCaptureBar(alliesPresent, enemiesPresent, contested);
        UpdateOwnerFromControlValue();
    }

    // 아군이 밀 때의 초당 변화량. 한 번도 완전히 점령된 적 없는(Neutral) 상태에서 적이 채워둔 진행치를
    // 지우는 중(controlValue < 0)이면 자연 감쇠(decayRate)와 미는 힘(1)이 겹쳐 2배속. 이미 한쪽으로
    // 완전히 점령된 적 있는 상태(Owner == Ally/Enemy)를 되돌리는 중이거나, 자기 진영 진행치를 늘리는
    // 중이면 보너스 없이 1배속 (doc/0357).
    private float AllyRate() => CurrentOwner == CaptureOwner.Neutral && controlValue < 0f ? 1f + decayRate : 1f;

    private float EnemyRate() => CurrentOwner == CaptureOwner.Neutral && controlValue > 0f ? 1f + decayRate : 1f;

    // 아무도 없을 때 서서히 되돌아갈 지점. 이미 완전히 점령된 적 있는 상태(Owner == Ally/Enemy)면 그
    // 소유자의 극값(원래 상태로 회복), 한 번도 완전히 점령된 적 없으면(Neutral) 중립(0)으로 줄어든다.
    private float RestPoint() =>
        CurrentOwner == CaptureOwner.Ally ? captureDuration :
        CurrentOwner == CaptureOwner.Enemy ? -captureDuration :
        0f;

    // controlValue가 ±captureDuration(완전 점령) 또는 정확히 0(중립)을 "실제로 통과"했을 때만 소유자가
    // 바뀐다. 그 사이를 지나가는 동안은 이전 소유자를 그대로 유지한다(sticky) - 완전 점령된 거점을
    // 되돌리는 30초 동안은 여전히 이전 소유자로 표시되다가, 정확히 중립을 통과하는 순간에만 Neutral로
    // 바뀌고 거기서 다시 반대쪽 극값까지 가야 그 진영 소유가 된다 (doc/0357).
    private void UpdateOwnerFromControlValue()
    {
        CaptureOwner newOwner = CurrentOwner;

        if (controlValue >= captureDuration) newOwner = CaptureOwner.Ally;
        else if (controlValue <= -captureDuration) newOwner = CaptureOwner.Enemy;
        else if (CurrentOwner == CaptureOwner.Ally && controlValue <= 0f) newOwner = CaptureOwner.Neutral;
        else if (CurrentOwner == CaptureOwner.Enemy && controlValue >= 0f) newOwner = CaptureOwner.Neutral;

        if (newOwner == CurrentOwner) return;

        CurrentOwner = newOwner;
        ApplyEffect(newOwner);

        if (newOwner == CaptureOwner.Ally)
            SoundManager.Instance?.PlayTerritoryCapturedVoice(); // 거점 점령 나레이션(doc/0642)

        Debug.Log($"점령 상태 변경: {newOwner}");
    }

    // 진행 중이거나(아군/적이 밀고 있음) 방치 상태로 되돌아가는 중(RestPoint에 아직 안 도달)이면 바를
    // 보여준다. 이미 그 방향으로 완전히 점령 완료됐거나 교착 상태면 숨긴다. 값은 절댓값 하나로 통일해서
    // "완전 점령 상태(꽉 참)에서 밀려나 0으로 줄어들다, 반대쪽으로 다시 차오르는" 과정이 끊김없이 하나의
    // 슬라이더 애니메이션으로 보이게 한다 (doc/0357).
    private void UpdateCaptureBar(bool alliesPresent, bool enemiesPresent, bool contested)
    {
        bool returningToRest = !alliesPresent && !enemiesPresent && !Mathf.Approximately(controlValue, RestPoint());

        bool progressing = !contested && (alliesPresent || enemiesPresent || returningToRest)
            && !(alliesPresent && controlValue >= captureDuration)
            && !(enemiesPresent && controlValue <= -captureDuration)
            && FogVisibility.IsRevealed(fogWar, transform.position);

        SetCaptureBarVisible(progressing);

        if (!progressing || captureBar == null) return;

        captureBar.value = Mathf.Clamp(Mathf.Abs(controlValue), 0f, captureDuration);
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
