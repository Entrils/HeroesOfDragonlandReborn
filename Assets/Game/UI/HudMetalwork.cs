using UnityEngine;
using UnityEngine.UIElements;

namespace HeroesOfDragonland.UI
{
    // All coordinates are panel units. The frame and emblems scale with the HUD document.
    internal static class HudMetalwork
    {
        internal static readonly Color Brass = new Color32(185, 152, 95, 255);
        internal static readonly Color Ink = new Color32(17, 20, 26, 239);

        internal static void Plate(VisualElement element)
        {
            element.generateVisualContent += context =>
            {
                Rect r = element.contentRect;
                float w = element.layout.width, h = element.layout.height;
                if (r.width <= 0 || h < 2) return;
                var pen = context.painter2D;
                Bevel(pen, 1, 1, w - 2, h - 2, 7);
                pen.fillColor = Ink;
                pen.Fill();
                pen.lineWidth = 1;
                pen.strokeColor = new Color32(105, 88, 63, 235);
                pen.Stroke();
                pen.BeginPath();
                pen.MoveTo(new Vector2(2, 12));
                pen.LineTo(new Vector2(2, 8));
                pen.LineTo(new Vector2(8, 2));
                pen.LineTo(new Vector2(27, 2));
                pen.strokeColor = Brass;
                pen.Stroke();
            };
        }

        internal static void Bevel(Painter2D pen, float x, float y, float w, float h, float cut)
        {
            cut = Mathf.Min(cut, Mathf.Min(w, h) * .5f);
            pen.BeginPath();
            pen.MoveTo(new Vector2(x + cut, y));
            pen.LineTo(new Vector2(x + w - cut, y));
            pen.LineTo(new Vector2(x + w, y + cut));
            pen.LineTo(new Vector2(x + w, y + h - cut));
            pen.LineTo(new Vector2(x + w - cut, y + h));
            pen.LineTo(new Vector2(x + cut, y + h));
            pen.LineTo(new Vector2(x, y + h - cut));
            pen.LineTo(new Vector2(x, y + cut));
            pen.ClosePath();
        }

        internal static VisualElement Crown()
        {
            var crown = new VisualElement();
            crown.style.width = 22;
            crown.style.height = 16;
            crown.generateVisualContent += context =>
            {
                var p = context.painter2D;
                p.BeginPath();
                p.MoveTo(new Vector2(3, 4));
                p.LineTo(new Vector2(7, 8));
                p.LineTo(new Vector2(11, 2));
                p.LineTo(new Vector2(15, 8));
                p.LineTo(new Vector2(19, 4));
                p.LineTo(new Vector2(17, 12));
                p.LineTo(new Vector2(5, 12));
                p.ClosePath();
                p.fillColor = Brass;
                p.Fill();
            };
            return crown;
        }

        internal static void StyleShortcut(Button button, bool inventory)
        {
            button.style.position = Position.Absolute;
            button.style.right = inventory ? 22 : 82;
            button.style.bottom = 22;
            button.style.width = 54;
            button.style.height = 34;
            button.style.color = Brass;
            button.style.backgroundColor = Ink;
            button.style.unityTextAlign = TextAnchor.MiddleRight;
            button.style.paddingRight = 6;
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.style.position = Position.Absolute;
            icon.style.left = 5;
            icon.style.top = 4;
            icon.style.width = icon.style.height = 24;
            icon.generateVisualContent += context =>
            {
                var p = context.painter2D;
                p.lineWidth = 1.6f;
                p.strokeColor = Brass;
                p.BeginPath();
                if (inventory)
                {
                    p.MoveTo(new Vector2(7, 6)); p.LineTo(new Vector2(17, 6));
                    p.LineTo(new Vector2(15, 10)); p.LineTo(new Vector2(20, 17));
                    p.LineTo(new Vector2(17, 21)); p.LineTo(new Vector2(7, 21));
                    p.LineTo(new Vector2(4, 17)); p.LineTo(new Vector2(9, 10)); p.ClosePath();
                    p.MoveTo(new Vector2(8, 10)); p.LineTo(new Vector2(16, 10));
                }
                else
                {
                    p.MoveTo(new Vector2(12, 2)); p.LineTo(new Vector2(18, 12));
                    p.LineTo(new Vector2(12, 22)); p.LineTo(new Vector2(6, 12)); p.ClosePath();
                    p.MoveTo(new Vector2(12, 6)); p.LineTo(new Vector2(12, 18));
                    p.MoveTo(new Vector2(3, 12)); p.LineTo(new Vector2(21, 12));
                }
                p.Stroke();
            };
            button.Add(icon);
        }
    }

    internal sealed class HudMeter : VisualElement
    {
        private readonly Color tint;
        private float fraction;
        internal float Fraction => fraction;
        internal string Readout { get; private set; }

        internal HudMeter(VisualElement parent, Color tint, int height)
        {
            this.tint = tint;
            style.height = height;
            style.flexShrink = 0;
            style.marginTop = 3;
            generateVisualContent += Paint;
            parent.Add(this);
        }

        internal void Set(float value, float maximum, string readout)
        {
            Readout = readout;
            float next = maximum > 0 ? Mathf.Clamp01(value / maximum) : 0;
            if (fraction == next) return;
            fraction = next;
            MarkDirtyRepaint();
        }

        private void Paint(MeshGenerationContext context)
        {
            float w = contentRect.width, h = contentRect.height;
            if (w < 3 || h < 3) return;
            var p = context.painter2D;
            HudMetalwork.Bevel(p, .5f, .5f, w - 1, h - 1, 3);
            p.fillColor = new Color32(34, 35, 42, 255);
            p.Fill();
            p.strokeColor = new Color32(110, 96, 77, 240);
            p.lineWidth = 1;
            p.Stroke();
            float fill = (w - 3) * fraction;
            if (fill <= 0) return;
            HudMetalwork.Bevel(p, 1.5f, 1.5f, fill, h - 3, 2);
            p.fillColor = tint;
            p.Fill();
            if (fill < 6 || h < 6) return;
            p.BeginPath();
            p.MoveTo(new Vector2(3, 2.5f));
            p.LineTo(new Vector2(fill - 1, 2.5f));
            p.strokeColor = Color.Lerp(tint, Color.white, .46f);
            p.Stroke();
        }
    }
}
