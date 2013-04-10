using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using Newtonsoft.Json;
using System.Management;
using System.IO;
using System.Collections.ObjectModel;

namespace Ynet
{
    public delegate void dDataRecived(object data, EndPoint from);

    public class BaseCilent
    {
        public const int port = 8010;
    }


    public partial class Client : BaseCilent
    {

        public static Client instance { get; set; }

        public ObservableCollection<RemoteClient> Clients { get; set; }
        public RemoteClient SelectedClient { get; set; }
        public ObservableCollection<string> Log { get; set; }
        public ObservableCollection<string> files { get; set; }
        public void WriteLog(string str)
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                Log.Insert(0,str);
            }), null);
        }

        
        public Client(System.Windows.Threading.Dispatcher dispatcher)
        {
            instance = this;
            this.dispatcher = dispatcher;
            init_settings();

            StartTCPListner();
            StartBroadcastListner();


            new Thread(SayHello).Start();
        }

        private void init_settings()
        {
            Clients = new ObservableCollection<RemoteClient>();
            Log = new ObservableCollection<string>();
            files = new ObservableCollection<string>();

            //fs = new FsSync(Environment.GetFolderPath(Environment.SpecialFolder.Personal)+"\\Ynet\\");

            //RefreshfileList();


            #region GetCurrentIP
            List<string> ipAdresses = new List<string>();
            ManagementClass mc = new ManagementClass(
              "Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection moc = mc.GetInstances();

            foreach (ManagementObject mo in moc)
            {
                // Make sure this is a IP enabled device. 
                // Not something like memory card or VM Ware
                if (((bool)mo["ipEnabled"]))
                {
                    ipAdresses.Add(((string[])mo["IPAddress"])[0]);
                }
            }
            IP = ipAdresses[0];
            #endregion
        }


        #region TCP listening

        public string IP { get; set; }
        private void StartTCPListner()
        {
            server = new TcpListener(IPAddress.Parse(IP), port + 1);
            server.Start();

            new Thread(StartListen).Start();

 
        }
        private void StartListen(object obj)
        {
            // listenSocket.Listen(5);
            string data = string.Empty;
            while (true)
            {
                Message m = null;

                TcpClient tcpclient = server.AcceptTcpClient();
                NetworkStream stream = tcpclient.GetStream();

                byte[] message = new byte[4096];
                int bytesRead;

                data = string.Empty;
                while (true)
                {
                    bytesRead = 0;

                    try
                    {
                        //blocks until a client sends a message
                        bytesRead = stream.Read(message, 0, 4096);
                    }
                    catch
                    {
                        //a socket error has occured
                        throw new Exception("WTF - Socket error???");
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        //the client has disconnected from the server
                        break;
                    }

                    //message has successfully been received
                    var encoder = new UTF8Encoding();
                    data+=(encoder.GetString(message, 0, bytesRead));
                }


                
                tcpclient.Close();

                m = Message.Decode(data);

                var IP = IPAddress.Parse(m.FromIP);
                if (!Clients.Any(x => x.IP.Equals(IP)))
                {
                    var client = new RemoteClient(this, IP.ToString());
                    AddClient(client);
                }
                else
                {
                    var client = Clients.First(x => x.IP.Equals(IP));
                    MessageRecived(client, m);
                }
                
            }
        }

        public  void AddClient(RemoteClient client)
        {

            dispatcher.BeginInvoke(new Action(() => { Clients.Add(client); }), null);
            
        }

        private void MessageRecived(RemoteClient client, Message m)
        {
            if (m == null)
            {
                WriteLog("Empty message from " + client.ToString());
                return;
            }

            ExcecuteCommand(m);

            WriteLog(string.Format("client {0} say {1}", client, m.Data));
        }

        
        #endregion

        #region Broadcast listening
         public void StartBroadcastListner()
        {
            sock = new Socket(AddressFamily.InterNetwork,
            SocketType.Dgram, ProtocolType.Udp);
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            IPEndPoint iep = new IPEndPoint(IPAddress.Any, port);
            sock.Bind(iep);
            ep = (EndPoint)iep;
            
            CreateReciver();
        }

        public void SendBroadcast(Message m)
        {
            m.FromIP = IP;
            IPEndPoint iep = new IPEndPoint(IPAddress.Broadcast, port);
            byte[] data = Encoding.UTF8.GetBytes(m.Code());
            sock.SendTo(data, iep);
        }

        private void CreateReciver()
        {
            new Thread(() =>
            {
                while (true)
                {
                    var lep = ep;

                    byte[] data = new byte[1024];
                    int recv = sock.ReceiveFrom(data, ref lep);
                    string stringData = Encoding.UTF8.GetString(data, 0, recv);
                    //Log.Add("received: {0} from: {1}",
                    //           stringData, ep.ToString());


                    BrodcastRecived(stringData, null);

                }
            }).Start();
        }
        public Socket sock { get; set; }
        public EndPoint ep { get; set; }


        void BrodcastRecived(object data, EndPoint from)
        {
            var str = data.ToString();
            Message m = Message.Decode(str);
            if (m.FromIP == IP) return;//ignore self messages

            ExcecuteCommand(m);
        }
        #endregion




        private System.Windows.Threading.Dispatcher dispatcher;
        private void SayHello()
        {
            WriteLog("Try Hello (5 sec) ...");

            for (int i = 0; i < 10 ; i++)
            {
                SendBroadcast(new mHello());
                Thread.Sleep(500);
            }
            WriteLog(string.Format("FinishHello. Found {0} clients", Clients.Count));
            
        }




        public TcpListener server { get; set; }



    }


}
