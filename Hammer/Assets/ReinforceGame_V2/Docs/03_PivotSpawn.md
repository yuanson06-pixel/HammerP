# 03 벽면 거치대의 피벗에서 상품 생성

## 출발 상태와 이번 기능

02 실습 씬을 Save As하여03 씬으로 저장한다. 완성 예시는 Scenes/03_PivotSpawn.unity다. 방·플레이어·조명은 씬에 고정되어 있고, 이번에는 **시작 시 상품만 생성**한다. 게임 전체를 만드는 시작 관리자는 추가하지 않는다.

## 가구 프리팹 월드 배치

| Prefabs/World의 파일 | 월드 Position | 월드 Rotation | Scale |
| --- | --- | --- | --- |
| ReinforcementBench | `(0,0.94,1.5)` | `(0,0,0)` | `(1,1,1)` |
| SupplyRack | `(-2.25,0,0.35)` | `(0,-90,0)` | `(1,1,1)` |
| StorageRack | `(2.25,0,0.35)` | `(0,90,0)` | `(1,1,1)` |

강화대 프리팹의 원점은 천판 중심이다. 테이블 전체의 위치를 위 값에 두면 다리는 바닥에 닿고 상품 피벗은 월드 `(0,1.19,1.4)`가 된다. SupplyRack과 StorageRack은 루트 회전이0인 자산이며 씬에서 각각-90/+90도를 입력한다. 표에 없는 추가180도를 넣지 않는다.

이 가구들은 기존 Editor Preview에서 분리되어 각각 독립 프리팹이 되었다. 원본 Workshop 프리팹 전체를 씬으로 가져오지 않는다. 그 안에는 V1 카메라·게임 코드가 있으므로 중복 실행될 수 있다. V2의DecorativeToolTray는 장식이며 판매 기능은 없다.

## 피벗과 클릭 면 구조

SupplyRack과 StorageRack을 펼치면 Slot_0~Slot_5가 있다. 각 Transform의 Scale은1이고, V2Slot의 Anchor는 자기 Transform이다. 상품을 받치는 Cube와 보관 클릭 면은 별도 오브젝트라서 상품에 크기 변형을 전달하지 않는다.

| index | 슬롯 로컬 Position |
| --- | --- |
| 0 | `(-0.73,1.95,0)` |
| 1 | `(0,1.95,0)` |
| 2 | `(0.73,1.95,0)` |
| 3 | `(-0.73,1.05,0)` |
| 4 | `(0,1.05,0)` |
| 5 | `(0.73,1.05,0)` |

공급의 Kind는Supply, 보관은Storage다. 강화대의 BenchSlot은Workbench이며 강화대 루트 기준 `(0,0.25,-0.1)`이다. 가구를 월드 어느 곳으로 옮겨도 상품은 각 Anchor의 로컬 위치0에 놓인다. 플레이어가 닿을 수 있는지는04장의 Reach8m 범위로 별도 결정된다.

상품 프리팹은 `Prefabs/Items`에 있고 V2Item과BoxCollider, 정규화된 visual 모델을 포함한다. `Art/Products`의 원본 모델을 스포너에 넣지 않는다. 원본에는 V2Item 컴포넌트가 없다.

## 상품과 슬롯 코드

상품은 현재 슬롯, 단계와 손 부착 자세를 가진다. 슬롯은 현재 상품 하나를 기억한다. 공급은 시작 생성만 허용하고 플레이어가 되돌려 놓는 것은 막는다.04장에서 같은 슬롯 타입으로 강화대·보관도 처리한다.

[V2Item.cs](../Runtime/V2Item.cs):

```csharp
using UnityEngine;

namespace ReinforceGameV2
{
    public sealed class V2Item : MonoBehaviour
    {
        public string itemId, displayName;
        public int basePrice = 35;
        public int level;
        public Transform visual;
        public Vector3 displayEuler = new Vector3(0, 180, 0);
        public Vector3 benchEuler = new Vector3(90, 0, 0);
        public Vector3 gripEuler = new Vector3(0, 180, 0);
        public Vector3 gripOffset;
        public V2Slot CurrentSlot { get; internal set; }
        public void SetLayer(int layer)
        {
            foreach (var part in GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
        }
        public void Hold(Transform socket)
        {
            if (CurrentSlot) CurrentSlot.Release(this);
            transform.SetParent(socket, false);
            transform.localRotation = Quaternion.Euler(gripEuler);
            transform.localPosition = gripOffset - Quaternion.Euler(gripEuler) * (visual ? visual.localPosition : Vector3.zero);
            SetLayer(30);
        }
    }
}
```

[V2Slot.cs](../Runtime/V2Slot.cs):

```csharp
using UnityEngine;

namespace ReinforceGameV2
{
    public enum V2SlotKind { Supply, Workbench, Storage }
    public sealed class V2Slot : MonoBehaviour
    {
        public V2SlotKind kind;
        public Transform anchor;
        public V2Item Occupant { get; private set; }
        public bool TryPlace(V2Item item, bool allowSupply = false)
        {
            if (!item || Occupant || (kind == V2SlotKind.Supply && !allowSupply)) return false;
            if (item.CurrentSlot) item.CurrentSlot.Release(item);
            Occupant = item; item.CurrentSlot = this;
            item.transform.SetParent(anchor ? anchor : transform, false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.Euler(kind == V2SlotKind.Workbench ? item.benchEuler : item.displayEuler);
            item.SetLayer(0); return true;
        }
        public void Release(V2Item item)
        {
            if (Occupant != item) return;
            Occupant = null; item.CurrentSlot = null;
        }
    }
}
```

## 스포너 붙이기

1. SupplyRack의 씬 인스턴스 루트에 V2RackSpawner를 추가한다.
2. Slots의 Size를6으로 하고 같은 거치대의 Slot_0~Slot_5를 순서대로 넣는다.
3. Products의 Size를6으로 하고 아래 Item 프리팹을 넣는다.
4. Spawn On Start를 켠다.
5. StatusHUD의 Tutorial Step을3으로 한다.

| index | Prefabs/Items |
| --- | --- |
| 0 | sword_1handed |
| 1 | axe_1handed |
| 2 | shield_round |
| 3 | staff |
| 4 | dagger |
| 5 | engineer_Wrench |

[V2RackSpawner.cs](../Runtime/V2RackSpawner.cs):

```csharp
using UnityEngine;

namespace ReinforceGameV2
{
    public sealed class V2RackSpawner : MonoBehaviour
    {
        public V2Slot[] slots;
        public V2Item[] products;
        public bool spawnOnStart = true;
        public int SpawnedCount { get; private set; }
        void Start() { if (spawnOnStart) SpawnMissing(); }
        public void SpawnMissing()
        {
            if (slots == null || products == null || slots.Length != products.Length)
            { Debug.LogError("V2: supply slots and products must have matching lengths.", this); return; }
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i] || slots[i].kind != V2SlotKind.Supply || !products[i])
                { Debug.LogError("V2: invalid supply slot/product at " + i, this); continue; }
                if (slots[i].Occupant) continue;
                var item = Instantiate(products[i]);
                if (slots[i].TryPlace(item, true)) SpawnedCount++;
                else Destroy(item.gameObject);
            }
        }
    }
}
```

Start에서6개 상품을 만들고 슬롯의 TryPlace에 allowSupply=true를 전달한다. 이미 점유한 슬롯에는 추가 생성하지 않는다. 배열 길이나 필수 참조가 잘못되면 Console로 알려 준다.

## 검증

Play 전에는 가구와 빈 슬롯만 있어야 한다. Play 후 왼쪽으로 우클릭 전환하면6개 상품이 보인다. Hierarchy에서 각 Slot 밑의 상품을 선택해 Local Position0과CurrentSlot 관계를 확인한다. CurrentSlot은 일반 프로퍼티라 기본 Inspector에 표시되지 않으며 부모와 화면을 통해 먼저 검사할 수 있다.

- 보관대는 비어 있고 강화대에도 상품이 없다.
- SpawnMissing을 다시 호출해도 점유된 칸에 복제되지 않는다.
- Stop하면 런타임 상품이 사라지고 가구는 남는다.
- Edit 모드에서 SupplyRack을 이동한 후 다시 Play하면 새 위치의 피벗에서 생성된다.

상품이 겹치면 슬롯 배열에 같은 Transform을 중복 연결했는지 확인한다. 모델 크기가 달라지면 가구·슬롯의 Scale과 아이템 프리팹 visual의 정규화를 확인한다. 이번 단계에서는 좌클릭해도 잡히지 않는다.

다음: [04 좌클릭 집기와 배치](04_GrabPlace.md).
