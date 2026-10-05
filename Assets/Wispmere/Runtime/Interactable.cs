using System.Collections.Generic;
using UnityEngine;

namespace Wispmere
{
    /// <summary>
    /// A readable placeholder interaction (town sign, fountain, or workshop).
    /// The restoration target also advances the first material-gathering
    /// objective when discovered.
    /// Radius is in Unity units (converted from layout px by TownBuilder).
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        public static readonly HashSet<Interactable> All = new HashSet<Interactable>();

        [TextArea] public string label = "Old Sign";
        [TextArea] public string text = "…";
        public float radius = 1.6f;
        public bool isRestorationTarget;
        public bool isWorkbench;

        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public void Interact()
        {
            if (isWorkbench)
            {
                GameManager.Instance.OpenWorkbench(transform);
                return;
            }
            if (isRestorationTarget)
                GameManager.Instance.DiscoverRestorationTarget(this);
            GameManager.Instance.Dialogue.Show(label, text);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isRestorationTarget ? Color.yellow : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
