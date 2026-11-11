using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Calendar
{
    public partial class MainWindow : Window
    {
        private enum ViewMode { Year, Month }

        private ViewMode _currentMode = ViewMode.Year;
        private DateTime _displayDate = DateTime.Today;
        private bool _isDark = false;
        private int _yearPageStart;

        private static readonly CultureInfo Ru = new CultureInfo("ru-RU");

        // Импорт функции из dwmapi.dll
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Константа для темного заголовка (актуальна для Windows 10 2004+ и Windows 11)
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCACTIVATE = 0x0086;

        public MainWindow()
        {
            InitializeComponent();
            YearPopup.PlacementTarget = PeriodContainer;

            SingleMonthView.MonthClicked += (s, dt) =>
            {
                _currentMode = ViewMode.Year;
                RefreshView();
            };

            RefreshView();

        }

        /// <summary>
        /// Применяет темный или светлый стиль к заголовку окна.
        /// </summary>
        private void ApplyTitleBarTheme(bool isDark)
        {
            try
            {
                // Получаем дескриптор окна (HWND)
                var hwnd = new WindowInteropHelper(this).EnsureHandle();
                if (hwnd == IntPtr.Zero) return;

                // 1 для темной темы, 0 для светлой
                int darkMode = isDark ? 1 : 0;

                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                SendMessage(hwnd, WM_NCACTIVATE, IntPtr.Zero, IntPtr.Zero);
                SendMessage(hwnd, WM_NCACTIVATE, new IntPtr(1), IntPtr.Zero);
            }
            catch (DllNotFoundException)
            {
                // На всякий случай, если система очень старая
            }
            catch (Exception ex)
            {
                // Логирование ошибки, если потребуется
                System.Diagnostics.Debug.WriteLine($"Failed to set title bar theme: {ex.Message}");
            }
        }


        // ---------- Отрисовка ----------
        private void RefreshView()
        {
            YearPopup.IsOpen = false;

            // ⬇️ Текст на кнопке = действие, которое произойдёт по клику
            ViewToggleButton.Content = _currentMode == ViewMode.Year ? "Месяц" : "Год";

            bool inYearMode = _currentMode == ViewMode.Year;
            PeriodContainer.Cursor = inYearMode ? Cursors.Hand : Cursors.Arrow;
            PeriodContainer.ToolTip = inYearMode ? "Нажмите, чтобы выбрать год" : null;
            PeriodChevron.Visibility = inYearMode ? Visibility.Visible : Visibility.Collapsed;

            if (_currentMode == ViewMode.Year)
            {
                YearGrid.Visibility = Visibility.Visible;
                SingleMonthView.Visibility = Visibility.Collapsed;
                PeriodLabel.Text = _displayDate.Year.ToString();

                YearGrid.Children.Clear();
                for (int m = 1; m <= 12; m++)
                {
                    var mv = new MonthView();
                    mv.SetMonth(_displayDate.Year, m);
                    mv.MonthClicked += MonthView_MonthClicked;
                    YearGrid.Children.Add(mv);
                }
            }
            else
            {
                YearGrid.Visibility = Visibility.Collapsed;
                SingleMonthView.Visibility = Visibility.Visible;

                string title = _displayDate.ToString("MMMM yyyy", Ru);
                PeriodLabel.Text = char.ToUpper(title[0]) + title.Substring(1);

                SingleMonthView.SetMonth(_displayDate.Year, _displayDate.Month);
            }
        }

        // ⬇️ Обработчик: клик по месяцу в году → открыть этот месяц
        private void MonthView_MonthClicked(object sender, DateTime selectedMonth)
        {
            _displayDate = selectedMonth;              // запоминаем выбранный месяц
            _currentMode = ViewMode.Month;             // переключаемся в режим «Месяц»
            RefreshView();
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            _displayDate = DateTime.Today;
            _currentMode = ViewMode.Month;
            RefreshView();
        }

        // ---------- Навигация ----------
        private void PrevPeriod_Click(object sender, RoutedEventArgs e)
        {
            _displayDate = _currentMode == ViewMode.Year
                ? _displayDate.AddYears(-1)
                : _displayDate.AddMonths(-1);
            RefreshView();
        }

        private void NextPeriod_Click(object sender, RoutedEventArgs e)
        {
            _displayDate = _currentMode == ViewMode.Year
                ? _displayDate.AddYears(1)
                : _displayDate.AddMonths(1);
            RefreshView();
        }

        private void ViewToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _currentMode = _currentMode == ViewMode.Year
                ? ViewMode.Month
                : ViewMode.Year;
            RefreshView();
        }

        // ---------- Тема ----------
        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _isDark = !_isDark;

            var uri = _isDark
                ? new Uri("Themes/Dark.xaml", UriKind.Relative)
                : new Uri("Themes/Light.xaml", UriKind.Relative);

            Application.Current.Resources.MergedDictionaries[0] =
                new ResourceDictionary { Source = uri };

            ThemeButton.Content = _isDark ? "☀" : "🌙";

            ApplyTitleBarTheme(_isDark);

            // ⬇️ Обновляем эффекты, т.к. DropShadowEffect не подписан на DynamicResource
            SingleMonthView.RefreshTheme();
            foreach (UIElement child in YearGrid.Children)
                if (child is MonthView mv) mv.RefreshTheme();
        }

        // Открытие попапа выбора года
        private void PeriodContainer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_currentMode != ViewMode.Year) return;

            // Страница из 12 лет, на которой находится текущий просматриваемый год
            _yearPageStart = (_displayDate.Year / 12) * 12;
            BuildYearPage();
            YearPopup.IsOpen = true;
            e.Handled = true;
        }

        // Следующая/предыдущая страница по 12 лет
        private void YearPagePrev_Click(object sender, RoutedEventArgs e)
        {
            _yearPageStart -= 12;
            BuildYearPage();
        }

        private void YearPageNext_Click(object sender, RoutedEventArgs e)
        {
            _yearPageStart += 12;
            BuildYearPage();
        }

        // Построение сетки из 12 кнопок для текущей страницы
        private void BuildYearPage()
        {
            YearRangeLabel.Text = $"{_yearPageStart} – {_yearPageStart + 11}";
            YearPopupGrid.Children.Clear();

            for (int i = 0; i < 12; i++)
            {
                int year = _yearPageStart + i;

                var btn = new Button
                {
                    Content = year.ToString(),
                    Margin = new Thickness(2),
                    Padding = new Thickness(4, 6, 4, 6),
                    FontSize = 13,
                    Tag = year
                };
                btn.Click += YearButton_Click;

                if (year == _displayDate.Year)
                {
                    // Текущий отображаемый год — акцентом, чтобы сразу найти взглядом
                    btn.FontWeight = FontWeights.Bold;
                    btn.Foreground = Brushes.White;
                    btn.SetResourceReference(Button.BackgroundProperty, "AccentBrush");
                }
                else
                {
                    btn.SetResourceReference(Button.BackgroundProperty, "ButtonBg");
                    btn.SetResourceReference(Button.ForegroundProperty, "WindowFg");
                }

                YearPopupGrid.Children.Add(btn);
            }
        }

        // Клик по году в попапе — переходим на него
        private void YearButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is int year)
            {
                // Сохраняем месяц/день — они не влияют на вид «Год», но пригодятся в «Месяц»
                _displayDate = new DateTime(year, _displayDate.Month, 1);
                YearPopup.IsOpen = false;
                RefreshView();
            }
        }
    }
}