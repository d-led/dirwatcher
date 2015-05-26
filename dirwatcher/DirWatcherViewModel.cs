using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using RxFileSystemWatcher;
using System.IO;

namespace dirwatcher
{
    public class DirWatcherViewModel : ReactiveObject
    {
        ObservableAsPropertyHelper<string> _Log;
        public string Log
        {
            get { return _Log.Value; }
        }

        public DirWatcherViewModel() {
            var fs_watcher = new System.IO.FileSystemWatcher("D:\\");
            fs_watcher.EnableRaisingEvents = true;
            fs_watcher.IncludeSubdirectories = true;
            var watcher = new ObservableFileSystemWatcher(fs_watcher);
            
            watcher
                .Changed
                .DistinctUntilChanged()
                .Select(f =>
                    String.Format("{0}[U]: {1}{2}", DateTime.Now.ToShortTimeString(), File.Exists(f.FullPath) ? "[F]" : "", f.FullPath)
                )
                .Scan(new StringBuilder(),(b,f)=>b.AppendLine(f))
                .Select(b=>b.ToString())
                .ToProperty(this,vm=>vm.Log,out _Log)
                ;
        }
    }
}
