//------------------------------------------------------------------------------
// <copyright file="LoadingWindow.xaml.cs">
//     Copyright (c) Adam Wulkiewicz.
// </copyright>
//------------------------------------------------------------------------------

using System.Windows;

namespace GraphicalDebugging
{
    public partial class LoadingWindow : Window
    {
        public LoadingWindow()
        {
            IsClosed = false;
            InitializeComponent();
        }

        public bool IsClosed { get; private set; }

        private void LoadingStopButton_Click(object sender, RoutedEventArgs e)
        {
            IsClosed = true;
            this.Close();
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            IsClosed = true;
        }
    }
}
