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
