using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BTL_UDMT
{
    public partial class Form1 : Form
    {
        // Hằng số cấu hình phần cứng DSO138 + RP2040
        private const int NUM_SAMPLES = 1024;
        private const double ADC_VREF = 3.301;
        private const double ADC_MID_VOLT = 1.650;
        private const double AFE_AMP_GAIN = (1.0 + 1000.0 / 560.0) * (1.0 + 1000.0 / 560.0 + 1000.0 / 3000.0);
        private const int GRID_COLS = 10; // 10 ô ngang (Time)
        private const int GRID_ROWS = 8;  // 8 ô dọc (Voltage)

        // Đối tượng truyền thông Serial và bộ đệm nhận Frame
        private SerialPort serialPort;
        private readonly List<byte> rxBuffer = new List<byte>();
        private readonly object dataLock = new object();
        private readonly object writeLock = new object();

        // Dữ liệu mẫu thu được (tính bằng Volt ở đầu vào thực tế sau khi quy đổi AFE)
        private readonly double[] rawAdcVolts = new double[NUM_SAMPLES];
        private readonly double[] inputSignalVolts = new double[NUM_SAMPLES];
        private readonly double[] workVolts = new double[NUM_SAMPLES];

        // Trạng thái thu thập & Trigger
        private volatile bool isConnected = false;
        private volatile bool isConnecting = false;
        private volatile bool isClosing = false;
        private volatile bool gotValidFrame = false;
        private volatile bool singleCaptured = false;
        private int connectSeq = 0;
        private bool triggerFound = false;
        private bool hasNewFrame = false;
        private bool hasNormalFrame = false;
        private int triggerIndex = NUM_SAMPLES / 2;
        private volatile byte hwSwByte = 0x12;
        private volatile byte hwRateCode = 2;
        private int lastHwSw3 = -1;

        // Thông số đo lường tự động
        private double measVmax, measVmin, measVpp, measVavg, measVrms, measFreq, measPeriod, measDuty;

        // Con trỏ đo (Cursors) trên màn hình
        private float cursor1X = 0.30f;
        private float cursor2X = 0.70f;
        private float cursor1Y = 0.30f;

        // Bộ định thời làm tươi màn hình (~30 FPS)
        private Timer renderTimer;

        public Form1()
        {
            InitializeComponent();
            BuildCustomUI();
            BindUIEvents();
            this.DoubleBuffered = true;

            // Khởi tạo giá trị nền 0V (tương ứng 1.65V ở chân ADC) trước khi kết nối
            for (int i = 0; i < NUM_SAMPLES; i++)
            {
                rawAdcVolts[i] = ADC_MID_VOLT;
                inputSignalVolts[i] = 0.0;
            }

            RefreshComPorts();

            renderTimer = new Timer { Interval = 33 };
            renderTimer.Tick += RenderTimer_Tick;
            renderTimer.Start();
        }

        private void BindUIEvents()
        {
            // Khối Kết nối COM
            btnRefresh.Click += (s, e) => RefreshComPorts();
            btnConnect.Click += BtnConnect_Click;
            btnDisconnect.Click += BtnDisconnect_Click;
            this.FormClosing += Form1_FormClosing;

            // Khối Trục đứng (cập nhật ngay khi đổi thang đo)
            EventHandler redrawHandler = (s, e) => picScope.Invalidate();
            radCplDC.CheckedChanged += redrawHandler;
            radCplAC.CheckedChanged += redrawHandler;
            radCplGND.CheckedChanged += redrawHandler;
            radSen1_1V.CheckedChanged += redrawHandler;
            radSen1_01V.CheckedChanged += redrawHandler;
            radSen1_10mV.CheckedChanged += redrawHandler;
            radSen2_x5.CheckedChanged += redrawHandler;
            radSen2_x2.CheckedChanged += redrawHandler;
            radSen2_x1.CheckedChanged += redrawHandler;

            // Khối Trigger (reset trạng thái giữ sóng Single khi đổi chế độ)
            EventHandler trigModeHandler = (s, e) =>
            {
                RadioButton rb = s as RadioButton;
                if (rb != null && rb.Checked) ResetTriggerMode();
            };
            radTrigAuto.CheckedChanged += trigModeHandler;
            radTrigNorm.CheckedChanged += trigModeHandler;
            radTrigSingle.CheckedChanged += trigModeHandler;
            radTrigSingle.Click += (s, e) => ResetTriggerMode();
            radEdgeRising.CheckedChanged += redrawHandler;
            radEdgeFalling.CheckedChanged += redrawHandler;

            // Các thanh trượt vị trí và mức Trigger
            trkVPos.Scroll += (s, e) =>
            {
                lblVPos.Text = $"Vị trí tia dọc (V-Position): {(trkVPos.Value / 10.0):+0.0;-0.0;0.0} div";
                picScope.Invalidate();
            };
            trkHPos.Scroll += (s, e) =>
            {
                lblHPos.Text = $"Dịch ngang (H-Position): {trkHPos.Value} mẫu";
                picScope.Invalidate();
            };
            trkTrigLevel.Scroll += (s, e) =>
            {
                lblTrigLevel.Text = $"Mức Trigger (Level): {GetTriggerVoltage():+0.00;-0.00;0.00} V";
                picScope.Invalidate();
            };

            // Đổi Time/Div -> gửi lệnh cấu hình tốc độ lấy mẫu xuống RP2040
            cboTimeDiv.SelectedIndexChanged += CboTimeDiv_SelectedIndexChanged;

            // Vẽ đồ thị và tương tác con trỏ
            chkShowCursors.CheckedChanged += redrawHandler;
            chkAutoMeasure.CheckedChanged += redrawHandler;
            picScope.Paint += PicScope_Paint;
            picScope.MouseDown += PicScope_MouseMove;
            picScope.MouseMove += PicScope_MouseMove;

            // Lưu ảnh và xuất CSV
            btnSaveImage.Click += BtnSaveImage_Click;
            btnExportCSV.Click += BtnExportCSV_Click;
            btnSetGen.Click += (s, e) => SendGenCommandToRP2040();
        }

        // =========================================================================
        // 1. QUẢN LÝ KẾT NỐI SERIAL & GIẢI MÃ GÓI TIN NHỊ PHÂN TỪ RP2040
        // =========================================================================
        private SerialPort CreateSerialPort(string portName, int baud)
        {
            return new SerialPort
            {
                PortName = portName,
                BaudRate = baud,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                ReadBufferSize = 65536,
                ReadTimeout = 200,
                WriteTimeout = 200,
                DtrEnable = true, // Cần thiết cho USB CDC của RP2040
                RtsEnable = true
            };
        }

        private void ClosePortAsync(SerialPort port)
        {
            if (port == null) return;
            try { port.DataReceived -= SerialPort_DataReceived; } catch { }
            Task.Run(() =>
            {
                try { if (port.IsOpen) port.Close(); } catch { }
                try { port.Dispose(); } catch { }
            });
        }

        private void SetDisconnectedUI(string statusText)
        {
            isConnecting = false;
            isConnected = false;
            gotValidFrame = false;
            singleCaptured = false;
            hasNormalFrame = false;
            triggerFound = false;
            btnConnect.Enabled = true;
            btnDisconnect.Enabled = false;
            btnRefresh.Enabled = true;
            cboPorts.Enabled = true;
            cboBaudrate.Enabled = true;
            lblComStatus.Text = statusText;
            picScope.Invalidate();
        }

        private void RefreshComPorts()
        {
            cboPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            if (ports.Length > 0)
            {
                cboPorts.Items.AddRange(ports);
                cboPorts.SelectedIndex = 0;
                lblComStatus.Text = $"Tìm thấy {ports.Length} cổng COM";
            }
            else
            {
                lblComStatus.Text = "Không tìm thấy cổng COM";
            }
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            if (isConnecting || isConnected) return;

            if (cboPorts.SelectedItem == null)
            {
                MessageBox.Show("Không tìm thấy cổng COM để kết nối!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string portName = cboPorts.SelectedItem.ToString();
            int baud = 115200;
            if (int.TryParse(cboBaudrate.SelectedItem?.ToString(), out int parsedBaud))
            {
                baud = parsedBaud;
            }

            int mySeq = ++connectSeq;
            isConnecting = true;
            btnConnect.Enabled = false;
            btnDisconnect.Enabled = true;
            btnRefresh.Enabled = false;
            cboPorts.Enabled = false;
            cboBaudrate.Enabled = false;
            lblComStatus.Text = $"Đang kết nối {portName}...";

            lock (dataLock)
            {
                rxBuffer.Clear();
                hasNewFrame = false;
            }
            singleCaptured = false;
            hasNormalFrame = false;
            triggerFound = false;
            gotValidFrame = false;
            lastHwSw3 = -1;

            SerialPort port = CreateSerialPort(portName, baud);
            Task openTask = Task.Run(() =>
            {
                port.Open();
                try { port.DiscardInBuffer(); } catch { }
            });

            Task finished = await Task.WhenAny(openTask, Task.Delay(2000));
            if (finished != openTask || mySeq != connectSeq || isClosing)
            {
                _ = openTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        var ex = t.Exception;
                        return;
                    }
                    ClosePortAsync(port);
                });
                if (mySeq == connectSeq && !isClosing)
                {
                    SetDisconnectedUI($"Hết thời gian kết nối {portName}");
                    MessageBox.Show($"Cổng {portName} không phản hồi (sai cổng COM hoặc thiết bị bận).", "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            try
            {
                await openTask;
            }
            catch (Exception ex)
            {
                ClosePortAsync(port);
                if (mySeq == connectSeq && !isClosing)
                {
                    SetDisconnectedUI("Lỗi kết nối COM");
                    MessageBox.Show("Không thể mở cổng COM: " + ex.Message, "Lỗi kết nối", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            serialPort = port;
            isConnected = true;
            port.DataReceived += SerialPort_DataReceived;
            SendRateCommandToRP2040();
            SendGenCommandToRP2040();

            int waited = 0;
            while (!gotValidFrame && waited < 2000 && mySeq == connectSeq && !isClosing && isConnected)
            {
                await Task.Delay(50);
                waited += 50;
            }

            if (mySeq != connectSeq || isClosing || !isConnected) return;

            if (!gotValidFrame)
            {
                DisconnectSerial();
                lblComStatus.Text = $"Sai cổng COM ({portName} không có dữ liệu RP2040)";
                MessageBox.Show($"Cổng {portName} không gửi dữ liệu máy hiện sóng RP2040. Vui lòng chọn đúng cổng COM!", "Sai cổng COM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            isConnecting = false;
            lblComStatus.Text = $"Đã kết nối {portName} ({baud} bps)";
            picScope.Invalidate();
        }

        private void BtnDisconnect_Click(object sender, EventArgs e)
        {
            DisconnectSerial();
        }

        private void DisconnectSerial()
        {
            connectSeq++;
            SerialPort port = serialPort;
            serialPort = null;
            SetDisconnectedUI("Đã ngắt kết nối");
            ClosePortAsync(port);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isClosing = true;
            connectSeq++;
            isConnected = false;
            isConnecting = false;
            renderTimer?.Stop();
            SerialPort port = serialPort;
            serialPort = null;
            ClosePortAsync(port);
        }

        private void CboTimeDiv_SelectedIndexChanged(object sender, EventArgs e)
        {
            SendRateCommandToRP2040();
            picScope.Invalidate();
        }

        private void WriteCommandAsync(string cmd)
        {
            SerialPort port = serialPort;
            if (port == null || !isConnected) return;
            Task.Run(() =>
            {
                lock (writeLock)
                {
                    try
                    {
                        if (isConnected && port == serialPort && port.IsOpen)
                        {
                            port.Write(cmd);
                        }
                    }
                    catch { }
                }
            });
        }

        private void SendRateCommandToRP2040()
        {
            if (isConnected && serialPort != null && serialPort.IsOpen)
            {
                int idx = cboTimeDiv.SelectedIndex;
                int code;
                if (idx <= 3) code = 8;
                else if (idx == 4) code = 7;
                else if (idx <= 6) code = 0;
                else if (idx == 7) code = 1;
                else if (idx == 8) code = 2;
                else if (idx == 9) code = 3;
                else if (idx == 10) code = 4;
                else code = 5;

                WriteCommandAsync($"R{code}\n");
            }
        }

        private void SendGenCommandToRP2040()
        {
            if (isConnected && serialPort != null && serialPort.IsOpen)
            {
                if (!int.TryParse(txtGenFreq.Text.Trim(), out int freq)) freq = 1000;
                if (!int.TryParse(txtGenDuty.Text.Trim(), out int duty)) duty = 50;
                if (freq < 10) freq = 10;
                if (freq > 500000) freq = 500000;
                if (duty < 1) duty = 1;
                if (duty > 99) duty = 99;
                txtGenFreq.Text = freq.ToString();
                txtGenDuty.Text = duty.ToString();
                WriteCommandAsync($"G{freq},{duty}\n");
            }
        }

        private void ResetTriggerMode()
        {
            singleCaptured = false;
            hasNormalFrame = false;
            triggerFound = false;
            lock (dataLock)
            {
                rxBuffer.Clear();
                hasNewFrame = false;
            }
            picScope.Invalidate();
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort port = sender as SerialPort;
            if (!isConnected || port == null || port != serialPort) return;

            try
            {
                for (int iter = 0; iter < 8 && isConnected && port == serialPort && port.IsOpen; iter++)
                {
                    int bytesToRead = port.BytesToRead;
                    if (bytesToRead <= 0) break;
                    if (bytesToRead > 8192) bytesToRead = 8192;

                    byte[] temp = new byte[bytesToRead];
                    int count = port.Read(temp, 0, bytesToRead);
                    if (count <= 0) break;

                    if (singleCaptured) continue;

                    lock (dataLock)
                    {
                        if (count == temp.Length)
                        {
                            rxBuffer.AddRange(temp);
                        }
                        else
                        {
                            byte[] slice = new byte[count];
                            Buffer.BlockCopy(temp, 0, slice, 0, count);
                            rxBuffer.AddRange(slice);
                        }
                        ParseBinaryFrames();
                    }
                }
            }
            catch { }
        }

        private void ParseBinaryFrames()
        {
            const int FRAME_SIZE = 4 + (NUM_SAMPLES * 2) + 1;

            if (rxBuffer.Count > FRAME_SIZE * 4)
            {
                rxBuffer.RemoveRange(0, rxBuffer.Count - FRAME_SIZE * 2);
            }

            while (rxBuffer.Count >= FRAME_SIZE)
            {
                if (rxBuffer[0] != 0xAA || rxBuffer[1] != 0x55)
                {
                    int nextSync = rxBuffer.IndexOf(0xAA, 1);
                    if (nextSync < 0)
                    {
                        rxBuffer.Clear();
                        break;
                    }
                    rxBuffer.RemoveRange(0, nextSync);
                    continue;
                }

                byte rateCode = rxBuffer[2];
                byte swByte = rxBuffer[3];
                byte calcChecksum = (byte)(rateCode ^ swByte);

                for (int i = 0; i < NUM_SAMPLES * 2; i++)
                {
                    calcChecksum ^= rxBuffer[4 + i];
                }

                byte frameChecksum = rxBuffer[FRAME_SIZE - 1];
                if (calcChecksum == frameChecksum)
                {
                    for (int s = 0; s < NUM_SAMPLES; s++)
                    {
                        int lo = rxBuffer[4 + s * 2];
                        int hi = rxBuffer[4 + s * 2 + 1];
                        int adcVal = (hi << 8) | lo;
                        if (adcVal > 4095) adcVal = 4095;
                        rawAdcVolts[s] = (adcVal / 4095.0) * ADC_VREF;
                    }
                    hwSwByte = swByte;
                    hwRateCode = rateCode;
                    hasNewFrame = true;
                    gotValidFrame = true;
                    rxBuffer.RemoveRange(0, FRAME_SIZE);
                }
                else
                {
                    int nextSync = rxBuffer.IndexOf(0xAA, 1);
                    if (nextSync < 0)
                    {
                        rxBuffer.Clear();
                        break;
                    }
                    rxBuffer.RemoveRange(0, nextSync);
                }
            }
        }

        // =========================================================================
        // 2. TÍNH TOÁN THANG ĐO DSO138, TRIGGER VÀ ĐO LƯỜNG TỰ ĐỘNG
        // =========================================================================
        private double GetVoltsPerDiv()
        {
            double sen1 = 1.0;
            if (radSen1_01V.Checked) sen1 = 0.1;
            else if (radSen1_10mV.Checked) sen1 = 0.01;

            double sen2Multiplier = 1.0;
            if (radSen2_x5.Checked) sen2Multiplier = 5.0;
            else if (radSen2_x2.Checked) sen2Multiplier = 2.0;

            return sen1 * sen2Multiplier;
        }

        private double GetSamplingFrequencyHz()
        {
            switch (cboTimeDiv.SelectedIndex)
            {
                case 0:
                case 1:
                case 2:
                case 3: return 3000000.0;
                case 4: return 1500000.0;
                case 5:
                case 6: return 500000.0;
                case 7: return 250000.0;
                case 8: return 100000.0;
                case 9: return 50000.0;
                case 10: return 20000.0;
                default: return 10000.0;
            }
        }

        private double GetRateHzFromCode(byte code)
        {
            switch (code)
            {
                case 0: return 500000.0;
                case 1: return 250000.0;
                case 2: return 100000.0;
                case 3: return 50000.0;
                case 4: return 20000.0;
                case 5: return 10000.0;
                case 6: return 1000000.0;
                case 7: return 1500000.0;
                case 8: return 3000000.0;
                default: return GetSamplingFrequencyHz();
            }
        }

        private double GetTimePerDivSeconds()
        {
            switch (cboTimeDiv.SelectedIndex)
            {
                case 0: return 1e-6;
                case 1: return 2e-6;
                case 2: return 5e-6;
                case 3: return 10e-6;
                case 4: return 20e-6;
                case 5: return 50e-6;
                case 6: return 100e-6;
                case 7: return 200e-6;
                case 8: return 500e-6;
                case 9: return 1e-3;
                case 10: return 2e-3;
                default: return 5e-3;
            }
        }

        private double GetTriggerVoltage()
        {
            return (trkTrigLevel.Value / 10.0) * GetVoltsPerDiv();
        }

        private void SyncSwitchesFromHardware()
        {
            byte sw;
            lock (dataLock)
            {
                sw = hwSwByte;
            }
            int sw1 = sw & 0x03;
            int sw2 = (sw >> 2) & 0x03;
            int sw3 = (sw >> 4) & 0x01;

            if (sw1 == 0 && !radCplGND.Checked) radCplGND.Checked = true;
            else if (sw1 == 1 && !radCplAC.Checked) radCplAC.Checked = true;
            else if (sw1 == 2 && !radCplDC.Checked) radCplDC.Checked = true;

            if (sw2 == 0 && !radSen1_1V.Checked) radSen1_1V.Checked = true;
            else if (sw2 == 1 && !radSen1_01V.Checked) radSen1_01V.Checked = true;
            else if (sw2 == 2 && !radSen1_10mV.Checked) radSen1_10mV.Checked = true;

            if (sw3 != lastHwSw3)
            {
                lastHwSw3 = sw3;
                if (sw3 == 1 && !radSen2_x1.Checked) radSen2_x1.Checked = true;
                else if (sw3 == 0 && !radSen2_x5.Checked) radSen2_x5.Checked = true;
            }
        }

        private void ProcessSignalAndMeasurements()
        {
            double alpha1 = radSen1_1V.Checked ? (20.0 / 2020.0) : (radSen1_01V.Checked ? 0.1 : 1.0);
            double hwAlpha2 = isConnected ? ((((hwSwByte >> 4) & 0x01) == 1) ? 1.0 : 0.2) : (radSen2_x5.Checked ? 0.2 : 1.0);
            double totalGain = AFE_AMP_GAIN * alpha1 * hwAlpha2;

            lock (dataLock)
            {
                if (!hasNewFrame) return;
                hasNewFrame = false;

                for (int i = 0; i < NUM_SAMPLES; i++)
                {
                    if (radCplGND.Checked)
                    {
                        workVolts[i] = 0.0;
                    }
                    else
                    {
                        workVolts[i] = (rawAdcVolts[i] - ADC_MID_VOLT) / totalGain;
                    }
                }
            }

            // Khử thành phần DC trung bình nếu đang chọn chế độ AC
            double sum = 0;
            for (int i = 0; i < NUM_SAMPLES; i++) sum += workVolts[i];
            double dcOffset = sum / NUM_SAMPLES;

            if (radCplAC.Checked)
            {
                for (int i = 0; i < NUM_SAMPLES; i++) workVolts[i] -= dcOffset;
                dcOffset = 0;
            }

            // Thuật toán bắt điểm đồng bộ (Trigger)
            double trigLevel = GetTriggerVoltage();
            double hyst = GetVoltsPerDiv() * 0.05;
            bool rising = radEdgeRising.Checked;
            bool found = false;
            int foundIndex = NUM_SAMPLES / 2;

            for (int i = NUM_SAMPLES / 4; i < (NUM_SAMPLES * 3) / 4; i++)
            {
                if (rising)
                {
                    if (workVolts[i - 1] < (trigLevel - hyst) && workVolts[i] >= trigLevel)
                    {
                        foundIndex = i;
                        found = true;
                        break;
                    }
                }
                else
                {
                    if (workVolts[i - 1] > (trigLevel + hyst) && workVolts[i] <= trigLevel)
                    {
                        foundIndex = i;
                        found = true;
                        break;
                    }
                }
            }

            triggerFound = found;
            if ((radTrigNorm.Checked || radTrigSingle.Checked) && !found)
            {
                return;
            }

            Array.Copy(workVolts, inputSignalVolts, NUM_SAMPLES);
            triggerIndex = foundIndex;

            // Tính Vmax, Vmin, Vpp, Vavg, Vrms
            double vmax = -1e9, vmin = 1e9, sumSq = 0;
            for (int i = 0; i < NUM_SAMPLES; i++)
            {
                double v = inputSignalVolts[i];
                if (v > vmax) vmax = v;
                if (v < vmin) vmin = v;
                sumSq += v * v;
            }

            measVmax = vmax;
            measVmin = vmin;
            measVpp = vmax - vmin;
            measVavg = dcOffset;
            measVrms = Math.Sqrt(sumSq / NUM_SAMPLES);

            if (radTrigNorm.Checked)
            {
                hasNormalFrame = true;
            }
            if (radTrigSingle.Checked && found)
            {
                singleCaptured = true;
            }

            CalculateFrequencyAndDuty((vmax + vmin) / 2.0, measVpp);
            UpdateMeasurementLabels();
        }

        private void CalculateFrequencyAndDuty(double midLevel, double vpp)
        {
            if (vpp < GetVoltsPerDiv() * 0.15)
            {
                measFreq = 0;
                measPeriod = 0;
                measDuty = 0;
                return;
            }

            double hyst = vpp * 0.1;
            List<int> risingCrossings = new List<int>();
            int highCount = 0;
            bool armed = false;

            for (int i = 1; i < NUM_SAMPLES; i++)
            {
                if (inputSignalVolts[i] > midLevel) highCount++;

                if (!armed && inputSignalVolts[i] < midLevel - hyst)
                {
                    armed = true;
                }
                else if (armed && inputSignalVolts[i] >= midLevel + hyst)
                {
                    risingCrossings.Add(i);
                    armed = false;
                }
            }

            if (risingCrossings.Count >= 2)
            {
                double totalSamples = risingCrossings[risingCrossings.Count - 1] - risingCrossings[0];
                double avgSamplesPerCycle = totalSamples / (risingCrossings.Count - 1);
                double fs = GetSamplingFrequencyHz();

                measFreq = fs / avgSamplesPerCycle;
                measPeriod = (1.0 / measFreq) * 1000.0;
                measDuty = (highCount / (double)NUM_SAMPLES) * 100.0;
            }
            else
            {
                measFreq = 0;
                measPeriod = 0;
                measDuty = 0;
            }
        }

        private void UpdateMeasurementLabels()
        {
            lblMeasVpp.Text = $"Vpp (Đỉnh-Đỉnh): {measVpp:F2} V";
            lblMeasVmax.Text = $"Vmax (Cực đại): {measVmax:+0.00;-0.00;0.00} V";
            lblMeasVmin.Text = $"Vmin (Cực tiểu): {measVmin:+0.00;-0.00;0.00} V";
            lblMeasVavg.Text = $"Vavg (Trung bình): {measVavg:+0.00;-0.00;0.00} V";
            lblMeasVrms.Text = $"Vrms (Hiệu dụng): {measVrms:F2} V";

            if (measFreq >= 1000.0)
                lblMeasFreq.Text = $"Tần số (Freq): {(measFreq / 1000.0):F3} kHz";
            else
                lblMeasFreq.Text = $"Tần số (Freq): {measFreq:F1} Hz";

            lblMeasPeriod.Text = $"Chu kỳ (Period): {measPeriod:F3} ms";
            lblMeasDuty.Text = $"Hệ số lấp đầy: {measDuty:F1} %";
        }

        // =========================================================================
        // 3. ĐỒ HỌA GDI+ ĐƠN GIẢN (NỀN TRẮNG, CHỮ ĐEN, SÓNG ĐEN)
        // =========================================================================
        private void RenderTimer_Tick(object sender, EventArgs e)
        {
            if (isConnected && gotValidFrame && !singleCaptured)
            {
                SyncSwitchesFromHardware();
                ProcessSignalAndMeasurements();
                picScope.Invalidate();
            }
        }

        private void PicScope_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            int w = picScope.Width;
            int h = picScope.Height;
            int topBarHeight = 30;

            Rectangle gridRect = new Rectangle(38, topBarHeight + 8, w - 52, h - topBarHeight - 20);
            if (gridRect.Width <= 50 || gridRect.Height <= 50) return;

            // 1. Thanh trạng thái phía trên (không hiển thị Fs = ...)
            DrawTopStatusBar(g, w, topBarHeight);

            // 2. Lưới tọa độ 10x8 ô
            DrawGraticule(g, gridRect);

            // 3. Dấu mốc 0V và đường mức Trigger
            DrawMarkersAndTriggerLine(g, gridRect);

            // 4. Vẽ dạng sóng
            if ((!radTrigNorm.Checked && !radTrigSingle.Checked) || hasNormalFrame || singleCaptured)
            {
                DrawWaveform(g, gridRect);
            }

            // 5. Vẽ con trỏ đo nếu bật
            if (chkShowCursors.Checked)
            {
                DrawCursors(g, gridRect);
            }
        }

        private void DrawTopStatusBar(Graphics g, int width, int height)
        {
            g.FillRectangle(Brushes.White, 0, 0, width, height);
            using (Pen sepPen = new Pen(Color.Silver, 1f))
            {
                g.DrawLine(sepPen, 0, height - 1, width, height - 1);
            }

            string stateText;
            if (!isConnected || !gotValidFrame) stateText = "CHƯA KẾT NỐI";
            else if (singleCaptured) stateText = "SINGLE (GIỮ)";
            else if (triggerFound) stateText = "TRIG'D";
            else stateText = "ĐANG ĐO";

            using (Font boldFont = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (Font regularFont = new Font("Segoe UI", 9F, FontStyle.Regular))
            {
                g.DrawRectangle(Pens.Black, 8, 5, 102, height - 10);
                g.DrawString(stateText, boldFont, Brushes.Black, 14, 7);

                string cplStr = radCplDC.Checked ? "DC" : (radCplAC.Checked ? "AC" : "GND");
                double vDiv = GetVoltsPerDiv();
                string vDivStr = vDiv >= 1.0 ? $"{vDiv:F2} V/div" : $"{(vDiv * 1000):F0} mV/div";

                string info = $"[{cplStr}]   Scale: {vDivStr}   |   Timebase: {cboTimeDiv.SelectedItem}   |   Trig: {GetTriggerVoltage():+0.00;-0.00;0.00}V";
                g.DrawString(info, regularFont, Brushes.Black, 122, 7);

                double fs = (isConnected && gotValidFrame) ? GetRateHzFromCode(hwRateCode) : GetSamplingFrequencyHz();
                string fsStr = fs >= 1000000.0 ? $"Fs: {(fs / 1000000.0):F2} MSa/s" : $"Fs: {(fs / 1000.0):F0} kSa/s";
                SizeF fsSize = g.MeasureString(fsStr, boldFont);
                g.DrawString(fsStr, boldFont, Brushes.Black, width - fsSize.Width - 10, 7);
            }
        }

        private void DrawGraticule(Graphics g, Rectangle r)
        {
            using (Pen borderPen = new Pen(Color.Black, 1.2f))
            using (Pen gridPen = new Pen(Color.LightGray, 1f) { DashStyle = DashStyle.Dot })
            using (Pen centerPen = new Pen(Color.Gray, 1f))
            {
                float cellW = (float)r.Width / GRID_COLS;
                float cellH = (float)r.Height / GRID_ROWS;

                for (int c = 1; c < GRID_COLS; c++)
                {
                    float x = r.Left + c * cellW;
                    g.DrawLine(c == GRID_COLS / 2 ? centerPen : gridPen, x, r.Top, x, r.Bottom);
                }

                for (int row = 1; row < GRID_ROWS; row++)
                {
                    float y = r.Top + row * cellH;
                    g.DrawLine(row == GRID_ROWS / 2 ? centerPen : gridPen, r.Left, y, r.Right, y);
                }

                // Các vạch chia nhỏ trên 2 trục chính giữa
                float midX = r.Left + r.Width / 2f;
                float midY = r.Top + r.Height / 2f;
                float subW = cellW / 5f;
                float subH = cellH / 5f;

                for (int i = 1; i < GRID_COLS * 5; i++)
                {
                    float sx = r.Left + i * subW;
                    g.DrawLine(centerPen, sx, midY - 3, sx, midY + 3);
                }
                for (int i = 1; i < GRID_ROWS * 5; i++)
                {
                    float sy = r.Top + i * subH;
                    g.DrawLine(centerPen, midX - 3, sy, midX + 3, sy);
                }

                g.DrawRectangle(borderPen, r);
            }
        }

        private void DrawMarkersAndTriggerLine(Graphics g, Rectangle r)
        {
            float cellH = (float)r.Height / GRID_ROWS;
            float midY = r.Top + r.Height / 2f;
            float vPosOffsetDiv = trkVPos.Value / 10.0f;

            float zeroY = midY - (vPosOffsetDiv * cellH);
            zeroY = Math.Max(r.Top, Math.Min(r.Bottom, zeroY));

            using (Font f = new Font("Segoe UI", 8F, FontStyle.Bold))
            {
                g.DrawString("0V>", f, Brushes.Black, 4, zeroY - 7);
            }

            double vDiv = GetVoltsPerDiv();
            double trigVolt = GetTriggerVoltage();
            float trigY = zeroY - (float)((trigVolt / vDiv) * cellH);
            trigY = Math.Max(r.Top, Math.Min(r.Bottom, trigY));

            using (Pen trigPen = new Pen(Color.Gray, 1f) { DashStyle = DashStyle.Dash })
            using (Font f = new Font("Segoe UI", 8F, FontStyle.Regular))
            {
                g.DrawLine(trigPen, r.Left, trigY, r.Right, trigY);
                g.DrawString("T>", f, Brushes.Black, 10, trigY - 7);
            }
        }

        private void DrawWaveform(Graphics g, Rectangle r)
        {
            double vDiv = GetVoltsPerDiv();
            double fs = GetSamplingFrequencyHz();
            double timePerDiv = GetTimePerDivSeconds();

            double totalScreenTime = timePerDiv * GRID_COLS;
            int samplesOnScreen = (int)(totalScreenTime * fs);
            if (samplesOnScreen < 20) samplesOnScreen = 20;
            if (samplesOnScreen > NUM_SAMPLES / 2) samplesOnScreen = NUM_SAMPLES / 2;

            int startSample = triggerIndex - (samplesOnScreen / 2) + trkHPos.Value;
            startSample = Math.Max(0, Math.Min(NUM_SAMPLES - samplesOnScreen - 1, startSample));

            float cellH = (float)r.Height / GRID_ROWS;
            float midY = r.Top + r.Height / 2f;
            float zeroY = midY - ((trkVPos.Value / 10.0f) * cellH);

            PointF[] pts = new PointF[samplesOnScreen];
            for (int i = 0; i < samplesOnScreen; i++)
            {
                float x = r.Left + (i / (float)(samplesOnScreen - 1)) * r.Width;
                double v = inputSignalVolts[startSample + i];
                float y = zeroY - (float)((v / vDiv) * cellH);
                y = Math.Max(r.Top, Math.Min(r.Bottom, y));
                pts[i] = new PointF(x, y);
            }

            if (chkAutoMeasure.Checked && isConnected)
            {
                float yMax = Math.Max(r.Top, Math.Min(r.Bottom, zeroY - (float)((measVmax / vDiv) * cellH)));
                float yMin = Math.Max(r.Top, Math.Min(r.Bottom, zeroY - (float)((measVmin / vDiv) * cellH)));
                using (Pen pBound = new Pen(Color.Gray, 1f) { DashStyle = DashStyle.Dot })
                {
                    g.DrawLine(pBound, r.Left, yMax, r.Right, yMax);
                    g.DrawLine(pBound, r.Left, yMin, r.Right, yMin);
                }
            }

            g.SetClip(r);
            using (Pen wavePen = new Pen(Color.Black, 1.6f))
            {
                g.DrawLines(wavePen, pts);
            }
            g.ResetClip();
        }

        private void DrawCursors(Graphics g, Rectangle r)
        {
            float x1 = r.Left + cursor1X * r.Width;
            float x2 = r.Left + cursor2X * r.Width;
            float y1 = r.Top + cursor1Y * r.Height;

            using (Pen cPen1 = new Pen(Color.Black, 1f) { DashStyle = DashStyle.Dash })
            using (Pen cPen2 = new Pen(Color.DimGray, 1f) { DashStyle = DashStyle.DashDot })
            using (Font f = new Font("Segoe UI", 8.5F, FontStyle.Regular))
            {
                g.DrawLine(cPen1, x1, r.Top, x1, r.Bottom);
                g.DrawLine(cPen2, x2, r.Top, x2, r.Bottom);
                g.DrawLine(cPen1, r.Left, y1, r.Right, y1);

                double dtMs = Math.Abs(cursor2X - cursor1X) * GRID_COLS * GetTimePerDivSeconds() * 1000.0;
                double fCursor = dtMs > 1e-6 ? (1000.0 / dtMs) : 0;

                string cursorBox = $"Con trỏ đo:  dT = {dtMs:F3} ms  |  1/dT = {fCursor:F1} Hz";
                Rectangle boxRect = new Rectangle(r.Left + 8, r.Top + 8, 310, 22);
                g.FillRectangle(Brushes.White, boxRect);
                g.DrawRectangle(Pens.Black, boxRect);
                g.DrawString(cursorBox, f, Brushes.Black, r.Left + 14, r.Top + 11);
            }
        }

        private void PicScope_MouseMove(object sender, MouseEventArgs e)
        {
            if (!chkShowCursors.Checked || e.Button == MouseButtons.None) return;

            int topBarHeight = 30;
            Rectangle r = new Rectangle(38, topBarHeight + 8, picScope.Width - 52, picScope.Height - topBarHeight - 20);
            if (!r.Contains(e.Location)) return;

            float normX = (e.X - r.Left) / (float)r.Width;
            float normY = (e.Y - r.Top) / (float)r.Height;

            if (e.Button == MouseButtons.Left)
            {
                cursor1X = normX;
                cursor1Y = normY;
            }
            else if (e.Button == MouseButtons.Right)
            {
                cursor2X = normX;
            }
            picScope.Invalidate();
        }

        // =========================================================================
        // 4. LƯU ẢNH MÀN HÌNH VÀ XUẤT FILE CSV
        // =========================================================================
        private void BtnSaveImage_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "PNG Image (*.png)|*.png", FileName = $"Oscilloscope_{DateTime.Now:yyyyMMdd_HHmmss}.png" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    Bitmap bmp = new Bitmap(picScope.Width, picScope.Height);
                    picScope.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    bmp.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                    MessageBox.Show("Đã lưu ảnh chụp màn hình thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnExportCSV_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog { Filter = "CSV File (*.csv)|*.csv", FileName = $"WaveformData_{DateTime.Now:yyyyMMdd_HHmmss}.csv" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    using (StreamWriter sw = new StreamWriter(sfd.FileName))
                    {
                        double fs = GetSamplingFrequencyHz();
                        sw.WriteLine("Sample_Index,Time_ms,Voltage_V");
                        for (int i = 0; i < NUM_SAMPLES; i++)
                        {
                            double tMs = (i / fs) * 1000.0;
                            sw.WriteLine($"{i},{tMs:F6},{inputSignalVolts[i]:F4}");
                        }
                    }
                    MessageBox.Show("Đã xuất dữ liệu sóng ra file CSV thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}