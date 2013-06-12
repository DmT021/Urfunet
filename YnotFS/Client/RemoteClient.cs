using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using NLog;
using YnetFS.FileSystem;
using YnetFS.InteractionEnvironment;
using YnetFS.Messages;

namespace YnetFS
{
    public interface INode
    {
        string Id { get; }
        bool IsRemote { get; }
        bool IsOnline { get; }
        int hash { get; }
    }
    public class RemoteClient : INode, INotifyPropertyChanged
    {

        public string Id { get; set; }
        [JsonIgnore]
        public BaseInteractionEnvironment Env { get; set; }
        public RemoteClient(string FromId, BaseInteractionEnvironment env)
        {
            this.Id = FromId;
            this.Env = env;
            IsOnline = false;
        }
        public bool IsRemote
        {
            get { return true; }
        }

        bool _isonline = true;
        public bool IsOnline
        {
            get { return _isonline; }
            set
            {
                if (_isonline == value) return;
                _isonline = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("IsOnline"));
            }
        }

        public int hash
        {
            get { return Id.GetHashCode(); }
        }

        //bool _LastOne = false;
        //public bool LastOne
        //{
        //    get
        //    {
        //        return _LastOne;
        //    }
        //    set
        //    {
        //        _LastOne = value;
        //        if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("LastOne"));
        //    }
        //}

        bool syncronized = false;
        public bool Synchronized
        {
            get
            {
                return syncronized;
            }
            set
            {
                syncronized = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Syncronized"));
            }
        }

        public void Send(Message message)
        {
            if (!IsOnline)
            {
                throw new Exception(string.Format("cant send message to {0}", this));
                //Env.ParentClient.Log(LogLevel.Error, "cant send message to {0}", this);
                // Env.RemoveRemoteClient(this);
                return;
            }

            message.FromId = Env.ParentClient.Id;
            Env.Send(this, message);
        }

        public override string ToString()
        {
            return Id + " [" + (IsOnline ? "+" : "-") + "]" + (Synchronized ? "syncd" : "");
        }

        public override bool Equals(object obj)
        {
            if (!(obj is RemoteClient)) return false;
            return hash == (obj as RemoteClient).hash;
        }
        public override int GetHashCode()
        {
            return hash;
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    
}
