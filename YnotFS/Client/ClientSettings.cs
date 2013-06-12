using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YnetFS
{

    public class ClientSettings
    {
        public ClientSettings()
        {
            RemainingClients = new ObservableCollection<string>();
            RemainingClients.CollectionChanged += RemainingClients_CollectionChanged;
        }

        public ClientSettings(Client c) :
            this()
        {
            this.c = c;
            var filename = Path.Combine(c.MyDir.FullName, "settings.dat");
            if (File.Exists(filename))
            {
                var tmp = (ClientSettings)JsonConvert.DeserializeObject(File.ReadAllText(filename), typeof(ClientSettings));
                RemainingClients = new ObservableCollection<string>(tmp.RemainingClients);
                RemainingClients.CollectionChanged += RemainingClients_CollectionChanged;
                WasInSynchronizedGroup = tmp.WasInSynchronizedGroup;
            }

            Save();
        }

        public void Save()
        {
            lock (this)
            {
                if (c == null) return;
                var filename = Path.Combine(c.MyDir.FullName, "settings.dat");
                // if (File.Exists(filename))
                //     File.Delete(filename);
                File.WriteAllText(filename, JSonPresentationFormatter.Format(JsonConvert.SerializeObject(this)));
            }
        }

        //bool _LastOne = false;
        //[JsonIgnore]
        //public bool LastOne
        //{
        //    //get { return _LastOne; }
        //    //set
        //    //{
        //    //    if (_LastOne == value) return;
        //    //    _LastOne = value;
        //    //    Save();
        //    //}
        //    get
        //    {
        //        if (FirstStart)
        //            return false;

        //        var online = c.RemoteClients.GetOnline();
        //        foreach (var item in RemainingClients)
        //        {
        //            if (!online.TrueForAll(x => x.Id != item))
        //                return false;
        //        }
        //        return true;
        //    }
        //}

        //public bool FirstStart = false;
        //public string Id { get; set; }
        //public DateTime LastAliveTime { get; set; }
        public ObservableCollection<string> RemainingClients { get; private set; }
        [JsonIgnore]
        public bool synchronized;
        public bool WasInSynchronizedGroup
        {
            get
            {
                return synchronized;
            }
            set
            {
                synchronized = value;
                Save();
            }
        }

        [JsonIgnore]
        public Client c { get; set; }

        void RemainingClients_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Save();
        }

        public ClientSettings Clone()
        {
            var r = new ClientSettings()
            {
                RemainingClients = new ObservableCollection<string>(this.RemainingClients),
                //FirstStart = this.FirstStart,
                c = this.c,
                WasInSynchronizedGroup = this.WasInSynchronizedGroup
            };
            r.RemainingClients.CollectionChanged += r.RemainingClients_CollectionChanged;
            return r;
        }
    }
}
