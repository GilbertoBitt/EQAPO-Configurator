using System.Windows.Controls;
using EQAPO_Configurator.Models;

namespace EQAPO_Configurator;

public partial class ApplicationChoiceDialog : Wpf.Ui.Controls.FluentWindow
{
    public IReadOnlyList<GameProfile> Profiles { get; }
    public GameProfile? SelectedProfile { get; private set; }

    public ApplicationChoiceDialog(IReadOnlyList<GameProfile> profiles)
    {
        Profiles = profiles;
        DataContext = this;
        InitializeComponent();
    }

    private void OnSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { SelectedItem: GameProfile profile }) return;
        SelectedProfile = profile;
        DialogResult = true;
    }
}
