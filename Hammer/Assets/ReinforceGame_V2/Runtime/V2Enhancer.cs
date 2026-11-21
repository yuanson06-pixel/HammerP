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
