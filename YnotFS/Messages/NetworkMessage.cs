using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YnetFS.Messages
{
    public abstract class NetworkMessage : Message
    {

        [JsonProperty(PropertyName = "FromIP")]
        public string FromIP { get; set; }

        public override void OnRecived(RemoteClient from, Client to)
        {
        }
    }
    public class mHello : NetworkMessage
    {
        public override void OnRecived(RemoteClient from, Client to)
        {
            if (!Environment.HasRemoteClient(FromId))
            {
                var client = new RemoteClient(FromId, Environment);
                Environment.AddRemoteClient(client);
                client.Send(new mHI());
            }
        }
    }

    public class mHI : NetworkMessage
    {
        public override void OnRecived(RemoteClient from, Client to)
        {
            if (!Environment.HasRemoteClient(FromId))
            {
                var client = new RemoteClient(FromId, Environment);
                Environment.RemoveRemoteClient(client);

            }
        }
    }
    /*
 
      *    public class mDebug : Message
     {
         public mDebug(string message)
         {
             Data = message;
         }

         public override void Invoke(Client c)
         {
             c.WriteLog(Data);
         }
     }

     public class FileTranfer : Message
     {
         [JsonProperty(PropertyName = "port")]
         public int port { get; set; }

         [JsonProperty(PropertyName = "length")]
         public long length { get; set; }

         [JsonProperty(PropertyName = "md5")]
         public string fileMd5 { get; set; }

         protected FileInfo file { get; set; }

         public static FileInfo Expect_file { get; set; }

         protected string GetMD5HashFromFile(string fileName)
         {
             FileStream file = new FileStream(fileName, FileMode.Open);
             MD5 md5 = new MD5CryptoServiceProvider();
             byte[] retVal = md5.ComputeHash(file);
             file.Close();

             StringBuilder sb = new StringBuilder();
             for (int i = 0; i < retVal.Length; i++)
             {
                 sb.Append(retVal[i].ToString("x2"));
             }
             return sb.ToString();
         }

         protected long BlockSize = 5 * 1024 * 1024;//1mb
         public override void Invoke(Client c)
         {
             throw new NotImplementedException();
         }
     }

     public class mFileUploadRequest : FileTranfer
     {


         public mFileUploadRequest(FileInfo f)
         {
             file = f;
             FileUploadRequestConfirmation.Expect_file = f;
             if (file != null)
             {
                 Data = file.FullName;
                 fileMd5 = GetMD5HashFromFile(file.FullName);
                 length = file.Length;
             }
         }
         public override void Invoke(Client c)
         {
             var r = new Random(DateTime.Now.Millisecond);
             port = r.Next(1100, 1200);
             //==============

             new Thread(() =>
             {
                 IPEndPoint ipEnd = new IPEndPoint(IPAddress.Parse(c.IP), port);
                 Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                 sock.Bind(ipEnd);
                 sock.Listen(1);
                 Socket client = sock.Accept();
                 byte[] buffer = new byte[BlockSize];
                 long receive = 0L;//, length = BitConverter.ToInt64(buffer, 0);
                 var filename = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "//Ynet//" + new FileInfo(Data).Name;
                 using (FileStream writer = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                 {
                     int received;
                     while (receive < length)
                     {
                         received = client.Receive(buffer);
                         writer.Write(buffer, 0, received);
                         writer.Flush();
                         receive += (long)received;

                        
                         c.WriteLog((((decimal)receive / (decimal)length)).ToString("##.##%") );
                     }
                 }

                 var md5 = GetMD5HashFromFile(filename);
                 c.WriteLog(md5);
                 System.Diagnostics.Debug.Assert(fileMd5 == md5, "Файл передан не верно");
             }).Start();
             //==============
             Sender.Send(new FileUploadRequestConfirmation(port));
         }
     }

     public class FileUploadRequestConfirmation : FileTranfer
     {


         public FileUploadRequestConfirmation(int port)
         {
             this.port = port;
         }

         //отправка
         public override void Invoke(Client c)
         {
             new Thread(() => {
                 try
                 {
                     Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                     sock.Connect(Sender.IP, port);
                     var filename = Expect_file.FullName;
                     using (FileStream reader = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.None))
                     {
                         long send = 0L;
                         length = reader.Length;
                         string fileName = Path.GetFileName(filename);

                         byte[] buffer = new byte[BlockSize];
                         int read, sent;

                         while ((read = reader.Read(buffer, 0, (int)BlockSize)) != 0)
                         {
                             sent = 0;
                             while ((sent += sock.Send(buffer, sent, read, SocketFlags.None)) < read )
                                 send += (long)sent;

                             send += (long)sent;
                             c.WriteLog((((decimal)send / (decimal)length)).ToString("##.##%"));
                         }
                     }
                 }
                 catch (Exception ex)
                 {
                         c.WriteLog("File Sending fail." + ex.Message);
                 }
             }).Start();
         }
     }
      * */
}
