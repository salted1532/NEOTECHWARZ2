# RadiusIndicator

`Assets/Scripts/Effects/RadiusIndicator.cs`

## 개요

원형 범위를 잠깐 동안 바닥에 그려서 보여주는 범용 이펙트(doc/0323 후속). 스카이 랜서 "지상 폭격" 같은 범위형 스킬이 실제로 어디까지 피해를 입혔는지 눈으로 확인할 수 있게 하기 위한 용도. 텍스처/머티리얼 에셋 없이 `LineRenderer` + 내장 `Sprites/Default` 셰이더로 원을 그리므로 준비물이 필요 없다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `segmentCount` | 원을 구성하는 세그먼트(꼭짓점) 수 |
| `lineWidth` | 선 두께 |
| `color` | 선 색상 |
| `cachedLineShader` | `Shader.Find` 문자열 조회를 스킬 사용 때마다 반복하지 않도록 캐싱(static, private) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Show(center, radius, duration)` (static) | 일회성 표시 — `duration` 뒤에 자동으로 사라진다. 스킬이 실제로 발동돼서 범위를 확인시켜줄 때 사용 |
| `CreateFollowing(radius)` (static) | 마우스를 따라다니는 지속형 표시(범위 지정 대기 중)를 생성해 반환 — 호출자가 `SetPosition()`으로 매 프레임 위치를 갱신하고, 지정 모드가 끝나면 직접 `Destroy(gameObject)`해야 함(자동 파괴 타이머 없음) |
| `SetPosition(position)` | 지속형 표시의 위치 갱신 |
| `Draw(radius)` (private) | `LineRenderer` 컴포넌트를 추가하고 원형 좌표를 계산해 세팅 (지면에서 0.1만큼 띄워 Z-fighting 방지) |

## 연관 컴포넌트

- **SkyLancerSkill**: "지상 폭격" 스킬 발동 시 `Show()`로 피해 범위 표시
