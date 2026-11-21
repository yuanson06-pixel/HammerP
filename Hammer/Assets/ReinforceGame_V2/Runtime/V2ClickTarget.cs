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
