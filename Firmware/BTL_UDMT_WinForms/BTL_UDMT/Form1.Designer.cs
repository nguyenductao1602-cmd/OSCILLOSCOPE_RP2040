using System;
using System.Drawing;
using System.Windows.Forms;

namespace BTL_UDMT
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        // Các vùng bố cục chính
        private Panel pnlTopConnection;
        private Panel pnlScreenContainer;
        private PictureBox picScope;
        private Panel pnlControlRight;
        private Panel pnlBottomMeasure;

        // 1. Khối Kết nối & Cấu hình COM (giống mẫu)
        private GroupBox grpConnection;
        private Label lblPort;
        private ComboBox cboPorts;
        private Label lblBaud;
        private ComboBox cboBaudrate;
        private Button btnRefresh;
        private Button btnConnect;
        private Button btnDisconnect;
        private Label lblComStatus;

        // 2. Khối Trục đứng (Vertical - Chuẩn DSO138)
        private GroupBox grpVertical;
        private Label lblCpl;
        private RadioButton radCplDC, radCplAC, radCplGND;
        private Label lblSen1;
        private RadioButton radSen1_1V, radSen1_01V, radSen1_10mV;
        private Label lblSen2;
        private RadioButton radSen2_x5, radSen2_x2, radSen2_x1;
        private Label lblVPos;
        private TrackBar trkVPos;

        // 3. Khối Trục ngang (Horizontal / Timebase)
        private GroupBox grpHorizontal;
        private Label lblTimeDiv;
        private ComboBox cboTimeDiv;
        private Label lblHPos;
        private TrackBar trkHPos;

        // 4. Khối Đồng bộ (Trigger)
        private GroupBox grpTrigger;
        private RadioButton radTrigAuto, radTrigNorm, radTrigSingle;
        private RadioButton radEdgeRising, radEdgeFalling;
        private Label lblTrigLevel;
        private TrackBar trkTrigLevel;

        // 5. Khối Tiện ích & Xuất dữ liệu
        private GroupBox grpTools;
        private CheckBox chkShowCursors;
        private CheckBox chkAutoMeasure;
        private Button btnSaveImage;
        private Button btnExportCSV;
        private GroupBox grpTestGen;
        private Label lblGenFreq, lblGenDuty;
        private TextBox txtGenFreq, txtGenDuty;
        private Button btnSetGen;

        // 6. Các nhãn hiển thị thông số đo (Measurements)
        private Label lblMeasVpp, lblMeasVmax, lblMeasVmin, lblMeasVavg, lblMeasVrms, lblMeasFreq, lblMeasPeriod, lblMeasDuty;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.Name = "Form1";
            this.ResumeLayout(false);
        }
        private void BuildCustomUI()
        {
            this.SuspendLayout();
            this.Name = "Form1";
            this.ResumeLayout(false);

            this.components = new System.ComponentModel.Container();
            this.Text = "DSO138 - RP2040 PC Oscilloscope";
            this.Size = new Size(1220, 760);
            this.MinimumSize = new Size(1050, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.ForeColor = Color.Black;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            // =========================================================================
            // PANEL TRÊN CÙNG: 1. KẾT NỐI VÀ CẤU HÌNH COM
            // =========================================================================
            pnlTopConnection = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.White,
                Padding = new Padding(8, 6, 8, 4)
            };

            grpConnection = new GroupBox
            {
                Text = "1. KẾT NỐI VÀ CẤU HÌNH COM",
                Dock = DockStyle.Fill,
                ForeColor = Color.Black,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            lblPort = new Label { Text = "COM:", Location = new Point(12, 26), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            cboPorts = new ComboBox { Location = new Point(54, 23), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };

            lblBaud = new Label { Text = "Baudrate:", Location = new Point(182, 26), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            cboBaudrate = new ComboBox { Location = new Point(246, 23), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            cboBaudrate.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200", "230400", "921600" });
            cboBaudrate.SelectedItem = "115200";

            btnRefresh = CreateSimpleButton("Refresh", 365, 22, 85, 25);
            btnConnect = CreateSimpleButton("Connect", 458, 22, 85, 25);
            btnDisconnect = CreateSimpleButton("Disconnect", 551, 22, 90, 25);
            btnDisconnect.Enabled = false;

            lblComStatus = new Label
            {
                Text = "Không tìm thấy cổng COM",
                Location = new Point(660, 26),
                AutoSize = true,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            grpConnection.Controls.AddRange(new Control[]
            {
                lblPort, cboPorts, lblBaud, cboBaudrate,
                btnRefresh, btnConnect, btnDisconnect, lblComStatus
            });
            pnlTopConnection.Controls.Add(grpConnection);

            // =========================================================================
            // PANEL BÊN PHẢI: BẢNG ĐIỀU KHIỂN MÁY HIỆN SÓNG
            // =========================================================================
            pnlControlRight = new Panel
            {
                Dock = DockStyle.Right,
                Width = 325,
                BackColor = Color.White,
                Padding = new Padding(6),
                AutoScroll = true
            };

            // --- 2. KHỐI TRỤC ĐỨNG (CHUẨN DSO138: CPL, SEN1, SEN2) ---
            grpVertical = CreateGroup("2. KHỐI TRỤC ĐỨNG (DSO138)", 6, 6, 305, 185);

            lblCpl = new Label { Text = "Ghép nối (CPL):", Location = new Point(10, 25), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            Panel pnlCpl = new Panel { Location = new Point(112, 20), Size = new Size(185, 26), BackColor = Color.White };
            radCplDC = CreateRadio("DC", 0, 3, true);
            radCplAC = CreateRadio("AC", 58, 3, false);
            radCplGND = CreateRadio("GND", 116, 3, false);
            pnlCpl.Controls.AddRange(new Control[] { radCplDC, radCplAC, radCplGND });

            lblSen1 = new Label { Text = "Thang đo (SEN1):", Location = new Point(10, 55), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            Panel pnlSen1 = new Panel { Location = new Point(112, 50), Size = new Size(188, 26), BackColor = Color.White };
            radSen1_1V = CreateRadio("1V", 0, 3, true);
            radSen1_01V = CreateRadio("0.1V", 55, 3, false);
            radSen1_10mV = CreateRadio("10mV", 116, 3, false);
            pnlSen1.Controls.AddRange(new Control[] { radSen1_1V, radSen1_01V, radSen1_10mV });

            lblSen2 = new Label { Text = "Hệ số (SEN2):", Location = new Point(10, 85), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            Panel pnlSen2 = new Panel { Location = new Point(112, 80), Size = new Size(185, 26), BackColor = Color.White };
            radSen2_x5 = CreateRadio("X5", 0, 3, false);
            radSen2_x2 = CreateRadio("X2", 58, 3, false);
            radSen2_x1 = CreateRadio("X1", 116, 3, true);
            pnlSen2.Controls.AddRange(new Control[] { radSen2_x5, radSen2_x2, radSen2_x1 });

            lblVPos = new Label { Text = "Vị trí tia dọc (V-Position): 0.0 div", Location = new Point(10, 114), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            trkVPos = new TrackBar { Location = new Point(10, 134), Size = new Size(285, 42), Minimum = -40, Maximum = 40, Value = 0, TickFrequency = 10, BackColor = Color.White };

            grpVertical.Controls.AddRange(new Control[] { lblCpl, pnlCpl, lblSen1, pnlSen1, lblSen2, pnlSen2, lblVPos, trkVPos });

            // --- 3. KHỐI TRỤC NGANG (HORIZONTAL / TIMEBASE) ---
            grpHorizontal = CreateGroup("3. KHỐI THỜI GIAN (HORIZONTAL)", 6, 198, 305, 128);
            lblTimeDiv = new Label { Text = "Time/Div:", Location = new Point(10, 28), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            cboTimeDiv = new ComboBox { Location = new Point(82, 25), Width = 208, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            cboTimeDiv.Items.AddRange(new object[]
            {
                "1 us/div",
                "2 us/div",
                "5 us/div",
                "10 us/div",
                "20 us/div",
                "50 us/div",
                "100 us/div",
                "200 us/div",
                "500 us/div",
                "1 ms/div",
                "2 ms/div",
                "5 ms/div"
            });
            cboTimeDiv.SelectedIndex = 8;

            lblHPos = new Label { Text = "Dịch ngang (H-Position): 0 mẫu", Location = new Point(10, 62), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            trkHPos = new TrackBar { Location = new Point(10, 82), Size = new Size(285, 42), Minimum = -300, Maximum = 300, Value = 0, TickFrequency = 50, BackColor = Color.White };
            grpHorizontal.Controls.AddRange(new Control[] { lblTimeDiv, cboTimeDiv, lblHPos, trkHPos });

            // --- 4. KHỐI ĐỒNG BỘ (TRIGGER) ---
            grpTrigger = CreateGroup("4. KHỐI ĐỒNG BỘ XUNG (TRIGGER)", 6, 332, 305, 146);
            Panel pnlTrigMode = new Panel { Location = new Point(10, 22), Size = new Size(285, 26), BackColor = Color.White };
            radTrigAuto = CreateRadio("Auto", 0, 3, true);
            radTrigNorm = CreateRadio("Normal", 85, 3, false);
            radTrigSingle = CreateRadio("Single", 180, 3, false);
            pnlTrigMode.Controls.AddRange(new Control[] { radTrigAuto, radTrigNorm, radTrigSingle });

            Panel pnlTrigEdge = new Panel { Location = new Point(10, 50), Size = new Size(285, 26), BackColor = Color.White };
            radEdgeRising = CreateRadio("Sườn lên (/)", 0, 3, true);
            radEdgeFalling = CreateRadio("Sườn xuống (\\)", 135, 3, false);
            pnlTrigEdge.Controls.AddRange(new Control[] { radEdgeRising, radEdgeFalling });

            lblTrigLevel = new Label { Text = "Mức Trigger (Level): 0.00 V", Location = new Point(10, 80), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            trkTrigLevel = new TrackBar { Location = new Point(10, 100), Size = new Size(285, 42), Minimum = -40, Maximum = 40, Value = 0, TickFrequency = 10, BackColor = Color.White };
            grpTrigger.Controls.AddRange(new Control[] { pnlTrigMode, pnlTrigEdge, lblTrigLevel, trkTrigLevel });

            // --- 5. KHỐI TIỆN ÍCH ---
            grpTools = CreateGroup("5. CON TRỎ ĐO & LƯU DỮ LIỆU", 6, 484, 305, 112);
            chkShowCursors = new CheckBox { Text = "Bật con trỏ đo (Chuột trái/phải)", Location = new Point(10, 24), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            chkAutoMeasure = new CheckBox { Text = "Hiển thị vạch gióng Vmax / Vmin", Location = new Point(10, 48), AutoSize = true, Checked = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            btnSaveImage = CreateSimpleButton("Lưu Ảnh PNG", 10, 74, 138, 28);
            btnExportCSV = CreateSimpleButton("Xuất File CSV", 156, 74, 138, 28);
            grpTools.Controls.AddRange(new Control[] { chkShowCursors, chkAutoMeasure, btnSaveImage, btnExportCSV });

            grpTestGen = CreateGroup("6. PHÁT XUNG VUÔNG TEST", 6, 602, 305, 86);
            lblGenFreq = new Label { Text = "Freq (Hz):", Location = new Point(10, 24), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            txtGenFreq = new TextBox { Text = "1000", Location = new Point(74, 21), Width = 78, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            lblGenDuty = new Label { Text = "Duty (%):", Location = new Point(160, 24), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            txtGenDuty = new TextBox { Text = "50", Location = new Point(222, 21), Width = 70, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            btnSetGen = CreateSimpleButton("Phát xung vuông", 10, 50, 282, 26);
            grpTestGen.Controls.AddRange(new Control[] { lblGenFreq, txtGenFreq, lblGenDuty, txtGenDuty, btnSetGen });

            pnlControlRight.Controls.AddRange(new Control[] { grpVertical, grpHorizontal, grpTrigger, grpTools, grpTestGen });

            // =========================================================================
            // PANEL PHÍA DƯỚI: BẢNG THÔNG SỐ ĐO LƯỜNG TỰ ĐỘNG
            // =========================================================================
            pnlBottomMeasure = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 86,
                BackColor = Color.White,
                Padding = new Padding(8, 4, 8, 8)
            };

            TableLayoutPanel tblMeasure = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 2,
                BackColor = Color.White
            };
            for (int i = 0; i < 4; i++) tblMeasure.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            for (int r = 0; r < 2; r++) tblMeasure.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            lblMeasVpp = CreateMeasureLabel("Vpp (Đỉnh-Đỉnh): 0.00 V");
            lblMeasVmax = CreateMeasureLabel("Vmax (Cực đại): 0.00 V");
            lblMeasVmin = CreateMeasureLabel("Vmin (Cực tiểu): 0.00 V");
            lblMeasFreq = CreateMeasureLabel("Tần số (Freq): 0.0 Hz");
            lblMeasVavg = CreateMeasureLabel("Vavg (Trung bình): 0.00 V");
            lblMeasVrms = CreateMeasureLabel("Vrms (Hiệu dụng): 0.00 V");
            lblMeasPeriod = CreateMeasureLabel("Chu kỳ (Period): 0.00 ms");
            lblMeasDuty = CreateMeasureLabel("Hệ số lấp đầy: 0.0 %");

            tblMeasure.Controls.Add(lblMeasVpp, 0, 0);
            tblMeasure.Controls.Add(lblMeasVmax, 1, 0);
            tblMeasure.Controls.Add(lblMeasVmin, 2, 0);
            tblMeasure.Controls.Add(lblMeasFreq, 3, 0);
            tblMeasure.Controls.Add(lblMeasVavg, 0, 1);
            tblMeasure.Controls.Add(lblMeasVrms, 1, 1);
            tblMeasure.Controls.Add(lblMeasPeriod, 2, 1);
            tblMeasure.Controls.Add(lblMeasDuty, 3, 1);
            pnlBottomMeasure.Controls.Add(tblMeasure);

            // =========================================================================
            // PANEL TRUNG TÂM: MÀN HÌNH HIỂN THỊ SÓNG (NỀN TRẮNG, NÉT ĐEN)
            // =========================================================================
            pnlScreenContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 6, 4, 4),
                BackColor = Color.White
            };

            picScope = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Cross
            };
            pnlScreenContainer.Controls.Add(picScope);

            // Thứ tự Add đảm bảo thanh kết nối nằm trên cùng toàn chiều ngang
            this.Controls.Add(pnlScreenContainer);
            this.Controls.Add(pnlBottomMeasure);
            this.Controls.Add(pnlControlRight);
            this.Controls.Add(pnlTopConnection);
        }

        private GroupBox CreateGroup(string title, int x, int y, int w, int h)
        {
            return new GroupBox
            {
                Text = title,
                Location = new Point(x, y),
                Size = new Size(w, h),
                ForeColor = Color.Black,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 8.75F, FontStyle.Bold)
            };
        }

        private RadioButton CreateRadio(string text, int x, int y, bool isChecked)
        {
            return new RadioButton
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Checked = isChecked,
                ForeColor = Color.Black,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
        }

        private Button CreateSimpleButton(string text, int x, int y, int w, int h)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = Color.White,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Standard,
                UseVisualStyleBackColor = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
        }

        private Label CreateMeasureLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.Black,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(3),
                Padding = new Padding(6, 0, 0, 0),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };
        }
    }
}