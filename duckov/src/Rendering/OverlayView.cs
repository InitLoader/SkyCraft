using DuckovCraft.Link;
using UnityEngine;
using UnityEngine.UI;

namespace DuckovCraft.Rendering
{
    internal sealed class OverlayView : System.IDisposable
    {
        private readonly GameObject root;
        private readonly RawImage image;
        private Texture2D texture;
        public int Frames { get; private set; }

        public OverlayView(BridgeAssets assets)
        {
            root = new GameObject("DuckovCraft overlay", typeof(Canvas));
            Object.DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var child = new GameObject("Minecraft hand and inventory", typeof(RectTransform), typeof(RawImage));
            child.transform.SetParent(root.transform, false);
            image = child.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.material = assets.Overlay;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.SetActive(false);
        }

        public void Update(SharedLink link, bool visible)
        {
            root.SetActive(visible);
            if (!visible || !link.OverlayFrame(out int width, out int height, out bool bottomUp, out byte[] data)) return;
            if (texture == null || texture.width != width || texture.height != height)
            {
                if (texture != null) Object.Destroy(texture);
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
                image.texture = texture;
            }
            texture.LoadRawTextureData(data);
            texture.Apply(false, false);
            image.uvRect = bottomUp ? new Rect(0, 0, 1, 1) : new Rect(0, 1, 1, -1);
            Frames++;
        }

        public void Dispose()
        {
            Object.Destroy(root);
            if (texture != null) Object.Destroy(texture);
        }
    }
}
