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
