# 0536 - 공격 웨이브와 점령지 탈환이 따로 도는지 질문

## 날짜
2026-08-13

## 요청 내용
"공격 웨이브와 점령지 탈환은 따로 돌아가나 어떻게 돌아가는거지?"

## 답변
`EnemyAIDirector.Start()`에서 서로 완전히 독립된 코루틴으로 각각 시작된다 - 하나가 다른 하나의 스케줄을
막거나 기다리지 않는다.

```csharp
if (waveTimes.Count > 0)
    StartCoroutine(AttackWaveRoutine());   // 공격 웨이브 - waveTimes 스케줄(다 쓰면 마지막 간격 반복)
if (raidTargets.Count > 0)
    StartCoroutine(RaidRoutine());          // 점령지 탈환 - raidInterval(기본 45초)마다
StartCoroutine(ReinforceRoutine());         // 보충 생산 - reinforceCheckInterval마다
```

**단, 완전히 무관하진 않다** - 둘 다 같은 `garrison`(이 director의 병력 풀)에서 `TakeSquad()`로 인원을
뽑아 쓴다. 한쪽이 방금 대규모로 병력을 빼갔으면 다른 쪽이 그 직후 차출할 때 인원이 부족하거나 0일 수
있음(별도 에러 처리 없이 조용히 못 나감) - 부족분은 `ReinforceRoutine`이 나중에 채워서 다음 주기엔
정상화됨. 자세한 후속 질문은 doc/0537 참고(같은 `garrison`/`TakeSquad`를 실제로 공유하는지 확인).

## 영향받는 파일
없음 (질문/설명, 코드 변경 없음)
