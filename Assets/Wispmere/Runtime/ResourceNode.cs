using System.Collections;
using System.Collections.Generic;
using System;
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
        public float interactionRadius = 1.7f;
        public GatherTool requiredTool;
        public string persistenceId;

        public bool IsRipe { get; private set; } = true;
        public long RegrowsAtUtcTicks { get { return IsRipe ? 0L : _regrowsAtUtcTicks; } }

        private long _regrowsAtUtcTicks;
        private Coroutine _regrowRoutine;
        private void OnEnable() { All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        public string GetPersistenceId()
        {
            return string.IsNullOrEmpty(persistenceId) ? gameObject.name : persistenceId;
        }

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
            BeginRegrow(TimeSpan.FromSeconds(regrowSeconds).Ticks);
            if (GameManager.Instance != null)
                GameManager.Instance.SaveGame();
        }

        public void RestoreDepletion(long regrowsAtUtcTicks)
        {
            long remainingTicks = regrowsAtUtcTicks - DateTime.UtcNow.Ticks;
            if (remainingTicks <= 0L) return;
            BeginRegrow(remainingTicks);
        }

        private void BeginRegrow(long remainingTicks)
        {
            IsRipe = false;
            _regrowsAtUtcTicks = DateTime.UtcNow.Ticks + remainingTicks;
            SetVisuals(false);
            if (_regrowRoutine != null) StopCoroutine(_regrowRoutine);
            _regrowRoutine = StartCoroutine(RegrowRoutine(remainingTicks));
        }

        private IEnumerator RegrowRoutine(long remainingTicks)
        {
            yield return new WaitForSeconds((float)TimeSpan.FromTicks(remainingTicks).TotalSeconds);
            IsRipe = true;
            _regrowsAtUtcTicks = 0L;
            _regrowRoutine = null;
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
