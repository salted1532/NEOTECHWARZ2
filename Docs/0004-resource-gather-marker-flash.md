# 0004. 자원 채취 우클릭 시 마커 깜빡임

## 날짜
2026-07-07

## 요청
일꾼으로 자원에다가 우클릭 했을때 자원 마커가 0.3초 간격으로 3번 깜박이고 마커꺼지도록 그니깐 어떤 광물을 우클릭해서 선택된건지 보여주는거야 일꾼선택 -> 광물 우클릭 -> 광물 깜박임 3번하고 꺼짐

## 답변 / 변경사항
- `ResourceNode.cs`에 `FlashMarker()` 추가. 우클릭으로 채취 명령을 내리면 `resourceMarker`가 0.3초 간격으로 3번 켜졌다 꺼졌다 하고(총 6단계), 마지막엔 꺼진 채로 남음.
- 좌클릭으로 이미 선택되어 마커가 켜져 있던 자원이라면, 깜빡임이 끝난 뒤 원래 선택 상태로 복원(꺼진 채로 방치하지 않음) — `rtsController.selectedResourceNode == this` 체크로 판별.
- `UserControl.cs`의 광물/가스 우클릭 처리(`HandleRightClick`)에서 `GatherSelectedUnits` 호출과 함께 `node.FlashMarker()` 호출하도록 연결.

## 변경 파일
- `Assets/Scripts/Resource/ResourceNode.cs`
- `Assets/Scripts/UserControl/UserControl.cs`
