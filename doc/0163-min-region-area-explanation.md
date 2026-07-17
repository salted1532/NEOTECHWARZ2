# 0163. Min Region Area 개념 질문

- 날짜: 2026-07-17

## 요청 내용

> Min Region Area는 뭐야?

([[0162-navmeshagent-testscene-move-command-stutter]]에서 Voxel Size 오버라이드를 끄고 재베이크한 뒤 경사로에 "한 폴리곤짜리 언덕"이 생긴 문제의 해결책으로 Min Region Area 상향을 제안했는데, 그 옵션 자체가 뭔지에 대한 후속 질문)

## 답변

`Min Region Area`는 Unity NavMeshSurface 베이크 설정 항목으로, **베이크 결과에서 일정 면적보다 작은 고립된(독립된) 리전을 통째로 걸러내는 임계값**이다.

- Recast가 지형을 복셀화한 뒤 걸을 수 있는 표면들을 리전 단위로 그룹핑하는데, 주변과 잘 이어지지 못하고 따로 떨어진 자잘한 리전(지형 이음새 틈, 경사로 경계에 튀어나온 조각 등)이 생길 수 있음.
- `Min Region Area`보다 넓이가 작은 리전은 NavMesh에서 아예 제외됨.
- 값이 작을수록(현재 TestScene은 `2`) 자잘한 리전도 살아남아 NavMesh에 포함되고, 값이 클수록 작은 리전이 잘려나가 노이즈가 줄어들지만 폭이 좁은 진짜 통로(좁은 경사로 초입 등)까지 같이 잘려나갈 위험이 있음.

[[0162-navmeshagent-testscene-move-command-stutter]]에서 보고된 "한 폴리곤짜리 들어올려진 언덕"은 전형적인 고립 리전 형태일 가능성이 높아, `Min Region Area`를 `2` → `4~6` 정도로 올리면 베이크 단계에서 걸러질 수 있음. 다만 상향 후에는 좁은 경사로 통로가 끊기지 않았는지 실제로 이동해보며 확인이 필요하다고 안내함.

## 요약 / 남은 작업

- 순수 개념 설명, 코드/설정 변경 없음.
- [[0162-navmeshagent-testscene-move-command-stutter]]에서 제안한 Min Region Area 상향(및 필요 시 Voxel Size 중간값 조정)을 사용자가 적용할지는 아직 미확정 — 확인되면 `TestScene.unity`의 NavMeshSurface 설정 수정 및 재베이크 진행.

## 변경된 파일

- (없음 — 개념 설명만)
