using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Dashboard;
using Microsoft.Win32;
using Newtonsoft.Json;
using YnetFS;
using YnetFS.FileSystem;
using YnetFS.FileSystem.Mock;

namespace YFSDashBoard
{
    /// <summary>
    /// Логика взаимодействия для ClientWindow.xaml
    /// </summary>
    public partial class ClientWindow : TabItem
    {
        public ClientWindow(Client c)
        {
            InitializeComponent();
 
            myGrid.Children.Add(new ClientControl(c));
            Header = c.Id.ToString();
        }


    }

}
