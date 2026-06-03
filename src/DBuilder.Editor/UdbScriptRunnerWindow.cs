// ABOUTME: Avalonia shell for the UDBScript runner progress, status, and log surface.
// ABOUTME: Applies UDBScript runner model state while execution wiring remains owned by callers.

using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using DBuilder.IO;

namespace DBuilder.Editor;

public sealed class UdbScriptRunnerWindow : Window
{
    private readonly ProgressBar _progress = new();
    private readonly TextBlock _status = new();
    private readonly Button _action = new();
    private readonly TextBox _log = new();
    private readonly Stopwatch _stopwatch = new();
    private readonly DispatcherTimer _timer = new();
    private double _runningSeconds;
    private bool _autoClose;
    private bool _running;

    public event Action? CancelRequested;
    public event Action? CloseRequested;
    public event Action? PauseRequested;
    public event Action? ResumeRequested;

    public bool IsRunnerRunning => _running;
    public bool IsProgressMarquee => _progress.IsIndeterminate;
    public double ProgressValue => _progress.Value;
    public bool AutoClose => _autoClose;
    public bool IsRuntimeTimerEnabled => _timer.IsEnabled;
    public TimeSpan ElapsedRuntime => _stopwatch.Elapsed;

    public UdbScriptRunnerWindow()
    {
        UdbScriptRunnerFormMetadata metadata = UdbScriptRunnerModel.FormMetadata();

        Title = metadata.InitialTitle;
        Width = metadata.MinimumWidth;
        Height = metadata.MinimumHeight;
        MinWidth = metadata.MinimumWidth;
        MinHeight = metadata.MinimumHeight;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _progress.Minimum = 0;
        _progress.Maximum = 100;
        _progress.Value = 0;
        _progress.Height = 23;

        _action.MinWidth = 75;
        _action.Click += (_, _) => ApplyActionButton();

        _log.IsReadOnly = true;
        _log.AcceptsReturn = true;
        _log.TextWrapping = Avalonia.Media.TextWrapping.NoWrap;

        _timer.Interval = TimeSpan.FromMilliseconds(UdbScriptRunnerModel.RunnerTimerIntervalMilliseconds);
        _timer.Tick += (_, _) => ApplyTimerTick(_stopwatch.Elapsed);

        var top = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            Margin = new Avalonia.Thickness(12, 9, 12, 6),
        };
        Grid.SetColumnSpan(_status, 2);
        Grid.SetRow(_progress, 1);
        Grid.SetRow(_action, 1);
        Grid.SetColumn(_action, 1);
        _action.Margin = new Avalonia.Thickness(6, 0, 0, 0);
        top.Children.Add(_status);
        top.Children.Add(_progress);
        top.Children.Add(_action);

        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(_log);
        Content = root;

        ApplyState(UdbScriptRunnerModel.InitialUiState());
    }

    public void Start()
    {
        UdbScriptRunnerStartPlan plan = UdbScriptRunnerModel.StartPlan();
        _runningSeconds = 0;
        _running = false;
        _progress.Value = plan.ResetProgressValue ? 0 : _progress.Value;
        if (plan.ClearLog)
            _log.Clear();

        ApplyState(plan.InitialState);
        if (plan.StartStopwatch)
        {
            _stopwatch.Reset();
            _stopwatch.Start();
        }
        if (plan.StartTimer)
            _timer.Start();
    }

    public void MarkRunning()
    {
        _running = true;
    }

    public object? InvokePaused(Delegate method)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return InvokePausedOnUiThread(method);

        return Dispatcher.UIThread
            .InvokeAsync(() => InvokePausedOnUiThread(method))
            .GetAwaiter()
            .GetResult();
    }

    public void RunAction(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread
            .InvokeAsync(action)
            .GetAwaiter()
            .GetResult();
    }

    public void Finish(TimeSpan runtime, bool autoClose)
    {
        _running = false;
        _stopwatch.Stop();
        ApplyState(UdbScriptRunnerModel.FinishedUiState(runtime, autoClose));
        _progress.Value = 0;
        if (autoClose)
            Close();
    }

    public void ApplyProgress(int value)
    {
        UdbScriptProgressUpdatePlan plan = UdbScriptRunnerModel.ProgressUpdatePlan(
            (int)_progress.Value,
            value,
            (int)_progress.Minimum,
            (int)_progress.Maximum,
            styleIsContinuous: !_progress.IsIndeterminate);

        _progress.IsIndeterminate = !plan.SetContinuousProgressStyle && _progress.IsIndeterminate;
        foreach (int valueWrite in plan.ValueWrites)
            _progress.Value = valueWrite;

        ApplyState(UdbScriptRunnerModel.ProgressReportedUiState(CurrentState()));
    }

    public void ApplyStatus(string status)
    {
        ApplyState(UdbScriptRunnerModel.StatusReportedUiState(CurrentState(), status));
    }

    public void ApplyLog(string text)
    {
        _log.Text = UdbScriptRunnerModel.AppendLog(_log.Text ?? "", text);
        ApplyState(UdbScriptRunnerModel.LogReportedUiState(CurrentState()));
    }

    public void ApplyTimerTick(TimeSpan elapsed)
    {
        UdbScriptRunnerTimerTickPlan plan = UdbScriptRunnerModel.TimerTickPlan(elapsed, _runningSeconds, Opacity);
        if (plan.MakeVisible)
            Opacity = 1.0;
        if (plan.UpdateRunningSeconds)
        {
            _runningSeconds = plan.RunningSeconds;
            Title = plan.Title;
        }
    }

    public void ApplyState(UdbScriptRunnerUiState state)
    {
        Title = state.Title;
        _status.Text = state.StatusText;
        _action.Content = state.ActionButtonText;
        _action.IsEnabled = state.ActionButtonEnabled;
        _progress.IsIndeterminate = state.ProgressIsMarquee;
        _autoClose = state.AutoClose;
        Opacity = state.Opacity;
    }

    private void ApplyActionButton()
    {
        UdbScriptRunnerActionButtonPlan plan = UdbScriptRunnerModel.ActionButtonPlan(_running);
        if (plan.DisableActionButton)
            _action.IsEnabled = false;
        if (plan.CancelToken)
            CancelRequested?.Invoke();
        if (plan.MakeInvisible)
            Opacity = 0.0;
        if (plan.CloseWindow)
        {
            CloseRequested?.Invoke();
            Close();
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        UdbScriptRunnerLifecycleEventPlan plan = UdbScriptRunnerModel.LifecycleEventPlan();
        if (plan.MakeInvisibleOnLoad)
            Opacity = 0.0;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        UdbScriptRunnerLifecycleEventPlan plan = UdbScriptRunnerModel.LifecycleEventPlan();
        if (plan.StopTimerOnClosed)
            _timer.Stop();
    }

    private UdbScriptRunnerUiState CurrentState()
        => new(
            Title?.ToString() ?? "",
            _status.Text ?? "",
            _action.Content?.ToString() ?? "",
            _action.IsEnabled,
            _progress.IsIndeterminate,
            Opacity,
            _autoClose);

    private object? InvokePausedOnUiThread(Delegate method)
    {
        PauseRequested?.Invoke();
        try
        {
            return method.DynamicInvoke();
        }
        finally
        {
            ResumeRequested?.Invoke();
        }
    }
}
