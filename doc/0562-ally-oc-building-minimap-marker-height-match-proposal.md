# 0562 - 아군 OC 건물 미니맵 마커 높이를 적 OC 건물과 동일하게

## 날짜
2026-08-13

## 요청 내용
"아군 OC 건물 미니맵 마커 낮추기 이것도 NTA, 적 OC건물 미니맵마커 위치랑 같게 조정해줘"

→ 아군(Ally) OC 건물의 미니맵 마커(`MiniMapIcon`) 높이를, 적(Enemy) OC 건물의 마커 높이와 같아지도록
낮춘다.

## 확인한 사실
`Assets/prefabs/OC/Ally/Building/Ally_*.prefab`은 전부 `Assets/prefabs/OC/Building/Enemy_*.prefab`을
소스로 하는 중첩 프리팹 인스턴스(태그만 `AllyOC`로 바꾸고 `EnemyBuildingController` → `AllyBuildingController`로
교체)다. 각 Enemy 프리팹의 `MiniMapIcon` 자식 Transform 기본값은 전부 `m_LocalPosition.y = 40`인데,
Ally 인스턴스 6개 전부 이 값을 `70`으로 덮어쓰고 있음 — 그래서 아군 건물 마커가 적 건물 마커보다
높게(30만큼) 떠 있다.

| 건물 | Enemy 원본 Y (기본값) | Ally 인스턴스 Y (오버라이드) |
|---|---|---|
| MainBase | 40 | 70 |
| Lab | 40 | 70 |
| SupplyDepot | 40 | 70 |
| Tier1 | 40 | 70 |
| Tier2 | 40 | 70 |
| Tier3 | 40 | 70 |

## 설계안
6개 `Ally_*.prefab` 파일에서 `MiniMapIcon` 오버라이드 블록의 `value: 70`을 `value: 40`으로 고쳐 Enemy
쪽과 동일하게 맞춘다(YAML 텍스트 직접 수정, doc/0356에서 쓴 방식과 동일). 예)

```yaml
# Assets/prefabs/OC/Ally/Building/Ally_MainBase.prefab
    - target: {fileID: 593320741992707879, guid: 78fe924d2053b004ea76bc750be7f871, type: 3}
      propertyPath: m_LocalPosition.y
      value: 70   # → 40으로 변경
      objectReference: {fileID: 0}
```

나머지 5개 파일(`Ally_Lab`, `Ally_SupplyDepot`, `Ally_Tier1`, `Ally_Tier2`, `Ally_Tier3`)도 각자의
target fileID/guid에 대해 동일하게 `70` → `40`.

## 영향받는 파일
- `Assets/prefabs/OC/Ally/Building/Ally_MainBase.prefab`
- `Assets/prefabs/OC/Ally/Building/Ally_Lab.prefab`
- `Assets/prefabs/OC/Ally/Building/Ally_SupplyDepot.prefab`
- `Assets/prefabs/OC/Ally/Building/Ally_Tier1.prefab`
- `Assets/prefabs/OC/Ally/Building/Ally_Tier2.prefab`
- `Assets/prefabs/OC/Ally/Building/Ally_Tier3.prefab`

C# 코드 변경 없음(순수 프리팹 오버라이드 값 수정).

## 확인 결과
사용자에게 물어본 결과 "6종 모두 40으로" 선택 - 위 설계안대로 그대로 적용.

## 변경 상세
6개 파일 전부 `MiniMapIcon` 오버라이드 블록의 `propertyPath: m_LocalPosition.y`를 `70` → `40`으로
교체(YAML 텍스트 직접 수정). 수정 후 `grep -c "value: 70"`으로 6개 파일 전부 잔여 `70` 없음을 확인,
`grep`으로 6개 파일 전부 마커 오버라이드가 `40`임을 재확인.

## 컴파일 확인
C# 코드 변경이 없어 별도 컴파일 불필요(직전 작업의 `compile` 결과가 여전히 유효).
