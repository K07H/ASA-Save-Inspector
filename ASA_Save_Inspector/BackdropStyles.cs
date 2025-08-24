using System;
using WinUIEx;

namespace ASA_Save_Inspector
{
    public class BlurredBackdrop : CompositionBrushBackdrop
    {
        protected override Windows.UI.Composition.CompositionBrush CreateBrush(Windows.UI.Composition.Compositor compositor) => compositor.CreateHostBackdropBrush();
    }

    public class ColorAnimatedBackdrop : CompositionBrushBackdrop
    {
        protected override Windows.UI.Composition.CompositionBrush CreateBrush(Windows.UI.Composition.Compositor compositor)
        {
            var brush = compositor.CreateColorBrush(Windows.UI.Color.FromArgb(255, 255, 0, 0));
            var animation = compositor.CreateColorKeyFrameAnimation();
            var easing = compositor.CreateLinearEasingFunction();
            animation.InsertKeyFrame(0, Windows.UI.Color.FromArgb(50, 52, 21, 57), easing);
            animation.InsertKeyFrame(.5f, Windows.UI.Color.FromArgb(50, 220, 77, 5), easing);
            animation.InsertKeyFrame(1, Windows.UI.Color.FromArgb(50, 52, 21, 57), easing);
            animation.InterpolationColorSpace = Windows.UI.Composition.CompositionColorSpace.Hsl;
            animation.Duration = TimeSpan.FromSeconds(15);
            animation.IterationBehavior = Windows.UI.Composition.AnimationIterationBehavior.Forever;
            brush.StartAnimation("Color", animation);
            return brush;
        }
    }
}
