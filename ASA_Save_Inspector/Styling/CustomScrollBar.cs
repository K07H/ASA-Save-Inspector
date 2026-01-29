using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.WinUI.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace ASA_Save_Inspector.Styling
{
    public partial class CustomScrollBar : DependencyObject
    {
        public static readonly DependencyProperty LargeVerticalScrollBarProperty = DependencyProperty.RegisterAttached("LargeVerticalScrollBar", typeof(bool), typeof(CustomScrollBar),
            new PropertyMetadata(false, (d, e) =>
            {
                if (d is FrameworkElement frameworkElement && e.NewValue is bool largeVerticalScrollBar)
                    OnLargeScrollBarPropertyChanged(frameworkElement, Orientation.Vertical, largeVerticalScrollBar);
            }));

        public static readonly DependencyProperty LargeHorizontalScrollBarProperty = DependencyProperty.RegisterAttached("LargeHorizontalScrollBar", typeof(bool), typeof(CustomScrollBar),
            new PropertyMetadata(false, (d, e) =>
            {
                if (d is FrameworkElement frameworkElement && e.NewValue is bool largeHorizontalScrollBar)
                    OnLargeScrollBarPropertyChanged(frameworkElement, Orientation.Horizontal, largeHorizontalScrollBar);
            }));

        public static readonly DependencyProperty KeepVerticalExpandedProperty = DependencyProperty.RegisterAttached("KeepVerticalExpanded", typeof(bool), typeof(CustomScrollBar),
            new PropertyMetadata(false, (d, e) =>
            {
                if (d is FrameworkElement frameworkElement && e.NewValue is bool keepVerticalExpanded)
                    OnKeepExpandedPropertyChanged(frameworkElement, Orientation.Vertical, keepVerticalExpanded);
            }));

        public static readonly DependencyProperty KeepHorizontalExpandedProperty = DependencyProperty.RegisterAttached("KeepHorizontalExpanded", typeof(bool), typeof(CustomScrollBar),
            new PropertyMetadata(false, (d, e) =>
            {
                if (d is FrameworkElement frameworkElement && e.NewValue is bool keepHorizontalExpanded)
                    OnKeepExpandedPropertyChanged(frameworkElement, Orientation.Horizontal, keepHorizontalExpanded);
            }));

        public static bool GetLargeVerticalScrollBar(DependencyObject obj) => (bool)obj.GetValue(LargeVerticalScrollBarProperty);

        public static void SetLargeVerticalScrollBar(DependencyObject obj, bool value) => obj.SetValue(LargeVerticalScrollBarProperty, value);

        public static bool GetLargeHorizontalScrollBar(DependencyObject obj) => (bool)obj.GetValue(LargeHorizontalScrollBarProperty);

        public static void SetLargeHorizontalScrollBar(DependencyObject obj, bool value) => obj.SetValue(LargeHorizontalScrollBarProperty, value);

        public static bool GetKeepVerticalExpanded(DependencyObject obj) => (bool)obj.GetValue(KeepVerticalExpandedProperty);

        public static void SetKeepVerticalExpanded(DependencyObject obj, bool value) => obj.SetValue(KeepVerticalExpandedProperty, value);

        public static bool GetKeepHorizontalExpanded(DependencyObject obj) => (bool)obj.GetValue(KeepHorizontalExpandedProperty);

        public static void SetKeepHorizontalExpanded(DependencyObject obj, bool value) => obj.SetValue(KeepHorizontalExpandedProperty, value);

        private static List<RemovingVisualState> RemovedVisualStates { get; } = new();

        private record RemovingVisualState(VisualStateGroup VisualStateGroup, VisualState VisualState);

        private static void OnLargeScrollBarPropertyChanged(FrameworkElement largeScrollBarTarget, Orientation orientation, bool largeScrollBar)
        {
            largeScrollBarTarget.Loaded -= LargeScrollBarTarget_Loaded;
            largeScrollBarTarget.Loaded += LargeScrollBarTarget_Loaded;

            //largeScrollBarTarget.LayoutUpdated -= LargeScrollBarTarget_LayoutUpdated;
            //largeScrollBarTarget.LayoutUpdated += LargeScrollBarTarget_LayoutUpdated;

            ApplyLargeScrollBar(largeScrollBarTarget, orientation, largeScrollBar);
        }

        private static void OnKeepExpandedPropertyChanged(FrameworkElement keepExpandedTarget, Orientation orientation, bool keepExpanded)
        {
            keepExpandedTarget.Loaded -= KeepExpandedTarget_Loaded;
            keepExpandedTarget.Loaded += KeepExpandedTarget_Loaded;

            ApplyKeepExpanded(keepExpandedTarget, orientation, keepExpanded);
        }

        private static void LargeScrollBarTarget_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement target)
            {
                target.Loaded -= LargeScrollBarTarget_Loaded;
                ApplyLargeScrollBar(target);
            }
        }

        private static void KeepExpandedTarget_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement target)
            {
                target.Loaded -= KeepExpandedTarget_Loaded;
                ApplyKeepExpanded(target);
            }
        }

        private static void ApplyLargeScrollBar(FrameworkElement target)
        {
            if (GetLargeVerticalScrollBar(target) is bool largeVerticalScrollBar)
                ApplyLargeScrollBar(target, Orientation.Vertical, largeVerticalScrollBar);
            if (GetLargeHorizontalScrollBar(target) is bool largeHorizontalScrollBar)
                ApplyLargeScrollBar(target, Orientation.Horizontal, largeHorizontalScrollBar);
        }

        private static void ApplyKeepExpanded(FrameworkElement target)
        {
            if (GetKeepVerticalExpanded(target) is bool keepVerticalExpanded)
                ApplyKeepExpanded(target, Orientation.Vertical, keepVerticalExpanded);
            if (GetKeepHorizontalExpanded(target) is bool keepHorizontalExpanded)
                ApplyKeepExpanded(target, Orientation.Horizontal, keepHorizontalExpanded);
        }

        private static void ApplyLargeScrollBar(FrameworkElement frameworkElement, Orientation targetScrollBarOrientation, bool largeScrollBar)
        {
            _ = frameworkElement switch
            {
                ScrollBar scrollBar => ApplyLargeScrollBarToScrollBar(scrollBar, targetScrollBarOrientation, largeScrollBar),
                ScrollViewer scrollViewer => ApplyLargeScrollBarToScrollViewer(scrollViewer, targetScrollBarOrientation, largeScrollBar),
                ListView listView => ApplyLargeScrollBarToListView(listView, targetScrollBarOrientation, largeScrollBar),
                NavigationView navigationView => ApplyLargeScrollBarToNavigationView(navigationView, targetScrollBarOrientation, largeScrollBar),
                _ => ApplyLargeScrollBarToUnknownTarget(frameworkElement, targetScrollBarOrientation, largeScrollBar)
            };
        }

        private static void ApplyKeepExpanded(FrameworkElement frameworkElement, Orientation targetScrollBarOrientation, bool keepExpanded)
        {
            _ = frameworkElement switch
            {
                ScrollBar scrollBar => ApplyKeepExpandedToScrollBar(scrollBar, targetScrollBarOrientation, keepExpanded),
                ScrollViewer scrollViewer => ApplyKeepExpandedToScrollViewer(scrollViewer, targetScrollBarOrientation, keepExpanded),
                ListView listView => ApplyKeepExpandedToListView(listView, targetScrollBarOrientation, keepExpanded),
                NavigationView navigationView => ApplyKeepExpandedToNavigationView(navigationView, targetScrollBarOrientation, keepExpanded),
                _ => ApplyKeepExpandedToUnknownTarget(frameworkElement, targetScrollBarOrientation, keepExpanded)
            };
        }

        private static bool ApplyLargeScrollBarToScrollBar(ScrollBar scrollBar, Orientation orientation, bool largeScrollBar)
        {
            if (scrollBar.Orientation == orientation)
            {
                scrollBar.EffectiveViewportChanged -= LargeScrollBar_EffectiveViewportChanged;
                scrollBar.EffectiveViewportChanged += LargeScrollBar_EffectiveViewportChanged;

                UpdateScrollBarDimensions(scrollBar, orientation, largeScrollBar);
            }

            return true;
        }

        private static bool ApplyKeepExpandedToScrollBar(ScrollBar scrollBar, Orientation orientation, bool keepExpanded)
        {
            if (scrollBar.Orientation == orientation)
            {
                scrollBar.EffectiveViewportChanged -= KeepExpanded_EffectiveViewportChanged;
                scrollBar.EffectiveViewportChanged += KeepExpanded_EffectiveViewportChanged;

                UpdateScrollBarVisualStates(scrollBar, orientation, keepExpanded);
            }

            return true;
        }

        private static bool ApplyLargeScrollBarToScrollViewer(ScrollViewer scrollViewer, Orientation orientation, bool largeScrollBar)
        {
            scrollViewer.EffectiveViewportChanged -= LargeScrollBar_EffectiveViewportChanged;
            scrollViewer.EffectiveViewportChanged += LargeScrollBar_EffectiveViewportChanged;

            UpdateScrollViewerScrollBarDimensions(scrollViewer, orientation, largeScrollBar);

            string targetScrollBarName = orientation is Orientation.Vertical ? "VerticalScrollBar" : "HorizontalScrollBar";

            if (scrollViewer.FindDescendant(targetScrollBarName) is ScrollBar scrollBar && ApplyLargeScrollBarToScrollBar(scrollBar, orientation, largeScrollBar) is true)
            {
                if (orientation is Orientation.Vertical)
                    SetLargeVerticalScrollBar(scrollBar, largeScrollBar);
                else
                    SetLargeHorizontalScrollBar(scrollBar, largeScrollBar);
            }

            return true;
        }

        private static bool ApplyKeepExpandedToScrollViewer(ScrollViewer scrollViewer, Orientation orientation, bool keepExpanded)
        {
            scrollViewer.EffectiveViewportChanged -= KeepExpanded_EffectiveViewportChanged;
            scrollViewer.EffectiveViewportChanged += KeepExpanded_EffectiveViewportChanged;

            if (keepExpanded is true)
            {
                if (orientation is Orientation.Vertical)
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                else
                    scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;
            }

            UpdateScrollViewerVisualStates(scrollViewer, keepExpanded);

            string targetScrollBarName = orientation is Orientation.Vertical ? "VerticalScrollBar" : "HorizontalScrollBar";

            if (scrollViewer.FindDescendant(targetScrollBarName) is ScrollBar scrollBar && ApplyKeepExpandedToScrollBar(scrollBar, orientation, keepExpanded) is true)
            {
                if (orientation is Orientation.Vertical)
                    SetKeepVerticalExpanded(scrollBar, keepExpanded);
                else
                    SetKeepHorizontalExpanded(scrollBar, keepExpanded);
            }

            return true;
        }

        private static bool ApplyLargeScrollBarToListView(ListView listView, Orientation orientation, bool largeScrollBar)
        {
            if (orientation is Orientation.Vertical && listView.FindDescendant("ScrollViewer") is ScrollViewer scrollViewer)
            {
                scrollViewer.Loaded -= LargeScrollBar_ScrollViewer_Loaded;
                scrollViewer.Loaded += LargeScrollBar_ScrollViewer_Loaded;
                SetLargeVerticalScrollBar(scrollViewer, largeScrollBar);
            }

            return true;
        }

        private static bool ApplyKeepExpandedToListView(ListView listView, Orientation orientation, bool keepExpanded)
        {
            if (orientation is Orientation.Vertical && listView.FindDescendant("ScrollViewer") is ScrollViewer scrollViewer)
            {
                scrollViewer.Loaded -= KeepExpanded_ScrollViewer_Loaded;
                scrollViewer.Loaded += KeepExpanded_ScrollViewer_Loaded;
                SetKeepVerticalExpanded(scrollViewer, keepExpanded);
            }

            return true;
        }

        private static bool ApplyLargeScrollBarToNavigationView(NavigationView navigationView, Orientation orientation, bool largeScrollBar)
        {
            if (navigationView.FindDescendant("MenuItemsScrollViewer") is ScrollViewer menuItemsScrollViewer)
                _ = ApplyLargeScrollBarToScrollViewer(menuItemsScrollViewer, orientation, largeScrollBar);

            return true;
        }

        private static bool ApplyKeepExpandedToNavigationView(NavigationView navigationView, Orientation orientation, bool keepExpanded)
        {
            if (navigationView.FindDescendant("MenuItemsScrollViewer") is ScrollViewer menuItemsScrollViewer)
                _ = ApplyKeepExpandedToScrollViewer(menuItemsScrollViewer, orientation, keepExpanded);

            return true;
        }

        private static bool ApplyLargeScrollBarToUnknownTarget(FrameworkElement frameworkElement, Orientation orientation, bool largeScrollBar)
        {
            foreach (ScrollBar scrollBar in frameworkElement
                .FindDescendants()
                .OfType<ScrollBar>()
                .Where(x => x.Orientation == orientation))
            {
                if (orientation is Orientation.Vertical)
                    SetLargeVerticalScrollBar(scrollBar, largeScrollBar);
                else
                    SetLargeHorizontalScrollBar(scrollBar, largeScrollBar);
            }

            foreach (ScrollViewer scrollViewer in frameworkElement.FindDescendants().OfType<ScrollViewer>())
            {
                if (orientation is Orientation.Vertical)
                    SetLargeVerticalScrollBar(scrollViewer, largeScrollBar);
                else
                    SetLargeHorizontalScrollBar(scrollViewer, largeScrollBar);
            }

            return true;
        }

        private static bool ApplyKeepExpandedToUnknownTarget(FrameworkElement frameworkElement, Orientation orientation, bool keepExpanded)
        {
            foreach (ScrollBar scrollBar in frameworkElement
                .FindDescendants()
                .OfType<ScrollBar>()
                .Where(x => x.Orientation == orientation))
            {
                if (orientation is Orientation.Vertical)
                    SetKeepVerticalExpanded(scrollBar, keepExpanded);
                else
                    SetKeepHorizontalExpanded(scrollBar, keepExpanded);
            }

            foreach (ScrollViewer scrollViewer in frameworkElement.FindDescendants().OfType<ScrollViewer>())
            {
                if (orientation is Orientation.Vertical)
                    SetKeepVerticalExpanded(scrollViewer, keepExpanded);
                else
                    SetKeepHorizontalExpanded(scrollViewer, keepExpanded);
            }

            return true;
        }

        private static void LargeScrollBar_ScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
                ApplyLargeScrollBar(scrollViewer);
        }

        private static void KeepExpanded_ScrollViewer_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
                ApplyKeepExpanded(scrollViewer);
        }

        private static void LargeScrollBar_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            sender.EffectiveViewportChanged -= LargeScrollBar_EffectiveViewportChanged;
            ApplyLargeScrollBar(sender);
        }

        private static void KeepExpanded_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            sender.EffectiveViewportChanged -= KeepExpanded_EffectiveViewportChanged;
            ApplyKeepExpanded(sender);
        }

        private static void UpdateScrollBarDimensions(ScrollBar scrollBar, Orientation orientation, bool largeScrollBar)
        {
            if (largeScrollBar is true)
            {
                if (orientation == Orientation.Vertical)
                    ChangeScrollBarWidth(scrollBar);
                else if (orientation == Orientation.Horizontal)
                    ChangeScrollBarHeight(scrollBar);
            }
            else
            {
                if (orientation == Orientation.Vertical)
                    RestoreScrollBarWidth(scrollBar);
                else if (orientation == Orientation.Horizontal)
                    RestoreScrollBarHeight(scrollBar);
            }
        }

        private static void UpdateScrollBarVisualStates(ScrollBar scrollBar, Orientation orientation, bool keepExpanded)
        {
            if (keepExpanded is true)
            {
                ChangeVisualState(scrollBar, "MouseIndicator");
                ChangeVisualState(scrollBar, "Expanded");
                RemoveVisualStatesFromScrollBar(scrollBar);
            }
            else
            {
                RestoreVisualStates(scrollBar);
                ChangeVisualState(scrollBar, "Collapsed");
                ChangeVisualState(scrollBar, "NoIndicator");
            }
        }

        private static void UpdateScrollViewerScrollBarDimensions(ScrollViewer scrollViewer, Orientation orientation, bool largeScrollBar)
        {
            if (largeScrollBar is true)
            {
                if (orientation == Orientation.Vertical)
                    ChangeScrollViewerScrollBarWidth(scrollViewer);
                else if (orientation == Orientation.Horizontal)
                    ChangeScrollViewerScrollBarHeight(scrollViewer);
            }
            else
            {
                if (orientation == Orientation.Vertical)
                    RestoreScrollViewerScrollBarWidth(scrollViewer);
                else if (orientation == Orientation.Horizontal)
                    RestoreScrollViewerScrollBarHeight(scrollViewer);
            }
        }

        private static void UpdateScrollViewerVisualStates(ScrollViewer scrollViewer, bool keepExpanded)
        {
            if (keepExpanded is true)
            {
                ChangeVisualState(scrollViewer, "MouseIndicator");
                ChangeVisualState(scrollViewer, "ScrollBarSeparatorExpanded");
                RemoveVisualStatesFromScrollViewer(scrollViewer);
            }
            else
            {
                RestoreVisualStates(scrollViewer);
                ChangeVisualState(scrollViewer, "NoIndicator");
            }
        }

        private static void ChangeVisualState(Control control, string stateName) => VisualStateManager.GoToState(control, stateName, true);

        private static List<Thumb> FindThumbs(DependencyObject obj, List<Thumb> retThumb)
        {
            int childrenCount = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child.ToString() == "Microsoft.UI.Xaml.Controls.Primitives.Thumb")
                {
                    Thumb? thumb = child as Thumb;
                    if (thumb != null)
                        retThumb.Add(thumb);
                }
                retThumb = FindThumbs(child, retThumb);
            }
            return retThumb;
        }

        private static void ChangeScrollBarWidth(ScrollBar scrollBar)
        {
            scrollBar.Margin = new Thickness(0.0d, 0.0d, -24.0d, 0.0d);
            scrollBar.MinWidth = 24;
            scrollBar.Width = 24;
            List<Thumb> thumbs = FindThumbs(scrollBar, new List<Thumb>());
            if (thumbs != null && thumbs.Count > 1)
            {
                thumbs[1].MinWidth = 24;
                thumbs[1].Width = 24;
            }
        }

        private static void ChangeScrollBarHeight(ScrollBar scrollBar)
        {
            scrollBar.Margin = new Thickness(0.0d, 0.0d, 0.0d, -24.0d);
            scrollBar.MinHeight = 24;
            scrollBar.Height = 24;
            List<Thumb> thumbs = FindThumbs(scrollBar, new List<Thumb>());
            if (thumbs != null && thumbs.Count > 0)
            {
                thumbs[0].MinHeight = 24;
                thumbs[0].Height = 24;
            }
        }

        private static void RestoreScrollBarWidth(ScrollBar scrollBar)
        {
            scrollBar.MinWidth = 12;
            scrollBar.Width = 12;
            List<Thumb> thumbs = FindThumbs(scrollBar, new List<Thumb>());
            if (thumbs != null && thumbs.Count > 1)
            {
                thumbs[1].MinWidth = 12;
                thumbs[1].Width = 12;
            }
        }

        private static void RestoreScrollBarHeight(ScrollBar scrollBar)
        {
            scrollBar.MinHeight = 12;
            scrollBar.Height = 12;
            List<Thumb> thumbs = FindThumbs(scrollBar, new List<Thumb>());
            if (thumbs != null && thumbs.Count > 0)
            {
                thumbs[0].MinHeight = 12;
                thumbs[0].Height = 12;
            }
        }

        private static void ChangeScrollViewerScrollBarWidth(ScrollViewer scrollviewer)
        {
            List<Thumb> thumbs = FindThumbs(scrollviewer, new List<Thumb>());
            if (thumbs != null && thumbs.Count > 1)
            {
                thumbs[1].MinWidth = 24;
                thumbs[1].Width = 24;
            }
        }

        private static void ChangeScrollViewerScrollBarHeight(ScrollViewer scrollviewer)
        {
            List<Thumb> thumbs = FindThumbs(scrollviewer, new List<Thumb>());
            if (thumbs != null && thumbs.Count > 0)
            {
                thumbs[0].MinHeight = 24;
                thumbs[0].Height = 24;
            }
        }

        private static void RestoreScrollViewerScrollBarWidth(ScrollViewer scrollviewer)
        {
            List<Thumb> thumbs = FindThumbs(scrollviewer, new List<Thumb>());
            if (thumbs != null && thumbs.Count > 1)
            {
                thumbs[1].MinWidth = 12;
                thumbs[1].Width = 12;
            }
        }

        private static void RestoreScrollViewerScrollBarHeight(ScrollViewer scrollviewer)
        {
            List<Thumb> thumbs = FindThumbs(scrollviewer, new List<Thumb>());
            if (thumbs != null && thumbs.Count > 0)
            {
                thumbs[0].MinHeight = 12;
                thumbs[0].Height = 12;
            }
        }

        private static void RemoveVisualStatesFromScrollBar(ScrollBar scrollBar)
        {
            if (scrollBar.FindDescendant("Root") is FrameworkElement root)
            {
                RemoveVisualState(root, "ScrollingIndicatorStates", "TouchIndicator");
                RemoveVisualState(root, "ScrollingIndicatorStates", "NoIndicator");
                RemoveVisualState(root, "ConsciousStates", "Collapsed");
                RemoveVisualState(root, "ConsciousStates", "CollapsedWithoutAnimation");
            }
        }

        private static void RemoveVisualStatesFromScrollViewer(ScrollViewer scrollViewer)
        {
            if (scrollViewer.FindDescendant("Root") is FrameworkElement root)
            {
                RemoveVisualState(root, "ScrollingIndicatorStates", "TouchIndicator");
                RemoveVisualState(root, "ScrollingIndicatorStates", "NoIndicator");
                RemoveVisualState(root, "ScrollBarSeparatorStates", "ScrollBarSeparatorCollapsed");
                RemoveVisualState(root, "ScrollBarSeparatorStates", "ScrollBarSeparatorCollapsedWithoutAnimation");
            }
        }

        private static void RemoveVisualState(FrameworkElement root, string visualStateGroupName, string visualStateName)
        {
            if (VisualStateManager.GetVisualStateGroups(root)
                    .FirstOrDefault(x => x.Name == visualStateGroupName) is VisualStateGroup visualStateGroup &&
                visualStateGroup.States
                    .FirstOrDefault(x => x.Name == visualStateName) is VisualState removingVisualState)
            {
                RemovedVisualStates.Add(new RemovingVisualState(visualStateGroup, removingVisualState));
                _ = visualStateGroup.States.Remove(removingVisualState);
            }
        }

        private static void RestoreVisualStates(FrameworkElement frameworkElement)
        {
            if (frameworkElement.FindDescendant("Root") is FrameworkElement root)
            {
                foreach (VisualStateGroup visualStateGroup in VisualStateManager.GetVisualStateGroups(root))
                {
                    IEnumerable<RemovingVisualState> restoring = RemovedVisualStates
                        .Where(x => x.VisualStateGroup == visualStateGroup);

                    foreach (VisualState visualState in restoring.Select(x => x.VisualState))
                        visualStateGroup.States.Add(visualState);

                    foreach (RemovingVisualState item in restoring.ToList())
                        _ = RemovedVisualStates.Remove(item);
                }
            }
        }
    }
}
