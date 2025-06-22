using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProjectMaster
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            // Регистрируем обработчики для обоих ScrollViewer'ов
            LeftScrollViewer.PreviewMouseWheel += OnScrollViewerMouseWheel;
            RightScrollViewer.PreviewMouseWheel += OnScrollViewerMouseWheel;
        }

        private void OnScrollViewerMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null)
            {
                // Прокручиваем на величину e.Delta / 3
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3.0);
                e.Handled = true;
            }
        }
    }
}