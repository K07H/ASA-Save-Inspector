using ASA_Save_Inspector.Pages;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Limiting;
using Mapsui.Manipulations;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Utilities;
using Mapsui.Widgets;
using Mapsui.Widgets.BoxWidgets;
using Mapsui.Widgets.InfoWidgets;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;

namespace ASA_Save_Inspector
{
    public class MapPoint
    {
        public string? ID { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class ASIMouseCoordinatesWidget : TextBoxWidget
    {
        public ASIMouseCoordinatesWidget()
        {
            InputAreaType = InputAreaType.Map;
            HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Center;
            VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Bottom;
            Text = "GPS coordinates";
            TextSize = 14.0d;
            TextColor = Color.Black;
        }

        public override void OnPointerMoved(WidgetEventArgs e)
        {
            var worldPosition = e.Map.Navigator.Viewport.ScreenToWorld(e.ScreenPosition);
            var mapCoords = Utils.GetMapCoordsFromASIMinimapCoords((!string.IsNullOrEmpty(SettingsPage._currentlyLoadedMapName) ? SettingsPage._currentlyLoadedMapName : "Unknown"), worldPosition.Y, worldPosition.X);
            Text = $"Lat {mapCoords.Key.ToString("F1", CultureInfo.InvariantCulture)}  Long {mapCoords.Value.ToString("F1", CultureInfo.InvariantCulture)}";
        }
    }

    public sealed partial class Minimap : Window
    {
        public const double X_MIN = 0.0d;
        public const double X_MAX = 4096.0d;
        public const double Y_MIN = 0.0d;
        public const double Y_MAX = 4096.0d;
        public const double MARGIN = 500.0d;

        private const double SYMBOL_SIZE = 36.0d;
        private const double SYMBOL_SCALE = 0.25d;
        private const double CALLOUT_MAX_WIDTH = 150.0d;

        private const int WINDOW_WIDTH = 1024;
        private const int WINDOW_HEIGHT = 1024;
        private const int TITLE_BAR_HEIGHT = (48 + 14);

        private const string POINTS_LAYER_NAME = "Points";

        private static readonly string _windowName = $"{MainWindow._appName} Minimap";
        private static readonly MRect _mapDimensions = new MRect(X_MIN, Y_MIN, X_MAX + (MARGIN * 2.0d), Y_MAX + (MARGIN * 2.0d));

        private static ILayer? _minimapLayer = null;
        private static ILayer? _pointsLayer = null;
        private static MemoryProvider? _featuresProvider = null;

        private static ImageStyle? _pinStyle = null;
        private static bool _initialized = false;
        private static Minimap? _minimap = null;
        private static Func<MapPoint?, bool>? _doubleTapCallback = null;

        public AppWindow? _appWindow = null;

        public Minimap(bool displayDebug = false)
        {
            InitializeComponent();

            this._appWindow = this.AppWindow;
            _minimap = this;
            Activated += Minimap_Activated;

            // Hide system title bar.
            ExtendsContentIntoTitleBar = true;
            if (ExtendsContentIntoTitleBar == true)
                this._appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

            // Set title bar title.
            this.Title = _windowName;
            TitleBarTextBlock.Text = _windowName;

            // Set icon.
            if (_appWindow != null)
            {
                _appWindow.SetIcon(@"Assets\ASI.ico");
                _appWindow.SetTitleBarIcon(@"Assets\ASI.ico");
                _appWindow.SetTaskbarIcon(@"Assets\ASI.ico");
            }

            AppWindow.Resize(new Windows.Graphics.SizeInt32(WINDOW_WIDTH, WINDOW_HEIGHT + TITLE_BAR_HEIGHT));

            ActiveMode activeMode = (displayDebug ? ActiveMode.Yes : ActiveMode.No);
            Mapsui.Logging.Logger.Settings.LogWidgetEvents = displayDebug;
            Mapsui.Logging.Logger.Settings.LogFlingEvents = displayDebug;
            Mapsui.Logging.Logger.Settings.LogMapEvents = displayDebug;
            LoggingWidget.ShowLoggingInMap = activeMode;
            Performance.DefaultIsActive = activeMode;
            MyMap.Map.Performance.IsActive = activeMode;
        }

        private void Minimap_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated)
                TitleBarTextBlock.Foreground = (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];
            else
                TitleBarTextBlock.Foreground = (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
        }

        public static void InitMap(IEnumerable<MapPoint?> points, string minimapFilename, Func<MapPoint?, bool>? doubleTapCallback)
        {
            if (!_initialized && _minimap != null)
            {
                _initialized = true;
                _doubleTapCallback = doubleTapCallback;
                _minimap.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => { _minimap.CreateMap(points, minimapFilename); });
            }
        }

        public static void ChangePoints(IEnumerable<MapPoint?> points, Func<MapPoint?, bool>? doubleTapCallback)
        {
            if (_minimap != null)
            {
                _doubleTapCallback = doubleTapCallback;
                _minimap.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => { _minimap.ModifyPoints(points); });
            }
        }

        private void CreateMap(IEnumerable<MapPoint?> points, string minimapFilename)
        {
            if (_pinStyle == null)
                _pinStyle = CreatePinSymbol();
            _minimapLayer = CreateLayerWithRasterFeature(_mapDimensions, minimapFilename);
            if (_minimapLayer != null)
                MyMap.Map.Layers.Add(_minimapLayer);
            _pointsLayer = CreatePointLayer(points);
            if (_pointsLayer != null)
                MyMap.Map.Layers.Add(_pointsLayer);

            MyMap.Map.Widgets.Add(new MapInfoWidget(MyMap.Map, l => l.Name == POINTS_LAYER_NAME) { FeatureToText = (f) => "" });
            MyMap.Map.Widgets.Add(CreateMouseCoordinatesWidget(MyMap.Map, Mapsui.Widgets.VerticalAlignment.Bottom, Mapsui.Widgets.HorizontalAlignment.Center));

            MyMap.Map.Navigator.Limiter = new ViewportLimiterKeepWithinExtent();
            MyMap.Map.Navigator.RotationLock = true;
            MyMap.Map.Navigator.OverridePanBounds = _mapDimensions;
            MyMap.Map.Navigator.OverrideZoomBounds = new MMinMax(0.04, 20.0);

            MyMap.Map.Navigator.ZoomToBox(_mapDimensions);
            MyMap.Map.Navigator.CenterOn(_mapDimensions.Centroid);

            MyMap.Map.Tapped += MapTapped;
        }

        private void ClearPoints()
        {
            if (_pointsLayer != null)
                MyMap.Map.Layers.Remove(_pointsLayer);
            if (_featuresProvider != null)
            {
                if (_featuresProvider.Features != null && _featuresProvider.Features.Count > 0)
                    foreach (var f in _featuresProvider.Features)
                        if (f.Styles != null)
                            f.Styles.Clear();
                _featuresProvider.Clear();
                _featuresProvider = null;
            }
            if (_pointsLayer != null)
            {
                _pointsLayer.Dispose();
                _pointsLayer = null;
            }
        }

        private void ClearBackgroundImage()
        {
            if (_minimapLayer != null)
            {
                MyMap.Map.Layers.Remove(_minimapLayer);
                _minimapLayer.Dispose();
                _minimapLayer = null;
            }
        }

        private void ModifyPoints(IEnumerable<MapPoint?> points)
        {
            ClearPoints();
            _pointsLayer = CreatePointLayer(points);
            if (_pointsLayer != null)
                MyMap.Map.Layers.Add(_pointsLayer);
            MyMap.Map.RefreshGraphics();
        }

        private static bool HideCallouts()
        {
            if (_pointsLayer == null)
                return false;

            bool hasChanged = false;
            var features = _pointsLayer.GetFeatures(_mapDimensions, 0.01d);
            if (features != null)
            {
                foreach (var feature in features)
                    if (feature != null)
                    {
                        var calloutStyle = feature.Styles.OfType<CalloutStyle>().FirstOrDefault();
                        if (calloutStyle is not null && calloutStyle.Enabled)
                        {
                            calloutStyle.Enabled = false;
                            hasChanged = true;
                        }
                    }
            }

            return hasChanged;
        }

        private static void MapTapped(object? s, MapEventArgs e)
        {
            if (e == null)
                return;

            var mapInfo = e.GetMapInfo(e.Map.Layers.Where(l => l.Name == POINTS_LAYER_NAME));
            if (mapInfo == null)
                return;

            var feature = mapInfo.Feature;
            bool hasChanged = HideCallouts();
            if (feature is not null)
            {
                var calloutStyle = feature.Styles.OfType<CalloutStyle>().FirstOrDefault();
                if (calloutStyle is not null)
                {
                    calloutStyle.Enabled = !calloutStyle.Enabled;
                    hasChanged = true;
                    e.Handled = true;

                    if (e.GestureType == GestureType.DoubleTap)
                        if (_doubleTapCallback != null)
                            _doubleTapCallback(feature.Data as MapPoint);
                }
            }

            if (hasChanged)
                mapInfo.Layer?.DataHasChanged();
        }

        private static ILayer? CreateLayerWithRasterFeature(MRect extent, string minimapFilename)
        {
            RasterFeature? rasterFeature = null;
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream($"ASA_Save_Inspector.Assets.{minimapFilename}"))
            {
                if (s != null)
                    rasterFeature = new RasterFeature(new MRaster(s.ToBytes(), extent)) { Styles = { new RasterStyle() } };
            }
            if (rasterFeature == null)
                return null;
            return new MemoryLayer() { Features = new List<RasterFeature> { rasterFeature }, Name = "Minimap", Opacity = 1, Style = null };
        }

        private static ILayer? CreatePointLayer(IEnumerable<MapPoint?>? points)
        {
            List<PointFeature> pointsFeatures = new List<PointFeature>();
            if (points != null && points.Count() > 0)
                foreach (var point in points)
                    if (point != null && point.Name != null && point.Description != null)
                    {
                        PointFeature pf = new PointFeature(point.X + MARGIN, point.Y + MARGIN);
                        pf["name"] = point.Name;
                        pf.Data = point;
                        pf.Styles.Add(CreateCalloutStyle($"{point.Name}\n{point.Description}"));
                        pointsFeatures.Add(pf);
                    }

            if (pointsFeatures.Count <= 0)
                return null;
            _featuresProvider = new MemoryProvider(pointsFeatures);
            if (_featuresProvider == null)
                return null;
            return new MemoryLayer()
            {
                Name = POINTS_LAYER_NAME,
                Features = _featuresProvider.Features,
                Style = _pinStyle,
            };
        }

        private static ImageStyle CreatePinSymbol() => new()
        {
            Image = new Mapsui.Styles.Image
            {
                Source = "embedded://ASA_Save_Inspector.Assets.RedCircle.svg",
                SvgFillColor = Color.FromString("#941212"),
                SvgStrokeColor = Color.DimGrey,
            },
            RelativeOffset = new RelativeOffset(0.0d, 0.0d),
            SymbolScale = SYMBOL_SCALE,
        };

        private static CalloutStyle CreateCalloutStyle(string content)
        {
            return new CalloutStyle
            {
                Title = content,
                TitleFont = { FontFamily = null, Size = 12, Italic = false, Bold = true },
                TitleFontColor = Color.Gray,
                MaxWidth = CALLOUT_MAX_WIDTH,
                Enabled = false,
                Offset = new Offset(0.0d, SYMBOL_SIZE * SYMBOL_SCALE),
                BalloonDefinition = new CalloutBalloonDefinition
                {
                    RectRadius = 10,
                    ShadowWidth = 4,
                },
            };
        }

        private static ASIMouseCoordinatesWidget CreateMouseCoordinatesWidget(Map map, Mapsui.Widgets.VerticalAlignment verticalAlignment, Mapsui.Widgets.HorizontalAlignment horizontalAlignment)
        {
            return new ASIMouseCoordinatesWidget()
            {
                VerticalAlignment = verticalAlignment,
                HorizontalAlignment = horizontalAlignment,
                Margin = new MRect(20),
            };
        }

        private void MinimapWindow_Closed(object sender, WindowEventArgs args)
        {
            ClearPoints();
            ClearBackgroundImage();
            MyMap.Map.ClearCache();
            MyMap.Map.Widgets.Clear();
            MyMap.Map.Layers.Clear();
            MyMap.Map.Dispose();
            MyMap.Dispose();

            _initialized = false;
            MainWindow._minimap = null;
        }
    }
}
