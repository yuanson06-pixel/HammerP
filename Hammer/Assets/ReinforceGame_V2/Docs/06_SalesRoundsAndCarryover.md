# 06 판매 목표와 3라운드 진행 및 보관품 이월

## 이번 단계에서 완성하는 것

05단계의 고정 각도 우클릭 시점과 ENHANCE 버튼 조작을 유지한다. 이제 강화한 상품을 팔아 목표액을 채우고 다음 라운드로 넘어간다. 저장대 상품은 종류·단계·칸을 유지하며 공급대만 새 상품으로 채워진다.

기본 테스트는1~3라운드다.3라운드 목표를 달성하고 NEXT ROUND를 누르면 테스트 완료 상태가 된다. 라운드 수를 코드에 고정하지 않으며 SO의 Rounds 배열에 항목을 추가하면4라운드 이상으로 늘어난다.

완성 씬은 `Scenes/06_RoundsAndSales.unity`, 기본 설정은 `Data/ThreeRoundPlan.asset`이다. 직접 실습하려면05 씬을 Save As로 복사하고 아래 순서대로 컴포넌트를 붙인다. Bootstrap과 런타임 맵 생성은 추가하지 않는다.

이 단계의 이월은 **같은 Play 실행 안에서 라운드를 넘기는 것**이다. 파일 저장과 재실행 복원은 포함하지 않는다. Stop 후 Play하면1라운드부터 다시 시작한다.

## 1 먼저 기존 동작 확인

05 씬을 실행해 상품 집기→작업대 배치→ENHANCE→TAKE→저장대 배치가 되는지 확인한다. 기본 강화 확률을 사용하고 테스트 성공·파괴 SO를 연결한 상태로 새 라운드 실습을 시작하지 않는다.

기존 Runtime 파일은 변경된 전체 파일을 사용한다. 새 코드는 V2RoundPlan, V2RoundSession이고 기존 V2ClickTarget, V2Interaction, V2Enhancer, V2StatusView에도 선택적 연결이 추가된다. 기존01~05 씬에서는 그 연결이 None이므로 해당 단계의 동작은 유지된다. 같은 이름의 클래스를 추가 파일로 중복 작성하지 않는다.

## 2 가격과 라운드 규칙 결정

| 데이터 | 값 |
| --- | --- |
| 시작 라운드 | 1 |
| 판매액 | 라운드마다0에서 시작 |
| 강화 횟수와 비용 | 무제한, 비용 없음 |
| 공급 | 각 라운드 지정 상품6개, 모두+0 |
| 보관 | 최대6개, 기존 오브젝트와 단계 유지 |
| 다음 라운드 조건 | 현재 판매액≥목표액, 손과 작업대 비어 있음, 연출·시점 전환 중 아님 |
| 최종 라운드 | 배열의 마지막 항목 완료 후 완료 상태 |
| 실패 | 목표 미달이며 공급·손·작업대·보관에 상품이 하나도 없음 |
| 재도전 | 실패 상태에서 R, 같은 라운드의 판매액·시도 수를 초기화하고 리필 |

목표를 달성해도 자동으로 넘어가지 않는다. 남은 공급품을 강화하거나 저장대에 옮길 수 있다. 초과 판매액은 다음 라운드로 이월하지 않는다. 손이나 작업대 상품을 자동 판매·보관하지 않으므로 직접 정리한 뒤 진행한다.

### 판매가

`판매가 = Mathf.RoundToInt(basePrice × priceMultipliers[level])`

| 단계 | +0 | +1 | +2 | +3 | +4 | +5 | +6 | +7 | +8 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 배율 | 1 | 1.8 | 3.2 | 5.5 | 9 | 15 | 25 | 42 | 70 |
| 한손검 가격 | 35 | 63 | 112 | 192 | 315 | 525 | 875 | 1470 | 2450 |

예를 들어+4 한손검315를 팔면1라운드 목표250을 달성한다. +5 한손검525는3라운드 목표526에1 부족하므로 다른 상품을 더 팔아야 한다.

## 3 라운드 SO 만들기

코드: [V2RoundPlan.cs](../Runtime/V2RoundPlan.cs).

```csharp
using System;
using UnityEngine;

namespace ReinforceGameV2
{
    [Serializable]
    public sealed class V2RoundDefinition
    {
        public string title;
        [Min(1)] public int salesTarget = 250;
        public V2Item[] supplyProducts;
    }

    [CreateAssetMenu(menuName = "Reinforce V2/Round Plan")]
    public sealed class V2RoundPlan : ScriptableObject
    {
        public V2RoundDefinition[] rounds;
        public float[] priceMultipliers = { 1, 1.8f, 3.2f, 5.5f, 9, 15, 25, 42, 70 };
        public int RoundCount => rounds == null ? 0 : rounds.Length;
        public int Price(V2Item item) => Mathf.RoundToInt(item.basePrice * priceMultipliers[Mathf.Clamp(item.level, 0, priceMultipliers.Length - 1)]);
        public string Validate(int supplySlotCount, int maxLevel)
        {
            if (RoundCount == 0) return "Add at least one round.";
            if (priceMultipliers == null || priceMultipliers.Length <= maxLevel) return "Provide a price multiplier for every upgrade level.";
            foreach (float price in priceMultipliers)
                if (float.IsNaN(price) || float.IsInfinity(price) || price <= 0) return "Price multipliers must be positive finite values.";
            for (int i = 0; i < RoundCount; i++)
            {
                var row = rounds[i];
                if (row == null || row.salesTarget <= 0) return "Invalid target for round " + (i + 1);
                if (row.supplyProducts == null || row.supplyProducts.Length != supplySlotCount) return "Supply product count must match slots in round " + (i + 1);
                foreach (var product in row.supplyProducts)
                    if (!product || product.basePrice <= 0) return "Invalid product in round " + (i + 1);
            }
            return null;
        }
    }
}
```

### Inspector 작성

1. Project의 Data 폴더에서 Create > Reinforce V2 > Round Plan을 선택한다.
2. 이름을 ThreeRoundPlan으로 한다. 제공된 자산이 있으면 새로 만들지 않고 내용을 확인한다.
3. Rounds Size=3으로 설정한다.
4. 각 항목에 아래 Title과 Sales Target을 입력한다.
5. 각 항목의 Supply Products Size=6으로 설정한다.
6. `Prefabs/Items`의 아래6개 상품을 순서대로 넣는다.
7. Price Multipliers Size=9와 위 가격 배율을 확인한다.

| 배열 index | 화면 라운드 | Title | Sales Target |
| --- | ---: | --- | ---: |
| 0 | 1 | First quota | 250 |
| 1 | 2 | Growing quota | 362 |
| 2 | 3 | Final test quota | 526 |

| 공급 index | Item 프리팹 |
| --- | --- |
| 0 | sword_1handed |
| 1 | axe_1handed |
| 2 | shield_round |
| 3 | staff |
| 4 | dagger |
| 5 | engineer_Wrench |

공급 상품의 개수는 씬의 공급 슬롯 수와 일치해야 한다. 기본 구성은 각 라운드6개다. 라운드별로 다른 상품 조합을 지정할 수 있지만 빈 참조를 넣지 않는다. 모델 원본 대신 V2Item이 붙은 Item 프리팹을 연결한다.

확률은 기존 DefaultBalance, 가격·공급·목표는 ThreeRoundPlan에서 조정한다. 서로 다른 목적의 데이터를 분리해 확률 실험이 라운드 설정을 덮어쓰지 않게 한다.

## 4 SELL과 NEXT ROUND를 월드에 배치

기존 방, 전등, 플레이어, 작업대와 거치대는05 위치를 유지한다. 아래 프리팹2개를 씬에 배치한다. 루트 Scale은1이다.

| 프리팹 | 월드 Position | 월드 Rotation |
| --- | --- | --- |
| Prefabs/World/SellStation | `(1,1.07,1.45)` | `(0,0,0)` |
| Prefabs/World/NextRoundStation | `(0,1.65,2.82)` | `(0,0,0)` |

강화대 자식 DecorativeToolTray는 비활성화한다. 같은 위치의 판매 프리팹과 장식 트레이가 겹치지 않도록 하는 것이다. 강화판·TAKE·ENHANCE는 그대로 둔다.

SellStation에는 Collider를 가진 트레이와 SELL 라벨이 있다. NextRoundStation에는 벽면 스위치와 NEXT ROUND 라벨이 있다. 클릭 액션만 설정한 공용 프리팹이므로 씬의 라운드 관리 참조는 다음 절차에서 연결한다. 씬 인스턴스 참조를 공용 프리팹 자산에 저장하지 않는다.

## 5 라운드 진행 컴포넌트 붙이기

빈 GameObject `RoundSession`을 만들고 V2RoundSession을 붙인다. 위치는 원점으로 둬도 되며 게임 로직은 이 오브젝트 위치를 사용하지 않는다.

| V2RoundSession 필드 | 연결 대상 |
| --- | --- |
| Plan | Data/ThreeRoundPlan.asset |
| Supply Spawner | 공급대의 V2RackSpawner |
| Storage Slots | Size6, 저장대 Slot_0~5의 V2Slot |
| Workbench | 강화대 BenchSlot |
| Interaction | 플레이어의 V2Interaction |
| Enhancer | 강화대의 V2Enhancer |

추가 연결은 다음과 같다.

1. SupplyRack의 V2RackSpawner에서 **Spawn On Start를 끈다**. 이 단계는 라운드 관리자가 공급을 시작한다.
2. V2Interaction의 Rounds에 RoundSession을 넣는다.
3. V2Enhancer의 Rounds에도 같은 RoundSession을 넣는다.
4. SellStation 루트의 V2ClickTarget에서 Action=Sell, Rounds=RoundSession으로 한다.
5. NextRoundStation 루트의 V2ClickTarget에서 Action=NextRound, Rounds=RoundSession으로 한다.
6. 두 대상의 Slot과 Enhancer 필드는 필요 없으므로 None으로 둔다.
7. StatusHUD의 V2StatusView에서 Tutorial Step=6, Rounds=RoundSession으로 한다. 기존 Interaction·Enhancer 연결도 유지한다.

공급대 Products 배열은 앞 단계의 값이 남아 있어도 되지만, 이번 단계의 실사용 목록은 라운드 SO에서 덮어쓴다. 스포너와 라운드 관리자가 각각 Start에서 생성하지 않도록 시작 책임을 한 곳에 모은다. Awake의 방어 코드도 Spawn On Start를 끄지만 Inspector 설정도 명시적으로 맞춘다.

코드: [V2RoundSession.cs](../Runtime/V2RoundSession.cs).

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ReinforceGameV2
{
    public enum V2RoundState { Active, GoalReached, Failed, Completed, ConfigurationError }

    public sealed class V2RoundSession : MonoBehaviour
    {
        public V2RoundPlan plan;
        public V2RackSpawner supplySpawner;
        public V2Slot[] storageSlots;
        public V2Slot workbench;
        public V2Interaction interaction;
        public V2Enhancer enhancer;
        public int RoundNumber { get; private set; } = 1;
        public int Revenue { get; private set; }
        public V2RoundState State { get; private set; } = V2RoundState.ConfigurationError;
        public int Target => plan && RoundNumber <= plan.RoundCount ? plan.rounds[RoundNumber - 1].salesTarget : 0;
        public bool BlocksGameplay => State == V2RoundState.Completed || State == V2RoundState.ConfigurationError;
        public bool IsBusy => (enhancer && enhancer.IsBusy) || (interaction && interaction.fixedView && interaction.fixedView.IsTurning);
        public string Message { get; private set; }
        InputAction retry;

        void Awake() { if (supplySpawner) supplySpawner.spawnOnStart = false; }
        void OnEnable() { retry = new InputAction("Retry round", InputActionType.Button, "<Keyboard>/r"); retry.Enable(); }
        void OnDisable() { retry?.Dispose(); retry = null; }
        void Start()
        {
            string error = ValidateScene();
            if (error != null) { Message = error; Debug.LogError("V2 rounds: " + error, this); return; }
            StartRound(1);
        }
        void Update()
        {
            RefreshState();
            if (retry != null && retry.WasPressedThisFrame()) RetryRound();
        }
        string ValidateScene()
        {
            if (!plan || !supplySpawner || !workbench || !interaction || !enhancer || !enhancer.balance || !enhancer.balance.IsValid) return "Connect round/session references and a valid balance.";
            if (workbench.kind != V2SlotKind.Workbench || enhancer.workbench != workbench || interaction.rounds != this || enhancer.rounds != this) return "Connect the same workbench and session to the player and enhancer.";
            if (supplySpawner.slots == null || supplySpawner.slots.Length == 0 || storageSlots == null || storageSlots.Length == 0) return "Connect supply and storage slots.";
            var used = new HashSet<V2Slot> { workbench };
            foreach (var slot in supplySpawner.slots)
                if (!slot || slot.kind != V2SlotKind.Supply || !used.Add(slot)) return "Invalid or duplicate supply slot.";
            foreach (var slot in storageSlots)
                if (!slot || slot.kind != V2SlotKind.Storage || !used.Add(slot)) return "Invalid or duplicate storage slot.";
            return plan.Validate(supplySpawner.slots.Length, enhancer.balance.MaxLevel);
        }
        void StartRound(int number)
        {
            ClearSupply();
            RoundNumber = number; Revenue = 0;
            enhancer.ResetAttempts();
            supplySpawner.products = plan.rounds[number - 1].supplyProducts;
            supplySpawner.SpawnMissing();
            State = V2RoundState.Active;
            Tell("ROUND " + number + ": supply refilled; stored items retained.");
            RefreshState();
        }
        void ClearSupply()
        {
            foreach (var slot in supplySpawner.slots)
            {
                var item = slot.Occupant;
                if (!item) continue;
                slot.Release(item); item.gameObject.SetActive(false); Destroy(item.gameObject);
            }
        }
        public int Price(V2Item item) => plan.Price(item);
        public bool SellHeld()
        {
            if (BlocksGameplay) return false;
            if (IsBusy || !interaction.Held) { Tell("Carry an item to SELL after the current action finishes."); return false; }
            var item = interaction.Held;
            int price = Price(item);
            if (price > int.MaxValue - Revenue) { Tell("Sales total is too large."); return false; }
            interaction.ConsumeHeld();
            item.gameObject.SetActive(false); Destroy(item.gameObject);
            Revenue += price; RefreshState();
            Tell("SOLD " + price + " / SALES " + Revenue + " of " + Target);
            return true;
        }
        public bool NextRound()
        {
            RefreshState();
            if (BlocksGameplay || IsBusy) return false;
            if (Revenue < Target) { Tell("Reach the sales target first."); return false; }
            if (interaction.Held || workbench.Occupant) { Tell("Sell or store the active item first."); return false; }
            if (RoundNumber == plan.RoundCount)
            {
                State = V2RoundState.Completed;
                Tell("ALL " + plan.RoundCount + " ROUNDS CLEARED. Stored items remain on display.");
                return true;
            }
            StartRound(RoundNumber + 1); return true;
        }
        public bool RetryRound()
        {
            RefreshState();
            if (State != V2RoundState.Failed || IsBusy) return false;
            StartRound(RoundNumber); return true;
        }
        public void RefreshState()
        {
            if (BlocksGameplay) return;
            State = Revenue >= Target ? V2RoundState.GoalReached : LivingItemCount() == 0 ? V2RoundState.Failed : V2RoundState.Active;
        }
        public int LivingItemCount()
        {
            var items = new HashSet<V2Item>();
            if (interaction && interaction.Held) items.Add(interaction.Held);
            if (workbench && workbench.Occupant) items.Add(workbench.Occupant);
            if (supplySpawner && supplySpawner.slots != null) foreach (var slot in supplySpawner.slots) if (slot && slot.Occupant) items.Add(slot.Occupant);
            if (storageSlots != null) foreach (var slot in storageSlots) if (slot && slot.Occupant) items.Add(slot.Occupant);
            return items.Count;
        }
        void Tell(string value) { Message = value; if (interaction) interaction.Message = value; }
    }
}
```

## 6 기존 컴포넌트와 연결되는 부분

### 클릭 액션

[V2ClickTarget.cs](../Runtime/V2ClickTarget.cs)에 Sell과 NextRound가 추가된다. 기존 Slot, Take, Enhance의 enum 순서는 유지해 앞 단계의 직렬화 값을 바꾸지 않는다.

```csharp
using UnityEngine;

namespace ReinforceGameV2
{
    public enum V2ClickAction { Slot, Take, Enhance, Sell, NextRound }
    public sealed class V2ClickTarget : MonoBehaviour
    {
        public V2ClickAction action;
        public V2Slot slot;
        public V2Enhancer enhancer;
        public V2RoundSession rounds;
    }
}
```

[V2Interaction.cs](../Runtime/V2Interaction.cs)의 Use는 두 액션을 라운드 컴포넌트로 보낸다. 판매 후에는 ConsumeHeld로 손 참조와 왼팔 표시를 비우고 상품을 제거한다. 완료·설정 오류에서는 새 집기·배치·판매를 차단한다.

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
        public V2RoundSession rounds;
        public float reach = 8;
        public V2Item Held { get; private set; }
        public string Message { get; set; } = "LMB: pick up / place. RMB: fixed view turn.";
        public bool Locked => (fixedView && fixedView.IsTurning) || (enhancer && enhancer.IsBusy) || (rounds && rounds.BlocksGameplay);
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
            if (target.action == V2ClickAction.Sell || target.action == V2ClickAction.NextRound)
            {
                if (!target.rounds) { Message = "Connect the round session."; return; }
                if (target.action == V2ClickAction.Sell) target.rounds.SellHeld();
                else target.rounds.NextRound();
            }
            else if (target.action == V2ClickAction.Enhance)
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
        public V2Item ConsumeHeld()
        {
            var item = Held; Held = null; if (hands) hands.carrying = false; return item;
        }
    }
}
```

### 강화와 시도 통계

V2Enhancer에는 선택적 rounds 참조와 ResetAttempts가 추가된다. 완료 상태에서 새 강화를 막고 다음 라운드에서는 시도 통계를0으로 한다. 시도 수는 계속 무제한이다. 확률 추첨 방식과 ENHANCE 조작은05와 동일하다.

전체 파일: [V2Enhancer.cs](../Runtime/V2Enhancer.cs). Rounds가 None인05 씬은 이전 방식으로 계속 실행된다.

```csharp
using System.Collections;
using UnityEngine;

namespace ReinforceGameV2
{
    public sealed class V2Enhancer : MonoBehaviour
    {
        public V2Slot workbench;
        public V2EnhancementBalance balance;
        public V2HandRig hands;
        public V2Interaction interaction;
        public Light resultLight;
        public V2RoundSession rounds;
        public bool IsBusy { get; private set; }
        public int Attempts { get; private set; }
        public bool TryEnhance()
        {
            if (IsBusy) return false;
            if (rounds && rounds.BlocksGameplay) return false;
            if (!balance || !balance.IsValid) { Tell("Assign a valid balance asset."); return false; }
            if (!workbench || !workbench.Occupant) { Tell("Place an item on the workbench first."); return false; }
            if (interaction && interaction.Held) { Tell("Place the held item first."); return false; }
            if (workbench.Occupant.level >= balance.MaxLevel) { Tell("Maximum level reached."); return false; }
            StartCoroutine(Strike()); return true;
        }
        IEnumerator Strike()
        {
            IsBusy = true;
            var item = workbench.Occupant;
            float elapsed = 0;
            while (elapsed < .5f)
            {
                elapsed += Time.deltaTime;
                if (hands) hands.strike = Mathf.Sin(Mathf.Clamp01(elapsed / .5f) * Mathf.PI);
                yield return null;
            }
            if (hands) hands.strike = 0;
            if (item && workbench.Occupant == item)
            {
                Attempts++;
                var result = balance.Resolve(item.level, Random.value);
                if (result == V2EnhanceResult.Success)
                { item.level++; Flash(new Color(.2f, 1, .5f)); Tell("SUCCESS  +" + item.level); }
                else if (result == V2EnhanceResult.Downgrade)
                { item.level = Mathf.Max(0, item.level - 1); Flash(new Color(1, .6f, .1f)); Tell("DOWNGRADE  +" + item.level); }
                else
                { workbench.Release(item); Destroy(item.gameObject); Flash(new Color(1, .12f, .07f)); Tell("DESTROYED"); }
            }
            yield return new WaitForSeconds(.35f);
            if (resultLight) resultLight.intensity = 0;
            IsBusy = false;
        }
        void Flash(Color color) { if (resultLight) { resultLight.color = color; resultLight.intensity = 4; } }
        public void ResetAttempts() { if (!IsBusy) Attempts = 0; }
        void Tell(string value) { if (interaction) interaction.Message = value; else Debug.Log(value, this); }
        void OnDisable() { StopAllCoroutines(); IsBusy = false; if (hands) hands.strike = 0; if (resultLight) resultLight.intensity = 0; }
    }
}
```

### HUD

현재 라운드/총 라운드, 판매액/목표액, 상품 가치와 완료·실패 상태를 표시한다. 상품 정보는 작업대가 우선이며 작업대가 비면 손의 상품을 사용한다.

전체 파일: [V2StatusView.cs](../Runtime/V2StatusView.cs).

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace ReinforceGameV2
{
    public sealed class V2StatusView : MonoBehaviour
    {
        public Text label;
        public int tutorialStep = 1;
        public V2RoomLighting lighting;
        public V2Interaction interaction;
        public V2Enhancer enhancer;
        public V2RoundSession rounds;
        void Update()
        {
            if (!label) return;
            string text = "REINFORCE V2 / STEP " + tutorialStep;
            if (rounds)
            {
                text += $" / ROUND {rounds.RoundNumber}/{(rounds.plan ? rounds.plan.RoundCount : 0)} / SALES {rounds.Revenue} / {rounds.Target}";
                if (rounds.State == V2RoundState.Failed) text += "\nTARGET MISSED: no items remain. Press R to retry this round.";
                else if (rounds.State == V2RoundState.GoalReached) text += "\nTARGET REACHED: sell or store active items, then click NEXT ROUND.";
                else if (rounds.State == V2RoundState.Completed || rounds.State == V2RoundState.ConfigurationError) text += "\n" + rounds.Message;
            }
            if (lighting && lighting.blackoutTest) text += "\nBLACKOUT TEST: all interior lights OFF";
            else text += "\nRMB click: FRONT > SUPPLY > FRONT > STORAGE";
            if (interaction) text += "\n" + interaction.Message;
            var item = enhancer && enhancer.workbench ? enhancer.workbench.Occupant : null;
            if (!item && interaction) item = interaction.Held;
            if (item && enhancer && enhancer.balance && enhancer.balance.IsValid)
            {
                text += "\n" + item.displayName + " +" + item.level;
                if (rounds && rounds.plan && rounds.plan.priceMultipliers != null && rounds.plan.priceMultipliers.Length > 0) text += " / VALUE " + rounds.Price(item);
                if (item.level < enhancer.balance.MaxLevel)
                {
                    var odds = enhancer.balance.At(item.level);
                    text += $" / UP {odds.success:P0} DOWN {1-odds.success-odds.destruction:P0} BREAK {odds.destruction:P0}";
                }
                else text += " / MAX LEVEL";
                text += " / STRIKES " + enhancer.Attempts;
            }
            label.text = text;
        }
    }
}
```

## 7 상태 전환을 확인하며 구현 읽기

| 상태 | 조건 | 가능한 진행 |
| --- | --- | --- |
| Active | 목표 미달, 상품 남음 | 집기·강화·판매·보관 |
| GoalReached | 목표액 이상 | 계속 플레이하거나 손·작업대를 비우고 NEXT ROUND |
| Failed | 목표 미달, 생존 상품0 | R로 같은 라운드 재도전 |
| Completed | 마지막 라운드 정리 후 NEXT ROUND | 완료 표시, 시점으로 공간 확인 |
| ConfigurationError | 필수 참조 또는 SO 오류 | Inspector 수정 후 재실행 |

### 판매 트랜잭션

상품 가격 확인→손 참조 해제→상품 비활성화·삭제→판매액 증가→상태 갱신 순서다. 같은 SELL을 다시 눌러도 손에 상품이 없으므로 판매액이 중복으로 올라가지 않는다. 보관 상품을 팔려면 먼저 손으로 꺼내야 한다.

### 다음 라운드

목표 달성·손 비어 있음·작업대 비어 있음·연출 종료를 모두 확인한다. 공급 슬롯의 기존 Occupant를 Release하고 제거한 뒤 새 라운드 공급품을 생성한다. 보관 슬롯을 정리하거나 보관품 프리팹을 새로 만들지 않는다. 따라서 기존 객체·단계·보관 칸이 그대로 이어진다.

새 라운드는 판매액과 시도 수만 초기화한다. 마지막 라운드라면 새 공급을 생성하지 않고 Completed로 전환한다. 별도4라운드 항목이 없는데 인덱스만 증가시키는 방식은 쓰지 않는다.

### 실패와 재도전

공급·작업대·저장대와 손에 있는 상품을 HashSet으로 모아 중복 없이 센다. 저장대에 하나라도 남으면 실패가 아니다. 목표를 이미 채운 상태에서 상품이0개인 경우도 실패가 아니다. R은 진짜 실패 상태에서만 동작하므로 남은 상품을 무한 리필하는 버튼으로 쓸 수 없다.

## 8 세 라운드 플레이 검증

### 1라운드

1. Play하면 Round1/3, Sales0/250, 공급6개, 보관0개인지 확인한다.
2. 목표 미달에서 NEXT ROUND를 누른다. 진행되지 않아야 한다.
3. 상품을 강화해 SELL에 판다. +4 한손검은315여서 첫 목표를 넘는다.
4. 다른 상품을+3 등으로 올려 저장대에 둔다. 그 단계와 칸을 기록한다.
5. 손에 상품이 있거나 작업대에 상품이 남은 상태로 NEXT ROUND를 누른다. 정리 안내와 함께 진행이 막혀야 한다.
6. 손과 작업대를 비우고 NEXT ROUND를 누른다.

### 2라운드

Round2/3, Sales0/362, 시도 수0인지 확인한다. 공급대는6개 모두+0이며 앞 라운드 보관품은 같은 단계와 칸에 남아 있어야 한다. 저장품을 꺼내 판매하거나 다시 강화할 수 있다.

+4 지팡이는450으로2라운드 목표를 넘는다. 목표를 채운 뒤 정리하고 다음 라운드로 간다. 초과 판매액이 다음 라운드에 추가되지 않는지 확인한다.

### 3라운드와 완료

Round3/3, Sales0/526인지 확인한다. +5 한손검525만 팔았다면 진행이 막혀야 한다. 추가 상품을 팔아526이상으로 만든 뒤 손·작업대를 비우고 NEXT ROUND를 누른다.

완료 상태가 표시되어야 한다. 라운드 번호는3에서 멈추고 공급 리필은 일어나지 않는다. 상품 집기와 ENHANCE는 차단되지만 V2 우클릭 시점 전환은 유지된다. 테스트를 다시 시작하려면 Stop 후 Play한다.

### 확률을 고정해 빠르게 시험

05의 Test_Success를 연결하면 목표 단계까지 확실하게 올릴 수 있다. 이번 튜토리얼 기본 검사가 끝나면 DefaultBalance로 되돌린다. 상품 level을 Play 중 Inspector에서 직접 조정하는 방법은 가격·진행 테스트용이며, 강화 성공 검증과 구분한다.

## 9 네 번째 이후 라운드로 확장

1. Stop한다. Play 중 캠페인 구조를 편집하지 않는다.
2. ThreeRoundPlan을 복제해 ExtendedRoundPlan으로 저장한다.
3. Rounds Size를4로 늘린다. 기존 마지막 항목이 복제될 수 있으므로 신규 항목을 직접 확인한다.
4. index3의 Title을Fourth quota, Sales Target을예를 들어750으로 한다.
5. Supply Products6개를 모두 연결한다. 이전 라운드와 다른 조합도 가능하다.
6. 씬의 RoundSession.Plan에 ExtendedRoundPlan을 넣는다.
7. Play하면 총 라운드 수가4가 되고3라운드 완료 후4라운드로 넘어가야 한다.

코드의 숫자3, 종료 분기, 슬롯 루프를 수정할 필요가 없다.5개 이상도 같은 방식으로 배열에 추가한다. 최종 완료는 항상 `RoundNumber == plan.RoundCount`로 결정한다.

현재 공급 슬롯은6개이므로 라운드마다6개를 지정한다. 슬롯 수도 바꾸려면 공급대 프리팹과 Spawner.Slots, 모든 라운드의 Supply Products 길이를 함께 바꾼다. 단순히 라운드만 늘리는 작업과 슬롯 수 변경은 별개다.

목표액을 계속 높이기만 하면 최대 강화 단계·상품 수 때문에 불가능한 목표가 생길 수 있다. 이 구조는 명시적인 유한 라운드 목록을 사용한다. 신규 목표가 준비된 공급품과 보관 전략으로 달성 가능한지 별도 밸런스 테스트한다.

## 10 예제 자산과 검증 결과

제공 자산을 사용해 실습하는 데 생성 도구 실행은 필요 없다. 개별 프리팹 배치와 Inspector 연결이 학습 경로다. 제작 도구 `Editor/V2RoundTutorialBuilder.cs`는05 씬을 읽어06 씬과 판매·진행 프리팹을 만드는 보조 도구다.

Reinforce V2 > Build Step 06 Rounds는06 씬과 해당 프리팹을 덮어쓴다. 기존 ThreeRoundPlan이 있으면 그 자산은 유지한다. 수정한 실습 씬은 다른 이름으로 보관한다. 앞 단계의 Rebuild Tutorial Assets와 구분하며01~05 씬을 다시 만들 필요는 없다.

`Validation/round-play-pass.txt`에는3라운드 완료, 보관 객체 이월, 목표 미달·점유·연출 차단, 실패 재도전, 데이터만 추가한4라운드 확장 검증 항목을 기록한다. 이 결과는 제작 시점 기준이므로 코드와 데이터를 바꾸면 관련 시험을 다시 수행한다.

실행 파일을 만들 때는 Build Profiles의 Scene List에06_RoundsAndSales 씬을 첫 번째 활성 씬으로 등록한다. 앞 단계의 연습 씬과 V1 Bootstrap을 함께 시작시키지 않는다. 게임의 시작 상태는 미리 배치한 RoundSession.Start가 준비한다.

| 최종 확인 | 기대 결과 |
| --- | --- |
| SELL 두 번 누르기 | 손에 있던 상품1개만 판매 |
| +5 한손검 판매 | 525,3라운드 목표526에는 미달 |
| 목표 달성 후 작업대 점유 | 진행 차단 |
| 보관+3 후 다음 라운드 | 같은 상품+3 유지, 공급6개+0 리필 |
| 강화 도중 진행·판매 | 차단 |
| 전 상품 소진, 목표 미달 | Failed, R로 같은 라운드 재시작 |
|3라운드 마지막 진행 | Completed, 자동4라운드 생성 없음 |
|4번째 SO 항목 추가 | 코드 수정 없이4라운드 진행 |
| Stop 후 Play | 파일 저장 없이1라운드부터 재시작 |
