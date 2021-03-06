﻿using System;
using System.Linq;
using System.Text;
using System.Reactive.Linq;
using RxFileSystemWatcher;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using ReactiveUI;

namespace dirwatcher
{
    public class DirWatcherViewModel : ReactiveObject
    {
        readonly FileSystemWatcher filesystem_watcher;

        string _StartPath = @"D:\";
        public string StartPath
        {
            get { return _StartPath; }
            set
            {
                if (Directory.Exists(value))
                {
                    if (filesystem_watcher != null)
                        filesystem_watcher.Path = value;

                    this.RaiseAndSetIfChanged(ref _StartPath, value);
                }
                else
                {
                    throw new DirectoryNotFoundException(String.Format("Directory not found: {0}", value));
                }
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

        public ReactiveCommand<System.Reactive.Unit, object> Clear { get; private set; }
        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> Exit { get; private set; }

        readonly ObservableAsPropertyHelper<string> _Log;
        public string Log
        {
            get { return _Log.Value; }
        }

        readonly ObservableAsPropertyHelper<int> _EventCount;
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
                .Select(f => new Tick
                {
                    FullPath = f.FullPath,
                    Type = type
                });
        }

        public DirWatcherViewModel()
        {
            if (!Directory.Exists(StartPath))
                StartPath = AppDomain.CurrentDomain.BaseDirectory;

            filesystem_watcher = new System.IO.FileSystemWatcher(StartPath)
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            var watcher = new ObservableFileSystemWatcher(filesystem_watcher);

            var changed = ToTick(watcher.Changed, "U");
            var created = ToTick(watcher.Created, "C");
            var deleted = ToTick(watcher.Deleted, "D");

            //////////////////////////////////////
            StringBuilder builder = new StringBuilder();
            Clear = ReactiveCommand.Create(() => (object)builder.Clear());
            //////////////////////////////////////
            Exit = ReactiveCommand.Create(()=> { Application.Current.Shutdown(); });
            //////////////////////////////////////

            var merged = changed.Merge(
                            created.Merge(deleted
                                    .Merge(Clear.Select(_ => new Tick { Clear = true })))
            );

            var merged_with_exceptions = merged.Catch<Tick, Exception>(ex => Observable.Return<Tick>(new Tick
            {
                FullPath = ex.ToString(),
                Type = "[Error]"
            })
            .Merge(merged));


            var filtered_ticks = merged_with_exceptions
                .Select(f => new
                {
                    Log = f.Clear ?
                        ""
                        :
                        String.Format("{0}[{1}]: {2}{3}",
                        DateTime.Now.ToString("hh:mm:ss.fff"),
                        f.Type,
                        File.Exists(f.FullPath) ? "[F]" : "",
                        f.FullPath),
                    Clear = f.Clear
                })
                .Where(l => (_Filter == null) || (_Filter.IsMatch(l.Log) || l.Clear))
            ;

            filtered_ticks
                .Scan(builder = new StringBuilder(), (b, f) => b.Insert(0, String.Format("{0}\n", f.Log)))
                .Select(b => b.ToString())
                .Merge(Clear.Select(_ => ""))
                .ToProperty(this, vm => vm.Log, out _Log)
            ;


            filtered_ticks
                .Scan(0, (c, f) => (f.Clear) ? 0 : (c + 1))
                .ToProperty(this, vm => vm.EventCount, out _EventCount)
            ;

            _Log.ThrownExceptions.Subscribe(_ => { });
            _EventCount.ThrownExceptions.Subscribe(_ => { });
        }
    }
}
