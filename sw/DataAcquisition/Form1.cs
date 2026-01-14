using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ACS_Interface_FDev
{
    // High-performance container for raw bytes + arrival time
    public struct RawDataPoint
    {
        public byte ByteValue;
        public long TimestampTicks;
    }

    public partial class Form1 : Form
    {
        private string _selectedPort = "COM11";
        private int _selectedBaud = 2000000;

        private SerialPort _serialPort;
        private ConcurrentQueue<RawDataPoint> _rawQueue = new ConcurrentQueue<RawDataPoint>();
        private ConcurrentQueue<string> _csvRowQueue = new ConcurrentQueue<string>();
        private CancellationTokenSource _cts;

        private Stopwatch _hiResTimer = new Stopwatch();
        private DateTime _startTime;

        private const int PacketSize = 20;
        private Button btnStart;
        private Button btnStop;
        private Label lblStatus;
        private System.Windows.Forms.Timer uiTimer;

        public Form1()
        {
            InitializeComponent();
            LoadConfig();
            SetupSimpleGui();
        }

        private void LoadConfig()
        {
            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.ini");
            if (!File.Exists(iniPath))
                File.WriteAllText(iniPath, "[Settings]\r\nPort=COM3\r\nBaud=115200");

            try
            {
                var lines = File.ReadAllLines(iniPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("Port=")) _selectedPort = line.Split('=')[1].Trim();
                    if (line.StartsWith("Baud=")) _selectedBaud = int.Parse(line.Split('=')[1].Trim());
                }
            }
            catch { /* Use defaults if error */ }
        }

        private void SetupSimpleGui()
        {
            this.Text = "ACS CAN Logger - Extreme Precision";
            this.Width = 400; this.Height = 160;
            btnStart = new Button { Text = "Start", Left = 10, Top = 10, Width = 110 };
            btnStop = new Button { Text = "Stop", Left = 130, Top = 10, Width = 110, Enabled = false };
            lblStatus = new Label { Text = "Status: Idle", Left = 10, Top = 50, Width = 350 };
            this.Controls.AddRange(new Control[] { btnStart, btnStop, lblStatus });

            btnStart.Click += async (s, e) => await StartCapture();
            btnStop.Click += (s, e) => StopCapture();

            uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            uiTimer.Tick += (s, e) => {
                lblStatus.Text = $"Port: {_selectedPort} | Q: {_rawQueue.Count} | Log: {_csvRowQueue.Count}";
            };
        }

        private async Task StartCapture()
        {
            try
            {
                _serialPort = new SerialPort(_selectedPort, _selectedBaud);
                _serialPort.Open();

                _cts = new CancellationTokenSource();
                _startTime = DateTime.Now;
                _hiResTimer.Restart();

                btnStart.Enabled = false; btnStop.Enabled = true; uiTimer.Start();

                _ = Task.Run(() => ReadSerialTask(_cts.Token));
                _ = Task.Run(() => ParseFramesTask(_cts.Token));
                _ = Task.Run(() => SaveToCsvTask(_cts.Token));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ReadSerialTask(CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            while (!token.IsCancellationRequested && _serialPort.IsOpen)
            {
                if (_serialPort.BytesToRead > 0)
                {
                    // Catch the exact timestamp of the incoming burst
                    long captureTime = _hiResTimer.ElapsedTicks;
                    int read = _serialPort.Read(buffer, 0, buffer.Length);

                    for (int i = 0; i < read; i++)
                    {
                        _rawQueue.Enqueue(new RawDataPoint
                        {
                            ByteValue = buffer[i],
                            TimestampTicks = captureTime
                        });
                    }
                }
                else { Thread.Sleep(1); }
            }
        }

        private void ParseFramesTask(CancellationToken token)
        {
            List<RawDataPoint> window = new List<RawDataPoint>();
            while (!token.IsCancellationRequested)
            {
                if (_rawQueue.TryDequeue(out RawDataPoint dp))
                {
                    window.Add(dp);
                    if (window.Count >= PacketSize)
                    {
                        if (window[0].ByteValue == 0xAA && window[1].ByteValue == 0x55)
                        {
                            // Use the timestamp from the FIRST byte of the packet (AA)
                            double seconds = (double)window[0].TimestampTicks / Stopwatch.Frequency;
                            DateTime exactTime = _startTime.AddSeconds(seconds);

                            string timeStr = exactTime.ToString("HH:mm:ss.ffffff");
                            string hex = string.Join(" ", window.Take(PacketSize).Select(x => x.ByteValue.ToString("X2")));

                            _csvRowQueue.Enqueue($"{timeStr},{hex}");
                            window.RemoveRange(0, PacketSize);
                        }
                        else { window.RemoveAt(0); }
                    }
                }
                else { Thread.Sleep(1); }
            }
        }

        private async Task SaveToCsvTask(CancellationToken token)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            while (!token.IsCancellationRequested)
            {
                await Task.Delay(60000, token);
                List<string> lines = new List<string>();
                while (_csvRowQueue.TryDequeue(out string row)) lines.Add(row);

                if (lines.Count > 0)
                {
                    string path = Path.Combine(folder, $"CAN_{DateTime.Now:yyyyMMdd_HHmm}.csv");
                    await File.AppendAllLinesAsync(path, lines);
                }
            }
        }

        private void StopCapture()
        {
            _cts?.Cancel();
            _hiResTimer.Stop();
            Thread.Sleep(500);
            if (_serialPort != null && _serialPort.IsOpen) _serialPort.Close();
            uiTimer.Stop();
            btnStart.Enabled = true; btnStop.Enabled = false;
        }
    }
}