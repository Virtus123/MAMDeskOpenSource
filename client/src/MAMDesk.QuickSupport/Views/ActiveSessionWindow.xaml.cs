using System.Windows;

namespace MAMDesk.QuickSupport.Views;

public partial class ActiveSessionWindow : Window
{
    public event Action? EndSessionRequested;

    public ActiveSessionWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PositionTopRight();
    }

    public void SetOperatorName(string name)
    {
        OperatorNameText.Text = string.IsNullOrWhiteSpace(name)
            ? "Um operador está controlando este computador."
            : $"{name} está acessando este computador.";
    }

    private void PositionTopRight()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 16;
        Top = work.Top + 16;
    }

    private void EndSessionBtn_Click(object sender, RoutedEventArgs e)
    {
        EndSessionRequested?.Invoke();
    }
}
