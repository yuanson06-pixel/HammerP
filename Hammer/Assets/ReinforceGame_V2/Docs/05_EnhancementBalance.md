# 05 강화 버튼과 SO 확률 밸런스

## 이번 단계의 결과

04 씬을 Save As하여05 실습 씬을 만든다. 완성 예시는 `Scenes/05_Enhancement.unity`다. 작업대의 상품을 ENHANCE 버튼으로 강화하고 성공·단계 하락·파괴 확률을 ScriptableObject에서 조정한다. 비용과 시도 횟수 제한은 없다.

이 장까지는 강화 기능 검증에 집중한다. 판매·라운드는 다음06단계에서 추가하며, 새 Play에서는 공급 상품을+0으로 다시 생성한다.

## 밸런스 자산

[V2EnhancementBalance.cs](../Runtime/V2EnhancementBalance.cs):

```csharp
using System;
using UnityEngine;

namespace ReinforceGameV2
{
    [Serializable]
    public sealed class V2LevelOdds
    {
        [Range(0, 1)] public float success = .8f;
        [Range(0, 1)] public float destruction = .1f;
    }
    public enum V2EnhanceResult { Success, Downgrade, Destroyed }
    [CreateAssetMenu(menuName = "Reinforce V2/Enhancement Balance")]
    public sealed class V2EnhancementBalance : ScriptableObject
    {
        public V2LevelOdds[] odds = {
            new V2LevelOdds { success=.9f, destruction=.05f },
            new V2LevelOdds { success=.8f, destruction=.08f },
            new V2LevelOdds { success=.7f, destruction=.12f },
            new V2LevelOdds { success=.6f, destruction=.18f },
            new V2LevelOdds { success=.5f, destruction=.25f },
            new V2LevelOdds { success=.4f, destruction=.32f },
            new V2LevelOdds { success=.3f, destruction=.4f },
            new V2LevelOdds { success=.2f, destruction=.5f }
        };
        public int MaxLevel => odds == null ? 0 : odds.Length;
        public bool IsValid
        {
            get
            {
                if (odds == null || odds.Length == 0) return false;
                foreach (var row in odds)
                    if (row == null || float.IsNaN(row.success) || float.IsNaN(row.destruction) || row.success < 0 || row.destruction < 0 || row.success + row.destruction > 1.00001f) return false;
                return true;
            }
        }
        public V2LevelOdds At(int level) => odds[Mathf.Clamp(level, 0, odds.Length - 1)];
        public V2EnhanceResult Resolve(int level, float roll)
        {
            if (!IsValid || level < 0 || level >= MaxLevel) throw new InvalidOperationException("Invalid V2 enhancement data or level.");
            var row = At(level);
            if (roll < row.success) return V2EnhanceResult.Success;
            if (roll < row.success + row.destruction) return V2EnhanceResult.Destroyed;
            return V2EnhanceResult.Downgrade;
        }
    }
}
```

`Data/DefaultBalance.asset`을 사용하거나 Create > Reinforce V2 > Enhancement Balance로 새 자산을 만든다. Odds Size를8로 하고 다음 값을 입력한다.

| 시도 전 단계 | Success | Destruction | 하락 확률 |
| --- | ---: | ---: | ---: |
| +0 | 0.90 | 0.05 | 0.05 |
| +1 | 0.80 | 0.08 | 0.12 |
| +2 | 0.70 | 0.12 | 0.18 |
| +3 | 0.60 | 0.18 | 0.22 |
| +4 | 0.50 | 0.25 | 0.25 |
| +5 | 0.40 | 0.32 | 0.28 |
| +6 | 0.30 | 0.40 | 0.30 |
| +7 | 0.20 | 0.50 | 0.30 |

하락은`1 - Success - Destruction`이다. 두 값의 합이1을 넘으면 데이터 오류로 강화가 차단된다. Odds가8행이면 최대+8이다. 난수 하나를 성공→파괴→나머지 하락의 구간으로 판정한다. 각각을 별도 난수로 추첨하지 않는다.

## 강화대 연결

1. ReinforcementBench 씬 루트에 V2Enhancer를 추가한다.
2. 아래 참조를 연결한다.
3. PlayerEngineer의 V2Interaction.Enhancer에 이 컴포넌트를 넣는다.
4. EnhanceButton의 V2ClickTarget.Action=Enhance를 확인하고 Enhancer에 같은 컴포넌트를 넣는다.
5. StatusHUD의 Tutorial Step=5, Enhancer=같은 V2Enhancer로 설정한다.

| V2Enhancer 필드 | 연결 |
| --- | --- |
| Workbench | 같은 가구의 BenchSlot |
| Balance | Data/DefaultBalance.asset |
| Hands | PlayerEngineer의 V2HandRig |
| Interaction | PlayerEngineer의 V2Interaction |
| Result Light | 아래에서 만드는 Point Light |

강화대 루트 아래에 ResultLight를 만들고 로컬 위치`(0,0.86,-0.1)`에 둔다. Light Type=Point, Range=2, Intensity=0으로 설정한다. 기본 조명 검사 배열에는 넣지 않는다. 결과에만 강도가4가 되며 가구 이동 시 같이 이동한다.

[V2Enhancer.cs](../Runtime/V2Enhancer.cs):

```csharp
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
        public bool IsBusy { get; private set; }
        public int Attempts { get; private set; }
        public bool TryEnhance()
        {
            if (IsBusy) return false;
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
        void Tell(string value) { if (interaction) interaction.Message = value; else Debug.Log(value, this); }
        void OnDisable() { StopAllCoroutines(); IsBusy = false; if (hands) hands.strike = 0; if (resultLight) resultLight.intensity = 0; }
    }
}
```

## 처리 순서

1. 확률 자산·작업대 상품·최대 단계·손 상태를 확인한다.
2. IsBusy=true로 잠그고0.5초간 망치의 strike를 갱신한다.
3. 상품이 여전히 작업대에 있으면 시도 수를1 늘리고 결과를 추첨한다.
4. 성공은+1, 하락은`max(0, level-1)`, 파괴는 상품 제거를 적용한다.
5. 결과 메시지와 조명을0.35초간 표시한다.
6. 조명·strike를 초기화하고 잠금을 푼다.

파괴 시 슬롯을 먼저 Release한 뒤 상품을Destroy한다. 삭제한 상품이 Occupant로 남아 있으면 다음 상품을 놓지 못한다. V2Interaction.Locked는 IsBusy와 시점 전환 상태를 확인해 강화 중 집기·배치·연타를 막는다. 시점 전환 자체는 가능하다. 망치 Collider의 실제 충돌로 결과를 판정하지 않는다.

## 테스트 SO로 검증

| Data 자산 | 전체 단계 성공/파괴 | 기대 결과 |
| --- | --- | --- |
| Test_Success | 1 / 0 | 1회마다+1 |
| Test_Downgrade | 0 / 0 | 1회마다-1, +0 아래로 내려가지 않음 |
| Test_Destroy | 0 / 1 | 1회에 상품 제거 |

1. Stop 상태에서 Balance를Test_Success로 바꾸고 Play한다.
2. 공급 상품을 작업대에 놓고 빈손으로 ENHANCE를 누른다. +1과 성공 메시지를 확인한다.
3. +8까지 올린 뒤 추가 시도에서 단계와 시도 수가 증가하지 않는지 확인한다.
4. **동일 Play 세션에서** Balance 참조를Test_Downgrade로 바꾸고 강화한다. +7이 되어야 한다. V2에는 저장이 없어 Stop하면+0부터 다시 시작한다.
5. +0에서 하락을 반복해도 음수가 되지 않는지 확인한다.
6. Balance를Test_Destroy로 바꾸고 강화한다. 상품이 사라지고 같은 작업대에 다른 상품을 놓을 수 있어야 한다.
7. 강화 중 연타와 집기를 시도한다. 결과1회만 발생해야 한다.
8. Stop하고 Balance를DefaultBalance로 돌린 뒤 씬을 저장한다.

ScriptableObject 내용은 Play 중 수정해도 자산에 남을 수 있다. 실험에는 테스트 자산을 사용한다. 확률 합 오류도 복제한 자산에서 검사하고 기본 자산의 표를 유지한다.

## 최종 확인

| 시험 | 통과 조건 |
| --- | --- |
| 실행 시작 | 방·가구·플레이어는 기존 Hierarchy에 있고 상품6개만 생성 |
| 우클릭 | 0/-75/0/+75의 고정 시점 전환 |
| 집기와 배치 | 공급→손→작업대→손→저장 가능 |
| 강화 | 성공·하락·파괴, 최대 단계와 중복 입력 차단 |
| Stop 후 Play | 실행 상품을 정리하고 기본6개로 재시작 |
| 가구 위치 변경 | 슬롯·조작 면·결과 광원이 가구와 같이 이동 |

저장대는 현재 Play 세션의 상품 거치 공간이다. 다음 라운드나 다음 실행까지의 영구 저장 기능과 구분한다.

## 자산 제작 도구와 독립 실행

다음: [06 판매 목표와 라운드 및 보관품 이월](06_SalesRoundsAndCarryover.md). V2 조작을 유지하며 기본3라운드와 데이터 기반 확장을 구성한다.

일반 실습에서는 Editor 도구를 실행하지 않는다. 제공 프리팹을 Project에서 가져와 각 장의 월드 좌표에 놓고 컴포넌트를 연결한다.

`Editor/V2AssetBuilder.cs`는 V1 Bootstrap의 Editor Preview에서 부품을 분리·복제하고5개 예제 씬을 만드는 제작 도구다. 원본 V1이 있는 환경에서 Reinforce V2 > Rebuild Tutorial Assets를 실행하면 V2 프리팹과 예제 씬을 덮어쓴다. 실습 변경분은 별도 이름으로 저장해 보존한다.

V2 폴더만 다른 URP 프로젝트에 가져와도 실행 씬과 상품은 독립적으로 동작한다. 원본 추출 도구를 다시 실행할 때만 V1이 필요하다. Reinforce V2 > Validate Assets는 V2 씬·프리팹의 Assets 의존성이V2 내부인지 검사한다. Validation의 결과와 카메라 출력은 제작 시점의 확인 자료이며 코드를 바꾸면 관련 항목을 다시 검사한다.
