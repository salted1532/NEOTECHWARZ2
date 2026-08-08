# 0475 - NTA 건물 이미지 미표시 원인 규명 및 수정

## 질문
"건물이미지가 안나오는 원인좀 찾아줘" (doc/0474로 NTA/OC/스포어 브루드 SO의 `Icon` 필드를 새 이미지로
연결한 직후)

## 원인
`BuildingData.Icon`(SO)은 인게임에서 두 곳에 쓰이는데, **둘 다 SO 값을 안 쓰고 프리팹/씬에 직접 박아둔
스프라이트 참조를 쓴다** — doc/0474는 SO만 고쳤으므로 이 두 곳은 여전히 깨져 있었음.

1. **NTA 건설 메뉴 버튼 아이콘** — `Assets/prefabs/Game/GameManager.prefab`의 `UIController`
   컴포넌트가 `commandCenterIcon`/`supplyDepotIcon`/`barracksIcon`/`factoryIcon`/`airportIcon`/
   `labIcon` 6개 필드를 인스펙터에 직접 갖고 있음(`UIController.cs:203-208`, `ShowBuildPanel` 등에서
   사용, `UIController.cs:1153-1158`).
2. **NTA 건물 선택 시 Info Panel 아이콘** — `BuildingController.cs:14`의 `private Sprite icon`을
   각 건물 프리팹(`MainBase`/`SupplyDepot`/`Lab`/`Tier1`(병영)/`Tier2`(공장)/`Tier3`(우주공항))이
   인스펙터에 직접 갖고 있음. `BuildingController.Start()`가 `BuildingDataSO`에서 `data`를 조회하긴
   하지만(`maxpopulationamount`만 사용) `icon` 필드에는 절대 대입하지 않음 — 애초에 코드상으로도
   SO의 `Icon`과 연결된 적이 없었음.

두 위치 다 정확히 같은 6개의 guid(`31f614dd...`, `71da5815...`, `b237c907...`, `0807f6fe...`,
`dd15a21c...`, `f381402a...`)를 참조 중이었는데, 이 guid에 매칭되는 `.meta` 파일이 프로젝트 어디에도
없었음 — 원본 텍스처가 예전에 삭제되고 참조만 남은 "깨진 링크" 상태(Unity 에디터에서는 스프라이트
슬롯이 빈 채로 보임). 흥미롭게도 이 guid들은 doc/0474에서 고치기 전 `OC Building Data SO.asset`이
갖고 있던 값과 완전히 동일 — 예전엔 NTA/OC가 이 죽은 참조를 그대로 공유하고 있었던 것으로 보임.

## 조치
같은 새 NTA 이미지(`Assets/images/Building/NTA/*.png`, doc/0474에서 계산해둔 정확한 fileID 재사용)로
7개 파일의 참조를 교체:

| 파일 | 필드 | 새 아이콘 |
|---|---|---|
| `GameManager.prefab` (UIController) | `commandCenterIcon` | 커맨드센터 1.png |
| 〃 | `supplyDepotIcon` | 보급고 1.png |
| 〃 | `barracksIcon` | 병영.png |
| 〃 | `factoryIcon` | 공장.png |
| 〃 | `airportIcon` | 공항.png |
| 〃 | `labIcon` | 연구소 1.png |
| `MainBase.prefab` (BuildingController.icon) | icon | 커맨드센터 1.png |
| `SupplyDepot.prefab` | icon | 보급고 1.png |
| `Lab.prefab` | icon | 연구소 1.png |
| `Tier1.prefab` (병영) | icon | 병영.png |
| `Tier2.prefab` (공장) | icon | 공장.png |
| `Tier3.prefab` (우주공항) | icon | 공항.png |

유니티에서 `AssetDatabase.Refresh()` 후 12개 필드 전부 리플렉션으로 non-null + 스프라이트 이름 확인
완료(전부 PASS, 기대한 이름과 일치). 파일 수정 없이 읽기 전용 검증.

## 남은 참고사항
- `BuildingController.icon`은 여전히 SO의 `Icon`과 코드로 연결돼 있지 않고 프리팹별 수동 할당 방식
  그대로임 — 앞으로 건물이 추가/변경될 때마다 이 필드를 프리팹에서 직접 챙겨야 함(유닛 쪽은
  `UnitData.Icon`을 코드에서 자동으로 가져다 쓰는 것과 다른 패턴). 필요하면 `EnemyBuildingController`
  처럼 `Start()`에서 `icon = data.Icon` 대입으로 통일하는 리팩터링을 별도로 제안할 수 있음 — 이번
  작업 범위에는 포함하지 않음.
