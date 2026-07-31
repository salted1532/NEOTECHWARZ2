# 0326. 비점령지 안 건물 시야 대폭 축소 (설계 제안)

**날짜:** 2026-07-31

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **설계 제안만** 담고 있고
> 실제 프로젝트 파일(`Assets/Scripts/**`, 프리팹)은 아직 건드리지 않았다. 검토 후 확정되면 코드에
> 반영한다.

## 요청

> "비점령지 안에 있는 건물은 시야가 극도로 줄어들었으면 좋겠어"

## 조사 내용

- 유닛/건물의 시야는 `Assets/Scripts/FogOfWar/FogRevealerAgent.cs`가 담당 — 같은 오브젝트에 붙여서
  `csFogWar`에 자신을 `FogRevealer`로 등록하는 어댑터. `sightRange`(인스펙터 값)를 그대로
  `csFogWar.FogRevealer` 생성자에 넘긴다.
- 모든 건물 프리팹(`MainBase`, `Tier1`, `Tier2`, `Tier3`, `SupplyDepot`, `Lab`)이 동일하게
  `FogRevealerAgent.sightRange = 15`로 고정돼 있음 — 점령 상태와 무관하게 항상 같은 시야.
- `csFogWar.FogRevealer.sightRange`는 **private, setter 없음**(`_SightRange`로 읽기만 가능) —
  기존 `TerritoryFogReveal`/`FogRevealerAgent` 설계 원칙([[territory-permanent-vision-design]] 0175,
  [[fogofwar-eye-scripts-implementation]] 0173)대로 에셋(`csFogWar.cs`)은 건드리지 않는다는
  전제라면, 시야 범위를 바꾸려면 **기존 등록을 제거하고 새 `sightRange`로 다시 등록**하는 수밖에 없음
  (`FogRevealerAgent.OnDestroy()`가 이미 "현재 인덱스를 다시 찾아서 제거"하는 동일한 패턴을 씀).
- "점령"은 `CaptureSystem`이 관리하는 `TerritoryZone.Owner`(`Neutral`/`Ally`/`Enemy`) —
  `TerritoryManager.IsInsideAlliedTerritory(worldPos)`로 "이 좌표가 지금 아군 소유 영토 안인가"를
  이미 물어볼 수 있음([[territory-restriction-implementation-applied]] 0142에서 건설/생산/자원채취에
  이미 같은 질의를 매 프레임 쓰고 있는 기존 패턴).
- 건물은 원래 아군 영토 안에서만 건설 가능하지만(0142), 지어진 뒤 **거점을 적에게 재점령당하면**
  `TerritoryZone.Owner`가 `Ally`가 아니게 바뀌어 그 건물은 "더 이상 점령되지 않은 땅 위"에 남는다 —
  이 순간에도 `FogRevealerAgent`는 여전히 15칸 시야를 그대로 제공 중이라, 요청한 "비점령지 안 건물은
  시야가 극도로 줄어야 한다"가 지금은 반영되지 않음.
- `FogRevealerAgent`는 유닛에도 붙는 공용 컴포넌트라, 이 축소 로직을 무조건 켜면 이동 중인 유닛이
  영토 밖으로 나갈 때마다 시야가 줄어버림(요청 범위 밖) — **건물 프리팹에서만 옵트인**하도록 플래그로
  막아야 유닛 동작에 영향이 없음.

## 설계안

### 1. `Assets/Scripts/FogOfWar/FogRevealerAgent.cs` — 영토 미점령 시 시야 축소 옵션 추가

Before:
```csharp
using FischlWorks_FogWar;
using UnityEngine;

// 유닛/건물에 부착해 csFogWar에 자신을 시야 소스(FogRevealer)로 등록/해제하는 어댑터.
// UnitController/BuildingController는 전혀 건드리지 않고, 같은 오브젝트에 이 컴포넌트만 추가하면 된다.
public class FogRevealerAgent : MonoBehaviour
{
    [Header("시야 범위 (월드 단위, csFogWar가 내부에서 unitScale로 나눠 셀 단위로 변환)")]
    [SerializeField] private int sightRange = 10;
    [SerializeField] private bool updateOnlyOnMove = true;

    private csFogWar fogWar;
    private csFogWar.FogRevealer fogRevealer;

    private void Start()
    {
        fogWar = FindFirstObjectByType<csFogWar>();

        if (fogWar == null)
        {
            Debug.LogWarning($"{name}: csFogWar를 씬에서 찾지 못해 시야를 등록하지 못했습니다.", this);
            return;
        }

        fogRevealer = new csFogWar.FogRevealer(transform, sightRange, updateOnlyOnMove);
        fogWar.AddFogRevealer(fogRevealer);
    }

    private void OnDestroy()
    {
        if (fogWar == null || fogRevealer == null)
            return;

        int currentIndex = fogWar._FogRevealers.IndexOf(fogRevealer);

        if (currentIndex >= 0)
            fogWar.RemoveFogRevealer(currentIndex);
    }
}
```

After:
```csharp
using FischlWorks_FogWar;
using UnityEngine;

// 유닛/건물에 부착해 csFogWar에 자신을 시야 소스(FogRevealer)로 등록/해제하는 어댑터.
// UnitController/BuildingController는 전혀 건드리지 않고, 같은 오브젝트에 이 컴포넌트만 추가하면 된다.
public class FogRevealerAgent : MonoBehaviour
{
    [Header("시야 범위 (월드 단위, csFogWar가 내부에서 unitScale로 나눠 셀 단위로 변환)")]
    [SerializeField] private int sightRange = 10;
    [SerializeField] private bool updateOnlyOnMove = true;

    [Header("영토(점령지) 연동 - 건물 전용, 유닛은 기본값(꺼짐) 유지")]
    [Tooltip("켜면 이 오브젝트가 아군 점령지 밖에 있을 때 시야가 unclaimedSightRange로 줄어든다.")]
    [SerializeField] private bool shrinkSightOutsideAlliedTerritory = false;
    [SerializeField] private int unclaimedSightRange = 2;

    private csFogWar fogWar;
    private csFogWar.FogRevealer fogRevealer;
    private bool isInsideAlliedTerritory; // 마지막으로 등록에 반영한 영토 상태

    private void Start()
    {
        fogWar = FindFirstObjectByType<csFogWar>();

        if (fogWar == null)
        {
            Debug.LogWarning($"{name}: csFogWar를 씬에서 찾지 못해 시야를 등록하지 못했습니다.", this);
            return;
        }

        isInsideAlliedTerritory = !shrinkSightOutsideAlliedTerritory || TerritoryManager.IsInsideAlliedTerritory(transform.position);
        RegisterRevealer(CurrentSightRange());
    }

    // 영토를 잃고/되찾을 때마다 재확인 - 건물은 제자리에 고정이라 매 프레임 검사해도 부담 없음
    // (PlacementSystem/BaseStructure/UnitSpawner도 이미 같은 질의를 매 프레임 사용 중, doc/0142).
    private void Update()
    {
        if (!shrinkSightOutsideAlliedTerritory || fogWar == null)
            return;

        bool nowInside = TerritoryManager.IsInsideAlliedTerritory(transform.position);
        if (nowInside == isInsideAlliedTerritory)
            return;

        isInsideAlliedTerritory = nowInside;
        ReplaceRevealer(CurrentSightRange());
    }

    private int CurrentSightRange() => isInsideAlliedTerritory ? sightRange : unclaimedSightRange;

    private void RegisterRevealer(int range)
    {
        fogRevealer = new csFogWar.FogRevealer(transform, range, updateOnlyOnMove);
        fogWar.AddFogRevealer(fogRevealer);
    }

    // csFogWar.FogRevealer.sightRange는 setter가 없어 값을 못 바꾸므로, 기존 등록을 지우고
    // 새 sightRange로 다시 등록하는 방식으로 우회한다 (에셋 파일은 건드리지 않음).
    private void ReplaceRevealer(int range)
    {
        int currentIndex = fogWar._FogRevealers.IndexOf(fogRevealer);

        if (currentIndex >= 0)
            fogWar.RemoveFogRevealer(currentIndex);

        RegisterRevealer(range);
    }

    private void OnDestroy()
    {
        if (fogWar == null || fogRevealer == null)
            return;

        // AddFogRevealer/RemoveFogRevealer는 리스트 인덱스 기반이라, 다른 유닛이 먼저 죽어서
        // 목록이 앞으로 당겨지면 등록 당시 캐싱해둔 인덱스가 어긋난다. 그래서 인덱스를 미리 저장해두지
        // 않고, 제거 직전에 내 FogRevealer 인스턴스의 "현재" 인덱스를 다시 찾아서 그 값으로 제거한다.
        int currentIndex = fogWar._FogRevealers.IndexOf(fogRevealer);

        if (currentIndex >= 0)
            fogWar.RemoveFogRevealer(currentIndex);
    }
}
```

### 2. 건물 프리팹 6개 — 플래그 켜기 (씬/프리팹 편집, 코드 아님)

`MainBase.prefab`, `Tier1.prefab`, `Tier2.prefab`, `Tier3.prefab`, `SupplyDepot.prefab`, `Lab.prefab`의
`FogRevealerAgent` 컴포넌트에 `shrinkSightOutsideAlliedTerritory: 1`을 추가(기존 `sightRange: 15`는
유지 — "점령지 안에 있을 때"의 시야는 그대로 15). `unclaimedSightRange`는 일단 `2`로 제안(15 대비
약 87% 축소, 건물 바로 코앞만 보이는 수준) — 원하는 수치가 있으면 알려주면 그 값으로 적용.

**유닛 프리팹은 건드리지 않음** — `shrinkSightOutsideAlliedTerritory` 기본값이 `false`라 유닛은
지금과 동일하게 항상 `sightRange` 그대로 유지.

## 결정이 필요한 부분

1. **"비점령지"의 기준**: 건물을 지은 아군 자신이 그 땅을 잃은 경우(적에게 재점령당함, `Owner`가
   `Ally`가 아니게 됨)만을 의미하는 것으로 해석함 — 혹시 "어떤 세력도 점령하지 않은 중립 지역"만
   따로 가리키는 것이라면(즉 `Enemy`가 점령한 경우는 제외하고 싶다면) 조건을
   `TerritoryManager.IsInsideTerritory(pos, CaptureOwner.Neutral)` 등으로 좁혀야 함.
2. **축소된 시야 값**: 위 제안은 `2`(현재 `15`의 약 13%) — "극도로"라는 표현에 맞춰 꽤 작게 잡았는데,
   원하는 구체적인 수치가 있으면 알려주면 그대로 반영.
3. **적용 대상**: 요청이 "건물"이라 6개 건물 프리팹 전부에 적용하는 것으로 해석함 — 혹시 `MainBase`
   (본진)는 예외로 두고 싶은지(본진은 대개 절대 뺏기지 않는 홈 영토 안에 있어 실질적으로 영향이 없을
   가능성이 높지만, 명확히 하고 싶으면 알려주세요).

## 다음 단계

위 3가지에 답을 주면 `FogRevealerAgent.cs` 수정 + 건물 프리팹 6개 플래그 적용을 진행한다.

## 확인 결과 및 구현

사용자가 3가지 결정 사항 모두 권장안으로 승인: (1) "비점령지" = 아군 소유가 아니면 전부(중립/적 무관),
(2) 축소 시야값 = 2, (3) `MainBase`(본진)도 포함.

설계안 그대로 적용:
- `Assets/Scripts/FogOfWar/FogRevealerAgent.cs`에 `shrinkSightOutsideAlliedTerritory`/`unclaimedSightRange`
  필드와 `Update()`에서 영토 상태 변화를 감지해 `FogRevealer`를 재등록하는 로직 추가.
- `MainBase.prefab`, `Tier1.prefab`, `Tier2.prefab`, `Tier3.prefab`, `SupplyDepot.prefab`, `Lab.prefab`
  6개 모두 `shrinkSightOutsideAlliedTerritory: 1`, `unclaimedSightRange: 2` 추가(`sightRange: 15`는 유지).
  유닛 프리팹은 건드리지 않음.
- `npx uloop-cli compile --wait-for-domain-reload true`로 컴파일 확인 — 에러 0개(기존에도 있던
  `FindFirstObjectByType` deprecated 경고 25개만 그대로, 이번 변경과 무관).

## 남은 수동 확인 작업

- 실제 플레이 테스트(에디터에서 거점을 점령했다가 다시 뺏기는 상황을 만들어, 그 안 건물의 시야가
  15→2로 줄어드는지·거점을 되찾으면 다시 15로 복원되는지)는 이번 세션에서 하지 않음 — 다음에
  요청하면 PlayMode로 재현해서 확인 가능.
