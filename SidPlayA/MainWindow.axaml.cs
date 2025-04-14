using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SharpSid;

namespace SidPlayA;

public partial class MainWindow : Window
{
    private readonly Player _sidPlayer = new();
    
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void OnPlayClicked(object? sender, RoutedEventArgs e)
    {
        if (_sidPlayer.TuneInfo.songs == 0)
        {
            _sidPlayer.PlayFromBinary(Songs.SONG_1, 2061, 2061);
        }

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
        if (_sidPlayer.TuneInfo.currentSong > 1)
        {
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
    }

    private void OnNextClicked(object? sender, RoutedEventArgs e)
    {
        if (_sidPlayer.TuneInfo.currentSong < _sidPlayer.TuneInfo.songs)
        {
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
    }

    private void RangeBase_OnValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        _sidPlayer.SetVolume((int)e.NewValue);
    }
}