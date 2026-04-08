using System;

namespace TheCollector.Windows;

public class ConfigWindow : IDisposable
{
    private readonly MainWindow _mainWindow;

    public ConfigWindow(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void Toggle() => _mainWindow.Toggle();

    public void Dispose() { }
}
