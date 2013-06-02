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
        public new_BaseInteractionEnvironment Env { get; set; }
        public RemoteClient(string FromId, new_BaseInteractionEnvironment env)
        {
            this.Id = FromId;
            this.Env = env;
            IsOnline = true;
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
        bool _LastOne = false;
        public bool LastOne
        {
            get
            {
                return _LastOne;
            }
            set
            {
                _LastOne = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("LastOne"));
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
            return Id + " [" + (IsOnline ? "+" : "-") + "]" + (LastOne ? "lo" : "");
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

    public class old_RemoteClient : INode
    {

        public old_RemoteClient(string FromId, BaseInteractionEnvironment env)
        {
            this.Id = FromId;
            this.Env = env;
        }
        /*
                 public IPAddress IP { get; set; }
                old_Client sourcelist = null;
                public old_RemoteClient(old_Client local, string IP)
                {
                    sourcelist = local;
                    this.IP = IPAddress.Parse(IP);
                }

                public void Send(Message m)
                {
                    m.FromIP = old_Client.instance.IP;
                    Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    IPEndPoint ipEndpoint = new IPEndPoint(IP, port + 1);
                    listenSocket.Connect(ipEndpoint);
                    listenSocket.Send(Encoding.UTF8.GetBytes(m.Code()));
                    listenSocket.Close();
                }
                public override string ToString()
                {
                    return IP.ToString();
                }
         */
        public string Id { get; private set; }

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

        public BaseInteractionEnvironment Env { get; set; }

        public override string ToString()
        {
            return Id;
        }

        public List<BaseFile> myFiles
        {
            get
            {
                var res = new List<BaseFile>();
                foreach (var it in Env.ParentClient.FileSystem.GetFileList())
                    if (it.meta.Replics.Contains(Id))
                        res.Add(it);
                return res;
            }
        }
        public bool isOwnerOf(BaseFile file) { return Id == file.meta.Owner; }

        public bool IsRemote
        {
            get { return true; }
        }

        /// <summary>
        /// true if online
        /// </summary>
        public bool last_online_state { get; set; }
        public bool IsOnline
        {
            get
            {
                last_online_state = Env.CheckRemoteClientState(this);
                return last_online_state;
            }
        }


        public int hash
        {
            get { return Id.GetHashCode(); }
        }

    }

}
