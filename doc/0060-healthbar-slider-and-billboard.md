# 0060. 유닛/건물 체력바 UI - Slider 필드 + 카메라 향한 빌보드 회전

**날짜:** 2026-07-12

## 요청 내용
> 유닛/건물별 체력바 UI 추가. 각 유닛/건물의 `HealthManager`에 `Slider` 필드를 추가하고, 그 슬라이더에 체력값을 넣어달라(체력바 자체는 직접 만들어서 각 유닛/건물에 붙일 것). 체력바는 카메라를 향해야 하는데, X값만 카메라를 따라 회전하고 Y/Z는 0으로 고정(유닛이 Y축으로 회전해도 체력바는 그 방향을 따라가지 않고 그대로 유지)되도록.

## 조사 결과 (현재 코드 상태)
- `HealthManager`(`Assets/Scripts/Unit/HealthManager.cs`)는 유닛/건물/BaseStructure가 공통으로 쓰는 체력 관리 컴포넌트다. `currentHp`/`maxHealth`가 바뀔 때마다(`GetDamage`/`Heal`/`SetMaxHealth`/`SetHealth`) 항상 `OnHealthChanged?.Invoke(currentHp, maxHealth)`를 호출한다 — 이미 "체력이 바뀔 때 알림받는" 지점이 한 곳(이벤트)으로 통일돼 있어서, 슬라이더 갱신도 이 이벤트를 구독하는 것만으로 4곳(`GetDamage`/`Heal`/`SetMaxHealth`/`SetHealth`)을 전부 건드리지 않고 처리할 수 있다.
- `UIController.BindInfoHealth()`가 이미 동일한 패턴(`OnHealthChanged`를 구독해서 텍스트 갱신)을 쓰고 있어서 일관성 있게 재사용 가능.
- 체력바 자체(Slider가 붙은 UI 오브젝트/프리팹)는 사용자가 직접 만들어서 각 유닛/건물 프리팹에 자식으로 배치할 계획이라, 코드에서는 (1) `HealthManager`가 그 `Slider`를 받아서 값만 갱신하는 부분과 (2) 그 체력바 오브젝트에 붙일, 카메라를 바라보는 회전 스크립트만 있으면 된다.
- "X만 카메라를 따라 회전 + Y/Z는 0" 요구사항은 매 프레임 `transform.rotation`(월드 회전)을 직접 `Quaternion.Euler(카메라의 X, 0, 0)`으로 대입하면 된다. Unity에서 자식 오브젝트의 `transform.rotation`(로컬이 아니라 월드 회전)을 직접 대입하면, 그 결과가 정확히 그 값이 되도록 내부적으로 로컬 회전을 역산해서 채워주기 때문에 부모(유닛)가 Y축으로 아무리 회전해도 이 오브젝트의 월드 회전은 항상 `(카메라의 X, 0, 0)`으로 고정된다 — 유닛이 회전해도 체력바가 같이 돌아가지 않는다는 요구사항이 정확히 이렇게 구현된다.
- 이 회전 스크립트는 유닛/건물 전용 로직이 아니라 순수 UI 컴포넌트라 `Assets/Scripts/UI/` 아래 새 파일로 분리하는 게 기존 폴더 구조(`Assets/Scripts/UI/TooltipUI.cs`, `ProductionSlot.cs` 등)와 맞는다.

## 설계안

### 1. `Assets/Scripts/Unit/HealthManager.cs`

**using 추가**:
```csharp
// 기존 코드
using UnityEngine;
```
```csharp
// 변경 코드
using UnityEngine;
using UnityEngine.UI;
```

**필드 추가**:
```csharp
// 기존 코드
    [SerializeField]
    private int maxHealth = 100;

    private int currentHp;
    private bool isDead;
```
```csharp
// 변경 코드
    [SerializeField]
    private int maxHealth = 100;

    [SerializeField] private Slider healthSlider; // 체력바 UI (프리팹에서 직접 연결) - 체력 변화에 맞춰 값만 자동 갱신됨

    private int currentHp;
    private bool isDead;
```

**`Awake()`에서 슬라이더 갱신 구독 + 초기값 반영**:
```csharp
// 기존 코드
    private void Awake()
    {
        currentHp = maxHealth;
    }
```
```csharp
// 변경 코드
    private void Awake()
    {
        currentHp = maxHealth;

        OnHealthChanged += UpdateHealthSlider;
        UpdateHealthSlider(currentHp, maxHealth);
    }

    // 체력이 바뀔 때마다(OnHealthChanged) 체력바 슬라이더 값을 함께 갱신한다. 슬라이더가 연결 안 돼 있으면 아무 것도 안 함.
    private void UpdateHealthSlider(int current, int max)
    {
        if (healthSlider == null)
            return;

        healthSlider.maxValue = max;
        healthSlider.value = current;
    }
```
(`GetDamage`/`Heal`/`SetMaxHealth`/`SetHealth`는 이미 전부 `OnHealthChanged`를 호출하고 있어서 별도 수정이 필요 없다 - 이벤트 구독만으로 4곳 모두 자동으로 슬라이더가 갱신된다.)

### 2. `Assets/Scripts/UI/HealthBarBillboard.cs` (신규)
```csharp
using UnityEngine;

// 체력바가 항상 카메라를 향하도록 하는 빌보드 회전. X(위아래 기울기)만 카메라 각도를 따라가고
// Y/Z는 항상 0으로 고정한다 - 유닛/건물이 Y축으로 회전해도 체력바 자체는 방향을 따라 돌지 않는다.
// 체력바 UI 오브젝트(Slider가 붙은 Canvas 등)에 직접 붙여서 사용한다.
public class HealthBarBillboard : MonoBehaviour
{
    private Camera targetCamera;

    private void Start()
    {
        targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                return;
        }

        float cameraPitch = targetCamera.transform.eulerAngles.x;
        transform.rotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }
}
```

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **`healthSlider`가 비어있는(연결 안 된) 경우**: 아무 것도 하지 않고 조용히 넘어갑니다 - 아직 체력바를 안 붙인 유닛/건물이 있어도 에러 없이 그대로 동작합니다.
- **회전 갱신 시점**: 카메라가 그 프레임에 움직인 뒤(보통 `LateUpdate`)에 맞춰 돌도록 `LateUpdate()`에서 처리해 한 프레임 밀리는 떨림을 방지했습니다.
- **`Camera.main` 탐색**: 씬 로드 직후 카메라가 아직 없을 수도 있는 상황을 대비해 `Start()`에서 한 번 찾고, 못 찾았으면 이후 `LateUpdate()`마다 다시 시도합니다(전투 유닛 스폰 등으로 오브젝트가 카메라보다 먼저 생성돼도 안전).
- **X 각도만 사용하는 이유**: 요청하신 대로 "카메라의 X값만 따라가고 Y/Z는 0 고정" — 즉 체력바는 항상 월드 기준 정면(Z+ 방향)을 바라본 채로, 카메라가 내려다보는 각도(피치)에만 맞춰 위아래로 기울어집니다. 유닛이 좌우로 돌아도(Y 회전) 체력바엔 영향이 없습니다.
- **스크립트 부착 위치**: `HealthBarBillboard`는 체력바 UI 오브젝트(사용자가 직접 만드실 Canvas/Slider 계층) 쪽에 붙이시면 되고, `HealthManager`의 `Health Slider` 필드에는 그 안의 실제 `Slider` 컴포넌트를 연결하시면 됩니다. 같은 오브젝트일 필요는 없습니다.

## 변경 예정 파일
- `Assets/Scripts/Unit/HealthManager.cs`
- `Assets/Scripts/UI/HealthBarBillboard.cs` (신규)

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).
