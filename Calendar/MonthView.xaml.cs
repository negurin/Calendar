using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Calendar
{
    public partial class MonthView : UserControl
    {
        private static readonly CultureInfo Ru = new CultureInfo("ru-RU");
        private int _year, _month;
        private bool _hasData;

        private bool _isHovered;        // мышь над карточкой
        private bool _isCurrentMonth;   // в этом месяце находится сегодняшний день

        public event EventHandler<DateTime> MonthClicked;

        private bool _isInteractive = true;
        public bool IsInteractive
        {
            get => _isInteractive;
            set
            {
                _isInteractive = value;
                Cursor = value ? Cursors.Hand : Cursors.Arrow;
                if (!value) _isHovered = false;
                ApplyBorderState();
            }
        }

        public MonthView()
        {
            InitializeComponent();

            SizeChanged += (s, e) => { if (_hasData) Render(); };
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseEnter += OnMouseEnter;
            MouseLeave += OnMouseLeave;

            Cursor = Cursors.Hand;
        }

        public void SetMonth(int year, int month)
        {
            _year = year;
            _month = month;
            _hasData = true;

            // ⬇️ Проверяем, содержит ли этот месяц сегодняшнюю дату
            _isCurrentMonth = year == DateTime.Today.Year
                           && month == DateTime.Today.Month;

            Render();
            ApplyBorderState();   // применить/снять акцентную рамку
        }

        public void RefreshTheme()
        {
            ApplyBorderState();
        }

        // ---------- Наведение ----------
        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            if (!_isInteractive) return;
            _isHovered = true;
            ApplyBorderState();
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isInteractive) return;
            _isHovered = false;
            ApplyBorderState();
        }

        /// <summary>
        /// Единая точка принятия решения о виде рамки.
        ///   обычный месяц, без наведения  → 1px, MonthBorder
        ///   обычный месяц, наведение     → 2px, AccentBrush
        ///   сегодняшний месяц, без наведения → 2px, AccentBrush
        ///   сегодняшний месяц, наведение → 3px, AccentBrush (видно, что кликабельно)
        /// </summary>
        private void ApplyBorderState()
        {
            bool hovered = _isInteractive && _isHovered;

            // Рамка — только для hover
            if (hovered)
            {
                RootBorder.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
                RootBorder.BorderThickness = new Thickness(2);
            }
            else
            {
                RootBorder.SetResourceReference(Border.BorderBrushProperty, "MonthBorder");
                RootBorder.BorderThickness = new Thickness(1);
            }

            // «Сегодня» — акцентное свечение. Цвет берём из темы,
            // чтобы в тёмной теме он был ярче и заметнее.
            if (_isCurrentMonth)
            {
                var glowColor = (Color)Application.Current.Resources["AccentGlowColor"];

                RootBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = glowColor,
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = hovered ? 0.85 : 0.65
                };
            }
            else
            {
                RootBorder.Effect = null;
            }
        }

        // ---------- Клик ----------
        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isInteractive || !_hasData) return;
            MonthClicked?.Invoke(this, new DateTime(_year, _month, 1));
            e.Handled = true;
        }

        // ---------- Отрисовка (без изменений) ----------
        private static double Clamp(double v, double min, double max)
            => Math.Max(min, Math.Min(max, v));

        private void Render()
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 1 || h <= 1) { w = 160; h = 130; }

            double titleFont = Clamp(Math.Min(h * 0.09, w * 0.09), 8, 28);
            double weekdayFont = Clamp(Math.Min(h * 0.05, w * 0.06), 6, 20);
            double dayFont = Clamp(Math.Min(h * 0.075, w * 0.08), 7, 42);

            MonthTitle.FontSize = titleFont;

            var firstOfMonth = new DateTime(_year, _month, 1);
            string title = firstOfMonth.ToString("MMMM yyyy", Ru);
            MonthTitle.Text = char.ToUpper(title[0]) + title.Substring(1);

            DaysGrid.Children.Clear();
            DaysGrid.ColumnDefinitions.Clear();
            DaysGrid.RowDefinitions.Clear();

            for (int c = 0; c < 7; c++)
                DaysGrid.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < 7; r++)
                DaysGrid.RowDefinitions.Add(new RowDefinition
                { Height = new GridLength(1, GridUnitType.Star) });

            string[] weekdays = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
            for (int i = 0; i < 7; i++)
            {
                var tb = new TextBlock
                {
                    Text = weekdays[i],
                    FontSize = weekdayFont,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                tb.SetResourceReference(TextBlock.ForegroundProperty, "WeekdayFg");
                Grid.SetRow(tb, 0);
                Grid.SetColumn(tb, i);
                DaysGrid.Children.Add(tb);
            }

            int startOffset = ((int)firstOfMonth.DayOfWeek + 6) % 7;
            int daysInMonth = DateTime.DaysInMonth(_year, _month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                int cellIndex = startOffset + day - 1;
                int row = cellIndex / 7 + 1;
                int col = cellIndex % 7;

                DateTime current = new DateTime(_year, _month, day);
                var txt = new TextBlock
                {
                    Text = day.ToString(),
                    FontSize = dayFont,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                FrameworkElement cell;
                if (current.Date == DateTime.Today)
                {
                    txt.Foreground = Brushes.White;
                    var border = new Border
                    {
                        Child = txt,
                        CornerRadius = new CornerRadius(dayFont * 0.7),
                        Padding = new Thickness(dayFont * 0.35, dayFont * 0.1,
                                                dayFont * 0.35, dayFont * 0.1),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    border.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
                    cell = border;
                }
                else
                {
                    bool isWeekend = current.DayOfWeek == DayOfWeek.Saturday
                                  || current.DayOfWeek == DayOfWeek.Sunday;
                    txt.SetResourceReference(TextBlock.ForegroundProperty,
                        isWeekend ? "WeekendFg" : "WindowFg");
                    cell = txt;
                }

                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, col);
                DaysGrid.Children.Add(cell);
            }
        }
    }
}