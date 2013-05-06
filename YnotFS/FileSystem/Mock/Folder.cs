using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YnetFS.FileSystem.Mock
{
    public class MockFolder : IFolder
    {
        public string Name { get; set; }
        [JsonIgnore]
        public string MetaPath { get; private set; }
        [JsonIgnore]
        public string RealPath { get; private set; }

        public string RelativePath { get; private set; }

        [JsonIgnore]
        public IFSObject ParentFolder { get; set; }
  

        public ObservableCollection<IFile> Files { get; set; }
        public ObservableCollection<IFolder> Folders { get; set; }


        public MockFolder(string Name, IFSObject ParentFolder)
        {
            this.ParentFolder = ParentFolder;
            this.Name = Name;
            if (ParentFolder != null)
            {
                RelativePath = Path.Combine(ParentFolder.RelativePath, Name == @"\" ? "" : Name);
                MetaPath = Path.Combine(ParentFolder.MetaPath, Name == @"\" ? "" : Name);
                RealPath = Path.Combine(ParentFolder.RealPath, Name == @"\" ? "" : Name);
            }
            Folders = new ObservableCollection<IFolder>();
            Files = new ObservableCollection<IFile>();

            Folders.CollectionChanged += Folders_CollectionChanged;
            Files.CollectionChanged += Files_CollectionChanged;

            Items = new ObservableCollection<IFSObject>();

        }

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


        public override string ToString()
        {
            return Name;
        }

        [JsonIgnore]
        public ObservableCollection<IFSObject> Items { get; set; }





    }



}
