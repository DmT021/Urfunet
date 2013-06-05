using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NLog;
using YnetFS.FileSystem;
using YnetFS.InteractionEnvironment;
using YnetFS.Messages;

namespace YnetFS
{


    public class RemoteClientsManager : ICollection<RemoteClient>, INotifyCollectionChanged
    {
        ObservableCollection<RemoteClient> Items { get; set; }
        public BaseInteractionEnvironment Env { get; set; }

        public RemoteClientsManager(BaseInteractionEnvironment env)
        {
            this.Env = env;
            Items = new ObservableCollection<RemoteClient>();
            Items.CollectionChanged += items_CollectionChanged;

            Load();
        }

        void items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (CollectionChanged != null) CollectionChanged(sender, e);
        }





        public RemoteClient this[string index]
        {
            get { lock (this)return Items.FirstOrDefault(x => x.Id == index); }
        }

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public void Add(RemoteClient item)
        {
            lock (this)
            {
                if (Items.Contains(item))
                {
                    var oldit = Items.FirstOrDefault(x => x.Id == item.Id);
                    var oldind = Items.IndexOf(oldit);
                    oldit.PropertyChanged -= item_PropertyChanged;
                    Items.Remove(oldit);
                    Items.Insert(oldind, item);
                }
                else
                    Items.Add(item);

                item.PropertyChanged += item_PropertyChanged;
                item_PropertyChanged(item, null);
            }
        }
        void Load()
        {
            var filename = Path.Combine(Env.ParentClient.MyDir.FullName, "contacts.dat");
            if (!File.Exists(filename)) return;
            Items = (ObservableCollection<RemoteClient>)JsonConvert.DeserializeObject(File.ReadAllText(filename, Encoding.UTF8), typeof(ObservableCollection<RemoteClient>));

        }
        void Save()
        {
            var filename = Path.Combine(Env.ParentClient.MyDir.FullName, "contacts.dat");

            //   if (File.Exists(filename))
            //      File.Delete(filename);
            File.WriteAllText(filename, JSonPresentationFormatter.Format(JsonConvert.SerializeObject(Items)), Encoding.UTF8);
        }
        void item_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Save();


            if (CollectionChanged != null)
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,sender));
        }

        public void Clear()
        {
            lock (this) Items.Clear();
        }

        public bool Contains(RemoteClient item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(RemoteClient[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { lock (Items)return Items.Count; }
        }

        public bool IsReadOnly
        {
            get { throw new NotImplementedException(); }
        }

        public bool Remove(RemoteClient item)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<RemoteClient> GetEnumerator()
        {
            lock (this) return Items.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            lock (this) return Items.GetEnumerator();
        }

        public int OnlineCount { get { lock (this)return Items.Count(x => x.IsOnline); } }
    }

    
}
