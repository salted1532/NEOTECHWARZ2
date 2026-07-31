# 0321. StageManager 기본 골격

**날짜:** 2026-07-31

## 요청 내용

> 이제 스테이지 매니저를 만들어야하는데 스테이지 매니저는 임무 목표 달성으로 게임의 승리 실패시 패배등이 나타나야해 일단 기본적인 틀만 좀 만들어줘

## 조사 내용

- `Assets/Scripts` 전체를 확인한 결과 게임 승리/패배, 스테이지, 미션 목표를 다루는 스크립트는 아직 없음(`StageManager`, `GameManager` 등 전무).
- 팀/진영 구분은 별도 `enum` 없이 클래스로 나뉘어 있음: 플레이어 쪽 `UnitController`/`BuildingController`/`BaseStructure`, 적 쪽 `EnemyUnitController`/`EnemyBuildingController` — 전부 `HealthManager.OnDeath` → 자기 자신의 `IDestructible.OnDestroyed()`로 사망 처리됨 (`Unit/HealthManager.cs`).
- 즉 "적 전멸", "본진 파괴" 같은 실제 승패 판정 로직은 위 컨트롤러들에 흩어져 있고, 이번 요청은 그 판정들이 나중에 보고할 "결과를 한 곳에 모아서 승리/패배 상태로 고정하고 이벤트로 알리는" 매니저의 틀만 원하는 것으로 이해함(구체적 목표/패배 조건 판정 로직은 이번 범위 밖).
- 싱글턴 패턴 참고: `SoundManager`가 "여러 곳에서 두루 호출돼야 해서 예외적으로 정적 싱글턴을 쓴다"고 명시(`Audio/SoundManager.cs:11`) — `StageManager`도 유닛 사망 처리, UI, 스포너 등 여러 곳에서 승패를 보고해야 하므로 같은 이유로 싱글턴 채택.

## 설계안 (기본 틀만 — 구체적 목표/패배 조건 판정은 이후 각 시스템에서 채워 넣음)

### 신규 파일: `Assets/Scripts/System/StageManager.cs`
```csharp
using System;
using UnityEngine;

// 스테이지(미션)의 승리/패배 "결과"만 담당하는 최소 골격.
// 어떤 조건이 목표 달성/패배인지는 이 매니저가 판단하지 않는다 - 각 시스템(적 전멸 판정,
// BaseStructure 파괴 감지 등)에서 조건을 직접 확인한 뒤 ReportVictory()/ReportDefeat()를
// 호출해서 결과만 보고하면, 여기서 상태를 한 번만 고정하고 이벤트로 알린다.
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    public enum StageResult { InProgress, Victory, Defeat }

    public StageResult Result { get; private set; } = StageResult.InProgress;

    public event Action OnVictory;
    public event Action OnDefeat;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 임무 목표 달성 시 호출 (예: 적 기지 파괴 등 - 조건 판단은 호출부 책임).
    public void ReportVictory()
    {
        if (Result != StageResult.InProgress) return;
        Result = StageResult.Victory;
        OnVictory?.Invoke();
    }

    // 패배 조건 충족 시 호출 (예: 아군 본진 파괴 등 - 조건 판단은 호출부 책임).
    public void ReportDefeat()
    {
        if (Result != StageResult.InProgress) return;
        Result = StageResult.Defeat;
        OnDefeat?.Invoke();
    }
}
```

### 이번에 포함하지 않는 것 (스코프 밖)
- 실제 승리/패배 조건 판정(적 전멸 카운트, 본진 파괴 감지 연결 등)
- 승리/패배 UI 패널(연출, 버튼 등) — `OnVictory`/`OnDefeat` 이벤트만 노출해두고, UI는 이후 `UIController` 쪽에서 구독해서 붙이면 됨
- 씬에 `StageManager` 오브젝트 배치 — 스크립트 작성 후 씬에 빈 GameObject를 만들어 붙이는 작업은 별도로 안내

## 검증

- 사용자 확인 후 위 설계안 그대로 `Assets/Scripts/System/StageManager.cs` 생성
- `uloop compile`: `Success: true, ErrorCount: 0` (기존 코드에서 이미 있던 다른 파일들의 obsolete API 경고만 존재, StageManager.cs 관련 에러/경고 없음)
- 씬에 `StageManager` 오브젝트 배치 및 실제 승패 조건 연결은 다음 작업으로 남김

## 영향받는 파일

- `Assets/Scripts/System/StageManager.cs` (신규, 생성 완료)
