# 0566 - 스키터윙 공격력 하향 (제안)

## 요청 내용

> 스키터윙 공격력 12로 낮추기

## 조사 결과

`Assets/Scripts/ScriptableObject/Data/Spore Brood Unit Data SO.asset:123`
(스키터윙 항목, ID 12, line 113~160 블록 안):

```
<attackDamge>k__BackingField: 18
```

현재 공격력 `18` → `12`로 낮추면 됨. 이 데이터가 유일한 소스(스키터윙 관련 파일 검색 결과 다른
곳엔 수치 중복 없음, `AllyPrefab`도 비어있어 별도 아군 버전 데이터 없음).

## 제안하는 수정

```diff
-    <attackDamge>k__BackingField: 18
+    <attackDamge>k__BackingField: 12
```

## 확인 요청

이대로 적용해도 될지 확인 부탁드립니다.

## 구현 결과 (사용자 승인 후)

`Spore Brood Unit Data SO.asset:123`의 `<attackDamge>k__BackingField: 18` → `12`로 변경 완료.
ScriptableObject 데이터 값 수정이라 컴파일 대상 아님(코드 변경 없음).
