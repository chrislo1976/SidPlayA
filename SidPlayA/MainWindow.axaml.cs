using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SharpSid;

namespace SidPlayA;

public partial class MainWindow : Window
{
    private readonly Player _sidPlayer = new();
    
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private async void OnLoadClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open sid file",
            AllowMultiple = false
        });

        if (files.Count >= 1)
        {
            _sidPlayer.LoadSidFromFile(files[0].Path.AbsolutePath);
        }
    }

    
    private void OnPlayClicked(object? sender, RoutedEventArgs e)
    {
        if (_sidPlayer.TuneInfo.songs == 0)
            return;

        if (_sidPlayer.State == State.Playing)
            return;
        
        _sidPlayer.Start();
    }
    
    private void OnPauseClicked(object? sender, RoutedEventArgs e)
    {
        if (_sidPlayer.State == State.Paused)
            return;
        
        _sidPlayer.Pause();
    }
    
    private void OnResumeClicked(object? sender, RoutedEventArgs e)
    {
        if (_sidPlayer.State != State.Paused)
            return;
        
        _sidPlayer.Resume();
    }

    private void OnStopClicked(object? sender, RoutedEventArgs e)
    {
        if (_sidPlayer.State == State.Stopped)
            return;
            
        _sidPlayer.Stop();
    }
    
    private void OnPreviousClicked(object? sender, RoutedEventArgs e)
    {
        if (_sidPlayer.TuneInfo.currentSong <= 1)
            return;
        
        switch(_sidPlayer.State)
        {
            case State.Playing:
            case State.Paused:
                _sidPlayer.Stop();
                break;
        }

//            sp_songInfo.DataContext = null;
        _sidPlayer.Start( _sidPlayer.TuneInfo.currentSong - 1 );
//            sp_songInfo.DataContext = _player.TuneInfo;
    }

    private void OnNextClicked(object? sender, RoutedEventArgs e)
    {
        if (_sidPlayer.TuneInfo.currentSong >= _sidPlayer.TuneInfo.songs)
            return;
        
        switch ( _sidPlayer.State )
        {
            case State.Playing:
            case State.Paused:
                _sidPlayer.Stop();
                break;
        }

//            sp_songInfo.DataContext = null;
        _sidPlayer.Start( _sidPlayer.TuneInfo.currentSong + 1 );
//            sp_songInfo.DataContext = _player.TuneInfo;
    }

    private void RangeBase_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        _sidPlayer.SetVolume((int)e.NewValue);
    }
}