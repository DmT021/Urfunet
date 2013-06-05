using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YnetFS.old
{
    //public class old_RemoteClient : INode
    //{

    //    public old_RemoteClient(string FromId, BaseInteractionEnvironment env)
    //    {
    //        this.Id = FromId;
    //        this.Env = env;
    //    }
    //    /*
    //             public IPAddress IP { get; set; }
    //            old_Client sourcelist = null;
    //            public old_RemoteClient(old_Client local, string IP)
    //            {
    //                sourcelist = local;
    //                this.IP = IPAddress.Parse(IP);
    //            }

    //            public void Send(Message m)
    //            {
    //                m.FromIP = old_Client.instance.IP;
    //                Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    //                IPEndPoint ipEndpoint = new IPEndPoint(IP, port + 1);
    //                listenSocket.Connect(ipEndpoint);
    //                listenSocket.Send(Encoding.UTF8.GetBytes(m.Code()));
    //                listenSocket.Close();
    //            }
    //            public override string ToString()
    //            {
    //                return IP.ToString();
    //            }
    //     */
    //    public string Id { get; private set; }

    //    public void Send(Message message)
    //    {
    //        if (!IsOnline)
    //        {
    //            throw new Exception(string.Format("cant send message to {0}", this));
    //            //Env.ParentClient.Log(LogLevel.Error, "cant send message to {0}", this);
    //            // Env.RemoveRemoteClient(this);
    //            return;
    //        }

    //        message.FromId = Env.ParentClient.Id;
    //        Env.Send(this, message);

    //    }

    //    public BaseInteractionEnvironment Env { get; set; }

    //    public override string ToString()
    //    {
    //        return Id;
    //    }

    //    public List<BaseFile> myFiles
    //    {
    //        get
    //        {
    //            var res = new List<BaseFile>();
    //            foreach (var it in Env.ParentClient.FileSystem.GetFileList())
    //                if (it.meta.Replics.Contains(Id))
    //                    res.Add(it);
    //            return res;
    //        }
    //    }
    //    public bool isOwnerOf(BaseFile file) { return Id == file.meta.Owner; }

    //    public bool IsRemote
    //    {
    //        get { return true; }
    //    }

    //    /// <summary>
    //    /// true if online
    //    /// </summary>
    //    public bool last_online_state { get; set; }
    //    public bool IsOnline
    //    {
    //        get
    //        {
    //            last_online_state = Env.CheckRemoteClientState(this);
    //            return last_online_state;
    //        }
    //    }


    //    public int hash
    //    {
    //        get { return Id.GetHashCode(); }
    //    }

    //}

}
