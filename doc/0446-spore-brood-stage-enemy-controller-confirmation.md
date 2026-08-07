# 0446. 스테이지 적 진영일 때 EnemyController 사용 확정 (질의응답)

**날짜:** 2026-08-07

## 요청 내용
> 그러자 그냥 스테이지에서 적일 경우는 EnemyController를 쓰자

## 답변

확정. `doc/0444`/`0445`에서 결론 낸 그대로 — 스테이지에서 Spore Brood가 **적 진영**으로 등장하는
경우는 `EnemyUnitController`/`EnemyBuildingController`를 그대로 씀(OC와 동일). 이미 프리팹에
붙어 있고, `RTSUnitController`가 Spore Brood 데이터베이스를 조회하도록 배선도 끝난 상태라 추가로
바꿀 코드는 없음.

"스테이지에서 적일 경우"라고 범위를 한정한 걸 보면, Spore Brood가 나중에 **플레이어가 직접 조종하는
진영**(스커미시 등)으로 쓰일 가능성은 열어둔 것으로 보임 — 그 경우엔 `UnitController`/
`BuildingController`(플레이어 진영용, 생산 큐/건설 등 포함) 쪽을 참고해야 하는 별개 작업이고,
지금 요청 범위는 아님. 필요해지면 그때 얘기해도 됨(YAGNI).

## 변경된 파일
- 없음 (기존 결정 확정, 코드/에셋 변경 없음)
