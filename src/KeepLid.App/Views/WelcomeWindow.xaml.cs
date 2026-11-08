using System.Globalization;
using System.Windows;
using KeepLid.Core.Resources;

namespace KeepLid.App.Views;

/// <summary>First-launch explanation in 3 steps.</summary>
public partial class WelcomeWindow : Window
{
    private static readonly (string Title, string Body)[] Steps =
    [
        (Strings.Welcome_Step1_Title, Strings.Welcome_Step1_Body),
        (Strings.Welcome_Step2_Title, Strings.Welcome_Step2_Body),
        (Strings.Welcome_Step3_Title, Strings.Welcome_Step3_Body),
    ];

    private readonly App _app;
    private int _step;

    internal WelcomeWindow(App app)
    {
        _app = app;
        InitializeComponent();
        ShowStep();
        Closed += (_, _) => _app.MarkFirstRunCompleted();
    }

    private void ShowStep()
    {
        StepCounter.Text = string.Format(CultureInfo.CurrentCulture, Strings.Welcome_StepOf, _step + 1, Steps.Length);
        StepTitle.Text = Steps[_step].Title;
        StepBody.Text = Steps[_step].Body;
        BackButton.Visibility = _step == 0 ? Visibility.Hidden : Visibility.Visible;
        NextButton.Content = _step == Steps.Length - 1 ? Strings.Welcome_Finish : Strings.Welcome_Next;
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_step > 0) _step--;
        ShowStep();
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (_step == Steps.Length - 1)
        {
            Close();
            return;
        }
        _step++;
        ShowStep();
    }
}
