# 0165. 언덕 아래 가려진 지상에도 NavMesh가 생기는 문제 해결법

- 날짜: 2026-07-17

## 요청 내용

> 현재 Testscene에서 layer1, 2로 2개의 층으로 언덕 지상을 표현하고 있는데 navmesh를 구우면 언덕 아래 가려진 지상도 한칸한칸 navmesh가 생성되는 이를 해결하려면 어떻게 해야해?

## 조사 내용

`doc/0158-camera-zoom-terrain-tier-tag-based.md`에서 이미 파악해둔 TestScene 지형 구조를 재활용:

- `Assets/prefabs/Maps/Mission1.prefab` 아래 `layer1`(태그 `Layer1`, 언덕), `layer2`(태그 `Layer2`, 언덕 위 언덕) GameObject가 있고, 실제 메시/콜라이더는 그 밑에 자식으로 붙은 수십~수백 개의 타일 프리팹 인스턴스에 있음(YuME 타일맵 방식).
- 태그 없는 기본 지상(tier 0)이 별도로 존재하며, `layer1`/`layer2`는 그 위에 XZ상으로 겹쳐 얹힌 형태.

### 원인

Recast는 하나의 XZ 복셀 기둥에 걸을 수 있는 표면이 여러 층 있어도, 그 위에 에이전트 Height(2) 이상의 빈 공간이 있으면 각 층을 전부 독립적으로 걸을 수 있다고 판단해 NavMesh를 생성한다. 언덕(layer1/layer2)이 얇은 지형 메시라서 그 밑면과 가려진 기본 지상 사이에 물리적으로 2 유닛 이상 뜬 공간이 있으면, 실제로는 언덕이 막고 있어 아무도 갈 수 없는 가려진 지상까지도 NavMesh로 구워진다.

## 제안하는 해결책 (구현 전 — 확인 필요)

`NavMeshModifierVolume`으로 가려진 하위 층을 공간 기준으로 제외:

1. `layer1`(언덕) 자식 타일들의 XZ 풋프린트와 동일한 크기의 `NavMeshModifierVolume`을 생성. Y 범위는 "기본 지상 표면 아래" ~ "layer1 표면 바로 아래"까지만 잡음(layer1 자신의 걷는 면은 볼륨 밖에 있어야 함). Area Type = `Not Walkable`.
2. `layer2`(언덕 위 언덕) 풋프린트에도 동일하게 — Y 범위 "layer1 표면 아래" ~ "layer2 표면 바로 아래"인 볼륨을 추가로 생성. Area Type = `Not Walkable`.
3. 타일이 수십~수백 개라 개별 `NavMeshModifier`(Ignore)를 하나하나 붙이는 것은 비현실적이므로, 오브젝트 단위가 아닌 공간(볼륨) 단위로 일괄 제외하는 이 방식을 권장.

주의: 볼륨의 위쪽 Y 경계가 상위 tier의 실제 표면 높이까지 침범하면 그 tier 자체도 같이 Not Walkable로 잘려나가므로, 상위 tier 표면보다 살짝(예: 0.1~0.2) 낮게 여유를 두고 잡아야 함. `doc/0158`에서 카메라 줌 보정에 쓴 tier 간격 `5` 유닛이 실제 지형 높이차와 정확히 일치하는지는 별도 확인 필요 — 에디터에서 각 layer 타일의 실제 표면 Y값을 직접 확인 후 볼륨 Y 범위를 맞추도록 안내함.

## 요약 / 남은 작업

- 순수 조사/제안, 코드·씬 변경 없음.
- 사용자가 각 tier의 실제 표면 Y좌표를 에디터에서 확인해 알려주면, `NavMeshModifierVolume` 오브젝트를 `TestScene.unity`(또는 `Mission1.prefab`)에 추가하는 작업을 진행할 수 있음 — 아직 미확정.

## 변경된 파일

- (없음 — 조사/제안만)
