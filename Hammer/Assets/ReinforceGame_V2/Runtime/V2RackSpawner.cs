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
