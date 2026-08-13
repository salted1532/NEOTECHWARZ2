# CylinderBoxColliderGenerator

`Assets/Scripts/Utility/CylinderBoxColliderGenerator.cs`

## 개요

Unity에는 원기둥 모양 콜라이더가 기본 제공되지 않아, `BoxCollider` 여러 개를 원형으로 배치해 실린더 형태를 근사하는 에디터 유틸리티. 컴포넌트를 붙이고 인스펙터에서 우클릭(컨텍스트 메뉴)으로 생성/제거한다 — 런타임 로직은 없고 순수 콜라이더 세팅용.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `radius` | 박스 세그먼트들이 배치될 원의 반지름 |
| `boxCount` | 원을 몇 개의 박스로 근사할지(최소 3) |
| `boxSize` | 박스 하나의 크기 — x=접선 방향 폭, y=높이, z=반지름 방향 두께 |
| `isTrigger` | 생성될 `BoxCollider`들의 트리거 여부 |

## 메소드 (컨텍스트 메뉴)

| 메소드 | 설명 |
|---|---|
| `Generate()` | 기존 세그먼트를 지운 뒤, `boxCount`개의 `BoxSegment_N` 자식 오브젝트를 360도로 균등 배치하고 각각에 `BoxCollider`를 붙임 + 부모에 `Kinematic Rigidbody` 보장 |
| `Clear()` | `BoxSegment_` 접두사를 가진 자식 오브젝트를 전부 제거 |
| `EnsureRigidbody()` | 자식 `BoxCollider`의 트리거 이벤트가 부모 스크립트(`CaptureSystem` 등)로 전달되려면 부모에 `Rigidbody`가 필요하다는 Unity 복합 콜라이더 규칙 때문에, 없으면 `Kinematic`+중력 끔으로 자동 추가 |
