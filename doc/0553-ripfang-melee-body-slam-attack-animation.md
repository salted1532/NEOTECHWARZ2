# 0553 - Ripfang 근접공격 몸통박치기 DOTween 애니메이션

## 날짜
2026-08-13

## 요청 내용
"Ripfang이 근접공격 할시 몸을 대상에게 몸통박치기 하는것 처럼 하는 Dotween 애니메이션을 만들어줘 내가
몸 모델링에다가 집어넣어줄게"

## 설계 - 기존 `TurretController.FireRecoil()` 패턴 재사용
`TurretController.cs`가 이미 "공격 판정 순간 DOTween으로 파츠를 움직였다 되돌리는" 동일한 문제를
풀어둔 선례라(포신이 뒤로 빠졌다 복귀) 그대로 재사용 - 방향만 반대(뒤로 빠지는 대신 앞으로 튀어나감)로
새 컴포넌트를 만듦. `EnemyUnitController.Attack()`이 `laserBeamAttack`/`turretController`처럼 "붙어있으면
쓰고 없으면 무시"하는 옵셔널 컴포넌트로 취급해서, 근접 유닛(Ripfang)에만 붙이면 되고 다른 유닛엔
영향 없음.

`EnemyUnitController.Attack()`은 데미지를 넣기 직전 `RotateYOnly(end)`로 몸체를 이미 대상 쪽으로
돌려놓으므로(`EnemyUnitController.cs:421`), 애니메이션은 "로컬 정면(+Z)으로 짧게 튀어나갔다 복귀"만
하면 그대로 몸통박치기로 보임 - 대상 위치를 따로 계산할 필요 없음.

## 신규 컴포넌트: `MeleeBodySlamAttack` (`Assets/Scripts/Animation/MeleeBodySlamAttack.cs`)
```csharp
public class MeleeBodySlamAttack : MonoBehaviour
{
    [SerializeField] private Transform bodyPart; // 비우면 이 오브젝트 자신
    [SerializeField] private float lungeDistance = 0.6f;
    [SerializeField] private float lungeDuration = 0.08f;
    [SerializeField] private float lungeReturnDuration = 0.15f;
    [SerializeField] private Ease lungeEase = Ease.OutQuad;
    [SerializeField] private Ease lungeReturnEase = Ease.OutBack;

    public void Slam() { /* DOLocalMove로 정면으로 튀어나갔다 OnComplete에서 복귀 - FireRecoil과 동일 구조 */ }
}
```

## `EnemyUnitController.cs` 연동
```csharp
private MeleeBodySlamAttack meleeBodySlamAttack; // 근접 유닛(Ripfang 등) 몸 파츠에만 붙어있는 옵셔널 컴포넌트
```
```csharp
meleeBodySlamAttack = GetComponentInChildren<MeleeBodySlamAttack>(); // 몸 모델(자식 오브젝트)에 붙는 컴포넌트라 turretController와 동일하게 자식까지 탐색
```
```csharp
laserBeamAttack?.Fire(target.transform);
turretController?.FireRecoil();
meleeBodySlamAttack?.Slam(); // 신규 - 근접 유닛만 붙어있는 옵셔널 컴포넌트
```

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개(경고는 다른 세션이 추가한 `AllyAIDirector.cs` 때문에 39→40개로
늘었을 뿐, 이번 변경과 무관 - 새로 추가된 경고 없음).

## 남은 작업 (사용자가 직접)
Ripfang 프리팹의 몸 모델 오브젝트(자식)에 `MeleeBodySlamAttack` 컴포넌트를 붙이기만 하면 됨 - 코드
쪽은 그 컴포넌트가 있으면 자동으로 찾아서 공격할 때마다 `Slam()`을 호출함. `bodyPart`를 비워두면
컴포넌트를 붙인 오브젝트 자신이 튀어나감(보통 그걸로 충분), 몸 전체가 아니라 특정 파츠만 움직이고
싶으면 `bodyPart`에 그 파츠의 Transform을 지정.

## 영향받는 파일
- 신규: `Assets\Scripts\Animation\MeleeBodySlamAttack.cs`
- 변경: `Assets\Scripts\FogOfWar\Enemy\EnemyUnitController.cs`
