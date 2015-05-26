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

        struct Tick
        {
            public string FullPath { get; set; }
            public string Type { get; set; }
        }

        IObservable<Tick> ToTick(IObservable<FileSystemEventArgs> input, string type)
        {
            return input
                .DistinctUntilChanged()
                .Select(f=>new Tick { 
                        FullPath=f.FullPath,
                        Type=type
                });
        }

        public DirWatcherViewModel() {
            var fs_watcher = new System.IO.FileSystemWatcher("D:\\");
            fs_watcher.EnableRaisingEvents = true;
            fs_watcher.IncludeSubdirectories = true;
            var watcher = new ObservableFileSystemWatcher(fs_watcher);

            var changed = ToTick(watcher.Changed,"U");
            var created = ToTick(watcher.Created,"C");
            var deleted = ToTick(watcher.Deleted,"D");

            var merged = changed.Merge(created.Merge(deleted));

            var merged_with_exceptions = merged.Catch<Tick, Exception>(ex => Observable.Return<Tick>(new Tick
            {
                FullPath = ex.ToString(),
                Type = "[Error]"
            })
            .Merge(merged));

            merged_with_exceptions
            .Select(f =>
                    String.Format("{0}[{1}]: {2}{3}",
                        DateTime.Now.ToString("hh:mm:ss.fff"),
                        f.Type,
                        File.Exists(f.FullPath) ? "[F]" : "",
                        f.FullPath)
            )
            .Scan(new StringBuilder(),(b,f)=>b.Insert(0,String.Format("{0}\n",f)))
            .Select(b=>b.ToString())
            .ToProperty(this,vm=>vm.Log,out _Log)
            ;
        }
    }
}
