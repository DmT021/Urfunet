using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using NLog;
using YnetFS.InteractionEnvironment;

namespace YnetFS
{
    public class RemoteClient
    {

        public RemoteClient(Guid FromId,BaseInteractionEnvironment env)
        {
            this.Id = FromId;
            this.Env = env;
        }
        /*
                 public IPAddress IP { get; set; }
                Client from = null;
                public RemoteClient(Client local, string IP)
                {
                    from = local;
                    this.IP = IPAddress.Parse(IP);
                }

                public void Send(Message m)
                {
                    m.FromIP = Client.instance.IP;
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
        public Guid Id { get; private set; }

        public void Send(Messages.Message message)
        {
            if (!IsOnline())
            {
                Env.ParentClient.Log(LogLevel.Error, "cant send message to {0}", this);
                Env.RemoveRemoteClient(this);
                return;
            }

            message.FromId= Env.ParentClient.Id;
            Env.Send(this, message);
        }

        private bool IsOnline()
        {
            return Env.CheckRemoteClientState(this)==RemoteClientState.Connected;
        }

        public BaseInteractionEnvironment Env { get; set; }

        public override string ToString()
        {
            return Id.ToString();
        }
    }
  
}
