# MinimapAlertController

`Assets/Scripts/Camera/MinimapAlertController.cs`

## 개요

아군 유닛/건물이 적에게 공격받으면 공격받은 위치(대상 자신의 위치) Y=40에 3D 마커
(`Attacked_MiniMapPointer` 프리팹, 미니맵 카메라 전용 레이어)를 잠깐 띄우는 싱글턴. `UnitAudio`/
`BuildingAudio`가 "적에게 공격받음" 경고음(`PlayUnitUnderAttackWarning`/`PlayBuildingUnderAttackWarning`)을
**실제로 새로 재생한 순간**(`SoundManager.underAttackWarningCooldown`, 기본 10초 쿨다운을 통과했을 때)에만
호출한다 — 계속 얻어맞아도 경고음처럼 10초 간격으로만 마커가 뜬다(doc/0362).

> 이전(doc/0349)엔 화면 밖일 때만 미니맵에 반투명 UI 핑(`ShowAttackPing`)을 띄우는 방식이었으나, 이번에
> 3D 마커 스폰 방식으로 완전히 대체되었다(UI 핑 관련 코드는 전부 제거됨).

`GameManager.prefab`의 `MiniMap_image`(미니맵 RawImage, `MinimapController`가 붙어있는 오브젝트)에
부착돼 있다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `Instance` | `MinimapAlertController` (static get) | 싱글턴 접근점 |
| `attackedPointerPrefab` | `GameObject` (SerializeField) | 스폰할 3D 마커 프리팹(`Attacked_MiniMapPointer`) |
| `attackedPointerHeight` | `float` (SerializeField, 기본 40) | 마커를 스폰할 Y좌표 |
| `attackedPointerLifetime` | `float` (SerializeField, 기본 3) | 마커가 유지되는 시간(초), 지나면 자동 파괴 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | `Instance` 등록 |
| `SpawnAttackedPointer(Vector3 attackedPosition)` | `(x, attackedPointerHeight, z)` 위치에 `attackedPointerPrefab`을 스폰하고 `attackedPointerLifetime` 뒤 자동 파괴. 프리팹이 비어있으면 아무 것도 안 함 |

## 연관 컴포넌트

- **UnitAudio / BuildingAudio**: `HandleDamaged()`에서 경고음이 실제로 재생된 경우에만 `SpawnAttackedPointer(transform.position)` 호출
- **SoundManager**: `PlayUnitUnderAttackWarning()`/`PlayBuildingUnderAttackWarning()`이 실제로 새로 재생을 시작했는지(`bool`)를 반환하도록 되어 있어, 그 결과를 게이팅에 그대로 사용
