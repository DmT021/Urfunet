using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
using Microsoft.Win32;
using Newtonsoft.Json;
using YFSDashBoard;
using YnetFS;
using YnetFS.FileSystem;

namespace Dashboard
{
    /// <summary>
    /// Логика взаимодействия для ClientControl.xaml
    /// </summary>
    public partial class ClientControl : UserControl, INotifyPropertyChanged
    {
        public static System.Windows.Threading.Dispatcher myDispatcher { get; set; }
        public Client c { get; set; }

        public ObservableCollection<string> Logs { get; set; }
        public ObservableCollection<RemoteClient> RemoteClients { get; set; }

        public ClientControl(Client client)
        {
            InitializeComponent();
            myDispatcher = Dispatcher;

            DataContext = this;
            c = client as Client;
            c.StateChanged += c_StateChanged;

            cpanel.IsEnabled = c.State == ClientStates.Online;

            Logs = new ObservableCollection<string>();

            foreach (var it in c.Logs) Logs.Add(it);
            c.Logs.CollectionChanged += Logs_CollectionChanged;
            //Header = c.Id.ToString();
            RemoteClients = new ObservableCollection<RemoteClient>();
            foreach (var it in c.RemoteClients) RemoteClients.Add(it);
            c.RemoteClients.CollectionChanged += RemoteClients_CollectionChanged;

            c.FileSystem.OnFileEvent += (s, de) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    v_curdir = v_curdir;
                }), null);
            };

            c.FileSystem.OnFolderEvent += (s, de) =>
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    v_curdir = v_curdir;
                }), null);
            };
            label1.Content = c.State.ToString() + "(" + (c.Synchronized ? "Synchronized" : "Not synchronized") + ")";

            real_curdir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)+"\\test";

            v_curdir = c.FileSystem.RootDir;



        }
        string _real_curdir = "";
        string real_curdir
        {
            get { return _real_curdir; }
            set
            {
                _real_curdir = value;
                realfiles.ItemsSource = new List<string> { ".." }.Union(Directory.GetFiles(real_curdir).Select(x => new FileInfo(x).Name)).Union(Directory.GetDirectories(real_curdir).Select(x => new DirectoryInfo(x).Name));
                N("");
            }
        }

        BaseFolder _v_curdir = null;
        BaseFolder v_curdir
        {
            get { return _v_curdir; }
            set
            {

                _v_curdir = value;

                var items = new List<string>();
                if (value.Name != "\\") items.Add("..");
                items.AddRange(v_curdir.Items.Select(x => x.Name));

                TvFS.ItemsSource = items;
                N("");
            }
        }


        private void realfiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (realfiles.SelectedItem != null)
            {
                var newpath = System.IO.Path.Combine(real_curdir, realfiles.SelectedItem.ToString());
                if (Directory.Exists(newpath))
                    real_curdir = newpath;


            }

        }



        private void realfiles_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (realfiles.SelectedItem != null)
                DragDrop.DoDragDrop(realfiles, realfiles.SelectedItem, DragDropEffects.Copy);
        }

        void c_StateChanged(object sender, ClientStates NewState)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                cpanel.IsEnabled = NewState == ClientStates.Online;
                label1.Content = NewState.ToString();// +"(" + c.Settings.LastOne.ToString() + ")";

            }), null);
        }


        void Logs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.NewItems != null)
                    foreach (var c in e.NewItems)
                    {
                        Logs.Insert(0, c as string);
                    }
                if (e.OldItems != null)
                    foreach (var c in e.OldItems)
                    {
                        Logs.Remove(c as string);
                    }
            }), null);
        }
        void RemoteClients_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RemoteClients.Clear();
                foreach (var it in (sender as ICollection<RemoteClient>))
                    RemoteClients.Add(it);
                //if (e.NewItems != null)
                //    foreach (var c in e.NewItems)
                //    {
                //        RemoteClients.Insert(0, c as RemoteClient);
                //    }
                //if (e.OldItems != null)
                //    foreach (var c in e.OldItems)
                //    {
                //        RemoteClients.Remove(c as RemoteClient);
                //    }
                //if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                //    RemoteClients.Clear();
            }), null);
        }





        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var foldname = Prompt.ShowDialog("Select folder name", "New folder");
            if (foldname != null)
                c.FileSystem.CreateFolder(v_curdir, foldname, FSObjectEvents.local_created);
        }


        void N(string p)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(p));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        string selected = string.Empty;

        public string Meta { get; set; }

        private void TvFS_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TvFS.SelectedItem == null) return;
            selected = TvFS.SelectedItem.ToString();
            //update meta info on window

            string str = "";
            var obj = v_curdir.Items.FirstOrDefault(x => x.Name == selected);
            if (obj == null) return;
            if (obj is BaseFile)
            {
                str = JsonConvert.SerializeObject((obj as BaseFile).meta);
            }
            if (obj is BaseFolder)
                str = obj.Name;
            Meta = JSonPresentationFormatter.Format(str.Substring(1, str.Length - 2));
            N("Meta");
        }

        private void TvFS_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

            if (selected == "..")
            {
                v_curdir = v_curdir.ParentFolder as BaseFolder;
                return;
            }

            var pfold = v_curdir.Folders.FirstOrDefault(x => x.Name == selected);
            if (pfold != null)
            {
                v_curdir = pfold;
                return;
            }

            var file = v_curdir.Files.FirstOrDefault(x => x.Name == selected);
            if (file == null) return;

            var opres = file.Open();
            if (!opres)
            {
                MessageBox.Show("Файл открыт на другом узле");
                return;
            }
            var p = new Process();
            p.EnableRaisingEvents = true;
            p.StartInfo = new ProcessStartInfo(file.RealPath);
            p.Exited += (s, q) =>
            {
                file.Close();
            };
            p.Start();

        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(selected)) return;
            var obj = v_curdir.Items.FirstOrDefault(x => x.Name == selected);
            if (obj != null)
                c.FileSystem.Delete(obj, FSObjectEvents.local_delete);
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (c.State != ClientStates.Offline)
                c.ShutDown();
            else
            {
                c.Up();

                RemoteClients.Clear();
                foreach (var it in c.RemoteClients) RemoteClients.Add(it);
                c.RemoteClients.CollectionChanged += RemoteClients_CollectionChanged;
                N("Items");
            }
        }

        private void TvFS_Drop(object sender, DragEventArgs e)
        {
            var data = (string)e.Data.GetData(DataFormats.Text);
            var fromitem = System.IO.Path.Combine(real_curdir, data);


            if (Directory.Exists(fromitem))//если это папка
            {
                copydir(fromitem, v_curdir);
            }
            else
                c.FileSystem.AddFile(v_curdir, fromitem);

        }

        void copydir(string from, BaseFolder to)
        {

            var newf = to.FS.CreateFolder(to, new DirectoryInfo(from).Name, FSObjectEvents.local_created);
            foreach (var f in Directory.GetFiles(from))
                c.FileSystem.AddFile(newf, f);
            foreach (var f in Directory.GetDirectories(from))
                copydir(f, newf);
        }



    }




}
