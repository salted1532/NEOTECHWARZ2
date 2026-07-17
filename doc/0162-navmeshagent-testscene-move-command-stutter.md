# 0162. TestScene에서만 NavMeshAgent 이동 명령이 잠깐 멈췄다가 시작하는 현상 조사

- 날짜: 2026-07-17

## 요청 내용

> navmeshagent를 사용하는 유닛들이 왜 SampleScene의 plane 맵에선 같은 위치나 다른 위치를 찍어도 끊김 없이 바로 바로 목적지를 정하는거 같은데
> TestScene에서 같은 위치, 다른위치를 지속적으로 찍게 되면 잠깐 멈췄다가 움직이는데 이유가 뭐야?

## 조사 내용

### 1) 코드 경로는 두 씬에서 동일함

`Assets/Scripts/Unit/UnitController.cs`의 `MoveTo` → `MoveAgentTo` → `navMeshAgent.SetDestination(destination)` 흐름과 `Assets/Scripts/System/RTSUnitController.cs`의 `MoveSelectedUnits`에는 딜레이를 유발할 코루틴/`Invoke`/`WaitForSeconds` 등이 전혀 없다. 즉 이동 명령을 내리는 코드 자체는 두 씬에서 완전히 동일하게 동작하므로, 씬별 차이는 코드가 아니라 **환경(NavMesh 베이크 데이터)**에서 온다.

### 2) 두 씬의 활성 NavMesh 에셋 크기 비교

각 씬의 `NavMeshSurface.m_NavMeshData` guid로 실제 사용 중인(활성) 베이크 에셋을 역추적:

| 씬 | 활성 NavMesh 에셋 | 파일 크기 |
|---|---|---|
| SampleScene | `NavMesh-TestMap 1.asset` | 9,356 bytes |
| TestScene | `NavMesh-Mission1 1.asset` | **2,154,536 bytes** |

TestScene의 활성 NavMesh가 SampleScene보다 **약 230배** 크다.

### 3) 원인: TestScene NavMeshSurface의 Voxel Size 오버라이드

`Assets/Scenes/SampleScene.unity` (NavMeshSurface 컴포넌트):
```yaml
m_OverrideVoxelSize: 0
m_VoxelSize: 0.16666667   # 자동 계산값 (에이전트 반경 0.5 기준 적정 수준)
```

`Assets/Scenes/TestScene.unity` (NavMeshSurface 컴포넌트):
```yaml
m_OverrideVoxelSize: 1
m_VoxelSize: 0.01          # 수동 오버라이드 - 극단적으로 세밀함
```

복셀 크기가 약 16배(0.1667 → 0.01) 더 촘촘해지면, 동일 부피 안의 복셀 개수는 세제곱으로 늘어나(약 4,600배) 베이크된 NavMesh의 타일/폴리곤 수가 폭증한다. 이게 파일 크기 차이(9KB vs 2.1MB)로 그대로 드러난다.

런타임에서 `NavMeshAgent.SetDestination()`은 내부적으로 비동기 A* 경로탐색을 수행하는데, 그래프가 이렇게 훨씬 촘촘하고 큰 TestScene에서는 탐색에 필요한 반복 횟수가 크게 늘어나 `pathPending`이 true로 유지되는 시간이 길어진다. 이게 "클릭 → 잠깐 멈춤 → 이동 시작"으로 체감되며, 같은/다른 위치를 연속으로 찍을 때마다 매번 무거운 재계산이 다시 발생해 더 두드러진다. SampleScene은 기본(자동) 복셀 크기라 평면 위에서 경로가 사실상 즉시(1프레임 내) 나온다.

## 결론

원인은 코드가 아니라 **TestScene NavMeshSurface의 `Override Voxel Size = 0.01` 설정**이다. 에이전트 반경(0.5) 기준으로 권장되는 값(자동 계산 시 약 0.167)보다 훨씬 세밀하게 잡혀 있어 NavMesh 데이터가 비정상적으로 커졌고, 그 결과 경로탐색 쿼리 비용이 커져 이동 명령마다 지연이 생긴다.

## 제안하는 수정 (구현 전 — 확인 필요)

TestScene NavMeshSurface의 `Override Voxel Size`를 끄거나(자동 계산 사용), 유지하더라도 `0.15~0.25` 범위로 올린 뒤 **Unity 에디터에서 Bake를 다시 실행**해야 한다. NavMesh `.asset`은 베이크 시 자동 생성되는 산출물이라 텍스트 편집만으로는 실제 경로탐색 데이터가 갱신되지 않는다 — `m_OverrideVoxelSize`/`m_VoxelSize` 값 자체는 미리 고쳐둘 수 있지만, 최종 재베이크는 에디터에서 NavMeshSurface의 Bake 버튼을 눌러야 한다.

## 요약 / 남은 작업

- 순수 조사/답변, 코드 변경 없음.
- 사용자 확인 시 `TestScene.unity`의 `m_OverrideVoxelSize`/`m_VoxelSize` 값 수정 예정 (재베이크는 에디터에서 사용자가 직접 실행 필요).

## 변경된 파일

- (없음 — 조사/답변만)
