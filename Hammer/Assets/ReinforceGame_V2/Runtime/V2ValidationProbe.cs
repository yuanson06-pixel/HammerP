#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ReinforceGameV2
{
    public sealed class V2ValidationProbe : MonoBehaviour
    {
        const string Root = "Assets/ReinforceGame_V2";
        readonly List<string> results = new List<string>();
        void Start() { DontDestroyOnLoad(gameObject); StartCoroutine(Guard()); }
        void Check(bool value, string label) { if (!value) throw new Exception(label); results.Add("PASS " + label); }
        IEnumerator Guard()
        {
            var run = Run();
            while (true)
            {
                object next;
                try { if (!run.MoveNext()) break; next = run.Current; }
                catch (Exception exception)
                { File.WriteAllText(Root + "/Validation/play-failure.txt", exception.ToString()); Debug.LogException(exception); EditorApplication.Exit(1); yield break; }
                yield return next;
            }
            File.WriteAllLines(Root + "/Validation/play-pass.txt", results);
            SessionState.SetBool("V2Validation", false); EditorApplication.Exit(0);
        }
        AsyncOperation Load(string name) => EditorSceneManager.LoadSceneAsyncInPlayMode(Root + "/Scenes/" + name + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
        IEnumerator Run()
        {
            yield return null;
            var lighting = FindFirstObjectByType<V2RoomLighting>();
            Check(lighting && lighting.blackoutTest && lighting.interiorLights.All(x => !x.enabled), "01: all interior lights disabled");
            Check(RenderSettings.ambientLight.maxColorComponent == 0 && RenderSettings.reflectionIntensity == 0 && !RenderSettings.skybox, "01: no sky, ambient or reflection light");
            Check(!FindFirstObjectByType<V2FixedView>() && !FindFirstObjectByType<V2RackSpawner>(), "01: later-stage behaviours absent");
            Capture("01_blackout");
            lighting.blackoutTest = false; lighting.Apply();
            Check(lighting.interiorLights.All(x => x.enabled), "01: lamp test restores interior lights");
            yield return null; Capture("01_lit");
            yield return Load("02_FixedView"); yield return null;
            var turn = FindFirstObjectByType<V2FixedView>();
            InputSystem.settings = Instantiate(InputSystem.settings);
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var mouse = InputSystem.AddDevice<Mouse>("V2ValidationMouse");
            InputSystem.EnableDevice(mouse);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return new WaitForSeconds(.4f);
            Check(Mathf.Abs(Mathf.DeltaAngle(turn.viewRig.localEulerAngles.y, -75)) < .1f, "02: RMB input turns to -75 degrees");
            turn.NextView(); yield return new WaitForSeconds(.35f);
            Check(Mathf.Abs(Mathf.DeltaAngle(turn.viewRig.localEulerAngles.y, 0)) < .1f, "02: next fixed view is front");
            turn.NextView(); yield return new WaitForSeconds(.35f);
            Check(Mathf.Abs(Mathf.DeltaAngle(turn.viewRig.localEulerAngles.y, 75)) < .1f, "02: next fixed view is +75 degrees");
            yield return Load("03_PivotSpawn"); yield return null;
            var spawner = FindFirstObjectByType<V2RackSpawner>();
            Check(spawner.SpawnedCount == 6 && spawner.slots.All(x => x.Occupant && x.Occupant.transform.parent == x.anchor), "03: six products spawn at preplaced anchors");
            spawner.SpawnMissing(); Check(FindObjectsByType<V2Item>(FindObjectsSortMode.None).Length == 6, "03: repeated spawn does not duplicate occupied slots");
            turn = FindFirstObjectByType<V2FixedView>(); turn.NextView(); yield return new WaitForSeconds(.35f);
            Capture("03_pivots");
            yield return Load("04_GrabPlace"); yield return null;
            spawner = FindFirstObjectByType<V2RackSpawner>();
            var input = FindFirstObjectByType<V2Interaction>();
            var bench = FindObjectsByType<V2Slot>(FindObjectsSortMode.None).First(x => x.kind == V2SlotKind.Workbench);
            var storage = FindObjectsByType<V2Slot>(FindObjectsSortMode.None).First(x => x.kind == V2SlotKind.Storage);
            var item = spawner.slots[0].Occupant;
            turn = FindFirstObjectByType<V2FixedView>(); turn.NextView(); yield return new WaitForSeconds(.35f);
            Vector2 pointer = input.view.WorldToScreenPoint(item.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }.WithButton(MouseButton.Left));
            yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }); yield return null;
            Check(input.Held == item && !spawner.slots[0].Occupant && item.transform.parent == input.handSocket, "04: LMB raycast releases supply and grips to hand");
            Check(!input.Place(spawner.slots[0]) && input.Held == item, "04: supply return rejected without losing held item");
            Check(input.Place(bench) && bench.Occupant == item && !input.Held, "04: hand to workbench docking");
            Check(input.Pickup(item) && input.Place(storage) && storage.Occupant == item && !bench.Occupant, "04: workbench to storage docking");
            var other = spawner.slots[1].Occupant;
            input.Pickup(other); Check(!input.Place(storage) && input.Held == other && storage.Occupant == item, "04: occupied storage rejects placement");
            input.Place(bench);
            Check(!FindFirstObjectByType<V2Enhancer>(), "04: enhancement controller not attached yet");
            turn.NextView(); yield return new WaitForSeconds(.35f); turn.NextView(); yield return new WaitForSeconds(.35f);
            Capture("04_storage");
            yield return Load("05_Enhancement"); yield return null;
            input = FindFirstObjectByType<V2Interaction>();
            var enhancer = FindFirstObjectByType<V2Enhancer>(); spawner = FindFirstObjectByType<V2RackSpawner>();
            item = spawner.slots[0].Occupant; input.Pickup(item); input.Place(enhancer.workbench);
            var testBalance = Instantiate(enhancer.balance); enhancer.balance = testBalance;
            foreach (var row in testBalance.odds) { row.success = 1; row.destruction = 0; }
            var button = enhancer.GetComponentsInChildren<V2ClickTarget>().First(x => x.action == V2ClickAction.Enhance);
            input.Use(button); Check(enhancer.IsBusy && !input.Pickup(item), "05: button starts strike and locks pickup");
            Check(!enhancer.TryEnhance(), "05: busy strike ignores repeated attempts");
            yield return new WaitForSeconds(1);
            Check(item.level == 1 && enhancer.Attempts == 1, "05: success balance upgrades one level");
            foreach (var row in testBalance.odds) { row.success = 0; row.destruction = 0; }
            enhancer.TryEnhance(); yield return new WaitForSeconds(1);
            Check(item.level == 0, "05: downgrade balance decreases level");
            enhancer.TryEnhance(); yield return new WaitForSeconds(1);
            Check(item.level == 0, "05: downgrade floor is zero");
            item.level = testBalance.MaxLevel;
            Check(!enhancer.TryEnhance(), "05: maximum level rejects strike"); item.level = 0;
            foreach (var row in testBalance.odds) row.destruction = 1;
            enhancer.TryEnhance(); yield return new WaitForSeconds(1);
            Check(!enhancer.workbench.Occupant && FindObjectsByType<V2Item>(FindObjectsSortMode.None).Length == 5, "05: destruction clears workbench occupancy");
            testBalance.odds[0].success = .8f; testBalance.odds[0].destruction = .4f;
            Check(!testBalance.IsValid && !enhancer.TryEnhance(), "05: invalid balance is rejected");
            enhancer.balance = AssetDatabase.LoadAssetAtPath<V2EnhancementBalance>(Root + "/Data/DefaultBalance.asset");
            Destroy(testBalance);
            item = spawner.slots[1].Occupant; input.Pickup(item); input.Place(enhancer.workbench);
            yield return null; Capture("05_workbench");
            Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1 && !FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(x => x && x.GetType().Namespace == "ReinforceGame"), "05: one scene camera and no V1 bootstrap/controller dependency");
        }
        void Capture(string name)
        {
            var camera = FindFirstObjectByType<Camera>(); var canvas = FindFirstObjectByType<Canvas>();
            if (canvas) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .3f; }
            var target = new RenderTexture(1280, 720, 24); target.Create();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var before = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes(Root + "/Validation/" + name + ".png", pixels.EncodeToPNG());
            RenderTexture.active = before; target.Release(); Destroy(target); Destroy(pixels);
            if (canvas) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
    }
}
#endif
