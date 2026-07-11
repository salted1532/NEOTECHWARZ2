# HealthBarBillboard

`Assets/Scripts/UI/HealthBarBillboard.cs`

## 개요

체력바 UI가 항상 카메라를 향하도록 만드는 빌보드 회전 컴포넌트. 카메라의 X(피치) 각도만 따라가고 Y/Z는 항상 0으로 고정한다 — 유닛/건물이 Y축으로 회전해도 체력바 자체는 방향을 따라 돌지 않는다. 체력바 UI 오브젝트(Slider가 붙은 Canvas 등)에 직접 붙여서 사용한다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `targetCamera` | 대상 카메라 캐시 (`Start()`에서 `Camera.main`으로 찾고, 못 찾으면 이후 `LateUpdate()`마다 재시도) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | `Camera.main` 캐싱 |
| `LateUpdate()` | 카메라가 그 프레임에 움직인 뒤에 맞춰(떨림 방지) `transform.rotation`을 `Quaternion.Euler(카메라의 X, 0, 0)`으로 직접 대입 — 부모(유닛/건물)가 Y축으로 회전해도 이 오브젝트의 월드 회전은 항상 고정됨 |

## 연관 컴포넌트

- **HealthManager**: `healthSlider` 필드에 연결된 `Slider`가 체력 변화(`OnHealthChanged`)에 맞춰 값을 갱신하고, 그 슬라이더가 속한 UI 오브젝트에 이 스크립트가 붙어 카메라를 향하게 회전시킴 (같은 오브젝트일 필요는 없음)
