# 01 검은 방과 플레이어 배치 및 암막 검사

## 이번 단계의 결과

씬에 방, 전등, 플레이어와 카메라를 직접 배치한다. 실행 시 외광이 없는 암막 상태를 먼저 확인하고 실내 조명만 켜서 물체가 보이는지 검사한다. 이 단계에서는 시점 전환, 상품 생성과 클릭 기능을 붙이지 않는다.

완성 예시는 `Scenes/01_BlackRoom.unity`다. 직접 제작하려면 새 빈 씬에서 아래 작업을 하고 같은 이름 또는 별도의 실습 이름으로 저장한다. 제공된 완성 씬을 수정하기 전에는 Save As로 복사하면 원본과 비교할 수 있다.

V2는 `ReinforceGameV2` namespace와 자체 모델·재질을 사용한다. `Runtime` 폴더의 모든 스크립트는 처음부터 프로젝트에 임포트해 컴파일하되, 동작 컴포넌트는 각 단계에 따라 붙인다. 뒤 단계의 타입을 참조하는 코드가 있으므로 파일을 일부만 복사하지 않는다. 파일을 임포트한 것만으로 기능이 실행되지는 않는다. 이 실습에는 Bootstrap 오브젝트가 없다.

## 준비 설정

| 설정 | 값 |
| --- | --- |
| Editor | Unity 6000.0.57f1 |
| 렌더링 | URP 17.0.4 |
| 입력 | Input System 1.14.2 |
| Active Input Handling | Input System Package 또는 Both |
| 공간 단위 | 1unit = 1m |
| 실행 시작 | 현재 실습 씬의 Play |

Project Settings의 Graphics/Quality가 정상 URP Asset을 참조하는지 확인한다. 기본 Directional Light, Camera, Global Volume이 자동 생성된 씬이라면 삭제하고 시작한다. 현재 Scene의 카메라와 AudioListener는 각각1개만 사용한다.

## 프리팹 배치

`Prefabs`에서 아래 자산을 Hierarchy로 끌어 놓는다. 표의 위치는 **월드 좌표**다. 별도 부모를 만들지 않으면 Inspector Position이 곧 월드 위치다. 정리용 부모를 사용한다면 부모 Position/Rotation0, Scale1을 유지한다.

| 프리팹 | 월드 Position | 월드 Rotation | Scale |
| --- | --- | --- | --- |
| World/RoomShell | `(0,0,0)` | `(0,0,0)` | `(1,1,1)` |
| Player/PlayerEngineer | `(0,1.6,-0.45)` | `(0,0,0)` | `(1,1,1)` |
| World/WorkbenchPendant | `(0,2.85,1.2)` | `(0,0,0)` | `(1,1,1)` |
| World/SupplyLamp | `(-1.9,2.3,0.3)` | `(0,0,0)` | `(1,1,1)` |
| World/StorageLamp | `(1.9,2.3,0.3)` | `(0,0,0)` | `(1,1,1)` |
| World/FillLamp | `(0,2.2,-0.7)` | `(0,0,0)` | `(1,1,1)` |
| UI/StatusHUD | 화면 UI, 기본값 유지 | 기본값 유지 | 기본값 유지 |

RoomShell에는 바닥·천장·네 벽과 바닥 이음새가 들어 있다. 방은 가로·세로 약6m이고 천장은 약3.3m다. 출구와 창문은 없다. 모든 부품은 기존 Bootstrap의 Editor Preview 작업실에서 분리한 것이다. 전등 프리팹에는 광원뿐 아니라 해당 전등의 하우징·케이블이 포함된다. Point Light에는 원래 별도 하우징이 없으므로 광원만 포함된다.

방도 부품부터 조립하려면 RoomShell 대신 아래 개별 프리팹을 사용한다. 두 방식을 동시에 배치하면 벽이 겹치므로 하나만 선택한다. 재질은 검은색에 가까운 회색이다. 완전한 RGB0을 쓰지 않아 조명을 켰을 때 형태를 읽을 수 있다.

| 개별 프리팹 | 월드 Position | Y 회전 |
| --- | --- | ---: |
| FloorPanel | `(0,-0.1,0)` | 0 |
| CeilingPanel | `(0,3.3,0)` | 0 |
| WallPanel 왼쪽/오른쪽 | `(-3,1.6,0)` / `(3,1.6,0)` | 0 |
| WallPanel 앞/뒤 | `(0,1.6,3)` / `(0,1.6,-3)` | 90 |

개별 프리팹도 Scale1로 배치한다. WallPanel 내부 메시의 두께0.2m·높이3.2m·길이6m는 자식에 저장되어 있으므로 루트 Scale을 다시 같은 수치로 바꾸지 않는다.

WorkbenchPendant의 루트 회전은0이다. 자식 Spot Light에 아래를 비추는 X90도 회전이 이미 들어 있다. 루트를 또90도 돌리면 벽이나 천장을 비춘다. Point Light 자식의 방향은 밝기에 영향을 주지 않는다.

프리팹의 위치를 옮기면 부품과 광원은 함께 이동한다. 방 배치를 통째로 옮기려면 월드 원점의 빈 World 아래에 방·전등·플레이어를 모두 넣고 World를 이동한다. UI Canvas는 이 정리 대상에서 제외해도 된다.

## 플레이어 내부 확인

PlayerEngineer는 Engineer 전신, 오른손 UpgradeHammer, Camera, AudioListener, V2HandRig를 포함한다. 카메라는 플레이어 루트 기준 위치·회전0, FOV68, Near0.12, Far30이다. 전체 플레이어는 처음부터 씬에 있고 실행 중 생성하지 않는다.

| 레이어 | 대상 | 카메라 표시 |
| --- | --- | --- |
| 0 | 방·가구·상품 | 표시 |
| 30 | 오른팔·망치, 물건을 든 왼팔 | 표시 |
| 31 | 머리·몸통·다리·장식, 빈 왼팔 | 제외 |

Project Settings > Tags and Layers에서30과31에 각각 FirstPersonArms, PlayerBody라는 이름을 붙여도 된다. 코드는 번호를 사용한다. Scene뷰의 Layers에서는 둘 다 표시해 전신을 확인한다. 카메라의 Culling Mask는31만 제외한다. Renderer 자체를 삭제하거나 전신 모델을 비활성화하지 않는다.

V2HandRig는 이미 연결된 양팔 본과 손 소켓을 움직인다. 이 단계에서는 해당 컴포넌트를 새로 작성하거나 시점 입력에 연결할 필요가 없다. 팔 구현 코드는 [V2HandRig.cs](../Runtime/V2HandRig.cs)에 있다. Left Arm은 `Engineer_ArmLeft`, 오른손·왼손 소켓은 HammerGrip·ItemGrip이다. 망치 크기와 소켓 보정은 제공 프리팹에 저장되어 있다.

## 암막 제어 컴포넌트 연결

1. Hierarchy에 빈 `RoomLighting`을 만들고 V2RoomLighting을 붙인다.
2. Blackout Test를 켠다.
3. Interior Lights 배열 Size를4로 한다.
4. 네 전등 프리팹 내부의 Light 컴포넌트를 각각 배열에 넣는다. 프리팹 루트가 빈 오브젝트라면 펼쳐서 실제 Light 자식을 넣는다.
5. StatusHUD의 V2StatusView에서 Tutorial Step=1, Lighting=RoomLighting으로 연결한다. Interaction과 Enhancer는 비운다.

코드는 [V2RoomLighting.cs](../Runtime/V2RoomLighting.cs)에 있다.

```csharp
using UnityEngine;
using UnityEngine.Rendering;

namespace ReinforceGameV2
{
    public sealed class V2RoomLighting : MonoBehaviour
    {
        public bool blackoutTest = true;
        public Light[] interiorLights;
        public Color ambient = new Color(.025f, .03f, .035f);
        void OnEnable() => Apply();
        void OnValidate() { if (Application.isPlaying && isActiveAndEnabled) Apply(); }
        public void Apply()
        {
            RenderSettings.skybox = null;
            RenderSettings.sun = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = blackoutTest ? Color.black : ambient;
            RenderSettings.ambientIntensity = blackoutTest ? 0 : 1;
            RenderSettings.reflectionIntensity = 0;
            RenderSettings.fog = false;
            if (interiorLights == null) return;
            foreach (var lamp in interiorLights) if (lamp) lamp.enabled = !blackoutTest;
        }
    }
}
```

OnEnable에서 실행되므로 컴포넌트를 다시 켜도 현재 설정을 적용한다. Play 중 Inspector의 Blackout Test를 바꾸면 OnValidate가 조명을 갱신한다. 실습 값을 씬에 저장하려면 Stop 후 값을 바꾸고 저장한 다음 다시 Play한다.

## 암막 검사와 정상 조명 검사

### A 모든 조명 끄기

Blackout Test=true로 Play한다. HUD 텍스트를 제외한3D 공간과 손·망치가 어두워야 한다. 메시가 사라진 것이 아니라 빛을 받지 못하는 상태다. Scene뷰에서는 에디터 기본 조명 때문에 보일 수 있다. 암막의 판정 기준은 **Game뷰**다.

게임에서 물체가 밝게 보이면 다른 Directional Light, 활성화된 별도 Light, 발광 재질, Global Volume의 노출·색보정, Reflection Probe를 확인한다. V2RoomLighting은 Skybox와 환경광·반사를 차단하지만 프로젝트에 추가한 임의의 조명 전체를 자동 검색해 끄지는 않는다. Interior Lights 배열에 없는 추가 조명은 직접 비활성화한다.

Lightmap이 있는 다른 씬에서 시작했다면 Lighting 창의 기존 Baked Data도 정리한다. Scene뷰의 Scene Lighting 버튼은 편집 화면만 바꾸며 Game뷰의 암막 여부를 바꾸지 않는다.

### B 실내등만 켜기

Stop 후 Blackout Test=false로 바꾸고 Play한다. 암막은 해제되지만 외광은 여전히 없다. 아래 설정의 실내 조명으로만 공간이 드러나야 한다.

| 광원 | Type | 색 RGB | Intensity | Range | 추가 설정 |
| --- | --- | --- | ---: | ---: | --- |
| WorkbenchPendant 자식 | Spot | `(1,0.73,0.42)` | 9 | 6 | X90°, Angle76° |
| SupplyLamp 자식 | Point | `(1,0.62,0.28)` | 3 | 3.2 | 따뜻한 상품 조명 |
| StorageLamp 자식 | Point | `(0.25,0.7,1)` | 3 | 3.2 | 차가운 보관 조명 |
| FillLamp 자식 | Point | `(0.5,0.6,0.7)` | 0.8 | 4 | 손과 공간 식별용 |

URP Asset의 Additional Lights가 켜져 있어야 Point Light들이 보인다. Forward 렌더러를 사용한다면 Per Object Limit이 여러 실내등을 수용하는지 확인한다. 조명이 부족하다고 Directional Light를 추가하기 전에 Range, 광원 방향, 재질 Base Color와 보조광을 확인한다.

벽 재질은 검정에 가까운 회청색이다. Base Color를 정확한 RGB0으로 만들면 빛이 켜져도 확산광을 거의 반사하지 않는다. 암막은 재질을 완전 검정으로 칠하는 방식이 아니라 외부 광원을 끊는 방식으로 검사한다. Scene뷰 상단의 Scene Lighting 토글은 편집 보기만 바꾸며 실제 게임 조명을 켜지 않는다.

다음 단계로 진행할 실습 씬은 Blackout Test=false로 저장한다. 제공된01 완성 씬은 암막 검사용으로 true이며,02부터는 false다.

## 완료 기준

- Play 전부터 방과 플레이어·카메라·전등이 Hierarchy에 존재한다.
- 암막 검사에서 HUD 외의3D 장면이 검게 보인다.
- 실내등을 켜면 방과 오른손 망치가 보이며 몸통은 보이지 않는다.
- Console에 오류가 없고 카메라·리스너 중복 경고가 없다.

다음: [02 고정 각도 우클릭 회전](02_FixedView.md).
