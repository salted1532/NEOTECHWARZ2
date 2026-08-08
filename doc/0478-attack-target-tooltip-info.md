# 0478 - 공격력 툴팁에 공격 가능 위치(지상/공중) 정보 추가

## 요청 내용
"유닛의 공격력 정보에다가 해당 유닛이 공격가능한 위치에 대한 정보도 추가해줘 지상/공중 이런식으로
영어로 추가해줘"

## 변경 내용
Info Panel의 공격력 아이콘(`attackDamageImage`) 호버 툴팁(`UIController.SetupInfoStatHoverTooltips`)에
"Attack Target : Ground/Air" 줄을 추가함. 이미 존재하던 `canAttackGround`/`canAttackAir`
(UnitData/BuildingData 데이터를 `UnitController`/`EnemyUnitController`/`AllyController`가 그대로
캐싱해둔 값)를 그대로 재사용 — 새 데이터 추가 없이 표시 경로만 뚫으면 됐음.

### 변경 파일
- `UnitController.cs`/`EnemyUnitController.cs`/`AllyController.cs`: `GetCanAttackGround()`/
  `GetCanAttackAir()` getter 추가 (기존 `GetAttackType()` 등과 동일한 패턴)
- `UIController.cs`: `infoCanAttackGround`/`infoCanAttackAir` 필드 추가, `ShowInfoPanel(...)`(공격
  스탯 있는 9-인자 오버로드)에 `bool canAttackGround = true, bool canAttackAir = true` 매개변수 추가,
  공격력 툴팁 텍스트에 `GetAttackTargetText()`(Ground/Air/Ground+Air 조합 문자열) 추가
- `RTSUnitController.cs`: 유닛/적유닛/아군유닛 3개 Info Panel 호출부에 `.GetCanAttackGround()`,
  `.GetCanAttackAir()` 인자 추가

건물(3-인자 `ShowInfoPanel` 오버로드)은 공격 스탯 자체가 없어 대상 밖.

컴파일 확인 완료(에러 0).
