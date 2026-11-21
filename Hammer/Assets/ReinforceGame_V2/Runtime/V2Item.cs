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
