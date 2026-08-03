# 0413 - UnreachableRepathInterval 1초 -> 5초 변경

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> UnreachableRepathInterval은 1초를 한 5초정도로 변경해볼래

[[0412]] 라이브 테스트에서 "여전히 도달 불가" 재확인이 코드가 의도한 1초보다 훨씬 자주(약 0.55초
간격) 찍히는 현상이 관찰됐다 - 정확한 원인은 아직 미확정. 간격을 5초로 늘려서 실제 플레이에서
체감되는 빈도를 줄이고, 어느 정도로 벌어지는지 다시 관찰해보기 위한 조정.

## 코드 변경

### `Assets/Scripts/Unit/UnitController.cs` (638번째 줄)

```csharp
private const float UnreachableRepathInterval = 1f;
```
->
```csharp
private const float UnreachableRepathInterval = 5f;
```

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (331번째 줄)

동일하게 `1f` -> `5f`.

## 영향받는 파일

- `Assets/Scripts/Unit/UnitController.cs` (638번째 줄)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (331번째 줄)
