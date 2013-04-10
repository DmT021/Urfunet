using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Ynet
{
 

    public class RemoteClient:BaseCilent
    {
        public IPAddress IP { get; set; }
        Client from = null;
        public RemoteClient(Client local,string IP)
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

    }
}
