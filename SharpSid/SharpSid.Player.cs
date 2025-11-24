using System;
using System.IO;
using System.Linq;
using System.Threading;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;

namespace SharpSid;

public class Player : IDisposable
{
    private const int MULTIPLIER_SHIFT = 4;
    private const int MULTIPLIER_VALUE = 1 << MULTIPLIER_SHIFT;

    private const int FREQUENCY = 44100;
    private const int BYTE_BUFFER_SIZE = 2 * FREQUENCY;
    private const int SHORT_BUFFER_SIZE = BYTE_BUFFER_SIZE / 2;

    private const int PLAY_BUFFER_SIZE = 16384;

    
    private bool _isStereo;

    private volatile Thread _thread;
    private volatile bool _aborting;

    private readonly CircularBufferStream _stream = new(PLAY_BUFFER_SIZE * 4);

    // ReSharper disable once NotAccessedField.Local
    private readonly MiniAudioEngine _audioEngine = new();
    private static readonly AudioFormat Format = AudioFormat.Cd;
    private AudioPlaybackDevice playbackDevice;
    
    private SoundPlayer _soundPlayer;

    private short[] _shortBuffer;
    private byte[] _byteBuffer;

    private readonly bool _aborted = true;

    private readonly Lock _lockObj = new();

    private InternalPlayer _internalPlayer;
    private SidTune _currentTune;



    /// <summary>
    /// Create a new Instance of Player
    /// </summary>
    public Player()
    {
        Init();
    }

    private void Init()
    {
        _shortBuffer = new short[SHORT_BUFFER_SIZE];
        _byteBuffer = new byte[BYTE_BUFFER_SIZE];
        
        _audioEngine.UpdateDevicesInfo();
        var defaultDevice = _audioEngine.PlaybackDevices.FirstOrDefault(x => x.IsDefault);
        playbackDevice = _audioEngine.InitializePlaybackDevice(defaultDevice, Format);

        _soundPlayer = new SoundPlayer(_audioEngine, Format, new RawDataProvider(_stream, SampleFormat.S16, 2, FREQUENCY));
        
        playbackDevice.MasterMixer.AddComponent(_soundPlayer);
        playbackDevice.Start();
    }
    
    private void Filler()
    {
        var playedSize = (int)_internalPlayer.play(_shortBuffer, PLAY_BUFFER_SIZE);

        var pos = playedSize;
        var idx = 2 * playedSize;

        if (_isStereo)
        {
            while (pos > 0)
            {
                int sl  = (short)((short)(_shortBuffer[--pos] << 8 ) | _shortBuffer[--pos]);
                int sr  = (short)((short)(_shortBuffer[--pos] << 8 ) | _shortBuffer[--pos]);
                sl = sl * MULTIPLIER_VALUE >> MULTIPLIER_SHIFT;
                sr = sr * MULTIPLIER_VALUE >> MULTIPLIER_SHIFT;

                _byteBuffer[--idx] = (byte)(sl >> 8);
                _byteBuffer[--idx] = (byte)(sl &  0xff);
                _byteBuffer[--idx] = (byte)(sr >> 8);
                _byteBuffer[--idx] = (byte)(sr &  0xff);
            }
        }
        else
        {
            while (pos > 0)
            {
                int s  = (short)((short)(_shortBuffer[--pos] << 8) | _shortBuffer[--pos]);
                s      = s * MULTIPLIER_VALUE >> MULTIPLIER_SHIFT;
                var sl = (byte)(s >> 8);
                var sr = (byte)(s & 0xFF);

                _byteBuffer[--idx] = sl;
                _byteBuffer[--idx] = sr;
                _byteBuffer[--idx] = sl;
                _byteBuffer[--idx] = sr;
            }
        }

        _stream.Write(_byteBuffer, 0, playedSize * 2);
    }
    
    public bool LoadSidFromFile(string filename)
    {
        Stop();
        try
        {
            using var file = new FileStream(filename, FileMode.Open, FileAccess.Read);
            return LoadSidFromStream(file);
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    public bool LoadSidFromStream(Stream stream)
    {
        Stop();

        if (stream == null)
        {
            return false;
        }

        _currentTune = new SidTune(stream);

        return _currentTune.StatusOk;
    }
    
    public bool LoadSidInfoFromFile(string filename, out SidTuneInfo info)
    {
        info = null;
        
        try
        {
            using var file = new FileStream(filename, FileMode.Open, FileAccess.Read);
            
            var tempTune = new SidTune(file);
            if (!tempTune.StatusOk)
            {
                return false;
            }
            
            info = tempTune.info;
            
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    public bool LoadSidInfoFromStream(Stream ioIn, out SidTuneInfo info)
    {
        info = null;
        
        try
        {
            using (ioIn)
            {
                var tempTune = new SidTune(ioIn);
                if (!tempTune.StatusOk)
                {
                    return false;
                }
                
                info = tempTune.info;
                
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    /// <summary>
    /// returns the current Status of the Player
    /// </summary>
    public State State
    {
        get
        {
            if (_internalPlayer == null)
                return State.Stopped;
            
            switch (_internalPlayer.State)
            {
                case SID2Types.sid2_player_t.sid2_paused:
                    return State.Paused;
                case SID2Types.sid2_player_t.sid2_playing:
                    return State.Playing;
                case SID2Types.sid2_player_t.sid2_stopped:
                default:
                    return State.Stopped;
            }
        }
    }

    public SidTuneInfo TuneInfo => _currentTune != null ? _currentTune.Info : new SidTuneInfo();


    /// <summary>
    ///  Start playing the tune with the default song
    /// </summary>
    public void Start()
    {
        if (State == State.Playing)
        {
            return;
        }
        
        Start(0);
    }



    /// <summary>
    /// Start playing the tune with the selected song
    /// </summary>
    /// <param name="songNumber">song id (1..count), 0 = default song</param>
    public void Start(int songNumber)
    {
        if (Stopping)
        {
            return;
        }
        
        if (_currentTune == null)
        {
            return;
        }

        _internalPlayer = new InternalPlayer();

        var config = _internalPlayer.config();
        config.frequency      = FREQUENCY;
        config.playback       = SID2Types.sid2_playback_t.sid2_mono;
        config.optimisation   = SID2Types.SID2_DEFAULT_OPTIMISATION;
        config.sidModel       = (SID2Types.sid2_model_t)_currentTune.Info.sidModel;
        config.clockDefault   = SID2Types.sid2_clock_t.SID2_CLOCK_CORRECT;
        config.clockSpeed     = SID2Types.sid2_clock_t.SID2_CLOCK_CORRECT;
        config.clockForced    = false;
        config.environment    = SID2Types.sid2_env_t.sid2_envR;
        config.forceDualSids  = false;
        config.volume         = 255;
        config.sampleFormat   = SID2Types.sid2_sample_t.SID2_LITTLE_SIGNED;
        config.sidDefault     = SID2Types.sid2_model_t.SID2_MODEL_CORRECT;
        config.sidSamples     = true;
        config.precision      = SID2Types.SID2_DEFAULT_PRECISION;
        _internalPlayer.config(config);

        _currentTune.selectSong(songNumber);
        _internalPlayer.load(_currentTune);

        _isStereo = _currentTune.isStereo;

        _internalPlayer.start();

        _thread = new Thread(ThreadProc);
        _thread.Start();
    }



    private void ThreadProc()
    {
        var started = false;
        
        while (!_aborting)
        {
            Thread.Sleep(20);

            if (_stream.GetBufferedByteCount() < PLAY_BUFFER_SIZE)
            {
                Filler();
            }

            if (!started && _stream.GetBufferedByteCount() >= 1_024)
            {
                _soundPlayer.Play();
                started = true;
            }
        }

        _thread = null;
    }



    /// <summary>
    /// stop playing the current tune
    /// </summary>
    public void Stop()
    {
        if (_currentTune == null)
        {
            return;
        }

        if (Stopping)
        {
            return;
        }

        lock (_lockObj)
        {
            _aborting = true;

            // if ( _WavePlayer != null )
            // {
            //     _WavePlayer.Stop();
            //     _WavePlayer.Dispose();
            //     _WavePlayer = null;
            // }

            while (_thread != null)
            {
                Thread.Sleep( 10 );
            }

            _internalPlayer?.stop();

            _aborting = false;
        }
    }
    
    /// <summary>
    /// pause playing
    /// </summary>
    public void Pause()
    {
        if (Stopping)
        {
            return;
        }

        if (_internalPlayer != null &&  _internalPlayer.State == SID2Types.sid2_player_t.sid2_playing)
        {
            _soundPlayer.Pause();
            _internalPlayer.pause();
            while (_internalPlayer.inPlay)
            {
                Thread.Sleep(1);
            }
        }
    }

    /// <summary>
    /// resume playing
    /// </summary>
    public void Resume()
    {
        if (Stopping)
        {
            return;
        }

        _soundPlayer.Play();
        _internalPlayer.resume();
    }



    /// <summary>
    /// is Player currently stopping?
    /// </summary>
    public bool Stopping => _aborting &&  !_aborted;


    public void Dispose()
    {
        Stop();

        _internalPlayer = null;
    }
    
    private static byte[] StringToByteArrayFastest(string hex)
    {
        if (hex.Length % 2 == 1)
            throw new Exception( "The binary key cannot have an odd number of digits" );

        var arr = new byte[hex.Length >> 1];

        for (var i = 0; i < hex.Length >> 1; ++i)
        {
            arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + GetHexVal(hex[(i << 1)+1]));
        }

        return arr;
    }



    private static int GetHexVal(char hex)
    {
        var val = (int)hex;
        //For uppercase A-F letters:
        //return val - (val < 58 ? 48 : 55);
        //For lowercase a-f letters:
        //return val - (val < 58 ? 48 : 87);
        //Or the two combined, but a bit slower:
        return val - (val < 58 ? 48 : val < 97 ? 55 : 87);
    }



    /// <summary>
    /// Inject regular program and set start address
    /// </summary>
    /// <param name="hexData"></param>
    /// <param name="dataStartAddress"></param>
    /// <param name="initialAddress"></param>
    /// <returns></returns>
    public bool PlayFromBinary(string hexData, int dataStartAddress, int initialAddress)
    {
        Stop();

        var byteData = StringToByteArrayFastest(hexData);

        _currentTune = new SidTune
        {
            info =
            {
                loadAddr = dataStartAddress,
                initAddr = initialAddress,
                playAddr = initialAddress,
                c64dataLen = byteData.Length,
                compatibility = SidTune.SIDTUNE_COMPATIBILITY_R64
            }
        };

        _currentTune.InjectProgramInMemory(byteData, dataStartAddress);
        _currentTune.status = true;

        _internalPlayer = new InternalPlayer();

        var config = _internalPlayer.config();
        config.frequency = FREQUENCY;
        config.playback = SID2Types.sid2_playback_t.sid2_mono;
        config.optimisation = SID2Types.SID2_DEFAULT_OPTIMISATION;
        config.sidModel = (SID2Types.sid2_model_t)_currentTune.Info.sidModel;
        config.clockDefault = SID2Types.sid2_clock_t.SID2_CLOCK_CORRECT;
        config.clockSpeed = SID2Types.sid2_clock_t.SID2_CLOCK_CORRECT;
        config.clockForced = false;
        config.environment = SID2Types.sid2_env_t.sid2_envR;
        config.forceDualSids = false;
        config.volume = 255;
        config.sampleFormat = SID2Types.sid2_sample_t.SID2_LITTLE_SIGNED;
        config.sidDefault = SID2Types.sid2_model_t.SID2_MODEL_CORRECT;
        config.sidSamples = true;
        config.precision = SID2Types.SID2_DEFAULT_PRECISION;
        config.environment = SID2Types.sid2_env_t.sid2_envR;

        _internalPlayer.load(_currentTune);
        _internalPlayer.config(config);

        // inject code
        for (var i = 0; i < byteData.Length; ++i)
        {
            _internalPlayer.mem_writeMemByte(dataStartAddress + i, byteData[i]);
        }

        _internalPlayer.SetCPUPos(initialAddress);
        _isStereo = _currentTune.isStereo;

        _internalPlayer.start();

        _thread = new Thread(ThreadProc);
        _thread.Start();

        return true;
    }
    
    public void SetVolume(int volume)
    {
        volume = volume switch
        {
            < 0 => 0,
            > 100 => 100,
            _ => volume
        };

        if (_soundPlayer != null)
        {
            _soundPlayer.Volume = volume * 0.01f;
        }
    }
}