# 0197 - 안개 속 우클릭 명령은 이동으로만 처리 (0196 확장)

## 요청

"우클릭 명령은 이동명령만 가능하고 만약 적 위로 우클릭 했을때 공격인것도 일단 이동명령으로
처리하도록 해줄래 안개 안에서는 나머지 이동,순찰,공격명령들을 전부 땅을 대상으로 하는거로
예외처리해줘" — [[0196]] 제안 말미에 "우클릭 명령까지 같이 막을지" 물었던 것에 대한 답변 겸 구체적
지시. 안개에 가려진 대상에 우클릭하면(추격 공격/채취 등 특정 대상을 지정하는 명령 대신) 그냥 그
지점으로 이동하는 명령으로 처리해달라는 것.

## 조사 내용

좌클릭 쪽(OrderState 기반 이동/순찰/공격 명령)은 [[0196]]에서 `HandleLeftClick()`의 "2. 적 클릭"/
"5. 광물 클릭"/"5. 가스 클릭" 분기를 `IsRevealedByFog(...)`로 감싸면 **이미 자동으로 원하는 동작이
된다**: 안개에 가려진 대상을 클릭하면 그 분기 자체가 통째로 스킵되고 "4. 땅 클릭" 분기로 자연스럽게
넘어가서, 현재 OrderState(Move/Attack/Patrol/Rally/BuildingMove)에 맞는 **땅 좌표 기준** 명령이
그대로 나간다(Attack 모드면 `AttackGroundSelectedUnits(땅좌표)`처럼 이미 "땅을 대상으로" 처리됨).
그래서 "나머지 이동/순찰/공격명령을 땅 대상으로 예외처리"는 0196 구현만으로 좌클릭 경로는 이미
충족된다 — 추가 코드 불필요.

새로 손봐야 하는 곳은 `HandleRightClick()`뿐이다. 여긴 OrderState와 무관하게 항상 "적 우클릭 =
추격 공격", "광물/가스 우클릭 = 채취"처럼 고정 동작이라, 안개 가림 여부를 검사하는 코드가 아예 없다.

## 계획된 코드 변경

`Assets/Scripts/UserControl/UserControl.cs`의 `HandleRightClick()` — [[0196]]에서 추가하는
`IsRevealedByFog(Vector3)` 헬�퍼를 그대로 재사용.

**"1. 적 우클릭" 분기**

Before:
```csharp
        if (clickedEnemy && rtsUnitController.IsUnitSelect())
        {
            EnemyController enemy = enemyHit.transform.GetComponent<EnemyController>();

            if (enemy != null)
            {
                rtsUnitController.AttackSelectedUnits(enemy);
                enemy.FlashMarker(); // 어느 적이 공격 대상인지 마커 깜빡임으로 표시

                attackPointer.transform.position = enemy.transform.position;
                attackPointer.SetActive(true);

                return;
            }
        }
```
After:
```csharp
        if (clickedEnemy && rtsUnitController.IsUnitSelect())
        {
            EnemyController enemy = enemyHit.transform.GetComponent<EnemyController>();

            if (enemy != null)
            {
                // 안개에 가려진 적은 추격 공격 대신 그 지점으로 이동만 시킨다 (보이지 않는 대상을 특정해서 명령할 수 없음)
                if (IsRevealedByFog(enemyHit.point))
                {
                    rtsUnitController.AttackSelectedUnits(enemy);
                    enemy.FlashMarker(); // 어느 적이 공격 대상인지 마커 깜빡임으로 표시

                    attackPointer.transform.position = enemy.transform.position;
                    attackPointer.SetActive(true);
                }
                else
                {
                    rtsUnitController.MoveSelectedUnits(enemyHit.point);

                    movePointer.transform.position = enemyHit.point;
                    movePointer.SetActive(true);
                }

                return;
            }
        }
```

**"5. 광물 우클릭" 분기**

Before:
```csharp
        if (clickedOre)
        {
            ResourceNode node = OreHit.transform.GetComponent<ResourceNode>();
            if (node != null)
            {
                rtsUnitController.GatherSelectedUnits(node);
                node.FlashMarker(); // 어느 자원이 채취 대상인지 마커 깜빡임으로 표시
            }
        }
```
After:
```csharp
        if (clickedOre)
        {
            ResourceNode node = OreHit.transform.GetComponent<ResourceNode>();
            if (node != null)
            {
                // 안개에 가려진 자원은 채취 명령 대신 그 지점으로 이동만 시킨다
                if (IsRevealedByFog(OreHit.point))
                {
                    rtsUnitController.GatherSelectedUnits(node);
                    node.FlashMarker(); // 어느 자원이 채취 대상인지 마커 깜빡임으로 표시
                }
                else
                {
                    rtsUnitController.MoveSelectedUnits(OreHit.point);

                    movePointer.transform.position = OreHit.point;
                    movePointer.SetActive(true);
                }
            }
        }
```

**"5. 가스 우클릭" 분기** (같은 패턴)

Before:
```csharp
        if (clickedGas)
        {
            ResourceNode node = GasHit.transform.GetComponent<ResourceNode>();
            if (node != null)
            {
                rtsUnitController.GatherSelectedUnits(node);
                node.FlashMarker(); // 어느 자원이 채취 대상인지 마커 깜빡임으로 표시
            }
        }
```
After:
```csharp
        if (clickedGas)
        {
            ResourceNode node = GasHit.transform.GetComponent<ResourceNode>();
            if (node != null)
            {
                // 안개에 가려진 자원은 채취 명령 대신 그 지점으로 이동만 시킨다
                if (IsRevealedByFog(GasHit.point))
                {
                    rtsUnitController.GatherSelectedUnits(node);
                    node.FlashMarker(); // 어느 자원이 채취 대상인지 마커 깜빡임으로 표시
                }
                else
                {
                    rtsUnitController.MoveSelectedUnits(GasHit.point);

                    movePointer.transform.position = GasHit.point;
                    movePointer.SetActive(true);
                }
            }
        }
```

## 범위 밖(안 건드림)

- 건물 우클릭(아군 건물로 이동/건설 재개)과 아군 유닛 우클릭(따라가기)은 원래 항상 보이는 대상이라
  이번 요청과 무관 — 변경 없음.
- `GatherSelectedUnits`/`MoveSelectedUnits` 앞에 `rtsUnitController.IsUnitSelect()` 가드를 새로
  추가하지 않음 — 원래 광물/가스 분기도 그 가드 없이 호출하던 코드였고(`GatherSelectedUnits`가 선택된
  유닛이 없으면 내부적으로 아무 일도 안 하는 것으로 보임), 대칭성을 위해 Move 쪽도 동일하게 가드 없이
  둠.

## 영향받는 파일

- `Assets/Scripts/UserControl/UserControl.cs` (이 문서는 [[0196]]과 함께 한 번에 적용됨: `using
  FischlWorks_FogWar;`, `fogWar` 필드, `Awake()`의 `FindFirstObjectByType<csFogWar>()`,
  `IsRevealedByFog()` 헬퍼는 0196 문서 참고, 여기서 중복 기술하지 않음)

## 결과

0196 + 0197 내용을 한 번에 적용 완료 (사용자가 0196 제안 말미의 질문에 이 메시지로 직접 답하며
바로 구현 방향을 지시했으므로, 별도 재확인 없이 두 문서 내용을 함께 구현).
