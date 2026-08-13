# 0537 - 점령지 탈환 별동대와 공격 웨이브가 따로 모집되는지 질문

## 날짜
2026-08-13

## 요청 내용
"그럼 점령지 탈환 별동대와 공격 별동대는 따로 모집되는건가?"

## 답변
아니요, **완전히 같은 풀에서 모집**된다. `LaunchWave()`(공격 웨이브)와 `RaidRoutine()`(점령지 탈환) 둘
다 똑같은 `TakeSquad(int size)` 메서드를 부르고, 이 메서드는 director 하나가 가진 단일 `garrison`
리스트 + `deployed` HashSet만 본다 - 웨이브 전용/별동대 전용으로 나뉜 예비 인원 같은 건 없다.

```csharp
private List<EnemyUnitController> TakeSquad(int size)
{
    garrison.RemoveAll(u => u == null);

    List<EnemyUnitController> squad = new List<EnemyUnitController>();
    foreach (EnemyUnitController unit in garrison)
    {
        if (squad.Count >= size) break;
        if (deployed.Contains(unit)) continue;

        squad.Add(unit);
        deployed.Add(unit); // 뽑히는 순간 "이미 나간 유닛"으로 표시 - 다음 호출(웨이브든 별동대든)에서 제외됨
    }

    return squad;
}
```

즉 "누가 먼저 `TakeSquad`를 호출하느냐"가 그 인원을 가져간다 - 웨이브 발사 타이밍과 별동대 발사 타이밍이
겹치면 먼저 실행된 쪽이 `garrison`의 앞쪽 인원을 채가고, 늦게 실행된 쪽은 남은 인원(또는 0명)만 받는다.
(doc/0536에서 이미 짚었던 "같은 수비대 명부를 놓고 경쟁"이 바로 이 `TakeSquad` 공유 구조 때문.)

## 영향받는 파일
없음 (질문/설명, 코드 변경 없음)
