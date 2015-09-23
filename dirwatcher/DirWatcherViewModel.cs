using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using RxFileSystemWatcher;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;

namespace dirwatcher
{
    public class DirWatcherViewModel : ReactiveObject
    {
        System.IO.FileSystemWatcher filesystem_watcher;

        string _StartPath = @"D:\";
        public string StartPath
        {
            get { return _StartPath; }
            set {
                if (Directory.Exists(value))
                {
                    filesystem_watcher.Path = value;
                }
                this.RaiseAndSetIfChanged(ref _StartPath, value);
                if (!Directory.Exists(value))
                    throw new DirectoryNotFoundException(value);
            }
        }

        string _RegexFilter = "";
        Regex _Filter = null;
        public string RegexFilter
        {
            get { return _RegexFilter; }
            set
            {
                var trimmed = value.Trim();
                _Filter = trimmed.Length != 0 ? new Regex(trimmed, RegexOptions.Compiled) : null;
                _RegexFilter = trimmed;
                this.RaiseAndSetIfChanged(ref _RegexFilter, value);
            }
        }

        public ReactiveCommand<object> Clear { get; private set; }
        public ReactiveCommand<object> Exit { get; private set; }

        ObservableAsPropertyHelper<string> _Log;
        public string Log
        {
            get { return _Log.Value; }
        }

        ObservableAsPropertyHelper<int> _EventCount;
        public int EventCount
        {
            get { return _EventCount.Value; }
        }

        struct Tick
        {
            public string FullPath { get; set; }
            public string Type { get; set; }
            public bool Clear { get; set; }
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
            if (!Directory.Exists(StartPath))
                StartPath="C:\\";

            filesystem_watcher = new System.IO.FileSystemWatcher(StartPath)
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            var watcher = new ObservableFileSystemWatcher(filesystem_watcher);

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

            StringBuilder builder=new StringBuilder();

            //////////////////////////////////////
            Clear = ReactiveCommand.Create();
            Clear.Subscribe(_ => builder.Clear());
            //////////////////////////////////////
            Exit = ReactiveCommand.Create();
            Exit.Subscribe(_ => Application.Current.Shutdown());
            //////////////////////////////////////

            merged_with_exceptions
                .Select(f =>
                        String.Format("{0}[{1}]: {2}{3}",
                            DateTime.Now.ToString("hh:mm:ss.fff"),
                            f.Type,
                            File.Exists(f.FullPath) ? "[F]" : "",
                            f.FullPath)
                )
                .Where(l=> _Filter!=null ? _Filter.IsMatch(l) : true)
                .Scan(builder = new StringBuilder(),(b,f)=>b.Insert(0,String.Format("{0}\n",f)))
                .Select(b=>b.ToString())
                .Merge(Clear.Select(_=>""))
                .ToProperty(this,vm=>vm.Log,out _Log)
            ;

            merged_with_exceptions
                .Merge(Clear.Select(_ => new Tick { Clear = true }))
                .Scan(0, (c, f) => (f.Clear) ? 0 : (c + 1))
                .ToProperty(this, vm => vm.EventCount, out _EventCount)
            ;
        }
    }
}
