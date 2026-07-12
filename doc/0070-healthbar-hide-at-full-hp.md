# 0070 - 체력바: 만피(풀피)일 때 숨기고, 피해를 입으면 보이도록

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라 이 문서는 **제안 코드만** 담고
> 실제 프로젝트 파일은 아직 건드리지 않았다. 검토 후 적용해도 된다고 하면 그때 반영한다.

## 1. 요청
"유닛의 체력이 최대체력(만땅)이면 체력바가 안 보이고, 공격을 받아 체력이 조금이라도 깎이면 그때부터
체력바가 보이도록 해줘. 다시 회복해서 최대체력에 도달하면 다시 안 보이게."

## 2. 현재 코드 분석
- `HealthManager.cs`는 체력이 바뀔 때마다(`GetDamage`/`Heal`/`SetMaxHealth`/`SetHealth`, 그리고 `Awake()`의 초기 호출까지 포함) `OnHealthChanged` 이벤트를 발생시키고, 그걸 구독한 `UpdateHealthSlider(int current, int max)`가 슬라이더 값만 갱신한다(`HealthManager.cs:36-43`).
- 체력바를 껐다 켰다 하는 수단은 이미 있다 — `SetHealthBarVisible(bool)`(`HealthManager.cs:46-50`). 지금은 `PreviewSystem.cs:104-109`에서 건설 프리뷰/고스트 생성 시 "항상 풀피라 체력바를 보여주면 오해를 준다"는 이유로 **한 번 호출해서 끄는 용도**로만 쓰이고 있다.
- 즉, 이번 요청은 `SetHealthBarVisible`을 대체하는 게 아니라, **체력이 바뀔 때마다 자동으로 켜고 끄는 로직**을 `UpdateHealthSlider` 쪽에 얹으면 된다. 이미 모든 체력 변화 경로가 `OnHealthChanged` → `UpdateHealthSlider`를 통과하므로 여기 한 곳만 고치면 모든 경로(피해/회복/최대체력 변경/초기화)가 자동으로 커버된다.

## 3. 설계
- `UpdateHealthSlider`에서 슬라이더 값 갱신 후 `current < max`일 때만 `SetActive(true)`, `current >= max`면 `SetActive(false)`.
- `Awake()`에서 `currentHp = maxHealth`로 초기화한 뒤 곧바로 `OnHealthChanged`를 한 번 호출하므로(`HealthManager.cs:31-33`), 스폰 직후엔 자동으로 "만피 → 숨김" 상태로 시작한다. 별도 초기화 코드가 필요 없다.
- `PreviewSystem`의 기존 `SetHealthBarVisible(false)` 호출은 그대로 둬도 된다 — 프리뷰 오브젝트는 생성 직후 만피 상태라 자동 숨김 로직과 결과가 같아서 충돌 없이 중복(무해)될 뿐이다. `SetHealthBarVisible`은 외부에서 명시적으로 강제로 켜고 끄고 싶을 때 쓰는 수단으로 계속 남겨둔다.
- 적용 범위: `HealthManager`는 유닛/건물/`EnemyController`/`BaseStructure` 등 `IDestructible`을 쓰는 모든 대상에 공용으로 붙는 컴포넌트이므로, 이 변경 하나로 유닛/적/건물 체력바 전부에 동일하게 적용된다(요청이 "유닛"이라고 했지만 컴포넌트 자체가 공용이라 자연스럽게 전체 적용됨 — 원치 않으면 알려달라).

## 4. 제안 코드

**`Assets/Scripts/Unit/HealthManager.cs`**

기존 코드:
```csharp
    // 체력이 바뀔 때마다(OnHealthChanged) 체력바 슬라이더 값을 함께 갱신한다. 슬라이더가 연결 안 돼 있으면 아무 것도 안 함.
    private void UpdateHealthSlider(int current, int max)
    {
        if (healthSlider == null)
            return;

        healthSlider.maxValue = max;
        healthSlider.value = current;
    }
```

제안 변경 코드:
```csharp
    // 체력이 바뀔 때마다(OnHealthChanged) 체력바 슬라이더 값을 함께 갱신한다. 슬라이더가 연결 안 돼 있으면 아무 것도 안 함.
    // 만피 상태에서는 체력바를 숨기고, 조금이라도 깎이면 다시 보여준다(회복해서 만피로 돌아가면 다시 숨김).
    private void UpdateHealthSlider(int current, int max)
    {
        if (healthSlider == null)
            return;

        healthSlider.maxValue = max;
        healthSlider.value = current;
        healthSlider.gameObject.SetActive(current < max);
    }
```

다른 파일은 수정하지 않는다(`SetHealthBarVisible`/`PreviewSystem`은 그대로 유지).

## 5. 적용 결과
사용자가 "이대로 적용시켜줘"로 확인 → `Assets/Scripts/Unit/HealthManager.cs`의 `UpdateHealthSlider`에 위 제안 코드를 그대로 적용 완료. 다른 파일은 수정하지 않음.
