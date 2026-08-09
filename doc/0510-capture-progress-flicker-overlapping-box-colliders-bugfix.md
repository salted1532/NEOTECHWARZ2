# 0510. 점령 진행도가 오르다 내려가다 반복되는 버그 수정 제안

**날짜:** 2026-08-10

## 요청 내용

> 유닛이 박스들 안에서 움직이면 거점 점령이 점령시간이 올랐다가 내려갔다가 하면서 계속 오르질 않네 이거 해결해줘

## 조사 내용

- `CylinderBoxColliderGenerator`로 만든 실린더 콜라이더는 8개의 **서로 많이 겹치는** `BoxCollider`(트리거)로 구성돼 있다 (반지름 5, 박스 크기 x=10/y=20/z=10 — 박스 하나가 원 전체를 거의 덮을 만큼 커서 인접 박스끼리 크게 겹침).
- `CaptureSystem.cs`의 기존 로직:
  ```csharp
  private void OnTriggerEnter(Collider other)
  {
      if (other.TryGetComponent<UnitController>(out var ally))
      {
          if (!alliesInRange.Contains(ally)) alliesInRange.Add(ally); // 중복 방지
      }
      ...
  }

  private void OnTriggerExit(Collider other)
  {
      if (other.TryGetComponent<UnitController>(out var ally))
          alliesInRange.Remove(ally); // 무조건 제거
      ...
  }
  ```
  - Unity는 겹치는 8개의 박스 각각에 대해 유닛과의 콜라이더 쌍(pair)마다 **개별적으로** `OnTriggerEnter`/`OnTriggerExit`를 보낸다.
  - 유닛이 박스A 안에 있다가 박스A·박스B가 겹치는 구간으로 들어가면: 박스A Enter(추가) → 박스B Enter(이미 있어서 중복 방지로 무시). 여기까지는 리스트에 1개.
  - 유닛이 계속 이동해서 박스A 밖(하지만 여전히 박스B 안)으로 나가면: 박스A Exit 이벤트가 발생 → `alliesInRange.Remove(ally)`가 **무조건 실행되어 리스트에서 완전히 빠짐** — 실제로는 여전히 박스B 안에 있는데도 "범위 이탈"로 처리됨.
  - 그 순간 `Update()`가 `alliesPresent = false`로 판단해 `controlValue`가 `RestPoint()`로 되돌아가기 시작(감소) → 이후 다시 박스 경계를 넘으며 Enter가 발생하면 다시 증가. 이게 반복되면서 "올랐다 내려갔다"가 계속 발생하고 `captureDuration`까지 도달하지 못한다.
- **근본 원인:** 겹치는 여러 트리거 콜라이더를 "하나의 범위"로 취급하면서, 진입/이탈을 유닛당 **겹친 박스 개수**가 아니라 단순 boolean(있음/없음)으로 관리하고 있는 것. `TerritoryZone.Contains()`처럼 폴리곤 판정을 쓰는 것도 아니고, 물리 엔진의 개별 트리거 이벤트를 그대로 boolean에 매핑하면 겹치는 구조에서는 항상 이 문제가 난다.

## 계획된 코드 변경

파일: `Assets/Scripts/CaptureSystem/CaptureSystem.cs`

**기존 코드:**
```csharp
    // 콜라이더 트리거 범위 안에 들어와 있는 아군/적 유닛 목록
    private readonly List<UnitController> alliesInRange = new List<UnitController>();
    private readonly List<EnemyUnitController> enemiesInRange = new List<EnemyUnitController>();
```
```csharp
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<UnitController>(out var ally))
        {
            if (!alliesInRange.Contains(ally)) alliesInRange.Add(ally);
        }
        else if (other.TryGetComponent<EnemyUnitController>(out var enemy))
        {
            if (!enemiesInRange.Contains(enemy)) enemiesInRange.Add(enemy);
        }
    }
```

**변경 코드:**
```csharp
    // 콜라이더 트리거 범위 안에 들어와 있는 아군/적 유닛 목록. 실린더 콜라이더가 서로 겹치는 여러
    // BoxCollider로 구성되어 있어서, 한 유닛이 동시에 여러 박스 안에 있을 수 있다. 그래서 이 리스트는
    // "겹친 박스 개수만큼" 같은 유닛이 중복으로 들어갈 수 있는 멀티셋으로 쓴다 (Enter마다 추가,
    // Exit마다 하나만 제거) - Count > 0이면 최소 하나의 박스 안에 있다는 뜻. 유닛당 1개로 중복 제거해
    // 버리면, 겹치는 두 박스 사이를 지나갈 때(박스A는 나가지만 여전히 박스B 안인 순간) Exit 하나만으로
    // 유닛이 통째로 빠져버려서 점령 진행이 계속 끊기고 되감기는 버그가 있었다.
    private readonly List<UnitController> alliesInRange = new List<UnitController>();
    private readonly List<EnemyUnitController> enemiesInRange = new List<EnemyUnitController>();
```
```csharp
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<UnitController>(out var ally))
            alliesInRange.Add(ally);
        else if (other.TryGetComponent<EnemyUnitController>(out var enemy))
            enemiesInRange.Add(enemy);
    }
```

`OnTriggerExit`는 그대로 둔다 (`List<T>.Remove()`는 첫 번째로 일치하는 항목 1개만 제거하므로, Enter로 추가된 개수와 정확히 짝을 맞춰 감소한다 - 이미 원하는 동작).

## 요약 / 영향받는 파일

- `Assets/Scripts/CaptureSystem/CaptureSystem.cs`: `OnTriggerEnter`에서 중복 방지 `Contains` 체크만 제거 (3줄 → 1줄, ally/enemy 각각). 그 외 로직(`Update`, `AllyRate`, `RestPoint` 등) 전부 그대로.
- **스킵한 것:** `Dictionary`로 바꿔 개수를 직접 세는 방식은 더 명시적이지만, `List.Add`/`List.Remove` 조합만으로 이미 정확히 같은 동작(멀티셋)을 하므로 더 복잡하게 만들 필요 없음.

---

## 확인 요청

위 코드 변경(CaptureSystem.cs)을 적용해도 될까요?
