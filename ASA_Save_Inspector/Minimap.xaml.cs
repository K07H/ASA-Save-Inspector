using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ASA_Save_Inspector.Pages;
using BruTile;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Fetcher;
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
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ASA_Save_Inspector
{
    public class MapPoint
    {
        public string? ID { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string? SubMapName { get; set; }
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
            var mapCoords = Utils.GetMapCoordsFromASIMinimapCoords(worldPosition.Y, worldPosition.X);
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

        private static MemoryLayer? _minimapLayer = null;
        private static ILayer? _pointsLayer = null;
        private static MemoryProvider? _featuresProvider = null;

        private static ImageStyle? _pinStyle = null;
        private static bool _initialized = false;
        private static Minimap? _minimap = null;

        public static IEnumerable<MapPoint?>? _points = null;
        public static Func<MapPoint?, bool>? _doubleTapCallback = null;

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

            // Restore "Pause minimap refresh" checkbox state.
            CB_StopRefreshingMinimap.IsChecked = PauseMinimapRefresh;

            // Resize window.
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

        public static void UpdatePointsAndDoubleTapData(IEnumerable<MapPoint?>? points, Func<MapPoint?, bool>? doubleTapCallback)
        {
            _points = points;
            _doubleTapCallback = doubleTapCallback;
        }

        public static void InitMap(IEnumerable<MapPoint?> points, Func<MapPoint?, bool>? doubleTapCallback, List<string> minimapFilenames)
        {
            if (!_initialized && _minimap != null)
            {
                _initialized = true;
                UpdatePointsAndDoubleTapData(points, doubleTapCallback);
                _minimap.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => { _minimap.CreateMap(points, minimapFilenames); });
            }
        }

        public static void ChangePoints(IEnumerable<MapPoint?> points, Func<MapPoint?, bool>? doubleTapCallback)
        {
            if (_minimap != null)
            {
                UpdatePointsAndDoubleTapData(points, doubleTapCallback);
                _minimap.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => { _minimap.ModifyPoints(points); });
            }
        }

        public static void StopMinimapPause()
        {
            if (_points != null)
                MainWindow.UpdateMinimap(_points, _doubleTapCallback);
        }

        private void CreateMap(IEnumerable<MapPoint?> points, List<string> minimapFilenames)
        {
            if (_pinStyle == null)
                _pinStyle = CreatePinSymbol();
            bool isFirstLayer = true;
            int foundSubMaps = 0;
            foreach (var filename in minimapFilenames)
                if (!string.IsNullOrWhiteSpace(filename))
                {
                    var backgroundLayer = CreateLayerWithRasterFeature(_mapDimensions, filename);
                    if (backgroundLayer != null)
                    {
                        ++foundSubMaps;
                        if (isFirstLayer)
                        {
                            isFirstLayer = false;
                            backgroundLayer.Enabled = true;
                            _minimapLayer = backgroundLayer;
                        }
                        else
                            backgroundLayer.Enabled = false;
                        MyMap.Map.Layers.Add(backgroundLayer);
                    }
                }
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

            if (foundSubMaps > 1)
            {
                TBSubMapName.Text = ASILang.Get("SubMap");
                if (!string.IsNullOrWhiteSpace(SettingsPage._currentlyLoadedMapName))
                {
                    var mapInfo = Utils.GetMapInfoFromName(SettingsPage._currentlyLoadedMapName);
                    if (mapInfo != null && !string.IsNullOrWhiteSpace(mapInfo.SubMapName))
                        TBSubMapName.Text = $"{ASILang.Get("SubMap")} {mapInfo.SubMapName}";
                }
                SPSubMap.Visibility = Visibility.Visible;
            }
            else
                SPSubMap.Visibility = Visibility.Collapsed;
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

        private void ChangeBackgroundImage(string newMinimapFilename)
        {
            if (MyMap.Map.Layers != null && MyMap.Map.Layers.Count > 0)
                foreach (var layer in MyMap.Map.Layers)
                    if (layer != null)
                    {
                        if (string.Compare(layer.Name, newMinimapFilename, StringComparison.InvariantCultureIgnoreCase) == 0 ||
                            string.Compare(layer.Name, "Points", StringComparison.InvariantCultureIgnoreCase) == 0)
                            layer.Enabled = true;
                        else
                            layer.Enabled = false;
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

        private void RefreshPointsForCurrentSubMap()
        {
            HideCallouts();
            var currentPageType = Utils.GetCurrentlyDisplayedPageType();
            if (currentPageType != null)
            {
                if (currentPageType == typeof(DinosPage))
                {
                    if (DinosPage._page != null)
                        DinosPage._page.ApplyFiltersAndSort();
                }
                else if (currentPageType == typeof(StructuresPage))
                {
                    if (StructuresPage._page != null)
                        StructuresPage._page.ApplyFiltersAndSort();
                }
                else if (currentPageType == typeof(ItemsPage))
                {
                    if (ItemsPage._page != null)
                        ItemsPage._page.ApplyFiltersAndSort();
                }
                else if (currentPageType == typeof(PlayerPawnsPage))
                {
                    if (PlayerPawnsPage._page != null)
                        PlayerPawnsPage._page.ApplyFiltersAndSort();
                }
                else if (currentPageType == typeof(PlayersPage))
                {
                    if (PlayersPage._page != null)
                        PlayersPage._page.ApplyFiltersAndSort();
                }
                else if (currentPageType == typeof(TribesPage))
                {
                    if (TribesPage._page != null)
                        TribesPage._page.ApplyFiltersAndSort();
                }
            }
        }

        private void ChangeToSubMap(string? subMapName)
        {
            if (string.IsNullOrWhiteSpace(SettingsPage._currentlyLoadedMapName))
                return;
            ArkMapInfo? mapInfo = Utils.GetMapInfoFromName(SettingsPage._currentlyLoadedMapName);
            if (mapInfo == null)
                return;
            if (!string.IsNullOrWhiteSpace(subMapName) &&
                string.Compare(subMapName, mapInfo.SubMapName, StringComparison.InvariantCultureIgnoreCase) != 0 &&
                mapInfo.SubMinimaps != null &&
                mapInfo.SubMinimaps.Count > 0)
                foreach (ArkMapInfo subMap in mapInfo.SubMinimaps)
                    if (subMap != null && string.Compare(subMapName, subMap.SubMapName, StringComparison.InvariantCultureIgnoreCase) == 0)
                    {
                        mapInfo = subMap;
                        break;
                    }
            SettingsPage._currentlyLoadedSubMapName = subMapName;
            if (string.IsNullOrWhiteSpace(mapInfo.SubMapName))
                SPSubMap.Visibility = Visibility.Collapsed;
            else
            {
                TBSubMapName.Text = $"{ASILang.Get("SubMap")} {mapInfo.SubMapName}";
                SPSubMap.Visibility = Visibility.Visible;
            }
            if (!string.IsNullOrWhiteSpace(mapInfo.MinimapFilename))
                ChangeBackgroundImage(mapInfo.MinimapFilename);
            RefreshPointsForCurrentSubMap();
            MyMap.RefreshGraphics();
        }

        private void MapShowCallout(string? ID, double x, double y, string? subMapName, bool noRecursion = false)
        {
            if (_featuresProvider == null)
                return;

            if (!string.IsNullOrWhiteSpace(subMapName))
                if (string.Compare(subMapName, SettingsPage._currentlyLoadedSubMapName, StringComparison.InvariantCultureIgnoreCase) != 0)
                {
#if DEBUG
                    Debug.WriteLine("WARNING: Not on proper sub map. Changing sub map.");
#endif
                    ChangeToSubMap(subMapName);
                    if (!noRecursion && _minimap != null)
                        _minimap.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, async () =>
                        {
                            await Task.Delay(1000);
                            MapShowCallout(ID, x, y, subMapName, true);
                        });
                    return;
                }

            HideCallouts();
            var features = _featuresProvider.Features.Where(f => ((f.Data as MapPoint)?.X == x && (f.Data as MapPoint)?.Y == y));
            if (features != null && features.Count() > 0)
                foreach (var feature in features)
                    if (feature != null && string.Compare((feature.Data as MapPoint)?.ID, ID, false, CultureInfo.InvariantCulture) == 0)
                    {
                        var calloutStyle = feature.Styles.OfType<CalloutStyle>().FirstOrDefault();
                        if (calloutStyle is not null)
                            calloutStyle.Enabled = !calloutStyle.Enabled;
                    }
            MyMap.RefreshGraphics();
        }

        public static void ShowCallout(string? ID, double x, double y, string? subMapName)
        {
            if (_minimap != null)
                _minimap.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () => { _minimap.MapShowCallout(ID, x, y, subMapName); });
        }

        private static MemoryLayer? CreateLayerWithRasterFeature(MRect extent, string minimapFilename)
        {
            RasterFeature? rasterFeature = null;
            bool fileFound = false;
            byte[]? imgBytes = null;

            // Try to read image from file path.
            try { fileFound = File.Exists(minimapFilename); }
            catch { fileFound = false; }
            if (fileFound)
                try { imgBytes = File.ReadAllBytes(minimapFilename); }
                catch (Exception ex)
                {
                    Logger.Instance.Log($"Failed to get image bytes from file at \"{minimapFilename}\". Exception=[{ex}]", Logger.LogLevel.ERROR);
                    imgBytes = null;
                }

            // Fallback: Try to read image from ASI's embedded files (using file name).
            if (imgBytes == null)
            {
                try
                {
                    using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream($"ASA_Save_Inspector.Assets.{minimapFilename}"))
                    {
                        if (s != null)
                            imgBytes = s.ToBytes();
                    }
                }
                catch (Exception exb)
                {
                    Logger.Instance.Log($"Failed to get image bytes from ASI's embedded resources with file name \"{minimapFilename}\". Exception=[{exb}]", Logger.LogLevel.ERROR);
                    imgBytes = null;
                }
            }

            // Create raster feature
            if (imgBytes != null)
                try { rasterFeature = new RasterFeature(new MRaster(imgBytes, extent)) { Styles = { new RasterStyle() } }; }
                catch (Exception exc)
                {
                    rasterFeature = null;
                    Logger.Instance.Log($"Exception caught while creating raster feature for filename \"{minimapFilename}\". Exception=[{exc}]", Logger.LogLevel.ERROR);
                }
            if (rasterFeature == null)
            {
                Logger.Instance.Log("Could not find minimap image. A valid image filename (or a valid file path to an image) must be provided in MinimapFilename field from maps_info.json.", Logger.LogLevel.ERROR);
                return null;
            }

            return new MemoryLayer() { Features = new List<RasterFeature> { rasterFeature }, Name = minimapFilename, Opacity = 1, Style = null };
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

        private ArkMapInfo? GetMapInfoFromMinimapFileName(string? minimapFilename)
        {
            if (string.IsNullOrWhiteSpace(minimapFilename))
                return null;
            if (string.IsNullOrWhiteSpace(SettingsPage._currentlyLoadedMapName))
                return null;
            ArkMapInfo? mapInfo = Utils.GetMapInfoFromName(SettingsPage._currentlyLoadedMapName);
            if (mapInfo == null)
                return null;
            if (string.Compare(minimapFilename, mapInfo.MinimapFilename, StringComparison.InvariantCultureIgnoreCase) == 0)
                return mapInfo;
            if (mapInfo.SubMinimaps != null && mapInfo.SubMinimaps.Count > 0)
                foreach (var subMap in mapInfo.SubMinimaps)
                    if (subMap != null && string.Compare(minimapFilename, subMap.MinimapFilename, StringComparison.InvariantCultureIgnoreCase) == 0)
                        return subMap;
            return null;
        }

        private void ChangeToSubMapForLayer(ILayer? layer)
        {
            if (layer == null)
                return;
            var mapInfo = GetMapInfoFromMinimapFileName(layer.Name);
            if (mapInfo == null || string.IsNullOrWhiteSpace(mapInfo.SubMapName))
                return;
            ChangeToSubMap(mapInfo.SubMapName);
        }

        private void BTNSubMapPrev_Click(object sender, RoutedEventArgs e)
        {
            if (MyMap.Map.Layers != null && MyMap.Map.Layers.Count > 0)
            {
                ILayer? lastLayer = null;
                for (int i = (MyMap.Map.Layers.Count - 1); i >= 0; i--)
                {
                    var layer = MyMap.Map.Layers.ElementAt(i);
                    if (layer != null && string.Compare(layer.Name, "Points", StringComparison.InvariantCultureIgnoreCase) != 0)
                    {
                        lastLayer = layer;
                        break;
                    }
                }

                ILayer? prevLayer = null;
                for (int i = 0; i < MyMap.Map.Layers.Count; i++)
                {
                    var layer = MyMap.Map.Layers.ElementAt(i);
                    if (layer != null && string.Compare(layer.Name, "Points", StringComparison.InvariantCultureIgnoreCase) != 0)
                    {
                        if (layer.Enabled)
                        {
                            if (prevLayer != null)
                            {
                                prevLayer.Enabled = true;
                                layer.Enabled = false;
                                ChangeToSubMapForLayer(prevLayer);
                            }
                            else if (lastLayer != null && lastLayer != layer)
                            {
                                lastLayer.Enabled = true;
                                layer.Enabled = false;
                                ChangeToSubMapForLayer(lastLayer);
                            }
                            return;
                        }
                        prevLayer = layer;
                    }
                }
            }
        }

        private void BTNSubMapNext_Click(object sender, RoutedEventArgs e)
        {
            if (MyMap.Map.Layers != null && MyMap.Map.Layers.Count > 0)
            {
                ILayer? firstLayer = null;
                for (int i = 0; i < MyMap.Map.Layers.Count; i++)
                {
                    var layer = MyMap.Map.Layers.ElementAt(i);
                    if (layer != null && string.Compare(layer.Name, "Points", StringComparison.InvariantCultureIgnoreCase) != 0)
                    {
                        firstLayer = layer;
                        break;
                    }
                }

                ILayer? currentLayer = null;
                bool foundCurrentLayer = false;
                bool foundNextLayer = false;
                for (int i = 0; i < MyMap.Map.Layers.Count; i++)
                {
                    var layer = MyMap.Map.Layers.ElementAt(i);
                    if (layer != null && string.Compare(layer.Name, "Points", StringComparison.InvariantCultureIgnoreCase) != 0)
                    {
                        if (foundCurrentLayer)
                        {
                            foundNextLayer = true;
                            if (layer != currentLayer)
                            {
                                layer.Enabled = true;
                                if (currentLayer != null)
                                    currentLayer.Enabled = false;
                                ChangeToSubMapForLayer(layer);
                            }
                        }
                        else
                        {
                            if (layer.Enabled)
                            {
                                foundCurrentLayer = true;
                                currentLayer = layer;
                            }
                        }
                    }
                }
                if (!foundNextLayer)
                    if (firstLayer != null && firstLayer != currentLayer)
                    {
                        firstLayer.Enabled = true;
                        if (currentLayer != null)
                            currentLayer.Enabled = false;
                        ChangeToSubMapForLayer(firstLayer);
                    }
            }
        }

        public static bool PauseMinimapRefresh = false;

        private void CB_StopRefreshingMinimap_Checked(object sender, RoutedEventArgs e)
        {
            PauseMinimapRefresh = true;
        }

        private void CB_StopRefreshingMinimap_Unchecked(object sender, RoutedEventArgs e)
        {
            PauseMinimapRefresh = false;
            StopMinimapPause();
        }

        private bool? _isSmallState = null;
        private void SPTopBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            bool isSmall = e.NewSize.Width < (ASILang._selectedLanguage == ASILang.DEFAULT_LANGUAGE_CODE ? 680.0d : 800.0d);
            if (_isSmallState == isSmall)
                return;
            if (isSmall)
                Grid.SetRow(SPRightButtons, 1);
            else
                Grid.SetRow(SPRightButtons, 0);
        }
    }
}
