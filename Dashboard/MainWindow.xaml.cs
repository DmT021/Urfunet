using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using YFSDashBoard;
using YnetFS;

namespace Dashboard
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Clients = new System.Collections.ObjectModel.ObservableCollection<ClientWindow>();

            Clients.Add(new ClientWindow(new Client("ed053226-8930-47d6-acfa-29a745314e02")));
            Clients.Add(new ClientWindow(new Client("5009b6ab-cd1f-416a-b259-2a1300a529ac")));


            mytc.SelectedIndex = 0;
        }

        static int i = 1;
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var str = string.Format("{0}{0}{0}{0}{0}{0}{0}{0}-{0}{0}{0}{0}-{0}{0}{0}{0}-{0}{0}{0}{0}-{0}{0}{0}{0}{0}{0}{0}{0}{0}{0}{0}{0}", i);
            i++;
            Clients.Add(new ClientWindow(new Client(str)));
        }
        public System.Collections.ObjectModel.ObservableCollection<ClientWindow> Clients { get; set; }
    }
}
