# 0031. 좁은 경사로에서 다수 유닛 이동 시 들썩거림(진동) 현상 조사

- 날짜: 2026-07-08

## 요청 내용

> 아군 유닛이 언덕아래로 경사로로 이동할때 유닛들끼리 서로 머리위로 올라가려는거 처럼 들썩거리는데 왜그럴까?
> 현재 상황은 꾀 좁은 언덕 경사로에서 많은 유닛들이 한번에 내려가는걸로 테스트해보고 있어

## 조사 내용

관련 코드: `Assets/Scripts/System/RTSUnitController.cs`, `Assets/Scripts/Unit/UnitController.cs`, 유닛 프리팹(`Assets/prefabs/NTA/Unit/Tier1/Assault Trooper.prefab` 등)의 `NavMeshAgent`/`Rigidbody`/`CapsuleCollider` 설정.

### 1) 다수 유닛이 전부 "정확히 같은 좌표"로 이동 명령을 받는다

`RTSUnitController.MoveSelectedUnits`:

```csharp
public void MoveSelectedUnits(Vector3 end)
{
    for (int i = 0; i < selectedUnitList.Count; ++i)
    {
        selectedUnitList[i].MoveTo(end);
    }
}
```

선택된 유닛 전부가 동일한 `end` 지점으로 `MoveTo`를 호출한다. 대형(formation) 오프셋이 전혀 없어서, 예를 들어 유닛 20기를 선택해 이동시키면 20기 모두 "같은 한 점"을 목적지로 삼는다.

### 2) NavMeshAgent 설정 (프리팹 공통)

```
m_Radius: 0.5
m_Speed: 3.5
m_AutoBraking: 1
m_Height: 2
m_BaseOffset: 1
m_ObstacleAvoidanceType: 4   # HighQualityObstacleAvoidance
```

`m_AvoidancePriority`는 프리팹에서 별도로 설정돼 있지 않아 전 유닛이 기본값(50)으로 동일하다.

### 3) Rigidbody는 Kinematic (물리 충돌로 밀어올리는 건 아님)

```
m_IsKinematic: 1
m_CollisionDetection: 0
```

Kinematic Rigidbody + Capsule Collider 조합이라 Unity 물리 엔진이 겹친 콜라이더를 강제로 밀어 올리는 것은 아니다. 즉 "위로 튀어 오르는" 원인은 물리 충돌 반발력이 아니라 **NavMeshAgent 자체의 경로/회피 계산**에서 나온다.

### 종합 원인

1. **동일 목적지 수렴**: 모든 유닛이 같은 한 점으로 이동하므로, 목적지 근처에서 반경 0.5짜리 원반 수십 개가 한 점에 겹치려고 한다. 평지에서는 공간이 넓어 Obstacle Avoidance가 알아서 주변에 퍼뜨려 눈에 띄는 문제가 없다.
2. **좁은 경사로 = 퍼질 공간 부족**: 경사로 폭이 좁으면 옆으로 회피할 여유가 없어서, 서로의 반경 안에 계속 끼어든 채로 "밀어내기 → 목적지로 당겨짐 → 다시 밀어내기"가 매 프레임 반복된다(Avoidance Quality 4=HighQuality라 계산은 정교하지만, 애초에 물리적으로 들어갈 공간이 없으면 진동만 발생).
3. **경사면이라 y값이 민감하게 흔들림**: NavMeshAgent는 매 프레임 새 위치의 NavMesh 표면 높이를 다시 샘플링해서 y를 맞춘다. 평지에서는 좌우로 살짝 밀려도 y가 거의 안 바뀌지만, 경사로에서는 좌우로 조금만 밀려도 y가 즉시 크게 바뀐다. 2번의 좌우 회피 진동이 경사로 위에서는 그대로 "위아래 들썩임"으로 보이게 되고, 이게 겹쳐 보이면 "서로 머리 위로 올라타려는 것처럼" 보인다.
4. **회피 우선순위(Avoidance Priority) 미설정**: 전 유닛이 동일 우선순위라 "누가 비켜야 하는가"에 대한 기준이 없어 자리다툼이 더 오래 지속된다.

즉, 핵심은 **"모든 선택 유닛이 같은 한 점으로 이동한다"는 설계**이고, 좁은 경사로는 이 문제를 극단적으로 드러내는 조건일 뿐이다(평지에서도 사실 겹치고 있었지만 티가 덜 났을 뿐).

## 제안하는 수정 (구현 전 — 확인 필요)

RTS에서 표준적인 해법인 **이동 대형(formation) 오프셋**을 적용한다. 유닛 개수만큼 목적지 주변에 격자/원형으로 흩어진 좌표를 계산해서, 유닛마다 다른 지점으로 보낸다.

### 기존 코드 (`RTSUnitController.cs`)

```csharp
public void MoveSelectedUnits(Vector3 end)
{
    for (int i = 0; i < selectedUnitList.Count; ++i)
    {
        selectedUnitList[i].MoveTo(end);
    }
}
```

### 변경 코드 (제안)

```csharp
[SerializeField] private float formationSpacing = 1.2f; // 유닛 반경(0.5) 대비 여유 있는 간격

public void MoveSelectedUnits(Vector3 end)
{
    List<Vector3> formationPoints = BuildFormationPoints(end, selectedUnitList.Count, formationSpacing);

    for (int i = 0; i < selectedUnitList.Count; ++i)
    {
        selectedUnitList[i].MoveTo(formationPoints[i]);
    }
}

// 목적지를 중심으로 정사각 격자 형태의 오프셋 좌표들을 생성한다.
// (NavMesh 위 유효 지점인지는 NavMeshAgent.SetDestination이 스스로 근처 유효 지점으로 보정해준다)
private List<Vector3> BuildFormationPoints(Vector3 center, int count, float spacing)
{
    var points = new List<Vector3>(count);
    int side = Mathf.CeilToInt(Mathf.Sqrt(count));
    int half = side / 2;

    for (int i = 0; i < count; i++)
    {
        int row = i / side;
        int col = i % side;
        Vector3 offset = new Vector3((col - half) * spacing, 0f, (row - half) * spacing);
        points.Add(center + offset);
    }

    return points;
}
```

이렇게 하면:
- 좁은 경사로에서도 유닛들이 각자 다른 목표점을 향하므로, 같은 지점에 몰려서 서로를 밀어내려는 진동 자체가 크게 줄어든다.
- 넓은 공간에서도 자연스러운 사각 대형으로 정렬되는 부가 효과가 있다.
- (보너스, 선택 사항) `navMeshAgent.avoidancePriority`를 유닛 생성 시 약간씩(예: 30~70 범위 랜덤) 다르게 주면, 같은 통로에서 마주칠 때 자리다툼이 더 빨리 해소된다. 다만 이건 좁은 경사로 문제의 근본 해결책은 아니고 보조 완화책이다.

## 요약 / 남은 작업

- 원인은 "선택 유닛 전원이 동일 좌표로 이동" + "좁은 경사로라 회피 공간 부족" + "경사면이라 y 샘플링이 민감하게 반응"의 조합.
- 위 formation 오프셋 수정을 적용할지 사용자 확인 필요. 확인되면 `RTSUnitController.cs`의 `MoveSelectedUnits`를 실제로 수정.
- 추가로 원한다면 `AttackGroundSelectedUnits`(공격-이동)에도 동일한 formation 로직을 적용할 수 있음(현재는 이 함수도 전원 동일 좌표로 보냄).

## 변경된 파일

- (아직 없음 — 조사/제안 단계)
