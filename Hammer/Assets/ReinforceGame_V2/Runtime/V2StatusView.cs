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
