using UnityEngine;
using UnityEngine.UI;

namespace Wispmere
{
    /// <summary>
    /// Builds directly selectable, named choices from AppearanceLibrary,
    /// previews each change, then hands off to the existing arrival flow.
    /// </summary>
    public class CreatorUI : MonoBehaviour
    {
        [Header("Wiring")]
        public Transform rowsParent;
        public CreatorRow rowPrefab;
        public InputField nameField;
        public Text warnText;
        public Button beginButton;
        public Transform previewRoot;
        public RawImage previewImage;

        private Appearance _appearance = Appearance.Default();
        private CharacterCustomizer _preview;
        private Camera _previewCamera;
        private RenderTexture _previewTexture;
        private static Font _optionFont;
        private const int PreviewLayer = 31;

        private struct Group
        {
            public string key;
            public string label;
        }

        private static readonly Group[] Groups =
        {
            new Group { key = "body", label = "Body" },
            new Group { key = "skin", label = "Skin" },
            new Group { key = "hair", label = "Hair style" },
            new Group { key = "hairColor", label = "Hair color" },
            new Group { key = "eyes", label = "Eye color" },
            new Group { key = "outfit", label = "Outfit" },
        };

        private static AppearanceOption[] OptionsFor(string key)
        {
            switch (key)
            {
                case "body": return AppearanceLibrary.Bodies;
                case "skin": return AppearanceLibrary.Skins;
                case "hair": return AppearanceLibrary.HairStyles;
                case "hairColor": return AppearanceLibrary.HairColors;
                case "eyes": return AppearanceLibrary.EyeColors;
                default: return AppearanceLibrary.Outfits;
            }
        }

        private string Get(string key)
        {
            switch (key)
            {
                case "body": return _appearance.body;
                case "skin": return _appearance.skin;
                case "hair": return _appearance.hair;
                case "hairColor": return _appearance.hairColor;
                case "eyes": return _appearance.eyes;
                default: return _appearance.outfit;
            }
        }

        private void Set(string key, string id)
        {
            switch (key)
            {
                case "body": _appearance.body = id; break;
                case "skin": _appearance.skin = id; break;
                case "hair": _appearance.hair = id; break;
                case "hairColor": _appearance.hairColor = id; break;
                case "eyes": _appearance.eyes = id; break;
                default: _appearance.outfit = id; break;
            }
        }

        private void OnEnable()
        {
            _appearance = Appearance.Default();
            if (previewRoot != null)
            {
                previewRoot.gameObject.SetActive(true);
                previewRoot.position = new Vector3(0f, -1000f, 0f);
                if (_preview == null)
                {
                    var go = new GameObject("PreviewRig");
                    go.transform.SetParent(previewRoot, false);
                    _preview = go.AddComponent<CharacterCustomizer>();
                }
                _preview.Apply(_appearance);
                SetLayerRecursively(_preview.transform, PreviewLayer);
                EnsurePreviewImage();
                SetupPreviewCamera();
            }

            BuildRows();
            if (warnText != null) warnText.gameObject.SetActive(false);
            if (beginButton != null)
            {
                beginButton.onClick.RemoveAllListeners();
                beginButton.onClick.AddListener(OnBegin);
            }
        }

        private void OnDisable()
        {
            if (_previewCamera != null) _previewCamera.enabled = false;
            if (previewRoot != null) previewRoot.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_previewTexture == null) return;
            _previewTexture.Release();
            Destroy(_previewTexture);
        }

        private void SetupPreviewCamera()
        {
            if (previewRoot == null || previewImage == null) return;

            if (_previewTexture == null)
            {
                _previewTexture = new RenderTexture(768, 1024, 24, RenderTextureFormat.ARGB32)
                {
                    name = "TravelerPreview",
                    antiAliasing = 2
                };
                _previewTexture.Create();
            }

            if (_previewCamera == null)
            {
                var cameraObject = new GameObject("CreatorPreviewCamera");
                cameraObject.transform.SetParent(previewRoot, false);
                cameraObject.transform.localPosition = new Vector3(0f, 1.48f, 3.8f);
                cameraObject.transform.LookAt(previewRoot.TransformPoint(new Vector3(0f, 0.7f, 0f)));
                _previewCamera = cameraObject.AddComponent<Camera>();
                _previewCamera.clearFlags = CameraClearFlags.SolidColor;
                _previewCamera.backgroundColor = new Color(0.19f, 0.25f, 0.22f, 1f);
                _previewCamera.fieldOfView = 32f;
                _previewCamera.nearClipPlane = 0.1f;
                _previewCamera.farClipPlane = 20f;
                _previewCamera.cullingMask = 1 << PreviewLayer;
                _previewCamera.allowHDR = false;
                _previewCamera.allowMSAA = true;
            }

            _previewCamera.targetTexture = _previewTexture;
            _previewCamera.enabled = true;
            previewImage.texture = _previewTexture;
        }

        private void EnsurePreviewImage()
        {
            if (previewImage != null) return;

            var frame = new GameObject("LivePreviewFrame", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            frame.transform.SetParent(transform, false);
            var frameRect = frame.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(470f, 640f);
            frameRect.anchoredPosition = new Vector2(465f, -20f);
            frame.GetComponent<Image>().color = new Color(0.12f, 0.17f, 0.15f, 1f);

            var captionObject = new GameObject("Caption", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            captionObject.transform.SetParent(frame.transform, false);
            var captionRect = captionObject.GetComponent<RectTransform>();
            captionRect.anchorMin = new Vector2(0.5f, 1f);
            captionRect.anchorMax = new Vector2(0.5f, 1f);
            captionRect.pivot = new Vector2(0.5f, 1f);
            captionRect.anchoredPosition = new Vector2(0f, -14f);
            captionRect.sizeDelta = new Vector2(430f, 34f);
            var caption = captionObject.GetComponent<Text>();
            if (_optionFont == null) _optionFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
            caption.font = _optionFont;
            caption.fontSize = 18;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.color = new Color(0.96f, 0.88f, 0.67f, 1f);
            caption.text = "LIVE TRAVELER PREVIEW";
            caption.raycastTarget = false;

            var imageObject = new GameObject("PreviewImage", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage));
            imageObject.transform.SetParent(frame.transform, false);
            var imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.sizeDelta = new Vector2(430f, 560f);
            imageRect.anchoredPosition = new Vector2(0f, -32f);
            previewImage = imageObject.GetComponent<RawImage>();
            previewImage.raycastTarget = false;
        }

        private void BuildRows()
        {
            if (rowsParent == null || rowPrefab == null) return;
            for (int i = rowsParent.childCount - 1; i >= 0; i--)
                Destroy(rowsParent.GetChild(i).gameObject);

            foreach (var group in Groups)
            {
                var row = Instantiate(rowPrefab, rowsParent);
                var options = OptionsFor(group.key);
                row.label.text = group.label;
                EnsureOptionsParent(row);
                var optionLayout = row.optionsParent.GetComponent<HorizontalLayoutGroup>();
                if (optionLayout == null)
                    optionLayout = row.optionsParent.gameObject.AddComponent<HorizontalLayoutGroup>();
                optionLayout.spacing = 6f;
                optionLayout.childAlignment = TextAnchor.MiddleLeft;
                optionLayout.childControlHeight = false;
                optionLayout.childControlWidth = false;
                optionLayout.childForceExpandHeight = false;
                optionLayout.childForceExpandWidth = false;

                float width = row.optionsParent.GetComponent<RectTransform>().rect.width;
                if (width <= 0f) width = 850f;
                float optionWidth = (width - optionLayout.spacing * (options.Length - 1)) / options.Length;
                for (int i = 0; i < options.Length; i++)
                {
                    AppearanceOption option = options[i];
                    string groupKey = group.key;
                    var button = CreateOptionButton(row.optionsParent, option, optionWidth);
                    button.onClick.AddListener(() => Select(groupKey, option, options, row));
                }
                RefreshRow(group.key, options, row);
            }
        }

        private void EnsureOptionsParent(CreatorRow row)
        {
            if (row.optionsParent != null) return;

            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            if (rowLayout != null) rowLayout.spacing = 10f;
            row.label.rectTransform.sizeDelta = new Vector2(130f, 48f);
            for (int i = 0; i < row.transform.childCount; i++)
            {
                var child = row.transform.GetChild(i);
                if (child != row.label.transform) child.gameObject.SetActive(false);
            }

            var options = new GameObject("Options", typeof(RectTransform));
            options.transform.SetParent(row.transform, false);
            options.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 44f);
            row.optionsParent = options.transform;
        }

        private Button CreateOptionButton(Transform parent, AppearanceOption option, float width)
        {
            var go = new GameObject(option.id, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 44f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.16f, 0.23f, 0.2f, 1f);
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            var textObject = new GameObject("OptionLabel", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(go.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(4f, 5f);
            textRect.offsetMax = new Vector2(-4f, -2f);
            var text = textObject.GetComponent<Text>();
            if (_optionFont == null) _optionFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
            text.font = _optionFont;
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = new Color(0.96f, 0.92f, 0.82f, 1f);
            text.raycastTarget = false;

            if (option.hasColor)
            {
                var stripObject = new GameObject("Color", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                stripObject.transform.SetParent(go.transform, false);
                var stripRect = stripObject.GetComponent<RectTransform>();
                stripRect.anchorMin = new Vector2(0f, 0f);
                stripRect.anchorMax = new Vector2(1f, 0f);
                stripRect.pivot = new Vector2(0.5f, 0f);
                stripRect.sizeDelta = new Vector2(0f, 5f);
                var strip = stripObject.GetComponent<Image>();
                strip.color = option.color;
                strip.raycastTarget = false;
            }
            return button;
        }

        private void Select(string key, AppearanceOption option, AppearanceOption[] options, CreatorRow row)
        {
            Set(key, option.id);
            RefreshRow(key, options, row);
            if (_preview != null)
            {
                _preview.Apply(_appearance);
                SetLayerRecursively(_preview.transform, PreviewLayer);
            }
        }

        private void RefreshRow(string key, AppearanceOption[] options, CreatorRow row)
        {
            for (int i = 0; i < row.optionsParent.childCount; i++)
            {
                Transform optionTransform = row.optionsParent.GetChild(i);
                var option = AppearanceLibrary.Find(options, optionTransform.name);
                bool selected = option.id == Get(key);
                var button = optionTransform.GetComponent<Button>();
                button.GetComponent<Image>().color = selected
                    ? new Color(0.84f, 0.68f, 0.39f, 1f)
                    : new Color(0.16f, 0.23f, 0.2f, 1f);
                var text = button.GetComponentInChildren<Text>();
                text.text = (selected ? "✓ " : "") + option.label;
                text.color = selected
                    ? new Color(0.13f, 0.16f, 0.13f, 1f)
                    : new Color(0.96f, 0.92f, 0.82f, 1f);
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        private void OnBegin()
        {
            string name = nameField != null ? nameField.text.Trim() : "";
            if (string.IsNullOrEmpty(name))
            {
                if (warnText != null) warnText.gameObject.SetActive(true);
                if (nameField != null) nameField.ActivateInputField();
                return;
            }
            if (warnText != null) warnText.gameObject.SetActive(false);
            GameManager.Instance.BeginAdventure(name, _appearance);
        }
    }
}
