using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using MessageBox = System.Windows.MessageBox;
using Cursors = System.Windows.Input.Cursors;
using DotnetGuard.DiskMap.App.Helpers;
using DotnetGuard.DiskMap.Core.Models;
using DotnetGuard.DiskMap.Data;

namespace DotnetGuard.DiskMap.App.Views
{
    public partial class MainWindow : Window
    {
        private const int MaxVisibleChildren = 80;

        private readonly DiskScanner _scanner = new DiskScanner();
        private readonly List<DiskNode> _navigationStack = new List<DiskNode>();
        private CancellationTokenSource _scanCts;

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) => DarkTitleBar.Apply(this);
        }

        // ── Scanning ────────────────────────────────────────────────

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    PathBox.Text = dialog.SelectedPath;
                }
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            string path = PathBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                MessageBox.Show("Pick a valid folder first.", "DotnetGuard DiskMap");
                return;
            }

            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();
            CancellationToken token = _scanCts.Token;

            ScanButton.IsEnabled = false;
            EmptyStateText.Visibility = Visibility.Collapsed;
            TreemapCanvas.Children.Clear();
            StatusText.Text = "Scanning...";
            ScanProgressBar.IsIndeterminate = true;
            ScanProgressBar.Visibility = Visibility.Visible;
            _navigationStack.Clear();

            DiskNode liveRoot = null;

            try
            {
                DiskNode root = await Task.Run(() => _scanner.Scan(path, partialRoot =>
                {
                    liveRoot = partialRoot;

                    Dispatcher.Invoke(() =>
                    {
                        // Only auto-refresh while still looking at the top level —
                        // don't yank the view if the user has drilled into a subfolder.
                        if (_navigationStack.Count == 1 && ReferenceEquals(_navigationStack[0], liveRoot))
                        {
                            RenderCurrentNode();
                        }
                        else if (_navigationStack.Count == 0)
                        {
                            _navigationStack.Add(liveRoot);
                            RenderCurrentNode();
                        }

                        StatusText.Text = $"Scanning... {liveRoot.DisplaySize} so far, {CountFiles(liveRoot)} files";
                    });
                }, token), token);

                if (_navigationStack.Count == 0)
                {
                    _navigationStack.Add(root);
                }

                RenderCurrentNode();
                StatusText.Text = $"{root.Name} — {root.DisplaySize} total, {CountFiles(root)} files";
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Scan failed: " + ex.Message, "DotnetGuard DiskMap");
                StatusText.Text = "Scan failed.";
            }
            finally
            {
                ScanButton.IsEnabled = true;
                ScanProgressBar.IsIndeterminate = false;
                ScanProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private static int CountFiles(DiskNode node)
        {
            int count = node.IsDirectory ? 0 : 1;

            foreach (DiskNode child in node.Children)
            {
                count += CountFiles(child);
            }

            return count;
        }

        // ── Navigation ──────────────────────────────────────────────

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_navigationStack.Count > 1)
            {
                AnimateTransition(() => _navigationStack.RemoveAt(_navigationStack.Count - 1));
            }
        }

        private void AnimateTransition(Action navigationChange)
        {
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(90));
            fadeOut.Completed += (s, e) =>
            {
                navigationChange();
                RenderCurrentNode();

                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
                TreemapCanvas.BeginAnimation(OpacityProperty, fadeIn);
            };
            TreemapCanvas.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void TreemapCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_navigationStack.Count > 0)
            {
                RenderCurrentNode();
            }
        }

        // ── Rendering ───────────────────────────────────────────────

        private void RenderCurrentNode()
        {
            TreemapCanvas.Children.Clear();

            if (_navigationStack.Count == 0)
            {
                BreadcrumbPanel.Children.Clear();
                return;
            }

            BuildBreadcrumb();

            DiskNode current = _navigationStack[_navigationStack.Count - 1];
            BuildLegend(current);

            DiskNode collapsed = DiskScanner.CollapseSmallItems(current, MaxVisibleChildren);

            double width = TreemapCanvas.ActualWidth;
            double height = TreemapCanvas.ActualHeight;

            if (width <= 0 || height <= 0 || collapsed.Children.Count == 0)
            {
                return;
            }

            LayoutRect bounds = new LayoutRect(0, 0, width, height);
            List<(LayoutRect Rect, DiskNode Node)> layout = TreemapLayout.Compute(collapsed.Children, bounds);

            foreach ((LayoutRect Rect, DiskNode Node) item in layout)
            {
                DrawTile(item.Rect, item.Node);
            }
        }

        private void BuildBreadcrumb()
        {
            BreadcrumbPanel.Children.Clear();

            for (int i = 0; i < _navigationStack.Count; i++)
            {
                int index = i;
                bool isLast = i == _navigationStack.Count - 1;

                TextBlock segment = new TextBlock
                {
                    Text = _navigationStack[i].Name,
                    FontWeight = FontWeights.Bold,
                    Foreground = isLast ? (Brush)FindResource("TextPrimaryBrush") : (Brush)FindResource("AccentBrush"),
                    Cursor = isLast ? Cursors.Arrow : Cursors.Hand
                };

                if (!isLast)
                {
                    segment.TextDecorations = TextDecorations.Underline;
                    segment.MouseLeftButtonUp += (s, e) => AnimateTransition(() =>
                    {
                        _navigationStack.RemoveRange(index + 1, _navigationStack.Count - index - 1);
                    });
                }

                BreadcrumbPanel.Children.Add(segment);

                if (!isLast)
                {
                    BreadcrumbPanel.Children.Add(new TextBlock
                    {
                        Text = "  ›  ",
                        Foreground = (Brush)FindResource("TextSecondaryBrush")
                    });
                }
            }
        }

        private void BuildLegend(DiskNode node)
        {
            LegendPanel.Children.Clear();

            Dictionary<string, long> totals = new Dictionary<string, long>();
            CollectExtensionSizes(node, totals);

            var top = totals.OrderByDescending(kv => kv.Value).Take(10).ToList();

            if (top.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<string, long> entry in top)
            {
                Grid row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Border swatch = new Border
                {
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(2),
                    Background = new SolidColorBrush(ColorForExtension(entry.Key)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(swatch, 0);

                StackPanel textStack = new StackPanel();
                textStack.Children.Add(new TextBlock
                {
                    Text = entry.Key,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextPrimaryBrush")
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = FormatSize(entry.Value),
                    FontSize = 10,
                    Foreground = (Brush)FindResource("TextSecondaryBrush")
                });
                Grid.SetColumn(textStack, 1);

                row.Children.Add(swatch);
                row.Children.Add(textStack);
                LegendPanel.Children.Add(row);
            }
        }

        private static void CollectExtensionSizes(DiskNode node, Dictionary<string, long> totals)
        {
            if (node.IsDirectory)
            {
                foreach (DiskNode child in node.Children)
                {
                    CollectExtensionSizes(child, totals);
                }
            }
            else
            {
                totals.TryGetValue(node.Extension, out long existing);
                totals[node.Extension] = existing + node.Size;
            }
        }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.#} {units[unitIndex]}";
        }

        private void DrawTile(LayoutRect rect, DiskNode node)
        {
            const double gap = 1.5;

            double x = rect.X + gap / 2;
            double y = rect.Y + gap / 2;
            double w = Math.Max(0, rect.Width - gap);
            double h = Math.Max(0, rect.Height - gap);

            if (w < 1 || h < 1)
            {
                return;
            }

            Brush fill;
            Brush border;

            if (node.IsDirectory)
            {
                fill = (Brush)FindResource("AccentTranslucentBrush");
                border = (Brush)FindResource("AccentBrush");
            }
            else
            {
                Color color = ColorForExtension(node.Extension);
                fill = new SolidColorBrush(color);
                border = new SolidColorBrush(Color.FromRgb(
                    (byte)Math.Min(255, color.R + 30),
                    (byte)Math.Min(255, color.G + 30),
                    (byte)Math.Min(255, color.B + 30)));
            }

            Border tile = new Border
            {
                Width = w,
                Height = h,
                Background = fill,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                ToolTip = $"{node.Name}\n{node.DisplaySize}",
                Cursor = node.IsDirectory && node.Children.Count > 0 ? Cursors.Hand : Cursors.Arrow
            };

            if (w > 40 && h > 30)
            {
                StackPanel labelStack = new StackPanel { Margin = new Thickness(4, 3, 4, 3) };
                labelStack.Children.Add(new TextBlock
                {
                    Text = node.Name,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                labelStack.Children.Add(new TextBlock
                {
                    Text = node.DisplaySize,
                    FontSize = 10,
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                tile.Child = labelStack;
            }
            else if (w > 40 && h > 16)
            {
                TextBlock label = new TextBlock
                {
                    Text = node.Name,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    Margin = new Thickness(4, 2, 4, 2),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                tile.Child = label;
            }

            Thickness normalThickness = tile.BorderThickness;
            tile.MouseEnter += (s, e) => tile.BorderThickness = new Thickness(2);
            tile.MouseLeave += (s, e) => tile.BorderThickness = normalThickness;

            if (node.IsDirectory && node.Children.Count > 0)
            {
                tile.MouseLeftButtonUp += (s, e) => AnimateTransition(() => _navigationStack.Add(node));
            }

            ContextMenu menu = new ContextMenu();
            MenuItem showInExplorer = new MenuItem { Header = "Show in Explorer" };
            showInExplorer.Click += (s, e) => ShowInExplorer(node);
            menu.Items.Add(showInExplorer);
            tile.ContextMenu = menu;

            Canvas.SetLeft(tile, x);
            Canvas.SetTop(tile, y);
            TreemapCanvas.Children.Add(tile);
        }

        private static void ShowInExplorer(DiskNode node)
        {
            try
            {
                if (node.IsDirectory)
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{node.FullPath}\"") { UseShellExecute = true });
                }
                else
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{node.FullPath}\"") { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open Explorer: " + ex.Message, "DotnetGuard DiskMap");
            }
        }

        private static Color ColorForExtension(string extension)
        {
            int hash = 17;
            foreach (char c in extension)
            {
                hash = hash * 31 + c;
            }

            double hue = Math.Abs(hash) % 360;
            return ColorFromHsl(hue, 0.42, 0.40);
        }

        private static Color ColorFromHsl(double hue, double saturation, double lightness)
        {
            double c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            double x = c * (1 - Math.Abs((hue / 60.0) % 2 - 1));
            double m = lightness - c / 2;

            double r1, g1, b1;

            if (hue < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (hue < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (hue < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (hue < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (hue < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            byte r = (byte)Math.Round((r1 + m) * 255);
            byte g = (byte)Math.Round((g1 + m) * 255);
            byte b = (byte)Math.Round((b1 + m) * 255);

            return Color.FromRgb(r, g, b);
        }
    }
}
