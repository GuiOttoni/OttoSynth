using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using OttoSynth.UI.ViewModels;

namespace OttoSynth.UI.Views;

public partial class PresetBrowserPanel : UserControl
{
    public PresetBrowserPanel()
    {
        InitializeComponent();
        Loaded += (_, _) => BuildCategoryStrip();
    }

    private void BuildCategoryStrip()
    {
        if (DataContext is not PresetBrowserViewModel vm) return;

        CategoryStrip.Children.Clear();
        foreach (var cat in vm.Categories)
        {
            var btn = new ToggleButton
            {
                Content   = cat,
                IsChecked = cat == vm.SelectedCategory,
                Style     = (Style)Resources["CategoryButtonStyle"]
            };
            btn.Checked += (_, _) =>
            {
                vm.SelectedCategory = cat;
                // uncheck siblings
                foreach (ToggleButton b in CategoryStrip.Children)
                    if (b != btn) b.IsChecked = false;
            };
            CategoryStrip.Children.Add(btn);
        }
    }

    // Double-click list item to load preset immediately
    private void PresetList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PresetBrowserViewModel vm)
        {
            ICommand cmd = vm.LoadSelectedCommand;
            if (cmd.CanExecute(null)) cmd.Execute(null);
        }
    }

    // Toggle favorite without changing list selection
    private void Star_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is PresetEntryViewModel entry)
        {
            entry.IsFavorite = !entry.IsFavorite;
            e.Handled = true;
        }
    }
}
