using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SharpSid;

namespace SidPlayA;

public partial class MainWindow : Window
{
    private readonly Player _player = new();
    
    public MainWindow()
    {
        InitializeComponent();
        
        //const string fileName = "/Users/christian/Music/C64music/MUSICIANS/H/Hubbard_Rob/Lightforce.sid";
        const string fileName = "/Users/christian/Music/C64music/MUSICIANS/G/Galway_Martin/Rambo_First_Blood_Part_II.sid";
        //const string fileName = "/Users/christian/Music/C64music/MUSICIANS/L/Lieblich_Russell/Ghostbusters.sid";

        _player.LoadSIDFromFile(fileName);
    }
    
    private void OnPlayClicked(object? sender, RoutedEventArgs e)
    {
        if (_player.State != State.PLAYING )
        {
            _player.Start();
        }
    }
    
    private void OnPauseClicked(object? sender, RoutedEventArgs e)
    {
        if (_player.State != State.PAUSED)
        {
            _player.Pause();
        }
    }
    
    private void OnResumeClicked(object? sender, RoutedEventArgs e)
    {
        if (_player.State == State.PAUSED)
        {
            _player.Resume();
        }
    }

    private void OnStopClicked(object? sender, RoutedEventArgs e)
    {
        _player.Stop();
    }
    
    private void OnPreviousClicked(object? sender, RoutedEventArgs e)
    {
        if (_player.TuneInfo.currentSong > 1)
        {
            switch(_player.State)
            {
                case State.PLAYING:
                case State.PAUSED:
                    _player.Stop();
                    break;
            }

//            sp_songInfo.DataContext = null;

            _player.Start( _player.TuneInfo.currentSong - 1 );

//            sp_songInfo.DataContext = _player.TuneInfo;

        }
    }

    private void OnNextClicked(object? sender, RoutedEventArgs e)
    {
        if (_player.TuneInfo.currentSong < _player.TuneInfo.songs)
        {
            switch ( _player.State )
            {
                case State.PLAYING:
                case State.PAUSED:
                    _player.Stop();
                    break;
            }

//            sp_songInfo.DataContext = null;

            _player.Start( _player.TuneInfo.currentSong + 1 );

//            sp_songInfo.DataContext = _player.TuneInfo;
        }
    }
}