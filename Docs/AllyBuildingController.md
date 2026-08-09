# AllyBuildingController

`Assets/Scripts/FogOfWar/Ally/AllyBuildingController.cs`

## 개요

아군 OC 건물 컨트롤러. `EnemyBuildingController`는 애초에 이동/전투 AI가 전혀 없는 순수 껍데기(체력/선택/사망 처리만)라 "AI를 따로 조종"할 대상 자체가 없다 — 그래서 `AllyController`(유닛)와 달리 로직을 복제하지 않고 그대로 상속한다. 이름만 다른 타입으로 두는 이유는 `AllyAttackRange`와 동일: 아군 OC 프리팹에 "EnemyBuildingController"라는 이름이 붙어 있으면 헷갈리기 때문(doc/0452).

## 구현

```csharp
public class AllyBuildingController : EnemyBuildingController
{
}
```

## 연관 컴포넌트

- **EnemyBuildingController**: 상속 원본 — 체력/선택/미니맵 마커/이름·설명 조회 전부
