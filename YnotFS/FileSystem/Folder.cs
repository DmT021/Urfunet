using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YnetFS.FileSystem
{
    public  class BaseFolder : IFSObject
    {
        [JsonIgnore]
        public BaseFileSystem FS;

        public string Name { get; set; }
        [JsonIgnore]
        public string MetaPath { get; set; }
        [JsonIgnore]
        public string RealPath { get; set; }

        public string RelativePath { get; set; }


        public BaseFolder(BaseFileSystem fs)
        {
            this.FS = fs;
            Name = "\\";
            init();

            RelativePath = Path.Combine(fs.RelativePath, "");
            MetaPath = Path.Combine(fs.MetaPath, "");
            RealPath = Path.Combine(fs.RealPath, "");
        }

        public BaseFolder(BaseFolder ParentFolder, string Name)
        {
            this.ParentFolder = ParentFolder;
            this.FS = ParentFolder.FS;
            this.RelativePath = RelativePath;
            this.Name = Name;

            RelativePath = Path.Combine(ParentFolder.RelativePath, Name);
            MetaPath = Path.Combine(ParentFolder.MetaPath, Name);
            RealPath = Path.Combine(ParentFolder.RealPath, Name);

            init();
        }

        public BaseFolder()
        {
            init();
        }

        private void init()
        {
            Folders = new ObservableCollection<BaseFolder>();
            Files = new ObservableCollection<BaseFile>();
            Items = new ObservableCollection<IFSObject>();

            Folders.CollectionChanged += Folders_CollectionChanged;
            Files.CollectionChanged += Files_CollectionChanged;
        }

        [JsonIgnore]
        public IFSObject ParentFolder { get; private set; }

        [JsonProperty(PropertyName = "Files", ItemTypeNameHandling = TypeNameHandling.Auto)]
        public ObservableCollection<BaseFile> Files { get; private set; }

        [JsonProperty(PropertyName = "Folders", ItemTypeNameHandling = TypeNameHandling.Auto)]
        public ObservableCollection<BaseFolder> Folders { get; private set; }

        public override string ToString()
        {
            return Name;
        }

        internal void Delete(FSObjectEvents eventtype)
        {
            foreach (var f in Files)
                FS.Delete(f, eventtype);
            foreach (var it in Folders)
                FS.Delete(it,eventtype);


            if (Directory.Exists(RealPath))
                Directory.Delete(RealPath,true);
            if (Directory.Exists(MetaPath))
                Directory.Delete(MetaPath, true);
        }

        internal BaseFolder CreateFolder(string Name)
        {
            var realdir = Path.Combine(RealPath, Name);
            var metadir = Path.Combine(MetaPath, Name);

            Directory.CreateDirectory(realdir);
            Directory.CreateDirectory(metadir);

            return new BaseFolder(this, Name);
        }

        internal BaseFile CreateFile(string PathToExistingFile)
        {
            if (!File.Exists(PathToExistingFile)) throw new Exception("File not found");
            
            var newfile = new FileInfo(PathToExistingFile);
            var newFileRealPath = Path.Combine(RealPath, newfile.Name);
            if (File.Exists(newFileRealPath)) throw new Exception("File already exists");

            File.Copy(PathToExistingFile, newFileRealPath);
            return new BaseFile(this, newfile.Name);
        }

        #region Items
        [JsonIgnore]
        public ObservableCollection<IFSObject> Items { get; set; }

        void Files_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {

                if (e.NewItems != null)
                    foreach (var i in e.NewItems)
                        Items.Add(i as IFSObject);

                if (e.OldItems != null)
                    foreach (var it in e.OldItems)
                        if (!Files.Contains(it as IFSObject))
                            Items.Remove(it as IFSObject); 
        }

        void Folders_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
                if (e.NewItems != null)
                    foreach (var i in e.NewItems)
                        Items.Add(i as IFSObject);
                if (e.OldItems != null)
                    foreach (var it in e.OldItems)
                        if (!Folders.Contains(it as IFSObject))
                            Items.Remove(it as IFSObject);

        }
        #endregion

    }


}
