# 0164. NavMesh Modifier Volume 개념 질문

- 날짜: 2026-07-17

## 요청 내용

> navmesh modifier volume은 어떨때 사용하는거야?

(현재 프로젝트 씬에는 `NavMeshModifierVolume`/`NavMeshModifier` 사용처가 없음 — grep 결과 `Assets/Scenes` 내 매치 없음. [[0162-navmeshagent-testscene-move-command-stutter]] / [[0163-min-region-area-explanation]]에서 다루던 경사로 "한 폴리곤짜리 언덕" 문제의 대안 해법 맥락에서 나온 개념 질문.)

## 답변

`NavMeshModifierVolume`은 특정 오브젝트에 붙는 게 아니라, 씬에 박스 모양의 3D 영역을 배치해서 그 영역에 들어오는 모든 지오메트리의 베이크 방식을 덮어쓰는 컴포넌트.

### 주요 사용 사례

1. 특정 구역만 Area Type을 다르게(예: 늪지=느림 영역으로 경로 비용↑, 도로=선호 영역으로 비용↓)
2. 특정 구역을 Not Walkable로 막기 (실제 차단 오브젝트 없이도 그 자리에 구멍을 뚫을 수 있음)
3. 특정 구역을 NavMesh 생성에서 통째로 제외 (장식 클러터, 이펙트 존 등)
4. 여러 오브젝트로 이루어진 불규칙한 넓은 영역에 오브젝트별 태깅 없이 일괄 적용

### `NavMeshModifier`(비-볼륨)와의 차이

- `NavMeshModifier`: 특정 GameObject/Renderer에 직접 부착, "이 메시만" 대상으로 정밀하게 Area Type 변경/Ignore.
- `NavMeshModifierVolume`: 공간(위치) 기준이라 그 자리에 있는 모든 메시에 한꺼번에 적용.

### 현재 경사로 이슈와의 연결

[[0162-navmeshagent-testscene-move-command-stutter]]의 "한 폴리곤짜리 언덕"이 만약 경사로 메시와 바닥 지형 메시가 겹쳐서 생기는 거라면, 전역 Voxel Size/Min Region Area 조정보다 그 경사로 부분에만 `NavMeshModifierVolume`(Not Walkable)을 씌우거나 겹치는 메시에 `NavMeshModifier`(Ignore)를 붙이는 게 더 국소적이고 부작용 없는 해법일 수 있음을 안내. 단, 이는 지오메트리 겹침이 실제 원인인지 씬 뷰에서 먼저 확인이 필요하다고 언급.

## 요약 / 남은 작업

- 순수 개념 설명, 코드/설정 변경 없음.
- 경사로 지오메트리 겹침 여부 확인은 아직 안 됨 — 사용자가 씬 뷰에서 확인 후 다음 방향(Modifier Volume 적용 vs Min Region Area 상향 vs Voxel Size 중간값) 결정 필요.

## 변경된 파일

- (없음 — 개념 설명만)
