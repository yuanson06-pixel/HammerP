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
