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

            this.Closed += MainWindow_Closed;

            DataContext = this;
            Clients = new System.Collections.ObjectModel.ObservableCollection<ClientWindow>();

            var a = new Client("A");
            var b = new Client("B");
            Clients.Add(new ClientWindow(a));
            Clients.Add(new ClientWindow(b));
            cts.Add(a);
            cts.Add(b);

            mytc.SelectedIndex = 0;
        }

        void MainWindow_Closed(object sender, EventArgs e)
        {
            foreach (var c in cts)
                c.Dispose();
        }

        static char i = 'B';
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            i = (char)(((byte)i)+1);
            var c = new Client(i.ToString());
            cts.Add(c);
            Clients.Add(new ClientWindow(c));

        }
        public System.Collections.ObjectModel.ObservableCollection<ClientWindow> Clients { get; set; }

        static List<Client> cts = new List<Client>();
    }
}
