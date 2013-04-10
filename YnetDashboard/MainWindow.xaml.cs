using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ynet;

namespace YnetDashboard
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        Client c = null;
        public MainWindow()
        {
            InitializeComponent();

          

        }

   
        public System.Collections.ObjectModel.ObservableCollection<string> Log { get; set; }
        public System.Collections.ObjectModel.ObservableCollection<RemoteClient> Clients { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;

        private void Window_Closed_1(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            c = new Client(this.Dispatcher);
            DataContext = c;
            Title = c.IP;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (c.SelectedClient != null)
                c.SelectedClient.Send(new mDebug("Why not FS=) LOL"));
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (c.SelectedClient != null)
            {
                var q = new Microsoft.Win32.OpenFileDialog();
                if (q.ShowDialog().Value)
                    c.SelectedClient.Send(new mFileUploadRequest(new FileInfo(q.FileName)));
            }
        }


    }
}
