//#define DEBUG_ALL
//#define DEBUG_MOVE_MAP
//#define DEBUG_FIND_GYMS
//#define DEBUG_FIND_BITMAP
#define SHOW_SECTOR_NUMBER

using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using gma.System.Windows;

namespace GoMapBot
{
    /// <summary>
	/// Summary description for Form1.
	/// </summary>
	public class GoMapBot : System.Windows.Forms.Form
	{
		UserActivityHook actHook;

        private string CFGFILENAME = Path.GetDirectoryName(Application.ExecutablePath) + "\\config.dat";
        
        private bool _abort = false;
        private DateTime _startTime = DateTime.Now;

        static private Point _startingPoint = new Point(200, 330);
        static private Size _workingArea = new Size(1580, 685);
        static private int _mapMovementPenalty = 10;
        static private float _brightnessThreshold = 0.10f;
        static private Size _dotSize = new Size(5, 6);
        static private int _forbiddenAreaRadius = 15;

        static private int _movementSteps = 20;
        static private bool _outputTimestamp = true;

        // Timing values
        private int[] _gymParserTiming = new int[] { 200, 500, 850, 200, 200, 20, 200 };
        private int[] _moveMapTiming = new int[] { 100, 100, 500 }; 

        // GymParser variables
        private enum STATE { STOPPED, START, FIND_GYMS, PARSE_GYMS, PARSE_GYMS_2, PARSE_GYMS_3, PARSE_GYMS_4, PARSE_GYMS_5, SELECT_GYM_INFO, OPEN_SELECTION_SOURCE_CODE_IN_NEW_TAB, COPY_SELECTION_SOURCE_CODE_TO_CLIPBOARD, SAVE_CLIPBOARD_TO_FILE, CLOSE_TAB, CLEAR_GYM_POPUP, MOVE_MAP };
        private STATE _state = STATE.STOPPED;
        private string _output_file_name = @"gyms.txt";
        private List<string> _path = new List<string> { "E", "E", "E", "S", "W", "W", "W", "S", "E", "E", "E", "S", "W", "W", "W" };
        private int _sector = 0;
        private List<Point> _gyms;
        private int _x_not_found_counter = 0;
        private Point _gym_point, _greyX_point, _lastUpdate_point;
        private string _path_direction;
        private Size _popupSize = new Size(300, 280);

        // Init reference bitmaps
        Bitmap _greyX = new Bitmap("img/greyX.png");
        Bitmap _lastUpdate = new Bitmap("img/lastUpdate.png");
        Bitmap _tabCloseX = new Bitmap("img/tabCloseX.png");

        // Init desktop bitmaps
        Size _screenSize = new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
        Bitmap _desktopBitmap;
        Graphics _desktopGraphics;


        private System.Windows.Forms.TextBox Debug;
        private Button button1;
        private Button button2;
        private Button buttonS;
        private Button buttonW;
        private Button buttonE;
        private Button buttonN;
        private System.Timers.Timer GymParser;
        private GroupBox groupBox1;
        private Button button_OutputFile;
        private TextBox textBox_OutputFile;
        private GroupBox groupBox3;
        private GroupBox groupBox2;
        private TextBox textBox_Path;
        private NumericUpDown numericUpDown4;
        private NumericUpDown numericUpDown2;
        private NumericUpDown numericUpDown3;
        private NumericUpDown numericUpDown1;
        private Label label2;
        private Label label1;
        private Button button_ShowArea;
        private System.Timers.Timer MoveMapTimer;
        private CheckBox checkBox_OutputTimestamp;
        private Label label3;
        private System.Windows.Forms.Button ButtonStart;


        private void WaitMS(int n)
        {
            if (n < 1) return;
            DateTime desired = DateTime.Now.AddMilliseconds(n);
            while (DateTime.Now < desired)
            {
                System.Windows.Forms.Application.DoEvents();
            }
        }

        private void ParsePath(string path)
        {
            _path.Clear();

            foreach (string p in path.Split(new Char[] { ' ' }))
            {
                _path.Add(p);
            }
        }

        #region Mouse & Keyboard

        // Import SendInput function from user32.dll
		[DllImport("User32.dll", SetLastError=true)]
		public static extern int SendInput(int nInputs, ref MINPUT pInputs, int cbSize);
		[DllImport("User32.dll", SetLastError=true)]
		public static extern int SendInput(int nInputs, ref KINPUT pInputs, int cbSize);

        [DllImport("User32.dll", SetLastError = true)]
        static extern bool SetCursorPos(int X, int Y);

		// Input type constant
		const int INPUT_MOUSE = 0;
		const int INPUT_KEY = 1;

		public struct MOUSEINPUT
		{
			public int dx;
			public int dy;
			public int mouseData;
			public int dwFlags;
			public int time;
			public int dwExtraInfo;
		};

		public struct MINPUT
		{
			public uint type;
			public MOUSEINPUT mi;
		};

		public struct KEYBDINPUT
		{
			public ushort wVk; // virtual key
			public ushort wScan;
			public uint dwFlags; // additional info (such as KeyUp)
			public long time;
			public uint dwExtraInfo;
		};

		public struct KINPUT
		{
			public uint type;
			public KEYBDINPUT mi;
		};


		// Import keybd_event function from user32.dll
		[DllImport("user32.dll",CharSet=CharSet.Auto, CallingConvention=CallingConvention.StdCall)]
		public static extern void keybd_event(byte bVk, byte bScan, long dwFlags, long dwExtraInfo);

		// Declare consts for mouse messages
		public const int MOUSEEVENTF_LEFTDOWN = 0x02;
		public const int MOUSEEVENTF_LEFTUP = 0x04;
		public const int MOUSEEVENTF_RIGHTDOWN = 0x08;
		public const int MOUSEEVENTF_RIGHTUP = 0x10;
		public const int MOUSEEVENTF_WHEEL = 0x800;
		// Declare consts for key scan codes
		public const byte VK_TAB = 0x09;
        public const byte VK_SHIFT = 0x10; // Shift
        public const byte VK_MENU = 0x12; // VK_MENU is Microsoft talk for the ALT key
        public const byte VK_LCONTROL = 0xA2; // Left Control key code
        public const byte VK_F10 = 0x79; // F10 key code
        public const int KEYEVENTF_EXTENDEDKEY = 0x01;
		public const int KEYEVENTF_KEYDOWN = 0x00;
		//		public const int KEYEVENTF_KEYPRESS = 0x00;
		public const int KEYEVENTF_KEYUP = 0x02;

        [DllImport("user32.dll",CharSet=CharSet.Auto, CallingConvention=CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);

        public void PerformMouseClick(Point p)
        {
            Cursor.Position = p;
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)p.X, (uint)p.Y, 0, 0);
        }

        public void PerformMouseSelect(Point p, Point q)
        {
            Cursor.Position = p;
            mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)p.X, (uint)p.Y, 0, 0);
            WaitMS(20);
            Cursor.Position = q;
            mouse_event(MOUSEEVENTF_LEFTUP, (uint)q.X, (uint)q.Y, 0, 0);
        }

        public static void PerformViewSelectionSource()
        {
            // Shift + F10
            keybd_event(VK_SHIFT, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(VK_F10, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(VK_F10, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);

            // A
            keybd_event(0x41, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(0x41, 0, KEYEVENTF_KEYUP, 0);
        }

        public void PerformViewSelectionSource(Point p)
        {
            // Right mouse click
            Cursor.Position = p;
            mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
            WaitMS(200);
 
            // A
            keybd_event(0x41, 0, KEYEVENTF_EXTENDEDKEY, 0);
            WaitMS(2);
            keybd_event(0x41, 0, KEYEVENTF_KEYUP, 0);
        }

        public static void PerformStrgC()
        {
            // Strg + C
            keybd_event(VK_LCONTROL, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(0x43, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(0x43, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCONTROL, 0, KEYEVENTF_KEYUP, 0);
        }

        public void PerformStrgC(Point p)
        {
            // Right mouse click
            Cursor.Position = p;
            mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
            WaitMS(200);

            // K
            keybd_event(0x4b, 0, KEYEVENTF_EXTENDEDKEY, 0);
            WaitMS(2);
            keybd_event(0x4b, 0, KEYEVENTF_KEYUP, 0);
        }
        
        public static void PerformStrgW() // Close Tab
        {
            // Strg + W
            keybd_event(VK_LCONTROL, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(0x57, 0, KEYEVENTF_EXTENDEDKEY, 0);
            keybd_event(0x57, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_LCONTROL, 0, KEYEVENTF_KEYUP, 0);
        }


        public void MouseMoved(object sender, MouseEventArgs e)
        {
        }

        public void MyKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData.ToString() == "Escape")
            {
                _abort = true;

                actHook.Stop();
            }
        }

        public void MyKeyPress(object sender, KeyPressEventArgs e)
        {
        }

        public void MyKeyUp(object sender, KeyEventArgs e)
        {

            //			Debug.AppendText("<<"+e.KeyData.ToString()+">>"+Environment.NewLine);

            if (e.KeyData.ToString() == "Escape")
            {
                _abort = true;

                actHook.Stop();
            }
        }

        #endregion

		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;
 
		public GoMapBot()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();
			//			this.SetStyle(ControlStyles.ResizeRedraw,true);

			actHook = new UserActivityHook(); // Crate an instance with global hooks
			// Hang on events
			actHook.OnMouseActivity+=new MouseEventHandler(MouseMoved);
			actHook.KeyDown+=new KeyEventHandler(MyKeyDown);
			actHook.KeyPress+=new KeyPressEventHandler(MyKeyPress);
			actHook.KeyUp+=new KeyEventHandler(MyKeyUp);
       }
 
		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}
 
    #region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.ButtonStart = new System.Windows.Forms.Button();
            this.Debug = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.buttonN = new System.Windows.Forms.Button();
            this.buttonE = new System.Windows.Forms.Button();
            this.buttonW = new System.Windows.Forms.Button();
            this.buttonS = new System.Windows.Forms.Button();
            this.GymParser = new System.Timers.Timer();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.checkBox_OutputTimestamp = new System.Windows.Forms.CheckBox();
            this.button_OutputFile = new System.Windows.Forms.Button();
            this.textBox_OutputFile = new System.Windows.Forms.TextBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.textBox_Path = new System.Windows.Forms.TextBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.button_ShowArea = new System.Windows.Forms.Button();
            this.numericUpDown4 = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown2 = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown3 = new System.Windows.Forms.NumericUpDown();
            this.numericUpDown1 = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.MoveMapTimer = new System.Timers.Timer();
            this.label3 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.GymParser)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown4)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown3)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.MoveMapTimer)).BeginInit();
            this.SuspendLayout();
            // 
            // ButtonStart
            // 
            this.ButtonStart.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.ButtonStart.Location = new System.Drawing.Point(12, 8);
            this.ButtonStart.Name = "ButtonStart";
            this.ButtonStart.Size = new System.Drawing.Size(48, 24);
            this.ButtonStart.TabIndex = 3;
            this.ButtonStart.Text = "Start";
            this.ButtonStart.Click += new System.EventHandler(this.ButtonStart_Click);
            // 
            // Debug
            // 
            this.Debug.AcceptsReturn = true;
            this.Debug.Location = new System.Drawing.Point(260, 8);
            this.Debug.Multiline = true;
            this.Debug.Name = "Debug";
            this.Debug.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.Debug.Size = new System.Drawing.Size(180, 301);
            this.Debug.TabIndex = 4;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(274, 122);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(64, 28);
            this.button1.TabIndex = 32;
            this.button1.Text = "Test";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Visible = false;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(344, 122);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(64, 28);
            this.button2.TabIndex = 33;
            this.button2.Text = "Test2";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Visible = false;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // buttonN
            // 
            this.buttonN.Location = new System.Drawing.Point(293, 21);
            this.buttonN.Name = "buttonN";
            this.buttonN.Size = new System.Drawing.Size(20, 20);
            this.buttonN.TabIndex = 34;
            this.buttonN.Text = "N";
            this.buttonN.UseVisualStyleBackColor = true;
            this.buttonN.Visible = false;
            this.buttonN.Click += new System.EventHandler(this.buttonN_Click);
            // 
            // buttonE
            // 
            this.buttonE.Location = new System.Drawing.Point(312, 44);
            this.buttonE.Name = "buttonE";
            this.buttonE.Size = new System.Drawing.Size(20, 20);
            this.buttonE.TabIndex = 35;
            this.buttonE.Text = "E";
            this.buttonE.UseVisualStyleBackColor = true;
            this.buttonE.Visible = false;
            this.buttonE.Click += new System.EventHandler(this.buttonE_Click);
            // 
            // buttonW
            // 
            this.buttonW.Location = new System.Drawing.Point(274, 44);
            this.buttonW.Name = "buttonW";
            this.buttonW.Size = new System.Drawing.Size(20, 20);
            this.buttonW.TabIndex = 36;
            this.buttonW.Text = "W";
            this.buttonW.UseVisualStyleBackColor = true;
            this.buttonW.Visible = false;
            this.buttonW.Click += new System.EventHandler(this.buttonW_Click);
            // 
            // buttonS
            // 
            this.buttonS.Location = new System.Drawing.Point(293, 67);
            this.buttonS.Name = "buttonS";
            this.buttonS.Size = new System.Drawing.Size(20, 20);
            this.buttonS.TabIndex = 37;
            this.buttonS.Text = "S";
            this.buttonS.UseVisualStyleBackColor = true;
            this.buttonS.Visible = false;
            this.buttonS.Click += new System.EventHandler(this.buttonS_Click);
            // 
            // GymParser
            // 
            this.GymParser.Interval = 1000D;
            this.GymParser.SynchronizingObject = this;
            this.GymParser.Elapsed += new System.Timers.ElapsedEventHandler(this.GymParser_Elapsed);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.checkBox_OutputTimestamp);
            this.groupBox1.Controls.Add(this.button_OutputFile);
            this.groupBox1.Controls.Add(this.textBox_OutputFile);
            this.groupBox1.Location = new System.Drawing.Point(12, 42);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(231, 74);
            this.groupBox1.TabIndex = 38;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Output file";
            // 
            // checkBox_OutputTimestamp
            // 
            this.checkBox_OutputTimestamp.AutoSize = true;
            this.checkBox_OutputTimestamp.Checked = true;
            this.checkBox_OutputTimestamp.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_OutputTimestamp.Location = new System.Drawing.Point(7, 47);
            this.checkBox_OutputTimestamp.Name = "checkBox_OutputTimestamp";
            this.checkBox_OutputTimestamp.Size = new System.Drawing.Size(185, 17);
            this.checkBox_OutputTimestamp.TabIndex = 2;
            this.checkBox_OutputTimestamp.Text = "Add timestamp to output file name";
            this.checkBox_OutputTimestamp.UseVisualStyleBackColor = true;
            this.checkBox_OutputTimestamp.KeyUp += new System.Windows.Forms.KeyEventHandler(this.HandleCheckboxInput);
            this.checkBox_OutputTimestamp.MouseUp += new System.Windows.Forms.MouseEventHandler(this.HandleCheckboxInput);
            // 
            // button_OutputFile
            // 
            this.button_OutputFile.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.button_OutputFile.Location = new System.Drawing.Point(187, 18);
            this.button_OutputFile.Name = "button_OutputFile";
            this.button_OutputFile.Size = new System.Drawing.Size(35, 20);
            this.button_OutputFile.TabIndex = 1;
            this.button_OutputFile.Text = "...";
            this.button_OutputFile.UseVisualStyleBackColor = true;
            this.button_OutputFile.Click += new System.EventHandler(this.button_OutputFile_Click);
            // 
            // textBox_OutputFile
            // 
            this.textBox_OutputFile.Enabled = false;
            this.textBox_OutputFile.Location = new System.Drawing.Point(6, 18);
            this.textBox_OutputFile.Name = "textBox_OutputFile";
            this.textBox_OutputFile.Size = new System.Drawing.Size(175, 20);
            this.textBox_OutputFile.TabIndex = 0;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.textBox_Path);
            this.groupBox2.Location = new System.Drawing.Point(12, 122);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(231, 77);
            this.groupBox2.TabIndex = 39;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Path";
            // 
            // textBox_Path
            // 
            this.textBox_Path.Location = new System.Drawing.Point(6, 18);
            this.textBox_Path.Multiline = true;
            this.textBox_Path.Name = "textBox_Path";
            this.textBox_Path.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBox_Path.Size = new System.Drawing.Size(216, 53);
            this.textBox_Path.TabIndex = 0;
            this.textBox_Path.KeyUp += new System.Windows.Forms.KeyEventHandler(this.textBox_Path_KeyUp);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.button_ShowArea);
            this.groupBox3.Controls.Add(this.numericUpDown4);
            this.groupBox3.Controls.Add(this.numericUpDown2);
            this.groupBox3.Controls.Add(this.numericUpDown3);
            this.groupBox3.Controls.Add(this.numericUpDown1);
            this.groupBox3.Controls.Add(this.label2);
            this.groupBox3.Controls.Add(this.label1);
            this.groupBox3.Location = new System.Drawing.Point(12, 205);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(231, 104);
            this.groupBox3.TabIndex = 40;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Working Area";
            // 
            // button_ShowArea
            // 
            this.button_ShowArea.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.button_ShowArea.Location = new System.Drawing.Point(9, 73);
            this.button_ShowArea.Name = "button_ShowArea";
            this.button_ShowArea.Size = new System.Drawing.Size(86, 20);
            this.button_ShowArea.TabIndex = 44;
            this.button_ShowArea.Text = "Show area";
            this.button_ShowArea.UseVisualStyleBackColor = true;
            this.button_ShowArea.Click += new System.EventHandler(this.button_ShowArea_Click);
            // 
            // numericUpDown4
            // 
            this.numericUpDown4.Location = new System.Drawing.Point(167, 40);
            this.numericUpDown4.Maximum = new decimal(new int[] {
            4320,
            0,
            0,
            0});
            this.numericUpDown4.Name = "numericUpDown4";
            this.numericUpDown4.Size = new System.Drawing.Size(55, 20);
            this.numericUpDown4.TabIndex = 43;
            this.numericUpDown4.KeyUp += new System.Windows.Forms.KeyEventHandler(this.HandleNumericUpDownInput);
            this.numericUpDown4.MouseUp += new System.Windows.Forms.MouseEventHandler(this.HandleNumericUpDownInput);
            // 
            // numericUpDown2
            // 
            this.numericUpDown2.Location = new System.Drawing.Point(167, 14);
            this.numericUpDown2.Maximum = new decimal(new int[] {
            4320,
            0,
            0,
            0});
            this.numericUpDown2.Name = "numericUpDown2";
            this.numericUpDown2.Size = new System.Drawing.Size(55, 20);
            this.numericUpDown2.TabIndex = 2;
            this.numericUpDown2.KeyUp += new System.Windows.Forms.KeyEventHandler(this.HandleNumericUpDownInput);
            this.numericUpDown2.MouseUp += new System.Windows.Forms.MouseEventHandler(this.HandleNumericUpDownInput);
            // 
            // numericUpDown3
            // 
            this.numericUpDown3.Location = new System.Drawing.Point(40, 41);
            this.numericUpDown3.Maximum = new decimal(new int[] {
            7680,
            0,
            0,
            0});
            this.numericUpDown3.Name = "numericUpDown3";
            this.numericUpDown3.Size = new System.Drawing.Size(55, 20);
            this.numericUpDown3.TabIndex = 42;
            this.numericUpDown3.KeyUp += new System.Windows.Forms.KeyEventHandler(this.HandleNumericUpDownInput);
            this.numericUpDown3.MouseUp += new System.Windows.Forms.MouseEventHandler(this.HandleNumericUpDownInput);
            // 
            // numericUpDown1
            // 
            this.numericUpDown1.Location = new System.Drawing.Point(91, 14);
            this.numericUpDown1.Maximum = new decimal(new int[] {
            7680,
            0,
            0,
            0});
            this.numericUpDown1.Name = "numericUpDown1";
            this.numericUpDown1.Size = new System.Drawing.Size(55, 20);
            this.numericUpDown1.TabIndex = 1;
            this.numericUpDown1.KeyUp += new System.Windows.Forms.KeyEventHandler(this.HandleNumericUpDownInput);
            this.numericUpDown1.MouseUp += new System.Windows.Forms.MouseEventHandler(this.HandleNumericUpDownInput);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 45);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(159, 13);
            this.label2.TabIndex = 41;
            this.label2.Text = "Width                               Height";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(158, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Starting point: X                       Y";
            // 
            // MoveMapTimer
            // 
            this.MoveMapTimer.Interval = 1000D;
            this.MoveMapTimer.SynchronizingObject = this;
            this.MoveMapTimer.Elapsed += new System.Timers.ElapsedEventHandler(this.MoveMapTimer_Elapsed);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(97, 14);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(137, 13);
            this.label3.TabIndex = 41;
            this.label3.Text = "Stop with Escape (Esc) key";
            // 
            // GoMapBot
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(448, 319);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.buttonS);
            this.Controls.Add(this.buttonW);
            this.Controls.Add(this.buttonE);
            this.Controls.Add(this.buttonN);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.Debug);
            this.Controls.Add(this.ButtonStart);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Name = "GoMapBot";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "GoMapBot by Celegast (v1.0)";
            this.Load += new System.EventHandler(this.GoMapBot_Load);
            ((System.ComponentModel.ISupportInitialize)(this.GymParser)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown4)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown2)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown3)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.MoveMapTimer)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

		}
    #endregion
 
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() 
		{
			Application.Run(new GoMapBot());
		}

        private void GoMapBot_Load(object sender, EventArgs e)
        {
            Stream stream;
            System.Reflection.Assembly assembly;

            assembly = System.Reflection.Assembly.LoadFrom(Application.ExecutablePath);

            stream = assembly.GetManifestResourceStream("GoMapBot.GoMapBot.ico");
            this.Icon = new Icon(stream);

            LoadConfiguration(CFGFILENAME);
        }

        /*
        // First version of the parsing script with processor intensive WaitMS function
        private void ButtonStart_Click(object sender, System.EventArgs e)
        {
            // Start hook and poll timer, which look for _abort command ('Escape')
            actHook.Start();
            _abort = false;

            Debug.ResetText();

            //PerformMouseClick(new Point(260, 50));
            //Thread.Sleep(2000);

            // File
            string file_name = @"E:\PGGA\graz_test.txt";
            CreateFile(file_name);

            // Path
            //List<string> path = new List<string> { "S" };
            //List<string> path = new List<string> { "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "E" };
            List<string> path = new List<string> { "E", "S", "W", "S", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "W", "S", "E", "E", "E", "E", "E", "S", "W", "W", "W", "S", "E", "E", "E", "S", "S", "W", "N", "W", "S", "W", "N", "W", "S", "W", "S", "E", "E", "E", "E", "E", "S", "W", "W", "W", "W", "S", "E", "E", "E", "E", "S", "W", "W", "W", "W", "S", "E", "E", "E", "E", "S", "W", "W", "W", "S", "E", "E", "E", "S", "W", "W", "W", "S", "E", "E", "E", "S", "W", "W", "S", "S", "W", "W", "S", "E", "E", "E", "S", "S" };
            int sector = 0;

            // Init reference bitmaps
            Bitmap greyX = new Bitmap("img/greyX.png");
            Bitmap lastUpdate = new Bitmap("img/lastUpdate.png");
            Bitmap tabCloseX = new Bitmap("img/tabCloseX.png");

            // Init desktop bitmap (screen size)
            Size screenSize = new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            Bitmap desktopBitmap = new Bitmap(screenSize.Width, screenSize.Height, PixelFormat.Format32bppArgb);

            // Link desktop bitmap with graphics
            Graphics desktopGraphics = Graphics.FromImage(desktopBitmap);

            foreach (string direction in path)
            {
                if (_abort)
                {
                    Debug.AppendText("Aborted.\r\n");
                    goto End;
                }

                // Check for valid direction
                if (!(direction == "N" || direction == "E" || direction == "S" || direction == "W"))
                    continue;
                sector++;

                //  Take screen shot
                desktopGraphics.CopyFromScreen(0, 0, 0, 0, screenSize, CopyPixelOperation.SourceCopy);
                List<Point> gyms = FindGyms(ref desktopBitmap, ref _startingPoint, ref _workingArea);

                foreach (Point p in gyms)
                {
                    if (_abort)
                    {
                        Debug.AppendText("Aborted.\r\n");
                        goto End;
                    }

                    int x_not_found_counter = 0;
                TryXAgain:

                    SetCursorPos(p.X, p.Y);
                    WaitMS(20);
                    PerformMouseClick(p);
                    WaitMS(200);

                    //  Take screen shot
                    desktopGraphics.CopyFromScreen(0, 0, 0, 0, screenSize, CopyPixelOperation.SourceCopy);
                    Point G = FindBitmap(greyX, ref desktopBitmap);

                    if (G.Equals(new Point(0, 0)))
                    {
                        Debug.AppendText("X not found\r\n");

                        if (++x_not_found_counter <= 1)
                        {
                            //SetCursorPos(p.X + x_not_found_counter, p.Y + x_not_found_counter);
                            //WaitMS(1000);
                            goto TryXAgain;
                        }

                        continue;
                        //goto End;
                    }
                    else
                    {
                        //Debug.AppendText(String.Format("greyX found @({0}, {1})\r\n", G.X, G.Y));
                        //SetCursorPos(G.X+5, G.Y+5);

                        Point LU = FindBitmap(lastUpdate, ref desktopBitmap);

                        if (LU.Equals(new Point(0, 0)))
                        {
                            Debug.AppendText("LU not found\r\n");
                            goto End;
                        }
                        else
                        {
                            //Debug.AppendText(String.Format("lastUpdate found @({0}, {1})\r\n", LU.X, LU.Y));
                            //SetCursorPos(LU.X + 5, LU.Y + 5);

                            // Select gym information
                            PerformMouseSelect(new Point(LU.X, G.Y), new Point(G.X + 5, LU.Y));
                            WaitMS(1000);

                            // Open selection source code in new tab
                            Point tmp = new Point((int)(G.X + LU.X) / 2, (int)(G.Y + LU.Y) / 2);
                            PerformViewSelectionSource(tmp);
                            WaitMS(1000);

                            // Copy selection source code to clipboard
                            PerformStrgC(new Point(300, 300));
                            WaitMS(200);

                            // Save clipboard to file
                            //Debug.AppendText(Clipboard.GetText());
                            AppendToFile(file_name, String.Format("{0}<td>{1}</td>", Clipboard.GetText(), sector)); // Add sector number to clipboard text
                            WaitMS(1000);

                            // Take screen shot
                            desktopGraphics.CopyFromScreen(0, 0, 0, 0, screenSize, CopyPixelOperation.SourceCopy);
                            Point tCX = FindBitmap(tabCloseX, ref desktopBitmap);

                            if (tCX.Equals(new Point(0, 0)))
                                Debug.AppendText("X not found\r\n");
                            else
                            {
                                // Close Tab
                                PerformMouseClick(tCX);
                            }

                            //PerformStrgW(); // Close Tab
                            //Thread.Sleep(200);

                            // Clear gym popup
                            PerformMouseClick(G);
                            WaitMS(200);
                        }
                    }
                }

                MoveMap(direction, _mapMovementPenalty);
                WaitMS(500);
            }

        End:
            return;
        }
        */

        private void ButtonStart_Click(object sender, System.EventArgs e)
        {
            // Start hook and parse timer
            _abort = false;
            _state = STATE.START;

            _startTime = DateTime.Now;

            actHook.Start();
            Debug.ResetText();

            GymParser.Interval = 1.0;
            GymParser.Enabled = true;
        }

        /// <summary>
        /// Timer based gym information parsing script:
        /// 1.) Find gmys (black dots) on the screen
        /// 2.) Click, mark and extract the HTML source code
        /// 3.) Write selected source code to file
        /// 4.) Repeat steps 2.-3. for every gym on the screen
        /// 5.) Move the map along the specified path
        /// </summary>
        private void GymParser_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Suspend timer
            GymParser.Enabled = false;

            if (_abort)
            {
                actHook.Stop();
                Debug.AppendText("Aborted.\r\n");
                _state = STATE.STOPPED;

                // Print elapsed time
                TimeSpan ts = DateTime.Now.Subtract(_startTime);
                Debug.AppendText(String.Format("Elapsed time: {0:hh\\:mm\\:ss}\r\n", ts));

                return;
            }

            switch (_state)
            {
                case STATE.START:
                    {
                        // Create save file
                        _output_file_name = textBox_OutputFile.Text;

                        if (_outputTimestamp)
                        {
                            string tmp = _output_file_name.Replace(".txt", "");
                            tmp += String.Format("{0:_yyyyMMdd_HHmmss}.txt", DateTime.Now);
                            _output_file_name = tmp;
                        }

                        CreateFile(_output_file_name);

                        // Init path
                        ParsePath(textBox_Path.Text);
                        _sector = 1;

                        // Init desktop bitmap
                        _desktopBitmap = new Bitmap(_screenSize.Width, _screenSize.Height, PixelFormat.Format32bppArgb);

                        // Link desktop bitmap with graphics
                        _desktopGraphics = Graphics.FromImage(_desktopBitmap);

                        GymParser.Interval = 1.0;
                        _state = STATE.FIND_GYMS;
                    }
                    break;
                case STATE.FIND_GYMS:
                    {
                        //  Take screen shot
                        _desktopGraphics.CopyFromScreen(0, 0, 0, 0, _screenSize, CopyPixelOperation.SourceCopy);
                        _gyms = FindGyms(ref _desktopBitmap, ref _startingPoint, ref _workingArea);

#if (SHOW_SECTOR_NUMBER)
    Debug.AppendText(String.Format("Sector {0}: {1}\r\n", _sector, _gyms.Count));
#endif

                        GymParser.Interval = 1.0;
                        _state = STATE.PARSE_GYMS;
                    }
                    break;
                case STATE.PARSE_GYMS:
                    {
                        if (_gyms.Count == 0)
                        {
                            GymParser.Interval = 1.0;
                            _state = STATE.MOVE_MAP;
                            break;
                        }

                        _x_not_found_counter = 0;

                        // Pop first gym point
                        _gym_point = _gyms[0];
                        _gyms.RemoveAt(0);

                        GymParser.Interval = 1.0;
                        _state = STATE.PARSE_GYMS_2;
                    }
                    break;
                case STATE.PARSE_GYMS_2:
                    {
                        SetCursorPos(_gym_point.X, _gym_point.Y);
                        GymParser.Interval = 20.0;
                        _state = STATE.PARSE_GYMS_3;
                    }
                    break;
                case STATE.PARSE_GYMS_3:
                    {
                        PerformMouseClick(_gym_point);
                        GymParser.Interval = _gymParserTiming[0];
                        _state = STATE.PARSE_GYMS_4;
                    }
                    break;
                case STATE.PARSE_GYMS_4:
                    {
                        //  Take screen shot
                        _desktopGraphics.CopyFromScreen(0, 0, 0, 0, _screenSize, CopyPixelOperation.SourceCopy);

                        // Calculate starting point for scan and check boundaries
                        Point starting_point = new Point(_gym_point.X - (int)(_popupSize.Width / 2), _gym_point.Y - _popupSize.Height);

                        if (starting_point.X < 0)
                        {
                            starting_point.X = 0;
                            Debug.AppendText("Invalid starting point (X). Check working area and popup size settings.\r\n");
                        }
                        if (starting_point.Y < 0)
                        {
                            starting_point.Y = 0;
                            Debug.AppendText("Invalid starting point (Y). Check working area and popup size settings.\r\n");
                        }

                        _greyX_point = FindBitmap(_greyX, ref _desktopBitmap, starting_point);

                        if (_greyX_point.Equals(new Point(0, 0)))
                        {
                            Debug.AppendText("X not found\r\n");

                            if (++_x_not_found_counter <= 1)
                            {
                                GymParser.Interval = 1.0;
                                _state = STATE.PARSE_GYMS_2;
                                break;
                            }

                            GymParser.Interval = 1.0;
                            _state = STATE.MOVE_MAP;
                            break;
                        }

                        GymParser.Interval = 1.0;
                        _state = STATE.PARSE_GYMS_5;
                    }
                    break;
                case STATE.PARSE_GYMS_5:
                    {
                        Point starting_point = new Point(_gym_point.X - (int)(_popupSize.Width / 2), _gym_point.Y - (int)(_popupSize.Height / 2));

                        if (starting_point.X < 0)
                        {
                            starting_point.X = 0;
                            Debug.AppendText("Invalid starting point (X). Check working area and popup size settings.\r\n");
                        }
                        if (starting_point.Y < 0)
                        {
                            starting_point.Y = 0;
                            Debug.AppendText("Invalid starting point (Y). Check working area and popup size settings.\r\n");
                        }

                        _lastUpdate_point = FindBitmap(_lastUpdate, ref _desktopBitmap, starting_point);

                        if (_lastUpdate_point.Equals(new Point(0, 0)))
                        {
                            Debug.AppendText("lastUpdate not found\r\n");

                            GymParser.Interval = 1.0;
                            _state = STATE.MOVE_MAP;
                            break;
                        }

                        GymParser.Interval = 1.0;
                        _state = STATE.SELECT_GYM_INFO;
                    }
                    break;
                case STATE.SELECT_GYM_INFO:
                    {
                        PerformMouseSelect(new Point(_lastUpdate_point.X, _greyX_point.Y), new Point(_greyX_point.X + 5, _lastUpdate_point.Y));
                        GymParser.Interval = _gymParserTiming[1];
                        _state = STATE.OPEN_SELECTION_SOURCE_CODE_IN_NEW_TAB;
                    }
                    break;
                case STATE.OPEN_SELECTION_SOURCE_CODE_IN_NEW_TAB:
                    {
                        Point tmp = new Point((int)(_greyX_point.X + _lastUpdate_point.X) / 2, (int)(_greyX_point.Y + _lastUpdate_point.Y) / 2);
                        PerformViewSelectionSource(tmp);

                        GymParser.Interval = _gymParserTiming[2];
                        _state = STATE.COPY_SELECTION_SOURCE_CODE_TO_CLIPBOARD;
                    }
                    break;
                case STATE.COPY_SELECTION_SOURCE_CODE_TO_CLIPBOARD:
                    {
                        PerformStrgC(new Point(300, 300));

                        GymParser.Interval = _gymParserTiming[3];
                        _state = STATE.SAVE_CLIPBOARD_TO_FILE;
                    }
                    break;
                case STATE.SAVE_CLIPBOARD_TO_FILE:
                    {
                        //Debug.AppendText(Clipboard.GetText());
                        AppendToFile(_output_file_name, String.Format("{0}<td>{1}</td>", Clipboard.GetText(), _sector)); // Add sector number to clipboard text

                        GymParser.Interval = _gymParserTiming[4];
                        _state = STATE.CLOSE_TAB;
                    }
                    break;
                case STATE.CLOSE_TAB:
                    {
                        // Take screen shot
                        _desktopGraphics.CopyFromScreen(0, 0, 0, 0, _screenSize, CopyPixelOperation.SourceCopy);
                        Point tCX = FindBitmap(_tabCloseX, ref _desktopBitmap);

                        if (tCX.Equals(new Point(0, 0)))
                            Debug.AppendText("tabCloseX not found\r\n");
                        else
                        {
                            // Close Tab
                            PerformMouseClick(tCX);
                        }

                        GymParser.Interval = _gymParserTiming[5];
                        _state = STATE.CLEAR_GYM_POPUP;
                    }
                    break;
                case STATE.CLEAR_GYM_POPUP:
                    {
                        PerformMouseClick(_greyX_point);

                        GymParser.Interval = _gymParserTiming[6];
                        _state = STATE.PARSE_GYMS;
                    }
                    break;
                case STATE.MOVE_MAP:
                    {
                        if (_path.Count == 0)
                        {
                            GymParser.Interval = 1.0;
                            _state = STATE.STOPPED;
                            break;
                        }

                        // Pop first path direction
                        _path_direction = _path[0];
                        _path.RemoveAt(0);

                        // Check for valid direction
                        if (!(_path_direction == "N" || _path_direction == "E" || _path_direction == "S" || _path_direction == "W"))
                        {
                            GymParser.Interval = 1.0;
                            //_state = STATE.MOVE_MAP;
                            break;
                        }

                        _sector++;

                        GymParser.Interval = MoveMap(_path_direction, _mapMovementPenalty);
                        _state = STATE.FIND_GYMS;
                    }
                    break;
                case STATE.STOPPED:
                    {
                        actHook.Stop();

                        // Print elapsed time
                        TimeSpan ts = DateTime.Now.Subtract(_startTime);
                        Debug.AppendText(String.Format("Elapsed time: {0:hh\\:mm\\:ss}\r\n", ts));

                        return;
                    }
            }

            // Start timer
            GymParser.Enabled = true;
        }



        private enum MOVEMAPSTATE { STOPPED, START, MOVE_MAP };
        private MOVEMAPSTATE _movemap_state = MOVEMAPSTATE.STOPPED;
        private List<Point> _contour = new List<Point>();

        /// <summary>
        /// Move map in direction N,E,S or W
        /// Returns: Duration in ms
        /// </summary>
        private int MoveMap(string direction, int mapMovementPenalty = 0)
        {
            Point a = new Point(0, 0);
            Point b = new Point(0, 0);
            int i = 0;
            int OFFSET = 5;

            Size wA = new Size(_workingArea.Width, _workingArea.Height);
            if (mapMovementPenalty > 0)
            {
                wA.Width -= mapMovementPenalty;
                wA.Height -= mapMovementPenalty;
            }

            //_contour = new List<Point>();
            _contour.Clear();

            switch (direction)
            {
                case "N":
                    a = new Point(_startingPoint.X + (int)(wA.Width / 2), _startingPoint.Y);
                    b = new Point(_startingPoint.X + (int)(wA.Width / 2), _startingPoint.Y + wA.Height);
                    _contour.Add(new Point(a.X, a.Y - OFFSET)); // Add dummy point
                    for (i = 0; i <= _movementSteps; i++) _contour.Add(new Point(a.X, a.Y + (int)(i * wA.Height / _movementSteps)));
                    break;
                case "E":
                    a = new Point(_startingPoint.X + wA.Width, _startingPoint.Y + (int)(wA.Height / 2));
                    b = new Point(_startingPoint.X, _startingPoint.Y + (int)(wA.Height / 2));
                    _contour.Add(new Point(a.X + OFFSET, a.Y)); // Add dummy point
                    for (i = 0; i <= _movementSteps; i++) _contour.Add(new Point(a.X - (int)(i * wA.Width / _movementSteps), a.Y));
                    break;
                case "S":
                    a = new Point(_startingPoint.X + (int)(wA.Width / 2), _startingPoint.Y + wA.Height);
                    b = new Point(_startingPoint.X + (int)(wA.Width / 2), _startingPoint.Y);
                    _contour.Add(new Point(a.X, a.Y + OFFSET)); // Add dummy point
                    for (i = 0; i <= _movementSteps; i++) _contour.Add(new Point(a.X, a.Y - (int)(i * wA.Height / _movementSteps)));
                    break;
                case "W":
                    a = new Point(_startingPoint.X, _startingPoint.Y + (int)(wA.Height / 2));
                    b = new Point(_startingPoint.X + wA.Width, _startingPoint.Y + (int)(wA.Height / 2));
                    _contour.Add(new Point(a.X - OFFSET, a.Y)); // Add dummy point
                    for (i = 0; i <= _movementSteps; i++) _contour.Add(new Point(a.X + (int)(i * wA.Width / _movementSteps), a.Y));
                    break;
            }

            Cursor.Position = a;

            // Use MoveMapTimer to perform the map movement
            _movemap_state = MOVEMAPSTATE.START;
            MoveMapTimer.Interval = _moveMapTiming[0];
            MoveMapTimer.Enabled = true;

            return _moveMapTiming[0] + _contour.Count * _moveMapTiming[1] + _moveMapTiming[2];
        }   

        private void MoveMapTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Suspend timer
            MoveMapTimer.Enabled = false;

            if (_abort)
            {
                actHook.Stop();
                Debug.AppendText("Aborted.\r\n");
                _state = STATE.STOPPED;
                _movemap_state = MOVEMAPSTATE.STOPPED;

                return;
            }

            switch (_movemap_state)
            {
                case MOVEMAPSTATE.START:
                    {
                        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
#if (DEBUG_ALL || DEBUG_MOVE_MAP)
    Debug.AppendText(String.Format("MoveMap - LEFTDOWN ({0}, {1})\r\n", a.X, a.Y));
#endif

                        MoveMapTimer.Interval = 1.0;
                        _movemap_state = MOVEMAPSTATE.MOVE_MAP;
                    }
                    break;
                case MOVEMAPSTATE.MOVE_MAP:
                    {
                        if (_contour.Count == 0)
                        {
                            MoveMapTimer.Interval = 1.0;
                            _movemap_state = MOVEMAPSTATE.STOPPED;
                            break;
                        }

                        // Pop first contour point
                        Point p = _contour[0];
                        _contour.RemoveAt(0);

                        Cursor.Position = p;
#if (DEBUG_ALL || DEBUG_MOVE_MAP)
    Debug.AppendText(String.Format("MoveMap - contour ({0}, {1})\r\n", p.X, p.Y));
#endif

                        MoveMapTimer.Interval = _moveMapTiming[1];
                        _movemap_state = MOVEMAPSTATE.MOVE_MAP;
                    }
                    break;
                case MOVEMAPSTATE.STOPPED:
                    {
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

                        //actHook.Stop();
                        return;
                    }
            }

            // Start timer
            MoveMapTimer.Enabled = true;
        }

        /*
        /// <summary>
        /// Move map in direction N,E,S or W
        /// </summary>
        private void MoveMap(string direction, int mapMovementPenalty = 0)
        {
            Point a = new Point(0, 0);
            Point b = new Point(0, 0);
            List<Point> contour = new List<Point>();
            int i = 0;
            int steps = 20;
            int OFFSET = 5;

            Size wA = new Size(_workingArea.Width, _workingArea.Height);
            if (mapMovementPenalty > 0)
            {
                wA.Width -= mapMovementPenalty;
                wA.Height -= mapMovementPenalty;
            }

            switch (direction)
            {
                case "N":
                    a = new Point(_startingPoint.X + (int)(wA.Width / 2), _startingPoint.Y);
                    b = new Point(_startingPoint.X + (int)(wA.Width / 2), _startingPoint.Y + wA.Height);
                    contour.Add(new Point(a.X, a.Y - OFFSET)); // Add dummy point
                    for (i = 0; i <= steps; i++) contour.Add(new Point(a.X, a.Y + (int)(i * wA.Height / steps)));
                    break;
                case "E":
                    a = new Point(_startingPoint.X + wA.Width, _startingPoint.Y + (int)(wA.Height / 2));
                    b = new Point(_startingPoint.X, _startingPoint.Y + (int)(wA.Height / 2));
                    contour.Add(new Point(a.X + OFFSET, a.Y)); // Add dummy point
                    for (i = 0; i <= steps; i++) contour.Add(new Point(a.X - (int)(i * wA.Width / steps), a.Y));
                    break;
                case "S":
                    a = new Point(_startingPoint.X + (int)(wA.Width / 2), _startingPoint.Y + wA.Height);
                    b = new Point(_startingPoint.X + (int)(wA.Width / 2), _startingPoint.Y);
                    contour.Add(new Point(a.X, a.Y + OFFSET)); // Add dummy point
                    for (i = 0; i <= steps; i++) contour.Add(new Point(a.X, a.Y - (int)(i * wA.Height / steps)));
                    break;
                case "W":
                    a = new Point(_startingPoint.X, _startingPoint.Y + (int)(wA.Height / 2));
                    b = new Point(_startingPoint.X + wA.Width, _startingPoint.Y + (int)(wA.Height / 2));
                    contour.Add(new Point(a.X - OFFSET, a.Y)); // Add dummy point
                    for (i = 0; i <= steps; i++) contour.Add(new Point(a.X + (int)(i * wA.Width / steps), a.Y));
                    break;
            }

            Cursor.Position = a;
            WaitMS(100);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
#if (DEBUG_ALL || DEBUG_MOVE_MAP)
    Debug.AppendText(String.Format("MoveMap - LEFTDOWN ({0}, {1})\r\n", a.X, a.Y));
#endif

            foreach (Point p in contour)
            {
                Cursor.Position = p;
#if (DEBUG_ALL || DEBUG_MOVE_MAP)
    Debug.AppendText(String.Format("MoveMap - contour ({0}, {1})\r\n", p.X, p.Y));
#endif
                WaitMS(100);
            }

            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            WaitMS(500);
        }   
        */

        /*
        /// <summary>
        /// This method checks whether Point A is in a forbidden area
        /// </summary>
        private bool ForbiddenArea(Point A, ref List<Point> pointList, ref Size areaSize)
        {
            foreach (Point p in pointList)
            {
                if (A.X >= p.X && A.X <= (p.X + areaSize.Width - 1) &&
                    A.Y >= p.Y && A.Y <= (p.Y + areaSize.Height - 1))
                    return true;
            }

            return false;
        }
        */

        /// <summary>
        /// This method checks whether Point A is in a forbidden area
        /// </summary>
        private bool ForbiddenArea(Point A, ref List<Point> pointList, int areaRadius)
        {
            foreach (Point p in pointList)
            {
                if (A.X >= (p.X - areaRadius) && A.X <= (p.X + areaRadius - 1) &&
                    A.Y >= (p.Y - areaRadius) && A.Y <= (p.Y + areaRadius - 1))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// This method returns a list of (screen-)coordinates of dark dots
        /// </summary>
        private List<Point> FindGyms(ref Bitmap desktopBitmap, ref Point startingPoint, ref Size workingArea)
        {
            List<Point> pointList = new List<Point>();
            Boolean isMatch = false;

            // Debug
#if (DEBUG_ALL || DEBUG_FIND_GYMS)
                Debug.AppendText(String.Format("dotSize=({0}, {1}); screenSize=({2}, {3})\r\n", dotSize.Width, dotSize.Height, screenSize.Width, screenSize.Height));
#endif

            // Scan desktop for dark dots
            for (int y = startingPoint.Y; y < (startingPoint.Y + workingArea.Height - _dotSize.Height); y++)
            {
                for (int x = startingPoint.X; x < (startingPoint.X + workingArea.Width - _dotSize.Width); x++)
                {
                    isMatch = true;

                    // Check whether pixels are dark in Y-direction
                    for (int j = 0; j < _dotSize.Height; j++)
                    {
                        if (!(desktopBitmap.GetPixel(x, y + j).GetBrightness() < _brightnessThreshold) ||
                            ForbiddenArea(new Point(x, y + j), ref pointList, _forbiddenAreaRadius))
                        {
                            isMatch = false;
                            break;
                        }
                        else
                        {
                            // Debug
#if (DEBUG_ALL || DEBUG_FIND_GYMS)
                                Color col = desktopBitmap.GetPixel(x, y + j);
                                Debug.AppendText(String.Format("screenColor@({0}, {1}) = [{2} {3} {4}]\r\n", x, y + j, col.R, col.G, col.B));
#endif
                        }

                        if (!isMatch) break; // speed up
                    }

                    // Check whether pixels are dark in X-direction
                    for (int i = 0; i < _dotSize.Width; i++)
                    {
                        if (!(desktopBitmap.GetPixel(x + i, y).GetBrightness() < _brightnessThreshold) ||
                            ForbiddenArea(new Point(x + i, y), ref pointList, _forbiddenAreaRadius))
                        {
                            isMatch = false;
                            break;
                        }
                        else
                        {
                            // Debug
#if (DEBUG_ALL || DEBUG_FIND_GYMS)
                                Color col = desktopBitmap.GetPixel(x + i, y);
                                Debug.AppendText(String.Format("screenColor@({0}, {1}) = [{2} {3} {4}]\r\n", x + i, y, col.R, col.G, col.B));
#endif
                        }

                        if (!isMatch) break; // speed up
                    }

                    if (isMatch)
                    {
                        // Debug
#if (DEBUG_ALL || DEBUG_FIND_GYMS)
                            Debug.AppendText(String.Format("Return ({0}, {1})\r\n", x, y));
#endif

                        pointList.Add(new Point(x, y));
                        //Debug.AppendText(String.Format("Gym found @ ({0}, {1})\r\n", x, y));
                    }
                }
            }

            return pointList;
        }

        /// <summary>
        /// This method searches on desktop for refBitmap, with optional starting point
        /// </summary>
        private Point FindBitmap(Bitmap refBitmap, ref Bitmap desktopBitmap, Point startingPoint = default(Point))
        {
            Boolean isMatch = false;

            // Debug
#if (DEBUG_ALL || DEBUG_FIND_BITMAP)
            Debug.AppendText(String.Format("refBitmapSize=({0}, {1}); desktopBitmapSize=({2}, {3}); startingPoint=({4}, {5})\r\n", refBitmap.Width, refBitmap.Height, desktopBitmap.Width, desktopBitmap.Height, startingPoint.X, startingPoint.Y));
#endif

            // Scan desktop for refBitmap
            for (int y = startingPoint.Y; y < (desktopBitmap.Height - refBitmap.Height); y++)
            {
                for (int x = startingPoint.X; x < (desktopBitmap.Width - refBitmap.Width); x++)
                {
                    isMatch = true;

                    // Check whether section matches reference
                    for (int j = 0; j < refBitmap.Height; j++)
                    {
                        for (int i = 0; i < refBitmap.Width; i++)
                        {
                            if (!refBitmap.GetPixel(i, j).Equals(desktopBitmap.GetPixel(x+i, y+j)))
                            {
                                isMatch = false;
                                break;
                            }
                            else
                            {
                                // Debug
#if (DEBUG_ALL || DEBUG_FIND_BITMAP)
                                Color col = desktopBitmap.GetPixel(x + i, y + j);
                                Color col_ref = refBitmap.GetPixel(i, j);
                                Debug.AppendText(String.Format("screenColor@({0}, {1}) = [{2} {3} {4}]\r\n", x + i, y + j, col.R, col.G, col.B));
                                Debug.AppendText(String.Format("refBitmapColor@({0}, {1}) = [{2} {3} {4}]\r\n",  i, j, col_ref.R, col_ref.G, col_ref.B));
#endif
                            }
                        }

                        if (!isMatch) break; // speed up
                    }

                    if (isMatch)
                    {
                        // Debug
#if (DEBUG_ALL || DEBUG_FIND_BITMAP)
                        Debug.AppendText(String.Format("Return ({0}, {1})\r\n", x, y));
#endif

                        return new Point(x, y);
                    }
                }
            }

            return new Point(0, 0);
        }

        #region Test
        private void button1_Click(object sender, EventArgs e)
        {
            // Init desktop bitmap (screen size)
            Size screenSize = new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            Bitmap desktopBitmap = new Bitmap(screenSize.Width, screenSize.Height, PixelFormat.Format32bppArgb);

            // Link desktop bitmap with graphics
            Graphics desktopGraphics = Graphics.FromImage(desktopBitmap);

            //  Take screen shot
            desktopGraphics.CopyFromScreen(0, 0, 0, 0, screenSize, CopyPixelOperation.SourceCopy);
            List<Point> gyms = FindGyms(ref desktopBitmap, ref _startingPoint, ref _workingArea);

            foreach (Point p in gyms)
            {
                SetCursorPos(p.X, p.Y);
                WaitMS(2000);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // Init desktop bitmap (screen size)
            Size screenSize = new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            Bitmap desktopBitmap = new Bitmap(screenSize.Width, screenSize.Height, PixelFormat.Format32bppArgb);

            // Link desktop bitmap with graphics
            Graphics desktopGraphics = Graphics.FromImage(desktopBitmap);

            // Path
            List<string> path = new List<string> { "S", "N" };

            foreach (string direction in path)
            {
                if (_abort)
                {
                    Debug.AppendText("Aborted.\r\n");
                    break;
                }

                //  Take screen shot
                desktopGraphics.CopyFromScreen(0, 0, 0, 0, screenSize, CopyPixelOperation.SourceCopy);
                List<Point> gyms = FindGyms(ref desktopBitmap, ref _startingPoint, ref _workingArea);

                foreach (Point p in gyms)
                {
                    SetCursorPos(p.X, p.Y);
                    WaitMS(2000);
                }

                MoveMap(direction, _mapMovementPenalty);
                WaitMS(500);
            }
        }

        private void buttonN_Click(object sender, EventArgs e)
        {
            MoveMap("N", _mapMovementPenalty);
        }

        private void buttonE_Click(object sender, EventArgs e)
        {
            MoveMap("E", _mapMovementPenalty);
        }

        private void buttonS_Click(object sender, EventArgs e)
        {
            MoveMap("S", _mapMovementPenalty);
        }

        private void buttonW_Click(object sender, EventArgs e)
        {
            MoveMap("W", _mapMovementPenalty);
        }
        #endregion

        #region Load/Save Configuration File
        private void LoadConfiguration(string file_name)
        {
            string s;
            string[] split_line; // = new string[2];

            //bool offsetTimeUpdated = false;

            try
            {
                // try reading 
                using (TextReader tr = new StreamReader(file_name))
                {
                    while ((s = tr.ReadLine()) != null)
                    {
                        split_line = s.Split(new Char[] { ':' });

                        switch (split_line[0])
                        {
                            case "OutputFileName":
                                _output_file_name = textBox_OutputFile.Text = (split_line.Length > 2) ? split_line[1] + ":" + split_line[2] : split_line[1];
                                break;
                            case "OutputTimestamp":
                                _outputTimestamp = checkBox_OutputTimestamp.Checked = (split_line[1] == "True") ? true : false;
                                break;
                            case "Path":
                                textBox_Path.Text = split_line[1];
                                break;
                            case "StartingPoint":
                                {
                                    String[] str = split_line[1].Split(new Char[] { ' ' });
                                    if (str.Length != 2) break;
                                    
                                    _startingPoint.X = Int32.Parse(str[0]);
                                    _startingPoint.Y = Int32.Parse(str[1]);

                                    numericUpDown1.Value = (decimal)_startingPoint.X;
                                    numericUpDown2.Value = (decimal)_startingPoint.Y;
                                }
                                break;
                            case "WorkingAreaSize":
                                {
                                    String[] str = split_line[1].Split(new Char[] { ' ' });
                                    if (str.Length != 2) break;
                                    _workingArea.Width = Int32.Parse(str[0]);
                                    _workingArea.Height = Int32.Parse(str[1]);

                                    numericUpDown3.Value = (decimal)_workingArea.Width;
                                    numericUpDown4.Value = (decimal)_workingArea.Height;
                                }
                                break;
                            
                            case "PopupSize":
                                {
                                    String[] str = split_line[1].Split(new Char[] { ' ' });
                                    if (str.Length != 2) break;
                                    _popupSize.Width = Int32.Parse(str[0]);
                                    _popupSize.Height = Int32.Parse(str[1]);
                                }
                                break;
                            case "MapMovementPenalty":
                                _mapMovementPenalty = Int32.Parse(split_line[1]);
                                break;
                            case "BrightnessThreshold":
                                _brightnessThreshold = (float)Double.Parse(split_line[1]);
                                break;
                            case "DotSize":
                                {
                                    String[] str = split_line[1].Split(new Char[] { ' ' });
                                    if (str.Length != 2) break;
                                    _dotSize.Width = Int32.Parse(str[0]);
                                    _dotSize.Height = Int32.Parse(str[1]);
                                }
                                break;
                            case "ForbiddenAreaRadius":
                                _forbiddenAreaRadius = Int32.Parse(split_line[1]);
                                break;
                            
                            case "WaitAfterGymClick":
                                _gymParserTiming[0] = Int32.Parse(split_line[1]);
                                break;
                            case "WaitAfterGymInfoSelection":
                                _gymParserTiming[1] = Int32.Parse(split_line[1]);
                                break;
                            case "WaitAfterGymInfoSelectionClick":
                                _gymParserTiming[2] = Int32.Parse(split_line[1]);
                                break;
                            case "WaitAfterSourceCodeCopy":
                                _gymParserTiming[3] = Int32.Parse(split_line[1]);
                                break;
                            case "WaitAfterSaveToFile":
                                _gymParserTiming[4] = Int32.Parse(split_line[1]);
                                break;
                            case "WaitAfterCloseTab":
                                _gymParserTiming[5] = Int32.Parse(split_line[1]);
                                break;
                            case "WaitAfterClearGymPopup":
                                _gymParserTiming[6] = Int32.Parse(split_line[1]);
                                break;

                            case "WaitAfterMapSetCursorPos":
                                _moveMapTiming[0] = Int32.Parse(split_line[1]);
                                break;
                            case "WaitAfterMapContourMovement":
                                _moveMapTiming[1] = Int32.Parse(split_line[1]);
                                break;
                            case "WaitAfterMapMovement":
                                _moveMapTiming[2] = Int32.Parse(split_line[1]);
                                break;
                            case "MapMovementSteps":
                                _movementSteps = Int32.Parse(split_line[1]);
                                break;
                        }
                    }
                }
            }
            catch (FileNotFoundException)
            {
                if (file_name == CFGFILENAME)
                {
                    // If default file does not exist: Create it
                    SaveConfiguration(CFGFILENAME);

                    LoadConfiguration(CFGFILENAME); // To fill control elements with data
                }
            }

        }

        private void SaveConfiguration(string file_name)
        {
            try
            {
                // Write configuration parameters to file
                TextWriter tw = new StreamWriter(file_name);
                
                // Values
                tw.WriteLine("OutputFileName:" + textBox_OutputFile.Text);
                tw.WriteLine("OutputTimestamp:" + _outputTimestamp);
                tw.WriteLine("Path:" + textBox_Path.Text);
                tw.WriteLine("StartingPoint:" + _startingPoint.X + " " + _startingPoint.Y);
                tw.WriteLine("WorkingAreaSize:" + _workingArea.Width + " " + _workingArea.Height);

                tw.WriteLine("PopupSize:" + _popupSize.Width + " " + _popupSize.Height);
                tw.WriteLine("MapMovementPenalty:" + _mapMovementPenalty);
                tw.WriteLine("BrightnessThreshold:" + _brightnessThreshold);
                tw.WriteLine("DotSize:" + _dotSize.Width + " " + _dotSize.Height);
                tw.WriteLine("ForbiddenAreaRadius:" + _forbiddenAreaRadius);

                tw.WriteLine("WaitAfterGymClick:" + _gymParserTiming[0]);
                tw.WriteLine("WaitAfterGymInfoSelection:" + _gymParserTiming[1]);
                tw.WriteLine("WaitAfterGymInfoSelectionClick:" + _gymParserTiming[2]);
                tw.WriteLine("WaitAfterSourceCodeCopy:" + _gymParserTiming[3]);
                tw.WriteLine("WaitAfterSaveToFile:" + _gymParserTiming[4]);
                tw.WriteLine("WaitAfterCloseTab:" + _gymParserTiming[5]);
                tw.WriteLine("WaitAfterClearGymPopup:" + _gymParserTiming[6]);

                tw.WriteLine("WaitAfterMapSetCursorPos:" + _moveMapTiming[0]);
                tw.WriteLine("WaitAfterMapContourMovement:" + _moveMapTiming[1]);
                tw.WriteLine("WaitAfterMapMovement:" + _moveMapTiming[2]);

                tw.WriteLine("MapMovementSteps:" + _movementSteps);

                tw.Close();
            }
            catch
            {
                // Usually should not end up here
            }
        }
        #endregion

        #region File functions
        private void AppendToFile(string file_name, string txt)
        {
            try
            {
                using (StreamWriter sw = File.AppendText(file_name))
                {
                    sw.WriteLine(txt);
                }
            }
            catch (Exception ex)
            {
                Debug.AppendText(ex.ToString());
            }
        }

        public void CreateFile(string file_name)
        {
            try
            {
                // Delete the file if it exists.
                if (File.Exists(file_name))
                {
                    // Note that no lock is put on the
                    // file and the possibility exists
                    // that another process could do
                    // something with it between
                    // the calls to Exists and Delete.
                    File.Delete(file_name);
                }

                // Create the file
                File.Create(file_name);
            }
            catch (Exception ex)
            {
                Debug.AppendText(ex.ToString());
            }
        }
        #endregion

        private void button_OutputFile_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();

            saveFileDialog1.Filter = "Text Files (*.txt)|*.txt";
            saveFileDialog1.FilterIndex = 2;
            //openFileDialog1.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\MyFolder";

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                _output_file_name = textBox_OutputFile.Text = saveFileDialog1.FileName;
                SaveConfiguration(CFGFILENAME);
            }
        }

        private void button_ShowArea_Click(object sender, EventArgs e)
        {
            Point A;

            A = _startingPoint;
            Cursor.Position = A;
            WaitMS(2000);

            A.X += _workingArea.Width;
            Cursor.Position = A;
            WaitMS(2000);

            A.Y += _workingArea.Height;
            Cursor.Position = A;
            WaitMS(2000);

            A.X -= _workingArea.Width;
            Cursor.Position = A;
            //WaitMS(2000);
        }

        private void textBox_Path_KeyUp(object sender, KeyEventArgs e)
        {
            SaveConfiguration(CFGFILENAME);
        }

        private void HandleNumericUpDownInput(object sender, MouseEventArgs e)
        {
            SaveNumericUpDownInput((NumericUpDown)sender);
        }

        private void HandleNumericUpDownInput(object sender, KeyEventArgs e)
        {
            SaveNumericUpDownInput((NumericUpDown)sender);
        }

        private void SaveNumericUpDownInput(NumericUpDown sender)
        {
            if (sender == numericUpDown1)
                _startingPoint.X = (int)sender.Value;
            else if (sender == numericUpDown2)
                _startingPoint.Y = (int)sender.Value;
            else if (sender == numericUpDown3)
                _workingArea.Width = (int)sender.Value;
            else if (sender == numericUpDown4)
                _workingArea.Height = (int)sender.Value;

            SaveConfiguration(CFGFILENAME);
        }

        private void HandleCheckboxInput(object sender, MouseEventArgs e)
        {
            SaveCheckboxInput((CheckBox)sender);
        }

        private void HandleCheckboxInput(object sender, KeyEventArgs e)
        {
            SaveCheckboxInput((CheckBox)sender);
        }

        private void SaveCheckboxInput(CheckBox sender)
        {
            if (sender == checkBox_OutputTimestamp)
                _outputTimestamp = sender.Checked;

            SaveConfiguration(CFGFILENAME);
        }

    }
}

