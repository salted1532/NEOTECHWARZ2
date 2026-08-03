# 0404 - NavMeshPath 필드 이니셜라이저가 MonoBehaviour 생성자에서 실행되어 런타임 예외 발생 (제안)

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> UnityException: InitializeNavMeshPath is not allowed to be called from a MonoBehaviour
> constructor (or instance field initializer), call it in Awake or Start instead. Called from
> MonoBehaviour 'EnemyUnitController'.
> UnityEngine.AI.NavMeshPath..ctor () (at <bc8f9155f1b947428220a4ce72fdfd4c>:0)
> EnemyUnitController..ctor () (at Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs:370)

## 조사 결과

[[0403]]에서 추가한 재사용 필드가 원인이다.

```csharp
private readonly NavMeshPath reachabilityProbePath = new NavMeshPath();
```

C#에서 `= new NavMeshPath()` 같은 필드 이니셜라이저는 컴파일러가 자동으로 생성하는 인스턴스
생성자(`..ctor`) 안에서 실행된다. Unity는 MonoBehaviour의 생성자(및 필드 이니셜라이저) 시점에는
아직 네이티브 엔진 객체가 준비되지 않아 `NavMeshPath`처럼 네이티브 리소스를 감싸는 타입의 생성을
막아둔다 - `Awake()`/`Start()` 이후에나 허용된다.

`EnemyUnitController.cs:370`와 `UnitController.cs:685`(doc/0403에서 각각 추가) 둘 다 동일한
패턴이라 둘 다 같은 예외가 발생한다(에러 로그는 `EnemyUnitController` 쪽만 보였지만
`UnitController`도 인스턴스가 생성되는 즉시 똑같이 터진다).

## 제안하는 수정

필드 이니셜라이저 대신, 처음 쓰일 때 지연 생성한다 (Awake/Start를 새로 걸 필요 없이 가장 짧은 수정).

### `Assets/Scripts/Unit/UnitController.cs` (685번째 줄 부근)

기존:
```csharp
    private readonly NavMeshPath reachabilityProbePath = new NavMeshPath();
    private bool IsPositionReachable(Vector3 pos)
    {
        return NavMesh.CalculatePath(transform.position, pos, NavMesh.AllAreas, reachabilityProbePath) &&
            reachabilityProbePath.status == NavMeshPathStatus.PathComplete;
    }
```

변경:
```csharp
    private NavMeshPath reachabilityProbePath;
    private bool IsPositionReachable(Vector3 pos)
    {
        reachabilityProbePath ??= new NavMeshPath();
        return NavMesh.CalculatePath(transform.position, pos, NavMesh.AllAreas, reachabilityProbePath) &&
            reachabilityProbePath.status == NavMeshPathStatus.PathComplete;
    }
```

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (370번째 줄 부근)

동일하게 수정.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (685번째 줄 부근)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (370번째 줄 부근)

## 요약

- 원인: [[0403]]에서 추가한 `reachabilityProbePath` 필드의 `readonly ... = new NavMeshPath()`
  이니셜라이저가 컴파일러 생성 인스턴스 생성자에서 실행되어, Unity가 금지하는 시점(생성자/필드
  이니셜라이저)에 `NavMeshPath`를 생성하려다 예외 발생.
- 수정: 필드를 `readonly` 해제하고 `IsPositionReachable()` 호출 시
  `reachabilityProbePath ??= new NavMeshPath();`로 지연 생성하도록 변경. `UnitController.cs`,
  `EnemyUnitController.cs` 양쪽 다 수정.
- 컴파일 확인 완료(에러 0, 경고 33 - 기존과 동일).
