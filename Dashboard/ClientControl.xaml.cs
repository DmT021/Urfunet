using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            Logs = new ObservableCollection<string>();

            foreach (var it in c.Logs) Logs.Add(it);
            c.Logs.CollectionChanged += Logs_CollectionChanged;
            //Header = c.Id.ToString();
            RemoteClients = new ObservableCollection<RemoteClient>();
            foreach (var it in c.RemoteClients) RemoteClients.Add(it);
            c.RemoteClients.CollectionChanged += RemoteClients_CollectionChanged;

            var q = new q(c.FileSystem.RootDir);
            Items = q.Items;
        }

        void Logs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => {
                if (e.NewItems != null)
                    foreach (var c in e.NewItems)
                    {
                        Logs.Insert(0,c as string);
                    }
                if (e.OldItems!= null)
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
                if (e.NewItems != null)
                    foreach (var c in e.NewItems)
                    {
                        RemoteClients.Insert(0, c as RemoteClient);
                    }
                if (e.OldItems != null)
                    foreach (var c in e.OldItems)
                    {
                        RemoteClients.Remove(c as RemoteClient);
                    }
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                    RemoteClients.Clear();
            }), null);
        }
        public q Selected { get; set; }



        public ObservableCollection<q> Items { get; set; }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var si = TvFS.SelectedItem as q;

            var folder = c.FileSystem.RootDir;
            if (si!=null&&si.obj is IFolder)
                folder = si.obj as IFolder;

            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog().HasValue)
                c.FileSystem.PushFile(folder, ofd.FileName);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var si = TvFS.SelectedItem as q;
            var folder = c.FileSystem.RootDir;
            if (si!=null&&si.obj is IFolder)
                folder = si.obj as IFolder;
            c.FileSystem.CreateFolder(folder, "New",IFSObjectEvents.local_created);
        }

        private void TvFS_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (TvFS.SelectedItem!=null) 
            Selected = TvFS.SelectedItem as q;
            N("Selected");
        }

        void N(string p)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(p));
        }
        public event PropertyChangedEventHandler PropertyChanged;

        private void TvFS_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Selected == null) return;
            var f = Selected as IFSObject;
            if (File.Exists(f.RealPath) || Directory.Exists(f.RealPath))
                System.Diagnostics.Process.Start(f.RealPath);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            c.FileSystem.Delete(Selected.obj,IFSObjectEvents.local_delete);
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (c.On)
                c.BeeOff();
            else
            {
                c.init();
                RemoteClients.Clear();
                foreach (var it in c.RemoteClients) RemoteClients.Add(it);
                c.RemoteClients.CollectionChanged += RemoteClients_CollectionChanged;
            }

        }

        
    }

    public class q : IFSObject
    {
        public string Name { get; set; }
        public q(IFSObject obj)
        {
            Items = new ObservableCollection<q>();
            this.obj = obj;
            if (obj is IFile)
                this.Name = (obj as IFile).Name;

            Load();


        }

        void Files_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ClientControl.myDispatcher.BeginInvoke(new Action(() =>
            {
                Items.Clear();
                Load();
            }), null);
        }

        private void Load()
        {
            if (obj is IFolder)
            {
                var d = obj as IFolder;
                Name = d.Name;

                foreach (var f in d.Files)
                    Items.Add(new q(f));

                foreach (var f in d.Folders)
                    Items.Add(new q(f));

                d.Folders.CollectionChanged += Folders_CollectionChanged;
                d.Files.CollectionChanged += Files_CollectionChanged;
            }
        }

        void Folders_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ClientControl.myDispatcher.BeginInvoke(new Action(() =>
            {
                Items.Clear();
                Load();
            }), null);
        }
        public string MetaPath
        {
            get { return obj.MetaPath; }
        }

        public string RealPath
        {
            get { return obj.RealPath; }
        }

        public IFSObject obj { get; set; }
        public ObservableCollection<q> Items { get; set; }
        public string Meta
        {
            get
            {
                string str = "";
                if (obj is IFile)
                {
                    str = JsonConvert.SerializeObject((obj as IFile).meta);
                }
                if (obj is IFolder)
                    str = obj.Name;
                return JSonPresentationFormatter.Format(str.Substring(1, str.Length - 2));
            }
        }


        public string RelativePath
        {
            get { throw new NotImplementedException(); }
        }
    }

}
