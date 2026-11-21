# 02 우클릭으로 정해진 각도 전환

## 출발 상태

01에서 정상 조명을 켠 씬을 Save As하여 `02_FixedView.unity`로 저장한다. 비교할 완성 씬은 Scenes/02_FixedView.unity다. 방·전등·플레이어는 그대로 두고 시점 전환 컴포넌트만 추가한다.

## 회전 규칙

마우스 오른쪽 버튼을 **한 번 눌렀을 때** 다음 시점으로 이동한다. 드래그로 자유 회전하지 않는다.

| 현재 순서 index | 목적지 Y각도 | 바라보는 구역 |
| --- | ---: | --- |
| 0 | 0° | 정면 작업대 |
| 1 | -75° | 왼쪽 공급 거치대 |
| 2 | 0° | 정면 작업대 |
| 3 | +75° | 오른쪽 보관 거치대 |

그다음 다시 index0으로 돌아간다. 버튼을 누르고 있어도 연속 전환하지 않는다. 전환 시간은0.25초이며 이동 중의 추가 클릭은 무시한다. 포인터는 잠그지 않는다. 이후 좌클릭으로 상품을 선택할 수 있어야 하기 때문이다.

## 붙일 클래스와 Inspector

1. Hierarchy의 PlayerEngineer **루트**에 V2FixedView를 추가한다.
2. View Rig에 PlayerEngineer 자신의 Transform을 넣는다.
3. Headings의 Size를4로 하고0, -75, 0, 75를 순서대로 입력한다.
4. Transition Seconds를0.25로 한다.
5. StatusHUD의 Tutorial Step을2로 바꾼다.
6. PlayerEngineer의 시작 월드 Rotation은0으로 유지한다.

카메라 자식만 회전시키면 손과 캐릭터는 정면에 남는다. 손과 카메라가 모두 들어 있는 PlayerEngineer 루트를 회전시킨다. 이 루트 아래 별도의 시점 입력 스크립트나 이전 FirstPersonRig를 추가하지 않는다. V2HandRig는 팔 자세만 담당하고 회전 입력은 처리하지 않는다.

코드: [V2FixedView.cs](../Runtime/V2FixedView.cs).

```csharp
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
```

## 구현 순서 이해

1. OnEnable에서 새 Input System의 `<Mouse>/rightButton` Action을 만들고 활성화한다.
2. WasPressedThisFrame으로 누른 첫 프레임만 잡는다.
3. NextView에서 index를 증가시키고 현재 회전과 목적지 회전을 기록한다.
4. Update에서 elapsed를 증가시켜 Quaternion.Slerp로 전환한다.
5. 전환 완료 시 IsTurning을 false로 바꾼다. 04장의 상품 입력은 이 상태를 보고 회전 중 선택을 막는다.
6. OnDisable에서 Action을Dispose해 입력 누적을 방지한다.

위 입력은 PlayerInput과 Input Actions 자산을 필요로 하지 않는다. 기존 V1 입력 컴포넌트와 섞지 않는다.

## 검증

Play 후 Game뷰를 한 번 클릭해 포커스를 준다. 우클릭을 반복하며 Inspector의 PlayerEngineer Y 회전을 확인한다. -75는 Unity의 Euler 표시에서285로 보일 수 있으며 같은 회전이다.

| 시험 | 기대 결과 |
| --- | --- |
| 첫 우클릭 | 왼쪽 -75° |
| 두 번째 | 정면0° |
| 세 번째 | 오른쪽+75° |
| 네 번째 | 정면0° |
| 버튼 길게 누르기 | 한 번만 전환 |
| 전환 중 연타 | 현재 전환 종료 전에는 다음 전환하지 않음 |
| 시점 변경 중 손 | 망치가 화면 오른쪽에 유지 |

아직 거치대를 배치하지 않았으므로 좌우에는 벽만 보인다. 각도를 검사하는 단계이므로 정상이다. 회전이 없으면 Active Input Handling과 Game뷰 포커스, 컴포넌트 Enabled를 확인한다. 카메라가 원점으로 이동한다면 View Rig에 방이나 잘못된 Transform을 연결했는지 확인한다.

완료 후 Stop하고 저장한다. 다음: [03 벽면 피벗 상품 생성](03_PivotSpawn.md).
