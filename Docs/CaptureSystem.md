# CaptureSystem

`Assets/Scripts/CaptureSystem/CaptureSystem.cs`

## 개요

거점(비콘)에 부착하는 점령 시스템. 트리거 콜라이더 안에 있는 아군(`UnitController`)/적
(`EnemyUnitController`) 유닛 수에 따라 부호 있는 점령치(`controlValue`, -`captureDuration`~+`captureDuration`)를
밀고 당기며, 양쪽 다 있으면 교착(진행 정지), 한쪽만 있으면 그쪽으로 진행한다. 콜라이더는 Is Trigger가
켜져 있어야 한다.

## 핵심 개념 — 세 가지 상태 전이 규칙 (doc/0357)

1. **완전 점령을 되돌리기** (`Owner`가 이미 Ally/Enemy로 확정된 상태): 반대 진영이 혼자 밀면 1배속으로
   30초 걸려 정확히 중립(0)을 지나야 하고, 그 30초 동안은 여전히 이전 소유자로 표시된다(sticky). 중립을
   지난 뒤 다시 30초 밀어야 그 진영 소유가 된다(총 60초).
2. **중립 진행중 상태를 되돌리기** (한 번도 완전 점령된 적 없음, `Owner == Neutral`): 반대 진영 진행치를
   지우는 중이면 자연 감쇠(`decayRate`)와 미는 힘(1/sec)이 겹쳐 **2배속**으로 지워지고, 0을 넘으면
   그때부턴 평소처럼 1배속으로 30초 채운다.
3. **방치(아무도 없음)**: 완전 점령된 거점이면 원래 소유자 쪽 극값으로 서서히 **회복**, 중립 진행중이던
   거점이면 0으로 서서히 **감쇠**하며 바가 자동으로 사라진다.

`Owner`는 `controlValue`가 ±`captureDuration`(완전 점령) 또는 정확히 0(중립)을 **실제로 통과**했을 때만
바뀐다(`UpdateOwnerFromControlValue`) — 매 프레임 "지금 어느 구간에 있는지"로 재계산하지 않고, 경계를
지나는 순간에만 sticky하게 전환된다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `captureDuration` | `float` (SerializeField, 기본 30) | 한쪽 방향으로 완전히 점령되는 데 걸리는 시간(초) |
| `decayRate` | `float` (SerializeField, 기본 1) | 방치 시 되돌아가는 속도 + 중립 진행중 상태에서 반대 진영을 지울 때의 보너스 속도(점령 속도 1과 합쳐 2배속) |
| `neutralEffect`/`allyEffect`/`enemyEffect` | `GameObject` | 소유 상태별로 하나만 활성화되는 이펙트 |
| `captureBar` | `Slider` | 점령 진행도 UI, 진행 중이거나 방치 회복/감쇠 중일 때만 노출, 안개에 가려진 위치면 숨김 |
| `territoryZone` | `TerritoryZone` | 관리하는 영토(비워두면 같은 오브젝트/자식에서 자동 탐색) |
| `debugOwner` | `CaptureOwner` (SerializeField) | 인스펙터에서 직접 소유 상태를 강제 전환하는 테스트용 필드 |
| `CurrentOwner` | `CaptureOwner` (get) | 현재 실제 소유 상태 |
| `controlValue` | `float` (private) | -`captureDuration`~+`captureDuration`, 0=중립 |
| `fogWar` | `csFogWar` (private) | 점령 타이머 슬라이더의 안개 가림 여부 판정용 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | `territoryZone`/`fogWar` 캐싱, `debugOwner` 기준으로 시작 상태 반영(빌드에서도 동작) |
| `OnTriggerEnter/Exit` | 트리거 범위에 들어오고 나가는 아군/적 유닛을 `alliesInRange`/`enemiesInRange`에 등록/해제 |
| `Update()` | 매 프레임 존재 여부 판정 후 `AllyRate()`/`EnemyRate()`(진행)나 `RestPoint()`(방치)로 `controlValue` 갱신, 바/소유자 갱신 |
| `AllyRate()` / `EnemyRate()` (private) | 아군/적이 밀 때의 초당 변화량 — 중립 진행중 + 반대 진행치 지우는 중이면 `1 + decayRate`, 아니면 `1` |
| `RestPoint()` (private) | 아무도 없을 때 서서히 되돌아갈 목표값(Ally면 +captureDuration, Enemy면 -captureDuration, Neutral이면 0) |
| `UpdateOwnerFromControlValue()` (private) | 경계(±captureDuration, 0) 통과 시에만 `CurrentOwner`를 sticky하게 전환, 바뀌면 `ApplyEffect` 호출 |
| `UpdateCaptureBar(alliesPresent, enemiesPresent, contested)` (private) | 진행 중이거나 방치 회복/감쇠 중이면 바 노출(안개에 가려지면 숨김), 값은 `Mathf.Abs(controlValue)`로 통일 |
| `ApplyEffect(owner)` (private) | 소유 상태별 이펙트 전환 + `territoryZone.Owner` 갱신 + 에디터에서 `debugOwner` 동기화 |
| `OnValidate()` (private, 에디터 전용) | 인스펙터에서 `debugOwner`를 직접 바꿨을 때만 그 값으로 강제 전환 |

## 연관 컴포넌트

- **UnitController / EnemyUnitController**: 트리거 콜라이더로 감지되는 아군/적 유닛
- **TerritoryZone**: 소유자에 따라 외곽선 색이 자동 전환됨(`territoryZone.Owner`)
- **FogVisibility**: 점령 타이머 슬라이더가 안개에 가려진 위치면 표시 안 함
