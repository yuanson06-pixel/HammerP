using System;
using UnityEngine;

namespace ReinforceGameV2
{
    [Serializable]
    public sealed class V2LevelOdds
    {
        [Range(0, 1)] public float success = .8f;
        [Range(0, 1)] public float destruction = .1f;
    }
    public enum V2EnhanceResult { Success, Downgrade, Destroyed }
    [CreateAssetMenu(menuName = "Reinforce V2/Enhancement Balance")]
    public sealed class V2EnhancementBalance : ScriptableObject
    {
        public V2LevelOdds[] odds = {
            new V2LevelOdds { success=.9f, destruction=.05f },
            new V2LevelOdds { success=.8f, destruction=.08f },
            new V2LevelOdds { success=.7f, destruction=.12f },
            new V2LevelOdds { success=.6f, destruction=.18f },
            new V2LevelOdds { success=.5f, destruction=.25f },
            new V2LevelOdds { success=.4f, destruction=.32f },
            new V2LevelOdds { success=.3f, destruction=.4f },
            new V2LevelOdds { success=.2f, destruction=.5f }
        };
        public int MaxLevel => odds == null ? 0 : odds.Length;
        public bool IsValid
        {
            get
            {
                if (odds == null || odds.Length == 0) return false;
                foreach (var row in odds)
                    if (row == null || float.IsNaN(row.success) || float.IsNaN(row.destruction) || row.success < 0 || row.destruction < 0 || row.success + row.destruction > 1.00001f) return false;
                return true;
            }
        }
        public V2LevelOdds At(int level) => odds[Mathf.Clamp(level, 0, odds.Length - 1)];
        public V2EnhanceResult Resolve(int level, float roll)
        {
            if (!IsValid || level < 0 || level >= MaxLevel) throw new InvalidOperationException("Invalid V2 enhancement data or level.");
            var row = At(level);
            if (roll < row.success) return V2EnhanceResult.Success;
            if (roll < row.success + row.destruction) return V2EnhanceResult.Destroyed;
            return V2EnhanceResult.Downgrade;
        }
    }
}
