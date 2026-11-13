using System.Windows;

namespace KIGest.App.Views;

public partial class InputDialog : Window
{
    public string Valeur { get; private set; } = string.Empty;

    public InputDialog(string question, string valeurInitiale = "")
    {
        InitializeComponent();
        TxtQuestion.Text = question;
        TxtValeur.Text = valeurInitiale;
        Loaded += (_, _) => { TxtValeur.Focus(); TxtValeur.SelectAll(); };
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Valeur = TxtValeur.Text;
        DialogResult = true;
    }

    private void BtnAnnuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
