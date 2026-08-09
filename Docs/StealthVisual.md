# StealthVisual

`Assets/Scripts/Unit/StealthVisual.cs`

## 개요

살아있는 유닛의 렌더러 머티리얼을 일시적으로 반투명 흰색으로 바꿨다가 복원하는 범용 컴포넌트(doc/0323). `PreviewSystem`의 건물 배치 고스트 머티리얼과 같은 기법이지만, 임시 프리뷰 오브젝트가 아니라 살아있는 유닛 자신의 원본 머티리얼을 보존/복원해야 하므로 별도 컴포넌트로 둔다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `stealthMaterial` | 반투명 흰색 머티리얼 (`PreviewSystem.previewMaterialPrefab`과 같은 에셋 재사용 가능) |
| `excludeFromStealth` | 머티리얼을 바꾸지 않을 자식 오브젝트 목록(예: 선택 마커) — 이 아래의 Renderer는 건드리지 않음 |
| `originalMaterials` | Renderer→원본 머티리얼 배열 매핑 (복원용, private) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `EnterStealth()` | 제외 대상이 아닌 모든 자식 Renderer의 원본 머티리얼을 저장한 뒤 전부 `stealthMaterial`로 교체 |
| `IsExcluded(target)` (private) | 대상 트랜스폼이 `excludeFromStealth` 목록 아래에 있는지 확인 |
| `ExitStealth()` | 저장해둔 원본 머티리얼을 전부 복원하고 캐시를 비움 |

## 연관 컴포넌트

- **SharpshooterSkill**: 은신 스킬 진입/해제 시 `EnterStealth()`/`ExitStealth()` 호출
- **PreviewSystem**: 건물 배치 고스트에 동일한 머티리얼 교체 기법 사용(별도 구현)
