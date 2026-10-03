using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Wispmere
{
    /// <summary>
    /// One gatherable resource. Gather → +amount, visuals hide, regrow timer
    /// → visuals return. Optional tool requirements gate specific resources.
    /// </summary>
    public class ResourceNode : MonoBehaviour
    {
        public static readonly HashSet<ResourceNode> All = new HashSet<ResourceNode>();

        public string kind = "wood";
        public string label = "Fallen Log";
        public int amount = 1;
        public float regrowSeconds = 24f;
        public GatherTool requiredTool;

        public bool IsRipe { get; private set; } = true;

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public void Interact()
        {
            if (!IsRipe) return;
            if (requiredTool != GatherTool.None && !ResourceManager.Instance.HasTool(requiredTool))
            {
                if (GameManager.Instance == null || GameManager.Instance.Dialogue == null)
                {
                    Debug.LogError("[Wispmere] A tool-gated resource node requires the dialogue system.", this);
                    return;
                }
                GameManager.Instance.Dialogue.Show(label, "Requires " + requiredTool + ".");
                return;
            }
            ResourceManager.Instance.Add(kind, amount);
            StartCoroutine(RegrowRoutine());
        }

        private IEnumerator RegrowRoutine()
        {
            IsRipe = false;
            SetVisuals(false);
            yield return new WaitForSeconds(regrowSeconds);
            IsRipe = true;
            SetVisuals(true);
        }

        private void SetVisuals(bool on)
        {
            var own = GetComponent<Renderer>();
            if (own != null) own.enabled = on;
            foreach (Transform child in transform)
                child.gameObject.SetActive(on);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsRipe ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(transform.position, 0.6f);
        }
    }
}
