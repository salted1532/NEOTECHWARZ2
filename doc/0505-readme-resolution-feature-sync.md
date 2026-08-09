# 0505 - README 해상도 변경 기능 반영 제안

## 날짜
2026-08-09

## 요청 내용
"해상도 변경 기능 추가건 까지해서 Readme파일 갱신해줘"

커밋 `f816ff2 해상도 변경 기능 추가`(doc/0500~0504에서 설계/구현/버그수정된 내용)가 아직 README.md에
반영되어 있지 않아, 이번 커밋 내용을 README에 동기화해달라는 요청.

## 조사 내용
관련 커밋과 doc/0500~0504를 확인한 결과, 이번 기능은 크게 두 가지:

1. **해상도 변경 드롭다운** (`Assets/Scripts/UI/GraphicsSettingsPanel.cs`, 신규 스크립트)
   - `OptionPanel.prefab`의 "Screen Resolution" `TMP_Dropdown`에 연결
   - `Screen.resolutions`로 지원 해상도 조회 → 가로x세로 기준 중복 제거 → 높은 해상도부터 내림차순 정렬(doc/0504)
   - 선택 시 `Screen.SetResolution` 적용 + `PlayerPrefs`에 저장, 다음 씬 로드/재실행 시 복원
   - 씬 전환 후 에디터에서 640x480으로 되돌아가 보이던 버그 수정 — 에디터에서 `Screen.width/height`가
     아직 렌더 전 기본값을 반환하는 시점이 있어, 저장된 `PlayerPrefs` 값을 우선 사용하도록 수정(doc/0503)
   - `OptionPanel`은 `MainScene`과 `GameManager.prefab`(인게임)에 모두 있어 두 곳 다 자동 적용

2. **`MissionSelect` 씬 CanvasScaler 해상도 대응** (doc/0500)
   - 기존 `Constant Pixel Size`(800x600) → 메인씬/인게임과 동일한 `Scale With Screen Size`
     (기준 해상도 1920x1080, Match Width Or Height 0.5)로 통일
   - 해상도가 바뀌어도 UI가 좌측 상단에 몰리지 않도록 수정

README.md에서 이 내용이 들어갈 위치 4곳을 확인:
- "핵심 스크립트" 표 (신규 스크립트 `GraphicsSettingsPanel` 행 추가)
- "주요 기능" 목록 (그래픽/비주얼 항목 뒤에 해상도 변경 항목 추가)
- "구현 완료 기능 > UI" 체크리스트 (해상도 드롭다운 + MissionSelect Canvas 항목 추가)
- "해결된 이슈" 목록 (640x480 되돌아가는 버그 항목 추가)

## 코드 변경 (README.md, 제안)

### 1) 핵심 스크립트 표 — `SoundSettingsPanel`과 `MainMenuController` 사이에 삽입

**기존 코드**
```markdown
| `SoundSettingsPanel` | 볼륨 슬라이더/뮤트 토글 UI 로직 (SoundManager API 연결, 실제 Canvas 배치는 미완료) | [doc](Docs/SoundSettingsPanel.md) |
| `MainMenuController` | 메인 메뉴(MainScene) Play/Option/Exit 버튼 연결, 버튼 호버 시 커서 전환 | [doc](Docs/MainMenuController.md) |
```

**변경 코드**
```markdown
| `SoundSettingsPanel` | 볼륨 슬라이더/뮤트 토글 UI 로직 (SoundManager API 연결, 실제 Canvas 배치는 미완료) | [doc](Docs/SoundSettingsPanel.md) |
| `GraphicsSettingsPanel` | Option 패널의 해상도 드롭다운 — `Screen.resolutions`를 가로x세로 기준 중복 제거 후 높은 해상도부터 정렬해 채우고, 선택 시 `Screen.SetResolution` 적용 + `PlayerPrefs` 저장/복원(다음 씬 로드·재실행에도 유지) | [doc](doc/0501-optionpanel-screen-resolution-dropdown-proposal.md) |
| `MainMenuController` | 메인 메뉴(MainScene) Play/Option/Exit 버튼 연결, 버튼 호버 시 커서 전환 | [doc](Docs/MainMenuController.md) |
```

### 2) 주요 기능 — 그래픽/비주얼 항목 뒤에 삽입

**기존 코드**
```markdown
- **그래픽/비주얼**: URP Volume 포스트프로세싱(Bloom, Color Adjustments) + SSAO 적용, 빌드 프리뷰/셀 커서/이동·공격 명령 포인터는 전용 레이어 + 오버레이 카메라로 포스트프로세싱 미적용 처리, 3rd-party 유닛/건물 모델링 에셋(Canopus-III Sci-Fi Desert Units, Yoge Stylized Nature, Animated Sun Skybox) 임포트 및 Built-in → URP 머티리얼 변환 완료(게임플레이 프리팹 적용은 병영 건물 1개만 시작, 나머지는 로드맵)

> 스크립트별 상세 동작 방식은 위 표의 [`Docs/`](Docs) 링크를 참고하세요.
```

**변경 코드**
```markdown
- **그래픽/비주얼**: URP Volume 포스트프로세싱(Bloom, Color Adjustments) + SSAO 적용, 빌드 프리뷰/셀 커서/이동·공격 명령 포인터는 전용 레이어 + 오버레이 카메라로 포스트프로세싱 미적용 처리, 3rd-party 유닛/건물 모델링 에셋(Canopus-III Sci-Fi Desert Units, Yoge Stylized Nature, Animated Sun Skybox) 임포트 및 Built-in → URP 머티리얼 변환 완료(게임플레이 프리팹 적용은 병영 건물 1개만 시작, 나머지는 로드맵)
- **해상도 변경**: Option 패널의 "Screen Resolution" 드롭다운(`GraphicsSettingsPanel`)에서 `Screen.resolutions`로 조회한 지원 해상도를 중복 제거 후 높은 해상도부터 정렬해 표시, 선택 시 `Screen.SetResolution` 적용 + `PlayerPrefs`에 저장해 다음 씬 로드·재실행에도 유지 — `MainScene`과 `GameManager.prefab`을 통한 인게임 씬 모두에 자동 적용. `MissionSelect` 씬도 다른 씬과 동일한 `CanvasScaler`(Scale With Screen Size, 1920x1080 기준, Match 0.5)로 통일해 해상도 변경 시 UI가 한쪽에 몰리지 않음(doc/0500~0504)

> 스크립트별 상세 동작 방식은 위 표의 [`Docs/`](Docs) 링크를 참고하세요.
```

### 3) 구현 완료 기능 > UI — "유닛/건물 아이콘 이미지 개선" 뒤, "### 사운드" 앞에 삽입

**기존 코드**
```markdown
- [x] 유닛/건물 아이콘 이미지 개선

### 사운드
```

**변경 코드**
```markdown
- [x] 유닛/건물 아이콘 이미지 개선
- [x] 해상도 변경 — Option 패널 드롭다운(`GraphicsSettingsPanel`)에서 지원 해상도 목록을 높은 해상도부터 정렬해 선택, `Screen.SetResolution` 적용 및 `PlayerPrefs` 저장/복원(재실행·씬 전환 후에도 유지)
- [x] `MissionSelect` 씬 Canvas 해상도 대응 — 기존 Constant Pixel Size(800x600)를 메인씬/인게임과 동일한 Scale With Screen Size(1920x1080, Match 0.5)로 통일, 여러 해상도에서 UI가 한쪽에 몰리지 않게 수정

### 사운드
```

### 4) 해결된 이슈 — 마지막 항목 뒤에 삽입

**기존 코드**
```markdown
- **`TZ_Futuristic Panel Textures Lite`(15개 머티리얼), `LowPolyWater_Pack`(`IslandMat` + 커스텀 `WaterShaded` 수면 셰이더)도 마젠타로 깨짐**: 기존 Canopus/Yoge와 동일하게 Built-in RP 전용 셰이더였던 게 원인 — URP로 변환(`WaterShaded`는 죽은 코드였던 GrabPass도 함께 제거).

전체 세션별 변경 이력(코드 변경 전/후 diff 포함)은 [`doc/`](doc) 폴더에 번호순으로 정리돼 있습니다.
```

**변경 코드**
```markdown
- **`TZ_Futuristic Panel Textures Lite`(15개 머티리얼), `LowPolyWater_Pack`(`IslandMat` + 커스텀 `WaterShaded` 수면 셰이더)도 마젠타로 깨짐**: 기존 Canopus/Yoge와 동일하게 Built-in RP 전용 셰이더였던 게 원인 — URP로 변환(`WaterShaded`는 죽은 코드였던 GrabPass도 함께 제거).
- **해상도 드롭다운이 씬 전환 후 640x480으로 되돌아가 보임**: 에디터에서 씬 로드 직후 `Screen.width`/`height`가 아직 Game View 렌더 전 기본값(640x480)을 반환할 수 있는 게 원인 — 드롭다운 선택 인덱스 계산 시 `Screen.width/height` 대신 저장된 `PlayerPrefs` 값을 우선 사용하도록 수정(아직 저장된 적 없을 때만 폴백, doc/0503).

전체 세션별 변경 이력(코드 변경 전/후 diff 포함)은 [`doc/`](doc) 폴더에 번호순으로 정리돼 있습니다.
```

## 요약/영향받는 파일
- 변경 대상: `README.md` (4개 섹션에 추가만, 기존 문장 삭제 없음)
- 참고한 기존 문서: `doc/0500`~`doc/0504`, `Assets/Scripts/UI/GraphicsSettingsPanel.cs`
- 아직 미실행 — 사용자 확인 후 실제 `README.md`에 반영 예정.
