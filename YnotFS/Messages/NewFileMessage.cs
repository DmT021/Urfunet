

using Newtonsoft.Json;
using YnetFS.FileSystem;
using YnetFS.FileSystem.Mock;
namespace YnetFS.Messages
{
    public class NewFileMessage : Message
    {
        [JsonProperty]
        public FileMetaInfo Meta { get; set; }
        [JsonProperty]
        public string RelativePath { get; set; }

        private FileSystem.IFile srcFile;

        public NewFileMessage(FileSystem.IFile srcFile)
        {
            // TODO: Complete member initialization
            this.srcFile = srcFile;
        }
        public override void BeforeSend()
        {
            Meta = srcFile.meta;
            RelativePath = srcFile.ParentFolder.RelativePath;
        }

        public override void OnRecived(RemoteClient from, Client to)
        {
            //check folder exists

            var fs = Environment.ParentClient.FileSystem as MockFS;
            var fsobj = fs.FindFSObjByRelativePath(RelativePath) as IFolder;

            if (fsobj == null)
                fsobj = Environment.ParentClient.FileSystem.CreateFolder(Environment.ParentClient.FileSystem.RootDir, RelativePath, IFSObjectEvents.remote_create);

            Environment.ParentClient.FileSystem.PushFile(fsobj, Meta, IFSObjectEvents.remote_create);
        }
    }

}