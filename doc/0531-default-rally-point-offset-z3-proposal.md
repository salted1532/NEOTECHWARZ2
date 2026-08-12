# 0531 - 건물 기본 랠리 포인트 오프셋 -2 → -3

## 결과
사용자 확인 후 제안대로 구현 완료. `BuildingController.cs`, `BaseStructure.cs` 2개 파일 반영.
Unity 컴파일 확인 완료(에러 0). 경고 38개는 전부 기존 코드의 `FindFirstObjectByType` obsolete 등
이번 변경과 무관한 사전 존재 경고.

추가 요청으로 오프셋을 -3 → -5로 재조정(같은 2개 파일, 같은 라인). 컴파일 재확인 완료(에러 0, 경고 0).
최종 값: `transform.position + new Vector3(0, 0, -5f)`.

## 날짜
2026-08-12

## 요청 내용
"모든 건물의 초기 랠리포인트 위치를 본인 위치가 아니라 z값으로 -3정도 아래로 지정되도록 해줘 모든 건물에다가"

## 조사 내용
기본(미지정) 랠리 포인트를 계산하는 곳은 코드 전체에서 딱 두 군데이고, 둘 다 동일한 공식
`transform.position + new Vector3(0, 0, -2f)`을 쓴다.
- `Assets\Scripts\Building\BuildingController.cs:135` (완공된 생산 건물 `Start()`, `!rallyInitialized`일 때만)
- `Assets\Scripts\Building\BaseStructure.cs:54` (건설 중인 건물 기반 `Start()`)

(참고: `Assets\Scripts\UnitSpawner\UnitSpawner.cs:86`에도 같은 `-2f` 오프셋이 있지만 이건 유닛이 실제로
Instantiate되는 스폰 좌표이지 랠리 포인트가 아니라서 이번 요청 범위 밖으로 둠.)

우클릭으로 직접 지정한 랠리 포인트(`hasCustomRally` / `rallyInitialized`)는 이 기본값 계산을 거치지
않으므로 영향 없음 - 순수하게 "아직 아무도 지정 안 한 최초 기본값"만 -2 → -3으로 바뀐다.

## 설계 (제안)
두 파일의 `-2f`를 `-3f`로 바꾼다. 그 외 변경 없음.

## 코드 변경 (제안)

### Assets\Scripts\Building\BuildingController.cs
```csharp
        if (!rallyInitialized)
-           RallyPosition = transform.position + new Vector3(0, 0, -2f);
+           RallyPosition = transform.position + new Vector3(0, 0, -3f);
```

### Assets\Scripts\Building\BaseStructure.cs
```csharp
-       RallyPosition = transform.position + new Vector3(0, 0, -2f);
+       RallyPosition = transform.position + new Vector3(0, 0, -3f);
```

## 영향받는 파일
- `Assets\Scripts\Building\BuildingController.cs`
- `Assets\Scripts\Building\BaseStructure.cs`

## 스코프 밖(안 하는 것)
- `UnitSpawner.cs`의 스폰 좌표 오프셋은 랠리 포인트가 아니라 건드리지 않음.
