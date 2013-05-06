using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YnetFS.FileSystem.Mock
{
    public class MockFile : IFile
    {
        [JsonIgnore]
        public string MetaPath { get; private set; }
        [JsonIgnore]
        public string RealPath { get; private set; }

        public string RelativePath { get; private set; }

        public FileMetaInfo meta { get; set; }
        public string Name { get; set; }



        public IFolder ParentFolder { get; private set; }

        public MockFile(string name, IFolder Parentdir)
        {
            ParentFolder = Parentdir as MockFolder;
            Name = new FileInfo(name).Name;

            if (Parentdir != null)
            {
                RelativePath = Path.Combine(Parentdir.RelativePath, name);
                MetaPath = Path.Combine(Parentdir.MetaPath, name);
                RealPath = Path.Combine(Parentdir.RealPath, Name);
            }
            //if metafile exists - read and return
            //else (if file exists) create metafile ,save metafile

            if (File.Exists(MetaPath))
                meta = FileMetaInfo.ReadFrom(this);
            else if(File.Exists(RealPath))
            {
                meta = FileMetaInfo.CreateFromFile(this);
                meta.Save();
            }

        }
        public override string ToString()
        {
            return Name;
        }

        internal void SetMetaFile(FileMetaInfo MetaFile)
        {
            meta = MetaFile;
            meta.ParenFile = this;
        }

    }
}
