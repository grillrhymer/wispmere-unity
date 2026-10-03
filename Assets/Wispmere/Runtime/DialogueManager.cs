using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wispmere
{
    /// <summary>
    /// Minimal dialogue box: queue of (name, text), click / Interact advances,
    /// OnQueueEmpty fires (GameManager uses it to end the arrival sequence).
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        [Header("Wiring")]
        public GameObject panel;
        public Text nameText;
        public Text bodyText;
        public Button advanceButton;

        public event Action OnQueueOpened;
        public event Action OnQueueEmpty;

        private readonly Queue<Line> _lines = new Queue<Line>();

        private struct Line { public string name; public string text; }

        public bool IsOpen
        {
            get { return _lines.Count > 0; }
        }

        private void Awake()
        {
            if (advanceButton != null)
                advanceButton.onClick.AddListener(Advance);
            panel.SetActive(false);
        }

        public void Show(string name, string text)
        {
            bool wasEmpty = _lines.Count == 0;
            _lines.Enqueue(new Line { name = name, text = text });
            if (wasEmpty && OnQueueOpened != null) OnQueueOpened();
            Render();
        }

        public void Advance()
        {
            if (_lines.Count == 0) return;
            _lines.Dequeue();
            if (_lines.Count == 0)
            {
                panel.SetActive(false);
                if (OnQueueEmpty != null) OnQueueEmpty();
            }
            else Render();
        }

        private void Render()
        {
            var line = _lines.Peek();
            nameText.text = line.name;
            bodyText.text = line.text;
            panel.SetActive(true);
        }
    }
}
