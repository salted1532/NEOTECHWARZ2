## 2026-07-22

### 요청 내용

유닛/건물 수치 정리 문서를 사용자가 지정한 양식(유닛명/유닛ID/생산가능티어/공격범위/공격방식/장갑/크기/가격&인구수/생산시간/체력/공격력/사거리/공격속도/단축키)으로 새로 작성해서 `Docs/` 폴더에 md로 저장해달라는 요청. `UnitDataSO`/`BuildingDataSO`의 실제 값을 확인해서 작성. 데미지 시스템 설계 원문(장갑/크기/공격방식 배율표, 유닛별 공격방식·고유보너스)도 다시 첨부하며 참고해서 추가할 부분 있으면 추가해달라고 함.

순수 문서화 요청(코드 변경 없음)이라 `confirm-before-implementing` 게이트 없이 바로 진행함.

### 조사 내용

- `New Unit Data SO.asset`/`New Building Data SO.asset`을 다시 읽어 최신 값 확인. 유닛 ID는 이전 세션 사이 사용자가 1~9로 재정렬 완료된 상태(Sharpshooter=4, SkyLancer=7).
- `공격 방식`/`고유 보너스`(`bonusVersusArmorType`/`Percent`)/`공격 범위`(`isAirUnit`)는 `UnitDataSO`에 필드가 없어(`doc/0203`~`0205`에서 동기화 대상에서 제외된 필드들) 9개 프리팹에서 직접 확인:
  - 사용자가 이전 세션 이후 직접 고친 값들 확인됨: Worker Drone `attackType`이 0(소총)→3(화염)으로 수정되어 이제 설계와 일치, Sharpshooter `bonusVersusArmorPercent`가 0→80으로 설정, SkyLancer `bonusVersusArmorPercent`가 0→50으로 설정. Sharpshooter의 공격 방식도 소총(0)으로 맞춰져 있어(이전 세션에서 발견했던 "레이저 vs 소총" 기획 문서 자체의 모순은, 이번에 사용자가 소총 쪽으로 정리한 것으로 보임) 그대로 반영.
  - 여전히 안 고쳐진 것: SkyLancer의 `isAirUnit`이 0(지상)으로 남아있음 — 문서에 "참고/미구현 사항"으로 명시.
- `unitID` 필드도 전부 확인 — Sharpshooter=4, SkyLancer=7로 SO ID와 일치(사용자가 완료함).
- 건물 6종의 체력(`HealthManager.maxHealth`, 프리팹 기준)은 이전 세션과 동일: 1500/500/1000/1250/1300/850.
- 사용자가 준 데미지 시스템 설계 원문(장갑/크기 분류, 공격방식별 배율)은 이미 `doc/0201`에서 구현되어 `DamageMultiplierTable.asset`에 반영되어 있음(값도 정확히 일치) — 문서 상단에 참고용으로 요약해 넣음.
- 설계에는 있지만 코드에 없는 것들(지상/공중 타겟팅 제약, Sharpshooter의 "연구소 필요" 조건, IFV 레인저의 수송 기능, 지원기 유닛)을 "참고/미구현 사항" 섹션으로 정리.

### 코드 변경

없음.

### 결과물

- `Docs/UnitAndBuildingStats.md` (신규) — 사용자 지정 양식대로 유닛 9종 + 건물 6종 전체 수치, 데미지 시스템 요약, 미구현 사항 목록.

### 변경된 파일

- `Docs/UnitAndBuildingStats.md` (신규)
