using System.Collections.Generic;
using UnityEngine;

// 살아있는 유닛의 렌더러 머티리얼을 일시적으로 반투명 흰색으로 바꿨다가 복원하는 범용 컴포넌트 (doc/0323).
// PreviewSystem의 건물 배치 고스트 머티리얼과 같은 기법이지만, 임시 프리뷰 오브젝트가 아니라 살아있는
// 유닛 자신의 원본 머티리얼을 보존/복원해야 하므로 별도 컴포넌트로 둔다.
public class StealthVisual : MonoBehaviour
{
    [SerializeField] private Material stealthMaterial; // 반투명 흰색 (PreviewSystem.previewMaterialPrefab과 같은 에셋 재사용 가능)

    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

    public void EnterStealth()
    {
        if (stealthMaterial == null)
            return;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            originalMaterials[renderer] = renderer.materials;

            Material[] ghosts = new Material[renderer.materials.Length];
            for (int i = 0; i < ghosts.Length; i++)
                ghosts[i] = stealthMaterial;

            renderer.materials = ghosts;
        }
    }

    public void ExitStealth()
    {
        foreach (KeyValuePair<Renderer, Material[]> pair in originalMaterials)
        {
            if (pair.Key != null)
                pair.Key.materials = pair.Value;
        }

        originalMaterials.Clear();
    }
}
