using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Modinstaller2.Models;
using Modinstaller2.Services;
using Modinstaller2.Util;
using ReactiveUI;

namespace Modinstaller2.ViewModels
{
    public class ModListViewModel : ViewModelBase
    {
        private Settings Settings { get; }

        private IServiceProvider _sp;

        private bool _pbVisible;

        [UsedImplicitly]
        public bool ProgressBarVisible
        {
            get => _pbVisible;

            private set => this.RaiseAndSetIfChanged(ref _pbVisible, value);
        }

        private bool _pbIndeterminate;

        public bool ProgressBarIndeterminate
        {
            get => _pbIndeterminate;

            private set => this.RaiseAndSetIfChanged(ref _pbIndeterminate, value);
        }

        private double _pbProgress;

        [UsedImplicitly]
        public double Progress
        {
            get => _pbProgress;

            private set => this.RaiseAndSetIfChanged(ref _pbProgress, value);
        }

        private SortableObservableCollection<ModItem> Items { get; }

        private IEnumerable<ModItem> _selectedItems;

        private IEnumerable<ModItem> SelectedItems
        {
            get => _selectedItems;
            set
            {
                _selectedItems = value;
                this.RaisePropertyChanged(nameof(FilteredItems));
            }
        }

        private string? _search;

        private IEnumerable<ModItem> FilteredItems =>
            string.IsNullOrEmpty(_search)
                ? SelectedItems
                : SelectedItems.Where(x => x.Name.Contains(_search, StringComparison.OrdinalIgnoreCase));

        public void FilterItems(string search)
        {
            _search = search;

            this.RaisePropertyChanged(nameof(FilteredItems));
        }

        [UsedImplicitly]
        public ReactiveCommand<ModItem, Task> OnInstall { get; }

        private static (int priority, string name) ModToOrderedTuple(ModItem m) =>
        (
            m.State is InstalledState { Updated : false } ? -1 : 1,
            m.Name
        );

        public void SelectAll() => SelectedItems = Items;

        public void SelectInstalled() => SelectedItems = Items.Where(x => x.Installed);

        public void OpenModsDirectory()
        {
            Process.Start
            (
                new ProcessStartInfo(Path.Combine(Settings.ManagedFolder, "Mods"))
                {
                    UseShellExecute = true
                }
            );
        }

        public void SelectUnupdated() => SelectedItems = Items.Where(x => x.State is InstalledState { Updated: false });

        public void SelectEnabled() => SelectedItems = Items.Where(x => x.State is InstalledState { Enabled: true });

        public ModListViewModel(IServiceProvider sp)
        {
            _sp = sp;

            var db = sp.GetRequiredService<ModDatabase>();

            Settings = sp.GetRequiredService<Settings>();

            Items = new SortableObservableCollection<ModItem>(db.Items.OrderBy(ModToOrderedTuple));

            SelectedItems = _selectedItems = Items;

            OnInstall = ReactiveCommand.Create<ModItem, Task>(OnInstallAsync);
        }

        [UsedImplicitly]
        public async Task OnInstallAsync(ModItem item)
        {
            await item.OnInstall
            (
                _sp,
                val => ProgressBarVisible = val,
                progress =>
                {
                    ProgressBarIndeterminate = progress < 0;

                    if (progress >= 0)
                        Progress = progress;
                }
            );

            static int Comparer(ModItem x, ModItem y) => ModToOrderedTuple(x).CompareTo(ModToOrderedTuple(y));

            Items.SortBy(Comparer);
        }
    }
}