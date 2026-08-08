# 0486 - 툴팁 패널에서 이름/설명 텍스트가 삐져나오는 문제

## 요청 내용
"현재 인게임 ToolTip의 panel 이미지가 글씨 크기가 살짝 삐져 나오는것처럼 보이는데 이것좀
확인해주고" → 후속으로 "정확히는 건설 모드에서 건물 이름이랑 설명이 나오는 부분에서 이름이랑
설명 부분이 살짝 삐져 나오네"로 범위 확정 (건설모드 = 비용 있는 툴팁, title+description 둘 다
표시되는 경우).

## 조사
Play Mode에 직접 들어가서 `TooltipUI.Show()`를 건물(Barracks) 데이터로 강제 호출해 재현 후,
`GameManager.prefab`의 좌표를 직접 대조:
- 패널 배경(`ToolTip`, root): `sizeDelta.x = 200`, 중심 `anchoredPosition.x = 0` → 오른쪽 끝 +100
- `TitleText`: `sizeDelta.x = 200`(root와 동일한 폭)인데 `anchoredPosition.x = 15` → 오른쪽 끝 +115
- `DescriptionText`: 마찬가지로 폭 200에 `anchoredPosition.x = 15` → 오른쪽 끝 +115

즉 텍스트 박스가 패널과 정확히 같은 폭인데 중심만 오른쪽으로 15px 밀려있어서, 오른쪽 가장자리가
패널보다 15px 튀어나가 있었음 - 프리팹에 원래 그렇게 배치돼 있던 값.

`TooltipContentFitter.Fit()`은 `hasCost=false`(자원 비용 없는 이동/공격 등 툴팁)일 때만
`SetX(...,0f)`로 가운데 정렬을 다시 맞추고, `hasCost=true`(건물/유닛 생산처럼 비용이 있는 툴팁)일
때는 X 위치를 아예 건드리지 않아서 이 15px 오프셋이 그대로 남아있었음 - 건설모드 건물
이름/설명이 바로 이 경로. 최근 폰트를 Pretendard-Black(볼드)로 바꾸면서(doc/0485) 글자가 상대적
으로 넓어져 그 튀어나온 가장자리에 더 가깝게/넘어가게 닿아 눈에 띄게 된 것일 뿐, 오프셋 자체는
폰트 교체와 무관한 기존 프리팹 배치 값이었음.

## 수정
`TooltipContentFitter.cs`의 `Fit()`에서 title/description의 `SetX(...,0f)` 호출을
`if (autoWidth)` 조건 밖으로 빼서, 비용 유무와 상관없이 항상 가운데 정렬하도록 변경. 텍스트 박스
폭이 항상 root와 같으므로(200=200) 가운데 정렬하면 좌우 여백이 대칭이 되어 어느 쪽으로도
튀어나가지 않음.

## 확인
Play Mode에서 건설모드 상태를 강제로 만들고 Barracks 데이터로 툴팁을 다시 띄운 뒤, title/
description의 `anchoredPosition.x`가 `15` → `0`으로 바뀐 것을 직접 확인(패널과 텍스트 박스 폭이
정확히 일치하므로 0이면 좌우 대칭 = 오버플로우 없음). 컴파일 확인 완료(에러 0).
