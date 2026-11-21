#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ReinforceGameV2
{
    public sealed class V2RoundValidationProbe : MonoBehaviour
    {
        const string Root = "Assets/ReinforceGame_V2";
        readonly List<string> results = new List<string>();
        void Start() { DontDestroyOnLoad(gameObject); StartCoroutine(Guard()); }
        void Check(bool value, string name) { if (!value) throw new Exception(name); results.Add("PASS " + name); }
        IEnumerator Guard()
        {
            foreach (string name in new[] { "round-play-pass.txt", "round-play-failure.txt" })
                if (File.Exists(Root + "/Validation/" + name)) File.Delete(Root + "/Validation/" + name);
            var run = Run();
            while (true)
            {
                object next;
                try { if (!run.MoveNext()) break; next = run.Current; }
                catch (Exception error) { File.WriteAllText(Root + "/Validation/round-play-failure.txt", error.ToString()); Debug.LogException(error); EditorApplication.Exit(1); yield break; }
                yield return next;
            }
            File.WriteAllLines(Root + "/Validation/round-play-pass.txt", results);
            SessionState.SetBool("V2RoundValidation", false); EditorApplication.Exit(0);
        }
        AsyncOperation Reload() => EditorSceneManager.LoadSceneAsyncInPlayMode(Root + "/Scenes/06_RoundsAndSales.unity", new LoadSceneParameters(LoadSceneMode.Single));
        IEnumerator Run()
        {
            yield return null;
            var session = FindFirstObjectByType<V2RoundSession>();
            var input = session.interaction; var spawner = session.supplySpawner;
            var hud = FindFirstObjectByType<V2StatusView>();
            Check(hud.rounds == session && hud.tutorialStep == 6, "Step six HUD references survive saving and reopening the scene");
            Check(session.plan.RoundCount == 3 && session.RoundNumber == 1 && session.Target == 250 && session.Revenue == 0 && session.LivingItemCount() == 6, "Three-round plan initializes six products at round one");
            Check(!spawner.spawnOnStart, "Session owns spawning and disables independent Start spawn");
            Check(!session.NextRound(), "Next round is blocked below target");
            var item = spawner.slots[0].Occupant; item.level = 4;
            Check(session.Price(item) == 315, "Price uses base price and level multiplier");
            input.Pickup(item);
            var sellTarget = FindObjectsByType<V2ClickTarget>(FindObjectsSortMode.None).First(x => x.action == V2ClickAction.Sell);
            input.Use(sellTarget);
            Check(!input.Held && session.Revenue == 315 && session.State == V2RoundState.GoalReached, "SELL action consumes held item and reaches the quota");
            input.Use(sellTarget); Check(session.Revenue == 315, "Repeated SELL cannot duplicate revenue");
            var stored = spawner.slots[1].Occupant; stored.level = 3; input.Pickup(stored);
            Check(!session.NextRound(), "Held item blocks advancing even after quota reached");
            input.Place(session.storageSlots[0]);
            var pending = spawner.slots[2].Occupant; input.Pickup(pending); input.Place(session.workbench);
            Check(!session.NextRound(), "Workbench item blocks advancing");
            var balance = Instantiate(session.enhancer.balance); foreach (var row in balance.odds) { row.success = 1; row.destruction = 0; }
            var originalBalance = session.enhancer.balance; session.enhancer.balance = balance;
            Check(session.enhancer.TryEnhance() && !session.NextRound() && !session.SellHeld(), "Strike busy state blocks sales and round transitions");
            yield return new WaitForSeconds(1);
            Check(session.enhancer.Attempts == 1, "Attempts recorded before round reset");
            session.enhancer.balance = originalBalance; Destroy(balance);
            input.Pickup(pending); session.SellHeld();
            Check(session.NextRound() && session.RoundNumber == 2 && session.Target == 362 && session.Revenue == 0 && session.enhancer.Attempts == 0, "Round two resets sales and attempts");
            yield return null;
            Check(session.storageSlots[0].Occupant == stored && stored.level == 3 && spawner.slots.All(x => x.Occupant && x.Occupant.level == 0) && session.LivingItemCount() == 7, "Stored object identity and level survive while six supplies refill");
            item = spawner.slots[3].Occupant; item.level = 4; input.Pickup(item); Check(session.SellHeld() && session.Revenue == 450, "Round two item sold for level value");
            Check(session.NextRound() && session.RoundNumber == 3 && session.Target == 526, "Round three uses its data-defined target");
            yield return null;
            item = spawner.slots[0].Occupant; item.level = 5; input.Pickup(item); session.SellHeld();
            Check(session.Revenue == 525 && !session.NextRound(), "Target comparison rejects 525 against 526");
            input.Pickup(spawner.slots[4].Occupant); session.SellHeld();
            Check(session.Revenue == 550 && session.NextRound() && session.State == V2RoundState.Completed && session.RoundNumber == 3, "Third quota completes the configured plan without spawning round four");
            Check(input.Locked && !session.enhancer.TryEnhance() && !session.SellHeld(), "Completion locks item actions and enhancement");
            Check(session.Message.Contains("ROUNDS CLEARED"), "Completion message survives rejected terminal actions");
            yield return null;
            Capture("06_three-round-complete", session);
            yield return Reload(); yield return null;
            session = FindFirstObjectByType<V2RoundSession>(); input = session.interaction; spawner = session.supplySpawner;
            var extended = Instantiate(session.plan);
            foreach (var row in extended.rounds) row.salesTarget = 1;
            Array.Resize(ref extended.rounds, 4);
            extended.rounds[3] = new V2RoundDefinition { title = "Fourth test round", salesTarget = 1, supplyProducts = spawner.products.ToArray() };
            session.plan = extended;
            Check(extended.Validate(6, session.enhancer.balance.MaxLevel) == null, "Fourth round added through plan data passes validation");
            for (int number = 1; number <= 4; number++)
            {
                input.Pickup(spawner.slots[0].Occupant); Check(session.SellHeld(), "Extended plan sale in round " + number);
                Check(session.NextRound(), "Extended plan transition in round " + number); yield return null;
                if (number < 4) Check(session.RoundNumber == number + 1 && session.State != V2RoundState.Completed, "Extended plan continues to round " + (number + 1));
            }
            Check(session.State == V2RoundState.Completed && session.RoundNumber == 4, "Appended fourth round completes without controller changes");
            Destroy(extended);
            yield return Reload(); yield return null;
            session = FindFirstObjectByType<V2RoundSession>();
            foreach (var slot in session.supplySpawner.slots)
            { var dead = slot.Occupant; slot.Release(dead); Destroy(dead.gameObject); }
            session.RefreshState(); Check(session.State == V2RoundState.Failed, "No remaining items below quota is a failed round");
            Check(session.RetryRound() && session.RoundNumber == 1 && session.Revenue == 0 && session.LivingItemCount() == 6, "Retry refills the same round without retaining old sales");
            var invalid = Instantiate(session.plan); invalid.rounds[0].supplyProducts = new V2Item[0];
            Check(invalid.Validate(6, session.enhancer.balance.MaxLevel) != null, "Round data rejects a mismatched supply array"); Destroy(invalid);
            yield return null;
            Capture("06_round-one", session);
        }
        static void Capture(string name, V2RoundSession session)
        {
            var camera = session.interaction.view; var canvas = FindFirstObjectByType<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .3f;
            var target = new RenderTexture(1280, 720, 24); target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var old = RenderTexture.active; RenderTexture.active = target; var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); File.WriteAllBytes(Root + "/Validation/" + name + ".png", pixels.EncodeToPNG());
            RenderTexture.active = old; target.Release(); Destroy(target); Destroy(pixels); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
#endif
