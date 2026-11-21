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
