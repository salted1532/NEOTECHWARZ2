# 0015. 버그 수정: 아군 건물 강제 공격이 작동하지 않음

## 날짜
2026-07-07

## 요청
작동을 안하는데 내가 해야할일 있나?

## 원인
`UserControl.HandleLeftClick()`에서 건물 클릭 처리(당시 "4. 건물 클릭") 블록이 땅 클릭 처리("3. 땅 클릭") **뒤에** 있었음. 건물도 지면 위에 서 있어서 `clickedGround` 레이캐스트가 함께 `true`가 되는데, A 모드(`OrderState.Attack`) 상태에서 땅 클릭 블록이 먼저 실행되어 `AttackGroundSelectedUnits(groundHit.point)`(땅 공격-이동)를 호출하고 `return`해버려서, 건물 블록에 아예 도달하지 못했음. 0005에서 적(enemy) 클릭을 땅 클릭보다 먼저 처리하도록 순서를 바꿨던 것과 동일한 문제인데, 건물에는 그 순서 변경을 놓치고 적용하지 않았던 것이 원인.

## 답변 / 변경사항
`UserControl.cs`의 `HandleLeftClick()`에서 건물 클릭 처리 블록을 땅 클릭 처리보다 앞으로 이동 (유닛 → 적 → **건물** → 땅 → 광물/가스 순서로 재배치). 로직 자체는 변경 없음, 위치만 이동.

## 변경 파일
- `Assets/Scripts/UserControl/UserControl.cs`
