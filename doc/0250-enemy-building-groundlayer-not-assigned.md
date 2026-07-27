# 0250 - 적 건물(OC) groundLayer 미설정으로 지면 스냅이 동작 안 함 (적용 완료)

## 요청

> 적 건물도 그냥 설치된 위치에 그리드에 맞는 위치로 지정하고 그리드로 지어졌다고 그리드에 추가시켜줄수
> 있어? 지금 그냥 프리팹을 설치해두면 땅속에 박혀있어 지형 표면 위로 건물이 딱 붙었으면 좋겠고 그리드에
> 맞춰서 위치가 좀 조정되었으면 좋겠어

## 조사 내용 - 기능은 이미 구현돼 있었음, 그런데 프리팹 설정이 빠져있었음

요청하신 기능(씬에 배치된 건물이 시작 시 지면에 붙고 + 자기 크기만큼 그리드 셀에 맞춰 정렬 + 그리드
점유 정보에 등록)은 [[0246]]과 [[0247]]에서 이미 아군/적 건물 둘 다에 구현되어 있었다.

- `EnemyBuildingController.Start()` (`Assets/Scripts/Enemy/EnemyBuildingController.cs:44-49`)가
  `SnapToGround()`(지면 높이 레이캐스트로 Y 보정) → `RegisterToGridIfPossible()`(그리드 셀에 XZ 중앙정렬 +
  `PlacementSystem`의 `GridData`에 점유 등록)을 순서대로 호출한다.

그런데 실제로 프리팹을 확인해보니 **`SnapToGround()`가 쓰는 `groundLayer` 필드가 OC(적) 건물 프리팹
6개 전부 "Nothing"(0)으로 되어 있었다**:

- `Enemy_MainBase.prefab` - `groundLayer` 필드는 있지만 `m_Bits: 0`
- `Enemy_Tier1/2/3.prefab`, `Enemy_SupplyDepot.prefab`, `Enemy_Lab.prefab` - `groundLayer` 필드 자체가
  직렬화돼 있지 않음 (인스펙터에서 한 번도 건드린 적이 없어서 기본값 0 그대로)

```csharp
// EnemyBuildingController.SnapToGround()
private void SnapToGround()
{
    if (groundLayer == 0)   // ← 6개 프리팹 전부 여기서 조용히 return, 위치가 전혀 안 바뀜
        return;
    ...
}
```

반면 같은 위치(그리드 X/Z 중앙정렬)는 `RegisterToGridIfPossible()`이 `groundLayer`와 무관하게 항상
동작하므로 실제로 이미 되고 있다 - "OC Building Data SO.asset"에 ID 1~6 전부 `Size`가 정의돼 있어
그리드 등록 자체는 성공한다. 즉 **그리드 등록/XZ 정렬은 이미 되고 있고, Y(지면 높이)만 씬에 배치할 때의
좌표 그대로 남아있어서 "땅속에 박혀있다"고 느껴지는 것**이다.

참고로 아군(NTA) 건물 프리팹은 전부 `groundLayer`가 `Ground` 레이어(레이어 인덱스 7, `m_Bits: 128`)로
정확히 설정돼 있다 (`Tier1/2/3.prefab`, `SupplyDepot.prefab`, `MainBase.prefab`, `Lab.prefab` 전부 동일).
OC 프리팹을 만들 때 이 필드만 복사가 안 된 것으로 보인다.

## 수정 내용 - 프리팹 6개의 groundLayer를 Ground 레이어로 설정 (코드 변경 없음, 데이터만)

`ProjectSettings/TagManager.asset` 기준 `Ground` 레이어는 인덱스 7 (`m_Bits: 128`).

**`Assets/prefabs/OC/Building/Enemy_MainBase.prefab`**
```yaml
# 기존
  enemyBuildingID: 1
  groundLayer:
    serializedVersion: 2
    m_Bits: 0
# 변경
  enemyBuildingID: 1
  groundLayer:
    serializedVersion: 2
    m_Bits: 128
```

**`Assets/prefabs/OC/Building/Enemy_Tier1.prefab`**
```yaml
# 기존
  enemyBuildingID: 3
--- !u!208 &875391073210738237
# 변경
  enemyBuildingID: 3
  groundLayer:
    serializedVersion: 2
    m_Bits: 128
--- !u!208 &875391073210738237
```

**`Assets/prefabs/OC/Building/Enemy_Tier2.prefab`** (`enemyBuildingID: 4` 다음에 동일하게 삽입)

**`Assets/prefabs/OC/Building/Enemy_Tier3.prefab`** (`enemyBuildingID: 5` 다음에 동일하게 삽입)

**`Assets/prefabs/OC/Building/Enemy_SupplyDepot.prefab`** (`enemyBuildingID: 2` 다음에 동일하게 삽입)

**`Assets/prefabs/OC/Building/Enemy_Lab.prefab`** (`enemyBuildingID: 6` 다음에 동일하게 삽입)

## 요약

- 요청하신 "지면에 붙이기 + 그리드 정렬/등록" 기능은 코드상 이미 존재함(doc/0246, doc/0247) - 새로
  구현할 코드는 없었음.
- 실제 원인은 OC 적 건물 프리팹 6개의 `groundLayer` 인스펙터 필드가 전부 비어있어(Nothing) 지면
  레이캐스트가 아예 실행 안 되는 것이었음 - 그리드 XZ 정렬/등록은 이미 정상 동작 중이었음.
- 수정은 6개 프리팹 파일의 `groundLayer`를 아군 건물과 동일하게 `Ground` 레이어(`m_Bits: 128`)로
  설정하는 것뿐 (스크립트 코드 변경 없음). 사용자 확인 후 적용 완료.

## 변경된 파일

- `Assets/prefabs/OC/Building/Enemy_MainBase.prefab`
- `Assets/prefabs/OC/Building/Enemy_Tier1.prefab`
- `Assets/prefabs/OC/Building/Enemy_Tier2.prefab`
- `Assets/prefabs/OC/Building/Enemy_Tier3.prefab`
- `Assets/prefabs/OC/Building/Enemy_SupplyDepot.prefab`
- `Assets/prefabs/OC/Building/Enemy_Lab.prefab`
