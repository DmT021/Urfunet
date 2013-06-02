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

            Clients.Add(new ClientWindow(new Client("A")));
            Clients.Add(new ClientWindow(new Client("B")));


            mytc.SelectedIndex = 0;
        }

        static char i = 'B';
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            i = (char)(((byte)i)+1);
            Clients.Add(new ClientWindow(new Client(i.ToString())));
        }
        public System.Collections.ObjectModel.ObservableCollection<ClientWindow> Clients { get; set; }
    }
}
