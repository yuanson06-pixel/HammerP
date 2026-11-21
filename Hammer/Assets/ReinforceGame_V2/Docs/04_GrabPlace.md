# 04 좌클릭으로 집고 강화대와 저장대에 놓기

## 이번 단계의 결과

03 씬을 Save As하여04 실습 씬을 만든다. 완성 예시는 `Scenes/04_GrabPlace.unity`다. 거치대 상품을 왼손으로 잡고 강화대 또는 저장대의 빈 칸에 놓는다. 이미 찬 칸이나 공급대로의 잘못된 배치는 막는다. 강화 기능은 다음 단계에 연결한다.

01~03장의 월드 위치를 유지한다. 입력은 씬에 이미 있는 PlayerEngineer에 컴포넌트를 붙여 처리한다.

## 클릭 대상 확인

상품은 자신의 BoxCollider와 V2Item으로 판정한다. 가구의 V2ClickTarget은 사용할 슬롯을 가리킨다. 제공 프리팹에는 가구 내부 참조가 이미 연결되어 있으므로 펼쳐서 아래 구성을 확인한다.

| 대상 | Action | Slot | 동작 |
| --- | --- | --- | --- |
| 강화대 천판·강화판 | Slot | BenchSlot | 손의 상품 배치, 빈손이면 기존 상품 집기 |
| TAKE 버튼 | Take | BenchSlot | 빈손으로 작업대 상품 회수 |
| 저장대 클릭 면·받침 | Slot | 해당 저장 Slot_0~5 | 빈 칸에 배치 또는 기존 상품 회수 |
| 공급대 받침 | Slot | 해당 공급 슬롯 | 상품 회수, 공급대로 되돌려 놓기 금지 |
| ENHANCE 버튼 | Enhance | BenchSlot | 이번 단계는 연결 안내만 표시 |

V2에서는 **작업대 상품 자체를 클릭하면 집는다**. 강화는05장의 ENHANCE 버튼으로만 시작한다. V1의 작은 판매 트레이는 V2에서 장식용 도구 트레이이며 판매 기능이 없다.

[V2ClickTarget.cs](../Runtime/V2ClickTarget.cs):

```csharp
using UnityEngine;

namespace ReinforceGameV2
{
    public enum V2ClickAction { Slot, Take, Enhance }
    public sealed class V2ClickTarget : MonoBehaviour
    {
        public V2ClickAction action;
        public V2Slot slot;
        public V2Enhancer enhancer;
    }
}
```

## 컴포넌트 붙이기

1. PlayerEngineer 씬 루트에 V2Interaction을 추가한다.
2. 아래 참조를 연결한다.
3. StatusHUD의 Tutorial Step=4, Interaction=새 V2Interaction으로 설정한다.

| V2Interaction 필드 | 연결 대상 |
| --- | --- |
| View | PlayerEngineer 자식 Camera |
| Hand Socket | 왼손의 ItemGrip |
| Hands | PlayerEngineer의 V2HandRig |
| Fixed View | PlayerEngineer의 V2FixedView |
| Enhancer | 이번 단계는 None |
| Reach | 8 |

Hand Socket은 hand.l 본 자체가 아니라 ItemGrip이다. Hands.Item Grip도 같은 소켓을 가리켜야 한다. 도구 자체를 강화대에 붙이지 않고 플레이어에 붙인다.

[V2Interaction.cs](../Runtime/V2Interaction.cs):

```csharp
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReinforceGameV2
{
    public sealed class V2Interaction : MonoBehaviour
    {
        public Camera view;
        public Transform handSocket;
        public V2HandRig hands;
        public V2FixedView fixedView;
        public V2Enhancer enhancer;
        public float reach = 8;
        public V2Item Held { get; private set; }
        public string Message { get; set; } = "LMB: pick up / place. RMB: fixed view turn.";
        public bool Locked => (fixedView && fixedView.IsTurning) || (enhancer && enhancer.IsBusy);
        InputAction click;
        void OnEnable() { click = new InputAction("Select", InputActionType.Button, "<Mouse>/leftButton"); click.Enable(); }
        void OnDisable() { click?.Dispose(); click = null; }
        void Update()
        {
            if (hands) hands.carrying = Held != null;
            if (!Locked && click.WasPressedThisFrame() && Mouse.current != null && !Mouse.current.rightButton.isPressed)
                Select(view.ScreenPointToRay(Mouse.current.position.ReadValue()));
        }
        public void Select(Ray ray)
        {
            if (Locked) return;
            var hits = Physics.RaycastAll(ray, reach, ~((1 << 30) | (1 << 31)), QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var item = hit.collider.GetComponentInParent<V2Item>();
                if (item && item == Held) continue;
                if (item) { Pickup(item); return; }
                var target = hit.collider.GetComponentInParent<V2ClickTarget>();
                if (target) Use(target);
                return; // Opaque geometry blocks anything behind it.
            }
        }
        public bool Pickup(V2Item item)
        {
            if (Locked || !item || Held || !handSocket) { Message = "Empty your hand before picking up another item."; return false; }
            Held = item; item.Hold(handSocket);
            if (hands) hands.carrying = true;
            Message = "Carry to the workbench or an empty storage slot."; return true;
        }
        public bool Place(V2Slot slot)
        {
            if (Locked || !Held || !slot || !slot.TryPlace(Held)) { Message = "Choose an empty workbench or storage slot."; return false; }
            Held = null; if (hands) hands.carrying = false;
            Message = "Placed. Click the item to retrieve it."; return true;
        }
        public void Use(V2ClickTarget target)
        {
            if (Locked || !target) return;
            if (target.action == V2ClickAction.Enhance)
            {
                if (target.enhancer && target.enhancer.isActiveAndEnabled) target.enhancer.TryEnhance();
                else Message = "Enhancement is introduced in step 5.";
            }
            else if (target.action == V2ClickAction.Take)
            {
                if (Held) Message = "Place the held item first.";
                else if (target.slot && target.slot.Occupant) Pickup(target.slot.Occupant);
                else Message = "The workbench is empty.";
            }
            else if (Held) Place(target.slot);
            else if (target.slot && target.slot.Occupant) Pickup(target.slot.Occupant);
            else Message = "Carry an item here first.";
        }
    }
}
```

## 구현 흐름

### 포인터 판정

좌클릭 시 Camera.ScreenPointToRay로 포인터 레이를 만든다. RaycastAll을 거리순으로 정렬하고 처음 유효한 충돌만 처리한다. 벽에 닿았다면 뒤 상품까지 찾지 않는다. Trigger는 무시한다.

레이어30과31을 제외하므로 손·망치가 클릭을 막지 않는다. 상품을 들면 전체 계층을30으로 바꾸고 슬롯에 놓으면0으로 되돌린다. 시점 전환 중에는 FixedView.IsTurning으로 클릭을 막는다.

### 집기와 배치

Pickup은 빈손일 때만 가능하다. CurrentSlot의 점유를 해제하고 상품을 ItemGrip 자식으로 바꾼다. gripEuler와 손잡이 보정을 적용한 뒤 Hands.carrying=true로 왼팔을 표시한다.

Place는 슬롯의 TryPlace에 위임한다. 빈 Workbench 또는 Storage면 Anchor의 로컬 위치0에 놓는다. Workbench는 benchEuler, Storage는 displayEuler를 사용한다. 성공한 뒤에만 Held=null로 만들고 왼팔을 숨긴다. 실패하면 손의 상품을 유지한다.

소켓과 피벗 부모의 Scale은1이어야 한다. 상품 자세가 틀리면 가구 전체 크기보다 V2Item의 Display/Bench Euler와 Grip Offset을 조절한다.

## 직접 검증

1. Play 후 우클릭1회로 공급대를 본다.
2. 검을 좌클릭한다. 공급 칸이 비고 왼손에 검이 있어야 한다.
3. 우클릭1회로 정면을 본다. 회전 중 클릭에는 반응하지 않아야 한다.
4. 작업대 천판 또는 PLACE ITEM 판을 클릭한다. 손이 비고 검이 작업대에 놓인다.
5. 상품 또는 TAKE를 클릭해 회수한다.
6. 우클릭을 반복해 오른쪽 저장대를 본다. 빈 칸의 넓은 클릭 면을 눌러 놓는다.
7. 저장 상품을 다시 클릭하고 다른 빈 칸에 놓는다.
8. 다른 상품을 든 채 이미 찬 칸에 놓아 본다. 기존 상품과 손의 상품을 모두 유지해야 한다.
9. 빈 공급 칸에 돌려놓으려 한다. 거절되고 손의 상품이 유지되어야 한다.

**완료 기준:** 상품은 공급·손·작업대·저장 중 한 위치에만 존재한다. 복제되지 않고 이동한 칸이 올바르게 비워진다. 가구를 다른 월드 위치로 옮겨도 내부 피벗을 따라 동작한다. 단,8m 밖이나 벽에 가린 대상은 선택되지 않는다.

| 증상 | 확인 |
| --- | --- |
| 클릭 반응 없음 | Game뷰 포커스, View, Collider, Enabled |
| 손에 오지 않음 | Hand Socket, Hands, 이미 Held가 있는지 |
| 내려놓을 수 없음 | 슬롯 점유, Kind, 클릭 면, 레이어 복구 |
| 크기·방향 이상 | 부모 Scale, 상품 회전값, 손잡이 보정 |

이 단계는 세이브가 없으므로 Stop하면 실행 중 상품이 사라진다. 다음: [05 SO 기반 강화](05_EnhancementBalance.md).
