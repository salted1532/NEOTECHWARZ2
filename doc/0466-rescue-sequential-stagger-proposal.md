# 0466. 구조 완료 시 유닛별 순차 연출(0.1초 간격) 제안

**날짜:** 2026-08-08

## 요청 내용
> 클립들이 다 동시에 작동하지 않도록 랜덤하게 작동하도록 해줄래 아니면 Rescue되었다는걸 각 유닛별로
> 딜레이를 줘서 리스트 순서대로 0.1초에 한마리씩 구조되도록 해도 되고

두 방식 중 사용자가 "유닛별 순차 구조 연출(0.1초 간격)"을 선택함.

## 조사

`Stage3Objectives.Update()`(서브목표 완료 판정)가 완료되는 순간 이렇게 8마리 전부를 같은 프레임에
한꺼번에 구조 처리함:
```csharp
foreach (UnitController unit in rescuedUnits)
    unit?.Rescue();
```
`UnitController.Rescue()`(`doc/0465`)는 마커 초록 전환 + 깜빡임(`FlashMarkerRoutine`) + SFX
(`rescueSfx`) + 미니맵 색상 + 시야 확장을 전부 그 자리에서 처리하므로, 8마리가 같은 프레임에
동시에 불려서 마커 깜빡임/SFX가 전부 겹쳐 보이고 들림.

## 제안하는 변경

`Stage3Objectives.cs`에 새 인스펙터 필드로 간격을 노출하고(기존 `rescueFlashInterval` 등과 동일한
관례 - 매직넘버 대신 필드), `foreach` 직접 호출을 코루틴으로 바꿔 `rescuedUnits` 리스트 순서대로
그 간격만큼 텀을 두고 한 마리씩 `Rescue()`를 호출한다:

```csharp
[SerializeField] private float rescueStaggerInterval = 0.1f;

// Update()에서: foreach(...) unit?.Rescue(); 대신
StartCoroutine(RescueSequence());

private IEnumerator RescueSequence()
{
    foreach (UnitController unit in rescuedUnits)
    {
        unit?.Rescue();
        yield return new WaitForSeconds(rescueStaggerInterval);
    }
}
```

`UnitController.Rescue()`/`rescueSfx`(`doc/0465`)는 그대로 - 호출 시점만 리스트 순서대로 0.1초씩
벌어지므로 마커 깜빡임과 SFX가 자연히 한 마리씩 순서대로 재생된다(추가 랜덤/딜레이 로직 불필요).

## 영향 범위
- `survivorsRescued`(서브목표 완료 텍스트/판정) 자체는 `Update()`에서 즉시 `true`로 세팅된 그대로
  유지 - 순차 연출은 `rescuedUnits` 개별 `Rescue()` 호출 타이밍에만 영향, 목표 완료 판정 자체는
  지연되지 않음.
- 8마리 전부 순차 처리가 끝나기까지 약 0.7초(8마리 × 0.1초 간격) 소요.

## 변경 예정 파일
- `Assets/Scripts/System/Stage3Objectives.cs`

## 구현 결과

제안한 그대로 적용:
- `rescueStaggerInterval`(기본 0.1f) 필드 추가.
- `IsAnyUnitTouchingBeacon()`으로 완료 판정된 프레임에 `foreach(...) unit?.Rescue();` 직접 호출
  대신 `StartCoroutine(RescueSequence())` 호출.
- `RescueSequence()`: `rescuedUnits` 리스트 순서대로 `Rescue()` 호출 후 `rescueStaggerInterval`만큼
  대기, 반복.

## 검증
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).

## 변경된 파일
- `Assets/Scripts/System/Stage3Objectives.cs`