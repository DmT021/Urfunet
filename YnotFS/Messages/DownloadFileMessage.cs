using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NLog;
using NLog.Targets;
using YnetFS.FileSystem;
using YnetFS.FileSystem.Mock;
using YnetFS.InteractionEnvironment;

namespace YnetFS.Messages
{

    //Запрос на загрузку файла
    public class DownloadFileMessage : FsObjectOperationMessage
    {

        public static DirectoryInfo ExchageFolder = null;
        public static int count = 0;


        public static Dictionary<string, EventWaitHandle> WaitHandles = new Dictionary<string, EventWaitHandle>();

        public DownloadFileMessage() { }

        public DownloadFileMessage(BaseFile baseFile, EventWaitHandle m)
            : base(baseFile)
        {
            if (m != null && !WaitHandles.ContainsKey(srcFile.RelativePath))
                WaitHandles.Add(srcFile.RelativePath, m);
        }

        public override void BeforeSend()
        {
            base.BeforeSend();
            var path = new DirectoryInfo(Environment.ParentClient.FileSystem.RealPath).Parent.Parent.FullName;
            path = Path.Combine(path, "exchange");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            ExchageFolder = new DirectoryInfo(path);
            count++;
        }

        public override void OnRecived(RemoteClient from, Client to)
        {
            Environment.ParentClient.Log(LogLevel.Info, "REMOTE: request to download {0}", RelativePath);
            Environment.ParentClient.Log(LogLevel.Info, "sending {0}", RelativePath);
            base.OnRecived(from, to);
 
            var newfilename = Path.Combine(ExchageFolder.FullName, srcFile.Name);
            if (!File.Exists(newfilename))
            {

                if (File.Exists(srcFile.RealPath))
                    File.Copy(srcFile.RealPath, newfilename);
                else
                    from.Send(new SendFileMessage());
            }
            from.Send(new SendFileMessage(srcFile));
        }
    }
    //передача файла
    public class SendFileMessage : FsObjectOperationMessage
    {



        public SendFileMessage() { }
        public SendFileMessage(BaseFile baseFile) : base(baseFile) { }


        public override void OnRecived(RemoteClient from, Client to)
        {
            base.OnRecived(from, to);
            if (srcFile == null) throw new Exception("remote client has not such file");
            Environment.ParentClient.Log(LogLevel.Info, "REMOTE: file recived {0}", RelativePath);
            var tmpfile = Path.Combine(DownloadFileMessage.ExchageFolder.FullName, srcFile.Name);
            srcFile.PushData(tmpfile);

            DownloadFileMessage.count--;
            if (DownloadFileMessage.count == 0)

                if (DownloadFileMessage.WaitHandles.ContainsKey(srcFile.RelativePath))
                {
                    var m = DownloadFileMessage.WaitHandles[srcFile.RelativePath];
                    if (m != null)
                    {

                        m.Set();
                        DownloadFileMessage.WaitHandles.Remove(srcFile.RelativePath);
                    }
                }
        }
    }
}