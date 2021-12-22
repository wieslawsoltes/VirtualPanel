using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VirtualScrollDemo
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<string> Items { get; set; }

        public double ItemHeight { get; set; }

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
            Items = new ObservableCollection<string>();

            for (var i = 0; i < 1_000; i++)
            {
                Items.Add($"Item {i}");
            }

            ItemHeight = 30;
            
            DataContext = this;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}