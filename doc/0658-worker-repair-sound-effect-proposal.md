# 0658 - 건물 수리 사운드/이펙트 (적용 완료)

## 날짜
2026-08-22

## 요청 내용
"건물에 수리 사운드를 추가해줬으면 좋겠어 수리 중일때 수리 사운드도 나오고 이펙트도 나왔으면 좋겠는데. 일꾼이 수리중인 방향(표면)에 레이저 공격받았을때 나는 이펙트가 나오도록 해줘"

## 조사 내용 - 재사용 가능한 기존 패턴

- **건물 사운드**: `BuildingAudio.cs` + `BuildingSoundBankSO.cs` - 건물 종류별로 `constructLoopSFX`/`takeoffSFX`/`destroySFX` 등을 담아두고, 특정 시점에 `SoundManager.Instance.PlaySFX(bank.xxxSFX, transform.position)`로 1회성 재생. `constructLoopSFX`도 실제로는 "루프 오디오"가 아니라 건설 시작 시 1회 재생되는 큐 - `SoundManager.PlaySFX`가 지속 루프 재생을 지원하지 않고 항상 원샷(pooled one-shot)이기 때문. 수리도 동일한 방식이 자연스러움 - **틱마다(0.5초 간격) 짧은 사운드를 재생**하면 체감상 "지지직" 반복음처럼 들림.
- **피격 이펙트**: `BuildingEffects.cs`가 이미 `HitEffectSet hitEffects` 필드를 들고 있고(총기/폭발/**레이저**/화염 4종 프리팹), 실제 피격 시 `EffectPlayer.PlayHit(transform, bodyCollider, attackerPosition, hitEffects.GetPrefab(attackType))`를 호출함. 이 함수가 정확히 "공격자 쪽을 향한 콜라이더 표면 지점"에 이펙트를 계산해서 재생함(`bodyCollider.ClosestPoint(attackerPosition)` + 바깥 방향 회전) - 요청하신 "일꾼이 수리중인 방향(표면)"과 완전히 동일한 계산.
- 건물마다 이미 `laserHitPrefab`이 인스펙터에 채워져 있으므로(실제 레이저 공격 시 쓰임) **새 프리팹을 추가할 필요 없이 그대로 재사용** 가능 - `hitEffects.GetPrefab(AttackEffectType.Laser)`를 그대로 넘기고 `attackerPosition` 자리에 "공격자 위치" 대신 "수리 중인 일꾼의 위치"를 넘기면 끝.

## 설계안

### 1. `BuildingSoundBankSO.cs`에 필드 추가
```csharp
[field: SerializeField]
public SoundClipSet repairTickSFX { get; private set; } // 수리 중 틱마다 재생 (지지직 소리 등)
```

### 2. `BuildingAudio.cs`에 메서드 추가
```csharp
public void PlayRepairTick()
{
    BuildingSoundBankSO bank = GetBank();
    if (bank != null)
        SoundManager.Instance?.PlaySFX(bank.repairTickSFX, transform.position);
}
```

### 3. `BuildingEffects.cs`에 메서드 추가 (새 프리팹 없이 기존 레이저 피격 이펙트 재사용)
```csharp
public void PlayRepairSpark(Vector3 workerPosition) =>
    EffectPlayer.PlayHit(transform, bodyCollider, workerPosition, hitEffects.GetPrefab(AttackEffectType.Laser));
```

### 4. `UnitController.cs` - `RepairTick()`에서 실제로 체력을 회복시키는 순간마다 호출
`BeginRepair()`에서 대상 건물의 `BuildingAudio`/`BuildingEffects`를 한 번만 `GetComponent`로 캐싱해두고(반복 조회 방지), `targetHealth.Heal(repairHpPerTick);` 바로 다음 줄에:
```csharp
repairAudio?.PlayRepairTick();
repairEffects?.PlayRepairSpark(transform.position); // transform = 수리 중인 일꾼 자신의 위치
```

결과: 일꾼이 건물에 붙어 수리하는 0.5초 틱마다 "지지직" 사운드 + 일꾼이 서 있는 쪽 건물 표면에 레이저 스파크 이펙트가 반복 재생됨. 자원이 부족해서 그 틱을 건너뛸 때는(기존 `warning.resource` 분기) 사운드/이펙트도 함께 건너뜀 - 실제로 회복이 일어난 틱에만 재생.

## 확인 필요한 사항
1. 이 설계대로 진행해도 될지 (기존 레이저 피격 프리팹/사운드뱅크 패턴 그대로 재사용, 새 이펙트 프리팹 없음)
2. `repairTickSFX` 클립은 비워두면 조용함(기존 사운드뱅크 필드들과 동일) - 나중에 인스펙터에서 직접 채워 넣으면 됨. 지금 당장 채워 넣을 클립이 있는지, 아니면 필드만 만들어두고 나중에 채울지

## 요약/영향받는 파일 (구현 시)
- `Assets/Scripts/ScriptableObject/BuildingSoundBankSO.cs` - `repairTickSFX` 필드 추가
- `Assets/Scripts/Audio/BuildingAudio.cs` - `PlayRepairTick()` 추가
- `Assets/Scripts/Effects/BuildingEffects.cs` - `PlayRepairSpark()` 추가
- `Assets/Scripts/Unit/UnitController.cs` - `repairAudio`/`repairEffects` 캐시 필드, `BeginRepair()`에서 캐싱, `RepairTick()`에서 회복 성공 시 호출

사용자 확인("예, 이대로 구현") 후 위 설계 그대로 구현함. `repairAudio`/`repairEffects`는 `UnitController.BeginRepair()`에서 `building.GetComponent<>()`로 한 번만 캐싱해서 매 틱 `GetComponent` 반복 조회를 피함(다른 캐시 패턴과 동일). `RepairTick()`에서 `targetHealth.Heal(repairHpPerTick)` 바로 다음에 `repairAudio?.PlayRepairTick()` + `repairEffects?.PlayRepairSpark(transform.position)` 호출 - 실제로 회복이 일어난 틱에만 재생되고, 자원부족으로 건너뛴 틱에는 재생 안 됨.

`repairTickSFX` 클립은 아직 비어있음 - 조용히 재생 스킵되며(기존 사운드뱅크 필드들과 동일 동작), 나중에 각 건물 사운드뱅크 에셋(`NTA Building Sound Bank SO` 등) 인스펙터에서 클립을 채워 넣으면 바로 재생됨.

`npx uloop-cli compile` 컴파일 성공 확인 (Success: true, 에러 0, 경고는 기존 49건 그대로 - 이번 변경과 무관).

## 후속: 실제 사운드 클립 연결
사용자가 `Assets/Sound/NTA/Building/Repair_Sound.mp3`를 추가함(guid `305d1d553e1921840a9798a0318192b4`). `NTA Building Sound Bank SO.asset`의 `repairTickSFX.clips`에 해당 guid를 텍스트로 직접 추가(다른 공용 SFX인 `placementSFX`/`selectVoice`와 동일하게 건물별이 아니라 NTA 전체가 공유하는 단일 사운드뱅크 자산이라 여기 한 곳만 채우면 됨). `uloop-execute-dynamic-code`로 `AssetDatabase.Refresh()` 후 `BuildingSoundBankSO`를 실제로 로드해서 `repairTickSFX`에 클립 1개("Repair_Sound")가 정상적으로 연결됐음을 확인함.

## 후속: 수리 속도 하향 (초당 20HP → 5HP)
사용자가 "초당 수리하는 수치를 좀 줄여줘야할거 같아"라고 요청. 얼마나 줄일지 물어봤고 "초당 5HP (1/4)"로 답변받음. `UnitController.cs`의 `repairTickInterval`/`repairHpPerTick` 기본값을 `0.5f`/`10`에서 `1f`/`5`로 변경(정수 회복 유지 - doc/0657에서 정한 "틱당 정수 HP" 원칙 그대로, 초당 5HP를 정수로 나타내려면 1초당 5HP가 0.5초당 10HP보다 더 단순함). 자원 소모(`repairOreCostPerTick`)는 `repairHpPerTick`에 비례해서 자동으로 같이 줄어듦(코드 변경 불필요). `npx uloop-cli compile` 컴파일 성공 확인 (에러 0).

## 후속: 0.5초당 3으로 재조정 + 체력 증가를 정수 틱에서 실수 누적(건설 중과 동일한 방식)으로 전환
요청: "0.5초당 3으로 바꾸고, 올라가는게 정수로 올라가는게 아니라 실수로 건물 건설때 체력 차는거 처럼 만들어보자". doc/0657에서 "정수로 수리되는편이 체력관련 버그가 없을거 같다"고 정했던 방향을 이번에 뒤집은 것 - 이번엔 시각적으로 매끄럽게 차오르는 쪽을 원함.

`BaseStructure.cs`(건설 중 파운데이션)가 이미 정확히 이 패턴을 쓰고 있음을 확인:
```csharp
healAccumulator += healthPerSecond * Time.deltaTime;
if (healAccumulator >= 1f) {
    int wholeHeal = Mathf.FloorToInt(healAccumulator);
    healAccumulator -= wholeHeal;
    healthManager.Heal(wholeHeal);
}
```
`HealthManager.Heal()`이 `int`만 받기 때문에, 매 프레임 소수점을 누적해뒀다가 정수가 모이면 그만큼만 `Heal()`을 호출하는 방식 - 이러면 `Heal()` 자체는 여전히 정수 단위지만(그래서 체력 관련 버그 없음), 매 프레임 조금씩 호출되므로 체력바가 시각적으로 부드럽게 올라간다. 이걸 수리에도 그대로 이식.

`UnitController.cs`:
- `repairTickInterval`=0.5f, `repairHpPerTick`=3 (0.5초당 3 = 초당 6)로 변경
- 자원 정산(광물)은 여전히 `repairTickInterval`마다 정수로 한 번에 처리 - 반올림 오차/경제 로직은 그대로 유지, 결제에 성공한 구간에서만 `repairPaidThisTick = true`
- 체력은 결제된 구간 동안 `repairHealAccumulator += (repairHpPerTick / repairTickInterval) * Time.deltaTime`로 매 프레임 누적하다가 1 이상 모이면 `Heal(정수만큼)` - `BaseStructure`와 동일한 계산
- 자원이 부족해서 그 구간이 결제되지 않으면(`repairPaidThisTick = false`) 그 구간 동안은 누적도 멈춤 - 돈 안 내고 체력만 차는 일 없음
- 사운드/이펙트는 구간이 성공적으로 결제되는 순간(0.5초 간격)에만 재생 - 체력 누적 프레임마다 재생하면 너무 잦아서 기존 그대로 유지

`npx uloop-cli compile` 컴파일 성공 확인 (Success: true, 에러 0).

## 후속4: 수리 중 건물 바라보기 + 최대한 가까이 붙기
요청: "일꾼이 건물 수리중일땐 건물을 쳐다보도록 하고, 최대한 건물 가까이 가도록해줘".

기존 코드에서 재사용 가능한 패턴 확인:
- **바라보기**: 건설 중 배회할 때 이미 `FaceConstructionStructure()`가 매 프레임 `Quaternion.Slerp`로 건물 쪽을 부드럽게 바라보게 하고 있었음 - 이 로직을 `FaceTransform(Transform target)`라는 공용 헬퍼로 뽑아내고, `FaceConstructionStructure()`는 `FaceTransform(attachedStructure.transform)` 한 줄로 위임하도록 리팩터링. `RepairTick()`에서도 매 프레임 `FaceTransform(repairTarget.transform)`를 호출.
- **가까이 붙기**: `GoBuild()`의 도착 판정 반경이 지금까지 전역 `buildInteractRange`(2f, 건설용)로 고정돼 있었음 - `GoBuild()`에 `interactRange` 선택 인자를 추가(기본값 -1이면 기존처럼 `buildInteractRange` 사용, 다른 호출부는 코드 변경 없이 그대로 동작)하고, `Repair()`만 새 `repairInteractRange`(기본 0.3f - 건설용보다 훨씬 좁음)를 넘기도록 함. 목적지 자체는 이미 `GetClosestSurfacePoint()`로 건물 콜라이더 표면의 가장 가까운 지점이므로, 도착 판정 반경만 좁히면 일꾼이 그 표면 지점에 거의 붙어서 멈춘다.

`npx uloop-cli compile` 컴파일 성공 확인 (Success: true, 에러 0).

## 후속5: "최대한 가까이" 되돌림
"최대한 가까이 붙기" 부분(`repairInteractRange`, `GoBuild()`의 `interactRange` 선택 인자, `activeBuildInteractRange`)이 필요 없다고 판단해서 요청받아 전부 되돌림. `GoBuild()`는 원래 시그니처(3개 인자)로 복귀, `BuildTick()`도 다시 `buildInteractRange`를 직접 참조. 건물 바라보기(`FaceTransform`)는 그대로 유지 - 요청은 "가까이 붙기"만 제거하는 것이었음. `npx uloop-cli compile` 컴파일 성공 확인 (에러 0).

## 후속: 스타크래프트 수리 공식 적용 (건물별 초당 회복량)
"초당 수리 HP = 0.9 × (대상 최대 HP ÷ 건설시간[초])" 공식을 적용해달라는 요청. `BuildingData.productionTime`(건설시간)이 이미 있으므로 새 데이터 없이 그대로 재사용 가능함을 먼저 확인한 뒤 적용.

건물별 실제 수치(참고, `NTA Building Data SO`의 `productionTime` × 프리팹 `HealthManager.maxHealth`):

| 건물 | 최대체력 | 건설시간 | 초당 회복 |
|---|---|---|---|
| CommandCenter(메인기지) | 1500 | 50 | 27.0 |
| SupplyDepot | 500 | 25 | 18.0 |
| Barracks(Tier1) | 1000 | 37 | 24.3 |
| Factory(Tier2) | 1250 | 37 | 30.4 |
| Spaceport(Tier3) | 1300 | 37 | 31.6 |
| Lab | 850 | 37 | 20.7 |

`UnitController.cs` 변경:
- 고정 `repairHpPerTick`(int) 필드를 제거하고, `[SerializeField] private float repairSpeedMultiplier = 0.9f;` 추가 (공식의 그 0.9 - 나중에 밸런스 조정하고 싶으면 이 값만 바꾸면 됨)
- `BeginRepair()`에서 `repairHealthPerSecond = repairSpeedMultiplier * maxHealth / buildTime;`로 건물별 초당 회복량을 1회 계산해서 고정 (`buildTime`은 `BuildingData.productionTime`, 없으면 임의 40초로 fallback)
- 자원 비용 계산도 고정값 대신 `hpPerTick = repairHealthPerSecond * repairTickInterval`로 그 구간에 실제로 채워질 HP를 구해서 사용 - 건물마다 회복 속도가 다르므로 구간당 광물 소모량도 건물마다 다르게 나옴(빨리 회복되는 건물은 그만큼 광물도 빨리 나감, 초당 소모량 자체는 "원가÷최대체력" 비율로 동일)
- 체력 누적(`repairHealAccumulator += repairHealthPerSecond * Time.deltaTime`)도 이 값을 그대로 사용 - 건설 중 체력 차오름과 동일한 매끄러운 방식은 그대로 유지

`npx uloop-cli compile` 컴파일 성공 확인 (Success: true, 에러 0).
