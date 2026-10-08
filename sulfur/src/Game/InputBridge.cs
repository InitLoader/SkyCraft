using System;
using System.Collections.Generic;
using SulfurCraft.Link;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SulfurCraft.Game
{
    internal sealed class InputBridge : IDisposable
    {
        private readonly SharedLink link;
        private readonly Dictionary<Key, ushort> keys = new Dictionary<Key, ushort>();
        private Keyboard keyboard;
        private bool active;
        public InputBridge(SharedLink link)
        {
            this.link = link;
            for (int n = 0; n < 26; n++) keys[(Key)((int)Key.A + n)] = (ushort)(4 + n);
            for (int n = 0; n < 9; n++) keys[(Key)((int)Key.Digit1 + n)] = (ushort)(30 + n);
            keys[Key.Digit0] = 39;
            keys[Key.Enter] = 40; keys[Key.Escape] = 41; keys[Key.Backspace] = 42; keys[Key.Tab] = 43; keys[Key.Space] = 44;
            keys[Key.Minus] = 45; keys[Key.Equals] = 46; keys[Key.LeftBracket] = 47; keys[Key.RightBracket] = 48;
            keys[Key.Backslash] = 49; keys[Key.Semicolon] = 51; keys[Key.Quote] = 52; keys[Key.Backquote] = 53;
            keys[Key.Comma] = 54; keys[Key.Period] = 55; keys[Key.Slash] = 56;
            for (int n = 0; n < 12; n++) if (n != 8 && n != 9) keys[(Key)((int)Key.F1 + n)] = (ushort)(58 + n);
            keys[Key.Insert] = 73; keys[Key.Home] = 74; keys[Key.PageUp] = 75; keys[Key.Delete] = 76;
            keys[Key.End] = 77; keys[Key.PageDown] = 78; keys[Key.RightArrow] = 79; keys[Key.LeftArrow] = 80; keys[Key.DownArrow] = 81; keys[Key.UpArrow] = 82;
            keys[Key.LeftCtrl] = 224; keys[Key.LeftShift] = 225; keys[Key.LeftAlt] = 226;
            keys[Key.RightCtrl] = 228; keys[Key.RightShift] = 229; keys[Key.RightAlt] = 230;
        }

        public void Update(bool enabled, bool screenOpen, int width, int height)
        {
            if (keyboard != Keyboard.current)
            {
                if (keyboard != null) keyboard.onTextInput -= Text;
                keyboard = Keyboard.current;
                if (keyboard != null) keyboard.onTextInput += Text;
            }
            enabled &= Application.isFocused;
            if (active && !enabled) link.Input(Protocol.ReleaseAll);
            active = enabled;
            if (!active) return;
            if (keyboard != null)
                foreach (var pair in keys)
                {
                    var key = keyboard[pair.Key];
                    if (key.wasPressedThisFrame) link.Input(Protocol.Key, pair.Value, 1);
                    if (key.wasReleasedThisFrame) link.Input(Protocol.Key, pair.Value, 0);
                }
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (screenOpen)
            {
                Vector2 position = mouse.position.ReadValue();
                link.Input(Protocol.Cursor, 0, Mathf.RoundToInt(position.x * width / Math.Max(1, Screen.width)), Mathf.RoundToInt((Screen.height - position.y) * height / Math.Max(1, Screen.height)));
            }
            Button(mouse.leftButton, 1); Button(mouse.middleButton, 2); Button(mouse.rightButton, 3);
            Button(mouse.backButton, 4); Button(mouse.forwardButton, 5);
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0) link.Input(Protocol.Scroll, 0, Mathf.RoundToInt(scroll));
        }
        private void Button(UnityEngine.InputSystem.Controls.ButtonControl button, ushort code)
        {
            if (button.wasPressedThisFrame) link.Input(Protocol.MouseButton, code, 1);
            if (button.wasReleasedThisFrame) link.Input(Protocol.MouseButton, code, 0);
        }
        private void Text(char c) { if (active && !char.IsControl(c) && !char.IsSurrogate(c)) link.Input(Protocol.Text, 0, c); }
        public void Dispose() { if (keyboard != null) keyboard.onTextInput -= Text; link.Input(Protocol.ReleaseAll); }
    }
}
