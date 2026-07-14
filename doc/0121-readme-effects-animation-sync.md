# 0121 - README 이펙트/애니메이션 스크립트 반영 동기화 (제안)

**날짜:** 2026-07-14

## 요청 내용

> 현재 코드를 분석하고 Readme파일 갱신해줘 새로 추가된 스크립트도 반영해줘

## 조사 내용

- `Assets/Scripts` 전체를 훑어본 결과 README에 아직 반영 안 된 새 스크립트/폴더 발견:
  - `Assets/Scripts/Effects/`(신규 폴더): `EffectPlayer`(정적 헬퍼), `HitEffectSet`(직렬화 클래스), `UnitEffects`, `BuildingEffects`, `ConstructionEffects`, `TrailRotationFollower` — doc/0105(설계) ~ doc/0117(건설 파괴 이펙트)에 걸쳐 구현된 공격/이동/피격/사망/이착륙/건설 이펙트 시스템.
  - `Assets/Scripts/Animation/`(신규 폴더): `HoverBob`(doc/0119, 공중유닛·리프트 건물 DOTween 부유), `VehicleShake`(doc/0120, 지상 차량 이동 중 DOTween 흔들림).
  - `Assets/Plugins/Demigiant/DOTween`: 신규 서드파티 트위닝 라이브러리 (기술 스택에 미반영).
  - `UserControl.cs`에 마우스 커서 상태 전환 로직 추가됨(doc/0102/0103, 새 스크립트는 아니고 기존 스크립트 확장) — 커서 텍스처 필드 4개 + `UpdateCursor()`.
  - `UserControl.cs`에 ESC로 대기 중인 명령(공격/이동/순찰/랠리/건물이동) 취소 로직 추가됨(doc/0114).
  - `ResourceManager.cs`의 인구수 한도 초과분 보존 버그 수정(doc/0104, `rawMaxPopulation`) — "해결된 이슈"에 미반영.
  - doc/0093~0101(아웃라인 셰이더 실험)은 doc/0101에서 세션 전체가 되돌려져 프로젝트에 실제로 남아있지 않음 — README에 반영하지 않음(현재 코드베이스와 무관).
- 기존 `doc/` 최신 번호는 0120 → 이 문서가 0121.
- [[confirm-before-implementing-rule]]에 따라 실제 `README.md`는 아직 수정하지 않았고, 아래 변경안만 정리함.

## 계획한 코드(문서) 변경

### 1. 기술 스택 표에 DOTween 행 추가

**기존 코드** (`README.md` 5~14번째 줄 표 마지막 행):
```
| 그래픽 | URP Volume 포스트프로세싱(Bloom/Color Adjustments, Tonemapping은 현재 None), Screen Space Ambient Occlusion(SSAO), 오버레이 카메라 기반 레이어 분리(프리뷰/포인터 제외) |
```

**변경 코드:**
```
| 그래픽 | URP Volume 포스트프로세싱(Bloom/Color Adjustments, Tonemapping은 현재 None), Screen Space Ambient Occlusion(SSAO), 오버레이 카메라 기반 레이어 분리(프리뷰/포인터 제외) |
| 애니메이션/트윈 | DOTween (Demigiant, `Assets/Plugins/Demigiant`) — 이펙트/모션 트위닝(호버링, 셰이크 등) |
```

### 2. 프로젝트 구조 트리에 `Animation/`, `Effects/` 폴더 추가

**기존 코드** (18~40번째 줄):
```
Assets/
├─ Scripts/
│  ├─ Building/        # 건물 컨트롤러, 건설 중 건물 기반(BaseStructure)
│  ├─ BuildSystem/      # 건물 배치 시스템 (그리드, 미리보기, 입력)
│  ├─ Camera/           # RTS 카메라/미니맵 이동·조작
│  ├─ Enemy/            # 적 유닛 컨트롤러 (마커/스탯 데이터만, AI 로직은 미구현)
│  ├─ Resource/         # 자원 노드 및 자원 관리 (`ResourceController.cs`는 미사용 빈 스텁)
│  ├─ ScriptableObject/ # 유닛/건물 데이터 정의(SO)
│  ├─ System/           # RTS 유닛 통합 컨트롤 시스템
│  ├─ UI/               # 생산 슬롯, 인게임 UI 컨트롤러, 툴팁
│  ├─ Unit/             # 유닛 컨트롤러, 공격 범위, 체력 관리
│  ├─ UnitSpawner/      # 유닛 생산/스폰
│  └─ UserControl/      # 유닛 선택 및 명령 입력 처리
```

**변경 코드:**
```
Assets/
├─ Scripts/
│  ├─ Animation/        # 공중유닛/리프트 건물 호버링(HoverBob), 지상 차량 이동 셰이크(VehicleShake) - DOTween 기반
│  ├─ Building/        # 건물 컨트롤러, 건설 중 건물 기반(BaseStructure)
│  ├─ BuildSystem/      # 건물 배치 시스템 (그리드, 미리보기, 입력)
│  ├─ Camera/           # RTS 카메라/미니맵 이동·조작
│  ├─ Effects/          # 공격/이동/피격/사망/건물 이착륙/건설 이펙트 재생 시스템(EffectPlayer 등)
│  ├─ Enemy/            # 적 유닛 컨트롤러 (마커/스탯 데이터만, AI 로직은 미구현)
│  ├─ Resource/         # 자원 노드 및 자원 관리 (`ResourceController.cs`는 미사용 빈 스텁)
│  ├─ ScriptableObject/ # 유닛/건물 데이터 정의(SO)
│  ├─ System/           # RTS 유닛 통합 컨트롤 시스템
│  ├─ UI/               # 생산 슬롯, 인게임 UI 컨트롤러, 툴팁
│  ├─ Unit/             # 유닛 컨트롤러, 공격 범위, 체력 관리
│  ├─ UnitSpawner/      # 유닛 생산/스폰
│  └─ UserControl/      # 유닛 선택 및 명령 입력 처리, 마우스 커서 상태 전환
```

### 3. 핵심 스크립트 표에 8개 행 추가 + `UserControl` 설명 보강

**기존 코드** (48~72번째 줄 표에서 관련 부분):
```
| `UserControl` | 마우스/키보드 입력을 해석해 선택·명령을 `RTSUnitController`에 전달 | [doc](Docs/UserControl.md) |
...
| `HealthBarBillboard` | 체력바 UI가 카메라의 X(피치) 각도만 따라 회전(Y/Z 고정)하도록 하는 빌보드 컴포넌트 | [doc](Docs/HealthBarBillboard.md) |
```

**변경 코드:**
```
| `UserControl` | 마우스/키보드 입력을 해석해 선택·명령을 `RTSUnitController`에 전달, 상태별(기본/선택/이동/공격) 마우스 커서 아이콘 전환, ESC로 대기 명령 취소 | [doc](Docs/UserControl.md) |
...
| `HealthBarBillboard` | 체력바 UI가 카메라의 X(피치) 각도만 따라 회전(Y/Z 고정)하도록 하는 빌보드 컴포넌트 | [doc](Docs/HealthBarBillboard.md) |
| `EffectPlayer` | 이펙트 프리팹(파티클/사운드) 스폰·자동 파괴 공용 정적 헬퍼 — 단발/다중지점/지속형 재생 지원 | [doc](doc/0105-effect-system-integration-design.md) |
| `HitEffectSet` | 공격 타입(총기/폭발/레이저/화염)별 피격 이펙트 프리팹 묶음(직렬화 클래스) | [doc](doc/0108-hit-effect-attack-type-variants.md) |
| `UnitEffects` | 유닛의 공격(총구)/이동(트레일)/피격/사망 이펙트 재생 전담 컴포넌트 | [doc](doc/0105-effect-system-integration-design.md) |
| `BuildingEffects` | 건물의 이착륙/피격/파괴 이펙트 재생 전담 컴포넌트 | [doc](doc/0116-building-destroy-effect.md) |
| `ConstructionEffects` | `BaseStructure`의 건설 중 지속/완공/피격/파괴 이펙트 재생 전담 컴포넌트 | [doc](doc/0117-construction-destroy-effect.md) |
| `TrailRotationFollower` | 지속형 이펙트가 부착 지점을 부모-자식으로 즉시 따라가지 않고, 위치는 매 프레임 추적하되 회전만 Slerp로 서서히 따라가게 하는 컴포넌트(급회전 중 축소 포함) | [doc](doc/0118-move-trail-smooth-rotation-follow-design.md) |
| `HoverBob` | 공중 유닛/리프트 중인 건물의 비주얼 자식 오브젝트를 DOTween으로 둥실거리게 하는 컴포넌트 | [doc](doc/0119-dotween-hover-bob-design.md) |
| `VehicleShake` | 지상 차량 유닛이 이동 중일 때 DOTween으로 흔들림을 재현하는 컴포넌트 | [doc](doc/0120-vehicle-shake-and-animation-folder.md) |
```

> 새로 추가된 8개 스크립트는 아직 `Docs/` 폴더에 필드/메소드 상세 문서가 없어서, 위 표에서는 대신 관련 `doc/` 세션 로그(설계·구현 문서)로 링크했습니다. 스크립트별 상세 문서가 필요하면 별도로 `Docs/EffectPlayer.md` 등을 요청해주세요.

### 4. "주요 기능" 절에 이펙트/모션/커서/ESC 취소 항목 추가

**기존 코드** (76~87번째 줄 중 관련 부분):
```
- **키보드 단축키**: 선택 상태(유닛/일꾼/건설모드/생산 패널/공중 건물)별 버튼에 단축키 배정 — 버튼이 자기 단축키를 직접 감지해 클릭과 동일하게 동작 + 눌림 시각 효과, 현재 패널에 없는 버튼의 단축키는 자동으로 비활성
- **UI**: 패널 기반 커맨드 UI, Info Panel(공격력/방어력 호버 툴팁), Squad Panel(최대 60마리 페이지네이션), 생산 대기열 UI, 미니맵
- **그래픽/비주얼**: URP Volume 포스트프로세싱(Bloom, Color Adjustments) + SSAO 적용, 빌드 프리뷰/셀 커서/이동·공격 명령 포인터는 전용 레이어 + 오버레이 카메라로 포스트프로세싱 미적용 처리, 3rd-party 유닛/건물 모델링 에셋(Canopus-III Sci-Fi Desert Units, Yoge Stylized Nature, Animated Sun Skybox) 임포트 및 Built-in → URP 머티리얼 변환 완료(게임플레이 프리팹에 실제 모델 적용은 아직 로드맵)
```

**변경 코드:**
```
- **키보드 단축키**: 선택 상태(유닛/일꾼/건설모드/생산 패널/공중 건물)별 버튼에 단축키 배정 — 버튼이 자기 단축키를 직접 감지해 클릭과 동일하게 동작 + 눌림 시각 효과, 현재 패널에 없는 버튼의 단축키는 자동으로 비활성
- **명령 취소**: 공격/이동/순찰/랠리/건물이동 등 대기 중인 명령 모드를 ESC로 즉시 취소(포인터 마커도 함께 사라짐)
- **마우스 커서**: 기본 화살표 외에 선택 가능 대상(유닛/적/건물/광물/가스) 호버 시 선택 커서, 공격/이동 대기 상태(A/M/P/랠리/건물이동)에서 각각 공격/이동 커서로 전환(`UserControl`), UI 위에서는 항상 OS 기본 커서로 복귀
- **이펙트 시스템**: `EffectPlayer` 공용 헬퍼로 공격(총구)/이동(트레일)/피격(공격 타입별 4종: 총기·폭발·레이저·화염)/사망/건물 이착륙/건설 진행·완공·파괴 이펙트를 재생 — 유닛/건물 프리팹에 붙는 `UnitEffects`/`BuildingEffects`/`ConstructionEffects`가 각각 전담, 스폰 위치는 `List<Transform>`으로 다중 지점 지정 가능(비워두면 오브젝트 자신 위치 하나로 폴백), 피격 이펙트는 콜라이더 표면의 공격자 쪽 지점에서 방향까지 계산해 재생
- **모션 연출**: 이동 트레일은 `TrailRotationFollower`로 위치는 매 프레임 추적하되 회전만 Slerp로 서서히 따라가 급회전 시 부자연스럽게 홱 도는 문제 방지(급회전 중엔 크기/방출량도 축소), 공중 유닛/리프트 중인 건물은 `HoverBob`으로 DOTween 기반 부유(호버링) 애니메이션, 지상 차량 유닛은 이동 중 `VehicleShake`로 DOTween 기반 흔들림 연출 — 둘 다 루트가 아닌 비주얼 자식 오브젝트에 부착해 이동 로직(루트 트랜스폼 직접 갱신)과 충돌하지 않음
- **UI**: 패널 기반 커맨드 UI, Info Panel(공격력/방어력 호버 툴팁), Squad Panel(최대 60마리 페이지네이션), 생산 대기열 UI, 미니맵
- **그래픽/비주얼**: URP Volume 포스트프로세싱(Bloom, Color Adjustments) + SSAO 적용, 빌드 프리뷰/셀 커서/이동·공격 명령 포인터는 전용 레이어 + 오버레이 카메라로 포스트프로세싱 미적용 처리, 3rd-party 유닛/건물 모델링 에셋(Canopus-III Sci-Fi Desert Units, Yoge Stylized Nature, Animated Sun Skybox) 임포트 및 Built-in → URP 머티리얼 변환 완료(게임플레이 프리팹에 실제 모델 적용은 아직 로드맵)
```

### 5. "구현 완료 기능" 체크리스트 — 신규 섹션 추가 + 자원 섹션 항목 추가

**기존 코드** (137~148번째 줄, "자원 / 인구수" 섹션 끝부분과 "그래픽 / 비주얼" 섹션 사이):
```
### 자원 / 인구수
- [x] `ResourceManager`로 광물/가스/인구수 중앙 관리, 변경 이벤트로 상단 UI 자동 갱신
- [x] 유닛 생산 시 `ResourceManager.TrySpend`로 자원·인구수 소모, 대기열 가득 참/자원 부족/인구수 부족 시 콘솔 로그로 사유 표시
- [x] 건물 배치 시 자원 소모 연결 — `PlacementSystem.PlaceStructure()`가 `TryConstructBuilding`으로 자원 확인 후 차감
- [x] 인구수 한도 200 상한(`ResourceManager.maxPopulationCap`)
- [x] 유닛 사망 시 인구수 반환(`RTSUnitController.ReleaseUnitPopulation`) — 생산 취소(광물/가스+인구수 전액 환불)와 별개로, 이미 생산된 유닛이 죽을 때는 인구수만 반환

### 그래픽 / 비주얼
```

**변경 코드:**
```
### 자원 / 인구수
- [x] `ResourceManager`로 광물/가스/인구수 중앙 관리, 변경 이벤트로 상단 UI 자동 갱신
- [x] 유닛 생산 시 `ResourceManager.TrySpend`로 자원·인구수 소모, 대기열 가득 참/자원 부족/인구수 부족 시 콘솔 로그로 사유 표시
- [x] 건물 배치 시 자원 소모 연결 — `PlacementSystem.PlaceStructure()`가 `TryConstructBuilding`으로 자원 확인 후 차감
- [x] 인구수 한도 200 상한(`ResourceManager.maxPopulationCap`)
- [x] 유닛 사망 시 인구수 반환(`RTSUnitController.ReleaseUnitPopulation`) — 생산 취소(광물/가스+인구수 전액 환불)와 별개로, 이미 생산된 유닛이 죽을 때는 인구수만 반환
- [x] 인구수 한도 초과분 누적치 보존(`ResourceManager.rawMaxPopulation`) — 캡(200)보다 많이 지어도 내부 누적치는 그대로 유지, 일부가 파괴돼도 남은 누적치가 캡을 넘으면 표시 한도는 캡 값 그대로 유지

### 이펙트 / 모션 연출
- [x] 공격(총구) / 이동(트레일) / 피격(공격 타입별 4종: 총기·폭발·레이저·화염) / 사망 이펙트 — `UnitEffects`, 공용 헬퍼 `EffectPlayer`
- [x] 건물 이착륙 이펙트 — `BuildingEffects`
- [x] 건설 진행 중 지속 이펙트, 완공 순간 이펙트 — `ConstructionEffects`
- [x] 건물/건설중 파운데이션(`BaseStructure`) 피격·파괴(전투로 파괴 시에만, 취소 버튼과는 구분) 이펙트 — `BuildingEffects`/`ConstructionEffects`
- [x] 이동 트레일의 부자연스러운 급회전 보정 — `TrailRotationFollower`(위치는 매 프레임 추적, 회전은 Slerp로 서서히 추적 + 급회전 중 크기/방출량 축소)
- [x] 공중 유닛/리프트 중인 건물 호버링(둥실거림) 애니메이션 — `HoverBob`(DOTween)
- [x] 지상 차량 유닛 이동 중 흔들림 애니메이션 — `VehicleShake`(DOTween)
- [x] 마우스 커서 상태 전환(기본/선택/이동/공격) — `UserControl`
- [x] ESC로 대기 중인 명령(공격/이동/순찰/랠리/건물이동) 취소 — `UserControl`

### 그래픽 / 비주얼
```

### 6. 로드맵에서 이펙트 항목 제거(구현 완료로 이동), 남은 사망 연출 옵션은 유지

**기존 코드** (163~173번째 줄):
```
- [ ] 유닛/건물 모델링 실제 적용 — 3rd-party 에셋(Canopus-III, Yoge) 임포트 및 URP 머티리얼 변환은 완료됐지만, 게임플레이 유닛/건물 프리팹(`prefabs/NTA/`)은 아직 기본 프리미티브 메시(캡슐/큐브/구)를 그대로 사용 중 — 실제 모델 교체는 남은 작업
- [ ] 공격 이펙트(VFX) 추가, 이동/건설/이륙/착륙/사망 이펙트
- [ ] 전장의 안개(Fog of War) 구현 — 설계 + 제안 코드는 [`doc/0069`](doc/0069-fog-of-war-design.md)에 정리돼 있으나 실제 `Assets/Scripts` 반영은 아직 승인 대기 상태
- [ ] 맵(스테이지) 제작
- [ ] Enemy AI 구현 — `EnemyController`는 현재 마커/아이콘/공격력·방어력 데이터만 갖고 있고, 실제로 공격하거나 이동하는 AI 로직은 없음(플레이어 유닛이 일방적으로 공격하는 대상)
- [ ] 유닛/건물 사운드, 사운드 매니저
- [ ] 메인 화면, 설정창 UI
- [ ] UI 버튼 하단 이미지 등 비주얼 개선
- [ ] `AttackRange`의 자동 사거리 탐지가 `BaseStructure`(건설 중인 건물)를 대상으로 삼는 경로 — 현재는 A 모드 강제 공격(오인사격 포함)으로만 공격 가능하고, 자동 교전 대상에는 포함되지 않음
```

**변경 코드:**
```
- [ ] 유닛/건물 모델링 실제 적용 — 3rd-party 에셋(Canopus-III, Yoge) 임포트 및 URP 머티리얼 변환은 완료됐지만, 게임플레이 유닛/건물 프리팹(`prefabs/NTA/`)은 아직 기본 프리미티브 메시(캡슐/큐브/구)를 그대로 사용 중 — 실제 모델 교체는 남은 작업
- [ ] 사망 시 래그돌/사망 애니메이션 — 현재는 사망 즉시 `Destroy(gameObject)` + 파티클 스폰만 지원(옵션 A), 오브젝트를 유지한 채 애니메이션 재생 후 지연 파괴하는 구조(옵션 B, doc/0105 3.5절)는 미구현
- [ ] 전장의 안개(Fog of War) 구현 — 설계 + 제안 코드는 [`doc/0069`](doc/0069-fog-of-war-design.md)에 정리돼 있으나 실제 `Assets/Scripts` 반영은 아직 승인 대기 상태
- [ ] 맵(스테이지) 제작
- [ ] Enemy AI 구현 — `EnemyController`는 현재 마커/아이콘/공격력·방어력 데이터만 갖고 있고, 실제로 공격하거나 이동하는 AI 로직은 없음(플레이어 유닛이 일방적으로 공격하는 대상)
- [ ] 유닛/건물 사운드, 사운드 매니저
- [ ] 메인 화면, 설정창 UI
- [ ] UI 버튼 하단 이미지 등 비주얼 개선
- [ ] `AttackRange`의 자동 사거리 탐지가 `BaseStructure`(건설 중인 건물)를 대상으로 삼는 경로 — 현재는 A 모드 강제 공격(오인사격 포함)으로만 공격 가능하고, 자동 교전 대상에는 포함되지 않음
```

### 7. "해결된 이슈" 절에 신규 항목 추가

**기존 코드** (269번째 줄 바로 앞, 마지막 항목 뒤):
```
- **Lab 체력바가 실제 체력과 무관하게 움직임**: `HealthManager.healthSlider` 연결 누락 + 체력바 슬라이더가 마우스로 드래그 가능한 상태였던 두 문제가 겹친 것 — 연결 추가 + 슬라이더 `Interactable` 비활성화로 해결.

전체 세션별 변경 이력(코드 변경 전/후 diff 포함)은 [`doc/`](doc) 폴더에 번호순으로 정리돼 있습니다.
```

**변경 코드:**
```
- **Lab 체력바가 실제 체력과 무관하게 움직임**: `HealthManager.healthSlider` 연결 누락 + 체력바 슬라이더가 마우스로 드래그 가능한 상태였던 두 문제가 겹친 것 — 연결 추가 + 슬라이더 `Interactable` 비활성화로 해결.
- **인구수 한도(200) 초과분이 보급고 파괴 시 통째로 사라짐**: 캡이 이미 적용된 값을 필드에 그대로 저장해 "캡을 넘겨 지었다"는 정보 자체가 소실되던 문제 — 캡 미적용 누적치(`rawMaxPopulation`)를 별도로 유지하고, 노출/판정 시점에만 캡을 씌우도록 수정.
- **지속형 파티클(이동 트레일 등)이 반복 재생 도중 여러 번 겹쳐 재생됨**: looping이 꺼진 파티클을 지속형(부모 부착)으로 스폰하면 자기 duration만큼만 방출하고 멈춰버리던 문제 — 지속형 스폰 시 loop 강제 on, 발사 후 잊기 스폰 시 loop 강제 off로 용도별 분리.
- **이동 트레일이 급회전 시 부자연스럽게 홱 돌거나, 이동 중간에 멈춤**: 부모-자식으로 직접 붙이면 회전이 매 프레임 즉시 동기화되던 문제 — `TrailRotationFollower`로 위치는 추적, 회전만 Slerp로 분리해 해결(관련 세부 버그는 doc/0112~0113 참고).

전체 세션별 변경 이력(코드 변경 전/후 diff 포함)은 [`doc/`](doc) 폴더에 번호순으로 정리돼 있습니다.
```

## 영향받는 파일 (예정)

- `README.md` (기술 스택 표, 프로젝트 구조 트리, 핵심 스크립트 표, 주요 기능, 구현 완료 기능, 로드맵, 해결된 이슈 — 7개 절)

## 요약

새로 추가된 `Assets/Scripts/Effects/`(6개 스크립트), `Assets/Scripts/Animation/`(2개 스크립트), DOTween 플러그인, `UserControl`의 커서/ESC 취소 확장, `ResourceManager` 인구수 캡 버그 수정을 README에 반영하는 변경안. 아직 `README.md`에는 적용하지 않음 — 사용자 확인 후 반영 예정.
