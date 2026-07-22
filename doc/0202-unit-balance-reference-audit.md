## 2026-07-22

### 요청 내용

사용자가 유닛 밸런싱 기획(메인기지/병영/공장/공항 유닛별 공격범위/방식/장갑/크기/가격/생산시간/체력/공격력/사거리/단축키, 데미지 시스템 배율표, 지원기 아이디어 등)을 상세 수치로 다시 전달하며 다음을 요청:
1. 현재 `UnitDataSO`/프리팹에 실제로 적힌 내용을 전부 확인
2. 각 유닛의 프리팹도 확인해서 어택레인지/어택데미지가 실제로 게임에서 작동하는 값을 적을 것
3. `UnitDataSO`의 attackRange/attackDamage가 실제로 게임에 쓰이는지 확인
4. 위 형식에 맞춰 유닛/건물 상세 정보 문서를 새 md 파일로 작성

코드 변경 요청이 아니라 조사+문서화 요청이라 `confirm-before-implementing` 게이트 없이 바로 진행함(memory: "Pure Q&A/research requests (no code change) are unaffected").

### 조사 내용

- `UnitData.hp`/`.attackDamge`/`.attackRange`를 전수 grep한 결과 **선언부와 `.asset` 데이터 외에는 프로젝트 어디에서도 읽히지 않음** — 완전히 죽은 데이터. 실제 체력/공격력/사거리는 스폰되는 프리팹의 `HealthManager.maxHealth`/`UnitController.attackDamage`/자식 `AttackRange.UnitRange`에서 나옴.
- 이전 세션(`doc/0201`) 이후 **사용자가 Unity 에디터에서 직접 `New Unit Data SO.asset`을 편집**해 Sharpshooter(ID8)/SkyLancer(ID9) 항목과 그에 대응하는 프리팹(`Sharpshooter.prefab`, `SkyLancer.prefab`)을 추가했고, Ranger IFV/Pulasr Tank의 SO상 armorType/sizeType도 변경했음을 발견.
- 새 프리팹 2종을 까보니 **Sharpshooter.prefab은 `unitID: 2`(Assault Trooper와 동일), SkyLancer.prefab은 `unitID: 4`(Ranger IFV와 동일)** 로, 원본 유닛의 미완성 복제본 상태(고유보너스 `bonusVersusArmorPercent`도 0이라 미적용, SkyLancer는 `isAirUnit`도 0)임을 확인.
- Ranger IFV/Pulasr Tank는 **SO 쪽 armorType/sizeType만 바뀌고 프리팹 쪽은 그대로**라, 실제 전투 계산(프리팹 값을 읽음)에는 반영되지 않은 상태.
- 이번에 주신 상세 수치(Worker Drone/Assault Trooper/Scout Drone/Sharpshooter)와 실제 프리팹 값을 비교한 결과, Worker Drone(공격방식·체력·사거리), Assault Trooper(공격력·사거리), Scout Drone(체력·공격력·사거리 — 공격력은 4배 차이)에서 불일치 발견.
- 건물(`BuildingDataSO`)의 비용/크기/건설시간/최대인구는 실제로 `PlacementSystem`/`BaseStructure`가 그대로 읽어 쓰는 authoritative 데이터임을 재확인(유닛과 반대). 건물 체력은 `BuildingData`에 필드 자체가 없고 완공 프리팹의 `HealthManager.maxHealth`가 실값.

### 코드 변경

없음 (순수 조사 + 문서화 요청). 위에서 발견한 불일치/버그는 사용자 확인 없이 임의로 고치지 않고 문서에만 기록함.

### 결과물

- `Docs/UnitBalanceReference.md` (신규) — 데미지 배율표, 유닛 9종(정상 7 + 미완성 복제본 2)과 건물 6종의 실측 스탯 전체, 설계 값과의 불일치 목록, "어느 필드를 고치면 실제로 게임에 반영되는지" 표.

### 요약/남은 작업

- Sharpshooter/SkyLancer를 실제로 구분되는 유닛으로 만들려면 각 프리팹의 `unitID`(8/9로), `bonusVersusArmorPercent`(80/50으로), SkyLancer의 `isAirUnit`(1로) 등을 직접 고쳐야 함 — 사용자 확인 후 진행 예정.
- Worker Drone/Assault Trooper/Scout Drone의 체력·공격력·사거리·공격방식이 이번에 주신 설계 수치와 다른 부분은 어느 쪽(프리팹 실값 vs 새 설계 수치)을 기준으로 맞출지 결정 필요.
- Ranger IFV/Pulasr Tank의 armorType/sizeType은 SO를 프리팹에 맞출지, 프리팹을 SO(최근 편집값)에 맞출지 결정 필요.

### 변경된 파일

- `Docs/UnitBalanceReference.md` (신규)
