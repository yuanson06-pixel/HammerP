using UnityEngine;
using UnityEngine.InputSystem;

namespace ReinforceGameV2
{
    public sealed class V2FixedView : MonoBehaviour
    {
        public Transform viewRig;
        public float[] headings = { 0, -75, 0, 75 };
        [Min(.05f)] public float transitionSeconds = .25f;
        public bool IsTurning { get; private set; }
        public int ViewIndex { get; private set; }
        InputAction turn;
        Quaternion from, to;
        float elapsed;
        void OnEnable()
        {
            turn = new InputAction("Turn", InputActionType.Button, "<Mouse>/rightButton");
            turn.Enable();
            if (!viewRig) viewRig = transform;
            ViewIndex = 0; IsTurning = false;
            if (headings != null && headings.Length > 0) viewRig.localRotation = Quaternion.Euler(0, headings[0], 0);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
        void OnDisable() { turn?.Dispose(); turn = null; IsTurning = false; }
        void Update()
        {
            if (turn.WasPressedThisFrame()) NextView();
            if (!IsTurning) return;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, transitionSeconds));
            viewRig.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, t));
            if (t >= 1) IsTurning = false;
        }
        public void NextView()
        {
            if (IsTurning || headings == null || headings.Length == 0) return;
            ViewIndex = (ViewIndex + 1) % headings.Length;
            from = viewRig.localRotation; to = Quaternion.Euler(0, headings[ViewIndex], 0);
            elapsed = 0; IsTurning = true;
        }
    }
}
