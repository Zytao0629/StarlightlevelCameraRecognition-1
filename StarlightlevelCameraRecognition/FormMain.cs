using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing; 
using System.Drawing.Imaging;
using System.IO.Ports;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DrawingPoint = System.Drawing.Point;


namespace StarlightlevelCameraRecognition
{
    public partial class FormMain : Form
    {
        Button btnOpenDevice;
        Button btnCloseDevice;
        Button btnSendCommand;
        Button btnSnapImage;
        Button btnOpenForeigndetection;
        Button btnCaluCRC;
        Button btnSavePaintApplyLayer;
        Button btnReloadLatestPaintLayer;
        Button btnClearCurrentPaintLayer;
        Button btnCloseForeigndetection;
        Button btnControlWaterpump;
        Button btnServoSteering;
        Button btnEstablishConnect;
        Button btnDisconnect;
        Button btnOpenCamera;
        Button btnCloseCamera;
        Button btnServoRotatingforward;
        Button btnServoRotatingBackforward;


        #region 全局变量
        private VideoCapture capture;  //相机对象，
        private bool isRunning = false; //相机是否打开
        private ModbusRTU modbuserialPortControl;

        private Bitmap backgroundImage;  // 存储背景图
        private Bitmap backgroundImageForUI; // 用于 PictureBox 显示底图
        private bool isBackgroundCaptured = false;  // 是否已捕获背景
        private bool isUpdatingBackground = false;
    

        private bool isDrawing = false;              // 是否正在绘制
        private List<LineSegment> lines = new();       // 所有已画的线
        private List<DrawingPoint> currentLinePoints = new(); // 当前鼠标拖动形成的线
        private int brushSize = 100;                  // 画笔大小
        private float overlapThreshold = 0.2f;       // 异物红框和涂抹区域重叠阈值
        private string savedLayerFile = @"D:\LineLayers"; // 涂抹层保存路径
        // 临时显示层
        private Bitmap drawingLayer;
        public class LineSegment
        {
            public DrawingPoint Start { get; set; }
            public DrawingPoint End { get; set; }
            public int Thickness { get; set; }
        }



        private List<(Rectangle rect, DateTime detectTime)> foreignObjects = new();
        private int highlightDuration = 5;  // 异物高亮时间
        private int diffThreshold = 35;
        private int regionMinSize = 30;
        private bool isDetecting = false; // 是否正在异物检测
        private bool isDetectionEnabled = true;  // 控制异物检测是否生效
        private DateTime lastBackgroundUpdateTime = DateTime.MinValue; // 上次更新底图时间


        private double frameWidth;
        private double frameHeight;


        private readonly object foreignObjectsLock = new object();
        private readonly object brushPointsLock = new object();
     

        private bool isPumpOn = false;
        bool isServoRunning = false;
        private readonly object logLock = new object();  // 写日志锁
        private volatile Bitmap latestFrameCache;
        private readonly object frameLock = new object();

        private readonly object paintLayerLock = new object();
        private Bitmap paintApplyLayer = null;


        System.Timers.Timer servoJogTimer;
        bool isServoJogging = false;
        float currentAngle = 135.0f;     // 当前角度（0–270）
        float jogStep = 0.5f;          // 每次点动的角度步进（1~3 比较合适）
        int jogInterval = 30;     // ms，点动频率


        #endregion



        public FormMain()
        {
            InitializeComponent();
            Init();
        }

        void Init()
        {
            btnOpenDevice = button1;
            btnCloseDevice = button2;
            btnSendCommand = button3;
            btnSnapImage = button4;
            btnOpenForeigndetection = button5;
            btnCaluCRC = button6;
            btnSavePaintApplyLayer = button8;
            btnReloadLatestPaintLayer = button7;
            btnClearCurrentPaintLayer = button9;
            btnCloseForeigndetection = button10;
            btnControlWaterpump = button11;
            btnServoSteering = button14;
            btnEstablishConnect = button12;
            btnDisconnect = button13;
            btnOpenCamera = button15;
            btnCloseCamera = button16;
            btnServoRotatingforward = button17;
            btnServoRotatingBackforward = button18;

            btnOpenDevice.Click += BtnOpenDevice_Click;
            btnCloseDevice.Click += BtnCloseDevice_Click;
            btnSendCommand.Click += BtnSendCommand_Click;
            btnSnapImage.Click += BtnSnapImage_Click;
            btnOpenForeigndetection.Click += BtnOpenForeigndetection_Click;
            btnCaluCRC.Click += BtnCaluCRC_Click;
            btnSavePaintApplyLayer.Click += BtnSavePaintApplyLayer_Click;
            btnReloadLatestPaintLayer.Click += BtnReloadLatestPaintLayer_Click;
            btnClearCurrentPaintLayer.Click += BtnClearCurrentPaintLayer_Click;
            btnCloseForeigndetection.Click += BtnCloseForeigndetection_Click;
            btnControlWaterpump.Click += BtnControlWaterpump_Click;
            btnServoSteering.Click += BtnServoSteering_Click;
            tabControl1.SelectedIndexChanged += TabControl1_SelectedIndexChanged;
            btnEstablishConnect.Click += BtnEstablishConnect_Click;
            btnDisconnect.Click += BtnDisconnect_Click;
            btnOpenCamera.Click += BtnOpenCamera_Click;
            btnCloseCamera.Click += BtnCloseCamera_Click;
            

            pictureBox1.MouseDown += PictureBox1_MouseDown;
            pictureBox1.MouseMove += PictureBox1_MouseMove;
            pictureBox1.MouseUp += PictureBox1_MouseUp;
            pictureBox1.Paint += PictureBox1_Paint;

            pictureBox3.MouseDown += PictureBox3_MouseDown;
            pictureBox3.MouseMove += PictureBox3_MouseMove;
            pictureBox3.MouseUp += PictureBox3_MouseUp;
            pictureBox3.Paint += PictureBox3_Paint;


            btnServoRotatingforward.MouseDown += BtnServoRotatingforward_MouseDown;
            btnServoRotatingforward.MouseUp += BtnServoRotatingforward_MouseUp;
            btnServoRotatingforward.MouseLeave += BtnServoRotatingforward_MouseLeave;

            btnServoRotatingBackforward.MouseDown += BtnServoRotatingBackforward_MouseDown;
            btnServoRotatingBackforward.MouseUp += BtnServoRotatingBackforward_MouseUp;
            btnServoRotatingBackforward.MouseLeave += BtnServoRotatingBackforward_MouseLeave;

            comboBoxPorts.Items.Clear();
            string[] ports = System.IO.Ports.SerialPort.GetPortNames();

            if (ports.Length > 0)
            {
                comboBoxPorts.Items.AddRange(ports);
                comboBoxPorts.SelectedIndex = 0; // 默认选中第一个
                comboBoxPorts.Enabled = true;
            }
            else
            {
                comboBoxPorts.Items.Add("无可用串口");
                comboBoxPorts.SelectedIndex = 0;
                comboBoxPorts.Enabled = false;
            }

            //button2.Enabled = false;
            //button3.Enabled = false;
            //button4.Enabled = false;
            button11.Text = !isPumpOn ? "打开水泵" : "关闭水泵";
            modbuserialPortControl = new ModbusRTU();
            modbuserialPortControl.SlaveAddress = 1;
            modbuserialPortControl.OnStatusMessage += ModbuserialPortControl_OnStatusMessage;//事件订阅
            LoadSavedLayer();
            UpdateCurrentAngle(currentAngle);

        }

        private void BtnServoRotatingBackforward_MouseLeave(object? sender, EventArgs e)
        {
            StopServoJog();
            btnServoRotatingBackforward.Text = "舵机反转";
            btnServoRotatingBackforward.BackColor = Color.White;
        }

        private void BtnServoRotatingBackforward_MouseUp(object? sender, MouseEventArgs e)
        {
            StopServoJog();
            btnServoRotatingBackforward.Text = "舵机反转";
            btnServoRotatingBackforward.BackColor = Color.White;
        }

        private void BtnServoRotatingBackforward_MouseDown(object? sender, MouseEventArgs e)
        {
            if (modbuserialPortControl.serialPort == null || !modbuserialPortControl.serialPort.IsOpen)
            {
                AppendLog($"✖ 串口未打开，无法控制舵机");
                return;
            }


            btnServoRotatingBackforward.Text = "舵机反转中";
            btnServoRotatingBackforward.BackColor = Color.Green;
            StartServoJog(-1);
        }

        private void BtnServoRotatingforward_MouseLeave(object? sender, EventArgs e)
        {
            StopServoJog();
            btnServoRotatingforward.Text = "舵机正转";
            btnServoRotatingforward.BackColor = Color.White;
        }

        private void BtnServoRotatingforward_MouseUp(object? sender, MouseEventArgs e)
        {
            StopServoJog();
            btnServoRotatingforward.Text = "舵机正转";
            btnServoRotatingforward.BackColor = Color.White;
        }

        private void BtnServoRotatingforward_MouseDown(object? sender, MouseEventArgs e)
        {
            if (modbuserialPortControl.serialPort == null || !modbuserialPortControl.serialPort.IsOpen)
            {
                AppendLog($"✖ 串口未打开，无法控制舵机");
                return;
            }

            btnServoRotatingforward.Text = "舵机正转中";
            btnServoRotatingforward.BackColor = Color.Green;
            StartServoJog(+1);
        }



        //事件订阅
        private void ModbuserialPortControl_OnStatusMessage(string message)
        {
            AppendLog($"{message}");
        }

        private void AppendLog(string message)
        {

            string logText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";

            if (richTextBox1.InvokeRequired)
            {
                richTextBox1.Invoke(new Action(() =>
                {
                    richTextBox1.AppendText(logText + "\r\n");
                    richTextBox1.ScrollToCaret();
                }));
            }
            else
            {
                richTextBox1.AppendText(logText + "\r\n");
                richTextBox1.ScrollToCaret();
            }


            try
            {
                lock (logLock)
                {
                    string dir = @"D:\Log\Recognition";
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    // 日志文件名
                    string filePath = Path.Combine(dir,
                        $"{DateTime.Now:yyyy-MM-dd-HH}.log");

                    File.AppendAllText(filePath, logText + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                richTextBox1.AppendText($"[LogError] {ex.Message}\r\n");
            }
        }








        #region blob的寻找监测高亮
        //寻找blob
        private List<Rectangle> FindAllDifferenceRegions(Bitmap background, Bitmap current)
        {
            if (background == null || current == null || background.Size != current.Size)
                return new List<Rectangle>();

            List<Rectangle> regions = new List<Rectangle>();
            int width = background.Width;
            int height = background.Height;

            bool[,] differenceMask = new bool[width, height];

            
            BitmapData bgData = null;
            BitmapData currData = null;
            byte[] bgBuffer;
            byte[] currBuffer;

            try
            {
                bgData = background.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                currData = current.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

                int stride = bgData.Stride;
                int bytes = stride * height;
                bgBuffer = new byte[bytes];
                currBuffer = new byte[bytes];

                System.Runtime.InteropServices.Marshal.Copy(bgData.Scan0, bgBuffer, 0, bytes);
                System.Runtime.InteropServices.Marshal.Copy(currData.Scan0, currBuffer, 0, bytes);

                // 遍历每个像素计算亮度差异
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * stride + x * 3;
                        int bgBrightness = (bgBuffer[index] + bgBuffer[index + 1] + bgBuffer[index + 2]) / 3;
                        int currBrightness = (currBuffer[index] + currBuffer[index + 1] + currBuffer[index + 2]) / 3;

                        differenceMask[x, y] = Math.Abs(currBrightness - bgBrightness) > diffThreshold;
                    }
                }
            }
            finally
            {
                if (background != null) background.UnlockBits(bgData);
                if (current != null) current.UnlockBits(currData);
            }

            // 使用 BFS 找连通差异区域
            bool[,] visited = new bool[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (differenceMask[x, y] && !visited[x, y])
                    {
                        int minX = x, minY = y, maxX = x, maxY = y;
                        Queue<(int, int)> queue = new Queue<(int, int)>();
                        queue.Enqueue((x, y));
                        visited[x, y] = true;

                        while (queue.Count > 0)
                        {
                            var (cx, cy) = queue.Dequeue();
                            minX = Math.Min(minX, cx);
                            minY = Math.Min(minY, cy);
                            maxX = Math.Max(maxX, cx);
                            maxY = Math.Max(maxY, cy);

                            foreach (var (dx, dy) in new List<(int, int)> { (-1, 0), (1, 0), (0, -1), (0, 1) })
                            {
                                int nx = cx + dx;
                                int ny = cy + dy;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                                    differenceMask[nx, ny] && !visited[nx, ny])
                                {
                                    visited[nx, ny] = true;
                                    queue.Enqueue((nx, ny));
                                }
                            }
                        }

                        int regionWidth = maxX - minX + 1;
                        int regionHeight = maxY - minY + 1;

                        if (regionWidth > regionMinSize && regionHeight > regionMinSize)
                            regions.Add(new Rectangle(minX, minY, regionWidth, regionHeight));
                    }
                }
            }

            return regions;
        }
        //检测并跟踪移动blob
        private void DetectAndTrackForeignObject(Bitmap currentFrame)
        {
            if (currentFrame == null) return;

            Bitmap frameCopyForDetection = null;

            // Clone 当前帧，确保线程独立
            lock (frameLock)
            {
                frameCopyForDetection = (Bitmap)currentFrame.Clone();
            }

            Bitmap layerCopy = null;
            lock (paintLayerLock)
            {
                if (paintApplyLayer != null)
                    layerCopy = (Bitmap)paintApplyLayer.Clone();
            }

            if (!isBackgroundCaptured || backgroundImage == null)
            {
                AppendLog($"✖ 背景图未捕获 ");
                frameCopyForDetection.Dispose();
                return;
            }

            Bitmap bgCopy;
            lock (foreignObjectsLock)
            {
                if (backgroundImage == null)
                    return;

                // 克隆独立副本
                bgCopy = (Bitmap)backgroundImage.Clone();
            }

            try
            {
                List<Rectangle> newRegions = FindAllDifferenceRegions(bgCopy, frameCopyForDetection);

                lock (foreignObjectsLock)
                {
                    foreignObjects = newRegions.Select(r => (rect: r, detectTime: DateTime.Now)).ToList();
                }

                if (newRegions.Count > 0)
                {
                    var obj = newRegions[0]; // 取第一个异物
                    float overlapRatio;

                    lock (brushPointsLock)
                    {
                        overlapRatio = CalculateOverlap(obj);
                    }

                    // 日志显示当前占比
                    this.Invoke(() => AppendLog(
                        $"🕷 异物占比: {overlapRatio:P1}，阈值: {overlapThreshold:P1}"));

                    if (overlapRatio >= overlapThreshold)
                    {
                        if (!isPumpOn)
                        {
                            byte[] pumpOn = modbuserialPortControl.BuildWriteSingleCommand(0x05, 0x0000, 0xFF00);
                            modbuserialPortControl.serialPort.Write(pumpOn, 0, pumpOn.Length);
                            isPumpOn = true;
                            AppendLog($"💧 水泵已打开 ");
                        }

                        if (!isServoRunning)
                        {
                            isServoRunning = true;
                            Bitmap servoFrame = (Bitmap)frameCopyForDetection.Clone();
                            _ = RotateServoAsync(servoFrame);
                        }
                    }
                    else
                    {
                        // 支线  异物消失，关闭水泵，更新底图
                        if (isPumpOn)
                        {
                            byte[] pumpOff = modbuserialPortControl.BuildWriteSingleCommand(0x05, 0x0000, 0x0000);
                            modbuserialPortControl.serialPort.Write(pumpOff, 0, pumpOff.Length);
                            isPumpOn = false;
                            AppendLog($"💧 水泵已关闭 ");
                            UpdateBackgroundImage(frameCopyForDetection);

                        }
                    }

                    // 定时更新底图
                    if (!isUpdatingBackground && !isServoRunning)
                    {
                        double intervalSeconds = (double)numericUpDownCleanTime.Value;
                        if (overlapRatio < overlapThreshold &&
                            (DateTime.Now - lastBackgroundUpdateTime).TotalSeconds >= intervalSeconds)
                        {
                            UpdateBackgroundImage(frameCopyForDetection);
                        }
                    }

                    // 保存异物画面
                    string resultFolder = @"D:\DetectionResultScreen";
                    Directory.CreateDirectory(resultFolder);
                    string resultPath = Path.Combine(resultFolder, $"异物画面_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    frameCopyForDetection.Save(resultPath, ImageFormat.Png);
                }
                else
                {
                    // 主线 无异物时，定时更新底图
                    double intervalSeconds = (double)numericUpDownCleanTime.Value;
                    if ((DateTime.Now - lastBackgroundUpdateTime).TotalSeconds >= intervalSeconds)
                    {
                        UpdateBackgroundImage(frameCopyForDetection);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($" DetectAndTrackForeignObject 异常: {ex.Message}");
            }
            finally
            {
                frameCopyForDetection.Dispose();
                bgCopy.Dispose();
                currentFrame.Dispose();
            }
        }
        // 绘制异物高亮标记
        private void DrawForeignObjectHighlight(Graphics g)
        {
            List<(Rectangle rect, DateTime detectTime)> activeObjects;

            // 线程安全拷贝
            lock (foreignObjectsLock)
            {
                activeObjects = foreignObjects
                    .Where(obj => (DateTime.Now - obj.detectTime).TotalSeconds < highlightDuration)
                    .ToList();
            }

            foreach (var obj in activeObjects)
            {
                // 红色边框
                using (Pen pen = new Pen(Color.Red, 3))
                {
                    g.DrawRectangle(pen, obj.rect);
                }

                // 半透明红色覆盖层
                using (Brush brush = new SolidBrush(Color.FromArgb(50, 255, 0, 0)))
                {
                    g.FillRectangle(brush, obj.rect);
                }

                // 剩余高亮时间
                double remainingSeconds = highlightDuration - (DateTime.Now - obj.detectTime).TotalSeconds;
                g.DrawString($"检测到异物,高亮时间剩余: {Math.Ceiling(remainingSeconds)}s",
                             SystemFonts.DefaultFont, Brushes.Red,
                             obj.rect.X, obj.rect.Y - 20);
            }

            // 更新异物列表
            lock (foreignObjectsLock)
            {
                foreignObjects = activeObjects;
            }
        }
        //计算占比
        private float CalculateOverlap(Rectangle foreignObject)
        {
            if (drawingLayer == null) return 0;

            int overlapArea = 0;
            int totalArea = foreignObject.Width * foreignObject.Height;

            // 确保不越界
            Rectangle intersect = Rectangle.Intersect(foreignObject, new Rectangle(0, 0, drawingLayer.Width, drawingLayer.Height));
            if (intersect.IsEmpty) return 0;

            for (int y = intersect.Top; y < intersect.Bottom; y++)
            {
                for (int x = intersect.Left; x < intersect.Right; x++)
                {
                    Color pixel = drawingLayer.GetPixel(x, y);
                    if (pixel.A > 0) // 非透明像素表示涂抹
                        overlapArea++;
                }
            }

            return (float)overlapArea / totalArea;
        }
        //实时更新底图
        private void UpdateBackgroundImage(Bitmap currentFrame)
        {
            if (currentFrame == null || isUpdatingBackground) return;
            isUpdatingBackground = true;

            Bitmap newBg = (Bitmap)currentFrame.Clone();

            lock (foreignObjectsLock)
            {
                var oldBg = backgroundImage;
                backgroundImage = newBg; // 检测线程使用
                oldBg?.Dispose();
            }

            // UI 使用单独副本
            if (pictureBox2.InvokeRequired)
            {
                pictureBox2.Invoke(new Action(() =>
                {
                    backgroundImageForUI?.Dispose();
                    backgroundImageForUI = (Bitmap)newBg.Clone();
                    pictureBox2.Image = backgroundImageForUI;
                }));
            }
            else
            {
                backgroundImageForUI?.Dispose();
                backgroundImageForUI = (Bitmap)newBg.Clone();
                pictureBox2.Image = backgroundImageForUI;
            }

            lastBackgroundUpdateTime = DateTime.Now;
            isBackgroundCaptured = true;
            isUpdatingBackground = false;


            // 保存底图
            try
            {
                if (backgroundImage == null) return;  

                string dir = @"D:\BackgroundDebugGet";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, $"底图_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                // 使用新的 Bitmap 实例保存，防止占用冲突
                using (Bitmap saveBmp = (Bitmap)backgroundImage.Clone())
                {
                    saveBmp.Save(path, ImageFormat.Png);
                }

                AppendLog($"🔁 底图已保存 ");
            }
            catch (Exception ex)
            {
                AppendLog($"保存背景图出错：{ex.Message}");
            }

        }


        #endregion


        #region 按钮事件
        private void BtnOpenDevice_Click(object? sender, EventArgs e)
        {
            if (isRunning) return;

            isRunning = false;
            Thread.Sleep(120); // 等待线程退出

            if (capture != null)
            {
                try
                {
                    capture.Release();
                    capture.Dispose();
                }
                catch { }
                finally
                {
                    capture = null;
                }
            }

            capture = new VideoCapture(1);
            if (!capture.IsOpened())
            {
                AppendLog("无法打开相机！");
                return;
            }

            frameWidth = capture.Get(VideoCaptureProperties.FrameWidth);
            frameHeight = capture.Get(VideoCaptureProperties.FrameHeight);
            double fps = capture.Get(VideoCaptureProperties.Fps);

            toolStripStatusLabel1.Text = "FPS" + $": {fps:F1}";
            toolStripStatusLabel2.Text = "Width" + $": {frameWidth}";
            toolStripStatusLabel3.Text = "Height" + $": {frameHeight}";
            toolStripStatusLabel1.ForeColor = Color.DarkOrange;
            toolStripStatusLabel2.ForeColor = Color.DarkOrange;
            toolStripStatusLabel3.ForeColor = Color.DarkOrange;

            isRunning = true;

            Task.Run(() =>
            {
                while (isRunning && capture != null && capture.IsOpened())
                {
                    using var frame = new Mat();
                    if (!capture.Read(frame) || frame.Empty())
                        continue;

                    // 从摄像头获取 Bitmap
                    using var bmp = BitmapConverter.ToBitmap(frame);

                    // 同步更新 PictureBox1 PictureBox3
                    UpdatePictureBoxes(bmp);

                    // 独立副本
                    Bitmap pbImage = (Bitmap)bmp.Clone();
                    pictureBox1.Invoke(() =>
                    {
                        var old = pictureBox1.Image;
                        pictureBox1.Image = pbImage;
                        old?.Dispose();
                    });

                    //独立副本
                    Bitmap cacheCopy = (Bitmap)bmp.Clone();
                    lock (frameLock)
                    {
                        var oldCache = latestFrameCache;
                        latestFrameCache = cacheCopy;
                        oldCache?.Dispose();
                    }

                    Thread.Sleep(30);
                }
            });
            AppendLog($"📷 相机已打开");

            LoadSavedLayer();

            // 通讯初始化
            string selectedPort = comboBoxPorts.SelectedItem.ToString();
            bool ok = modbuserialPortControl.OpenPort(selectedPort);
            if (ok)
            {
                toolStripStatusLabel4.Text = "通讯已建立✔";
                toolStripStatusLabel4.ForeColor = Color.DarkOrange;
                modbuserialPortControl.Initial();
            }
            else
            {
                toolStripStatusLabel4.Text = "通讯建立失败✖";
                toolStripStatusLabel4.ForeColor = Color.Red;
                modbuserialPortControl.Initial();
            }

            //btnCloseDevice.Enabled = isRunning;
            //btnSnapImage.Enabled = isRunning;
            //btnSendCommand.Enabled = isRunning;
            //btnOpenForeigndetection.Enabled = isRunning;
        }
        private void BtnCloseDevice_Click(object? sender, EventArgs e)
        {
            if (!isRunning) return;

            isRunning = false;

           

            // 释放摄像头
            if (capture != null)
            {
                try
                {
                    capture.Release();
                    capture.Dispose();
                }
                catch { }
                finally
                {
                    capture = null;
                }
            }

            // 清空 PictureBox1 显示
            pictureBox1.Invoke(() =>
            {
                var oldImage = pictureBox1.Image;
                pictureBox1.Image = null;
                oldImage?.Dispose();
            });



            // 清空底图 PictureBox
            if (pictureBox2 != null)
            {
                pictureBox2.Invoke(() =>
                {
                    var oldBgImage = pictureBox2.Image;
                    pictureBox2.Image = null;
                    oldBgImage?.Dispose();
                });
            }

            // 清空 PictureBox3 显示
            pictureBox3.Invoke(() =>
            {
                var oldImage = pictureBox3.Image;
                pictureBox3.Image = null;
                oldImage?.Dispose();
            });


            // 清零 StatusStrip
            toolStripStatusLabel1.Text = "";
            toolStripStatusLabel2.Text = "";
            toolStripStatusLabel3.Text = "";
            AppendLog($"📷 相机已关闭");

            // 关闭通讯
            modbuserialPortControl.ClosePort();
            toolStripStatusLabel4.Text = "通讯已断开✖";
            toolStripStatusLabel4.ForeColor = Color.Red;

            // 停止检测标志
            isDetecting = false;

            //// 禁用按钮
            //btnSnapImage.Enabled = isDetecting;
            //btnSendCommand.Enabled = isDetecting;
            //btnOpenForeigndetection.Enabled = isDetecting;

            // 清理缓存
            lock (frameLock)
            {
                latestFrameCache?.Dispose();
                latestFrameCache = null;
            }


        }
        private void BtnSendCommand_Click(object? sender, EventArgs e)
        {
            try
            {
                string input = textBox1.Text.Trim();
                if (string.IsNullOrWhiteSpace(input))
                {
                    AppendLog($" ✖ 命令不能为空");
                    return;
                }

                if (!modbuserialPortControl.serialPort.IsOpen)
                {
                    AppendLog($"✖ 串口未打开");
                    return;
                }

                // 清空接收缓冲区
                modbuserialPortControl.serialPort.DiscardInBuffer();

                // 处理格式化命令 C/R/I
                if (input.Contains(','))
                {
                    string[] parts = input.Split(',');
                    if (parts.Length != 3)
                    {
                        AppendLog($" ✖ 指令格式错误");
                        return;
                    }

                    string type = parts[0].Trim().ToUpper();
                    ushort address = ushort.Parse(parts[1].Trim());
                    string param = parts[2].Trim().ToUpper();

                    // 线圈控制
                    if (type == "C")
                    {
                        if (param == "ON" || param == "OFF")
                        {
                            bool success = modbuserialPortControl.WriteCoil(address, param == "ON");
                            AppendLog($" →发送线圈命令：{input}");
                            Thread.Sleep(50);
                            modbuserialPortControl.ReadCoils(address, 1);
                        }
                        else if (ushort.TryParse(param, out ushort count))
                        {
                            modbuserialPortControl.ReadCoils(address, count);
                            AppendLog($"→ 发送读线圈命令：{input}");
                        }
                        else
                        {
                            AppendLog($" ✖ 参数错误");
                        }
                    }
                    // 保持寄存器
                    else if (type == "R")
                    {
                        if (ushort.TryParse(param, out ushort value))
                        {
                            bool success = modbuserialPortControl.WriteHoldingRegister(address, value);
                            AppendLog($" → 发送写寄存器命令：{input}");
                            Thread.Sleep(50);
                            modbuserialPortControl.ReadHoldingRegisters(address, 1);
                        }
                        else if (ushort.TryParse(param, out ushort count))
                        {
                            modbuserialPortControl.ReadHoldingRegisters(address, count);
                            AppendLog($"→ 发送读寄存器命令：{input}");
                        }
                        else
                        {
                            AppendLog($" ✖ 参数错误");
                        }
                    }
                    // 输入寄存器
                    else if (type == "I")
                    {
                        if (ushort.TryParse(param, out ushort count))
                        {
                            modbuserialPortControl.ReadInputRegisters(address, count);
                            AppendLog($" → 发送读输入寄存器命令：{input}");
                        }
                        else
                        {
                            AppendLog($"✖ 参数错误");
                        }
                    }
                    else
                    {
                        AppendLog($" ✖ 未知指令类型");
                    }
                }
                else
                {
                    // 原始十六进制命令
                    string hex = input.Replace(" ", "");
                    if (hex.Length % 2 != 0)
                    {
                        AppendLog($" ✖ 十六进制长度必须为偶数");
                        return;
                    }

                    byte[] command = new byte[hex.Length / 2];
                    for (int i = 0; i < command.Length; i++)
                    {
                        command[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                    }

                    modbuserialPortControl.serialPort.Write(command, 0, command.Length);
                    AppendLog($"→ 发送原始命令：{BitConverter.ToString(command).Replace("-", " ")}");

                    // 等待响应
                    Thread.Sleep(100);
                    int bytesToRead = modbuserialPortControl.serialPort.BytesToRead;
                    if (bytesToRead > 0)
                    {
                        byte[] response = new byte[bytesToRead];
                        modbuserialPortControl.serialPort.Read(response, 0, bytesToRead);
                        AppendLog($"← 收到响应：{BitConverter.ToString(response).Replace("-", " ")}");
                    }
                    else
                    {
                        AppendLog($"✖ 未收到响应");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"✖ 执行异常：{ex.Message}");
            }
        }
        private void BtnSnapImage_Click(object? sender, EventArgs e)
        {
            if (capture == null || !capture.IsOpened() || !isRunning)
            {
                AppendLog("摄像头未打开或没有图像可捕获！");
                return;
            }

            Bitmap snapCopy = null;

            // 获取最新帧
            lock (frameLock)
            {
                if (latestFrameCache != null)
                    snapCopy = (Bitmap)latestFrameCache.Clone();
            }

            if (snapCopy == null)
            {
                AppendLog("当前没有缓存帧，无法保存！");
                return;
            }

            try
            {
                // 设置保存文件夹
                string SnapFilePath = @"D:\SnapImage";
                if (!Directory.Exists(SnapFilePath))
                    Directory.CreateDirectory(SnapFilePath);

                // 保存 snapCopy
                string fileName = Path.Combine(SnapFilePath, $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                snapCopy.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);
                AppendLog($"当前帧已保存为 {fileName}");
            }
            catch (Exception ex)
            {
                AppendLog($"保存快照失败: {ex.Message}");
            }
        }
        private void BtnOpenForeigndetection_Click(object? sender, EventArgs e)
        {
            if (latestFrameCache == null)
            {
                AppendLog($"✖ 暂无画面缓存，无法捕获背景");
                return;
            }

            // 捕获初始背景
            Bitmap bgCopy = null;
            lock (frameLock)
            {
                bgCopy = (Bitmap)latestFrameCache.Clone();
            }

            lock (foreignObjectsLock)
            {
                backgroundImage?.Dispose();
                backgroundImage = bgCopy;
                isBackgroundCaptured = true;
                lastBackgroundUpdateTime = DateTime.Now;
            }

            string folderPath = @"D:\BackgroundIntialImage";
            Directory.CreateDirectory(folderPath);
            string filePath = Path.Combine(folderPath, $"{DateTime.Now:yyyyMMdd_HHmmss}_初始背景图.png");
            backgroundImage.Save(filePath);

            AppendLog($"✔ 已捕获初始背景图，开启异物检测");

            if (textBoxStartAngle.Text.Trim() == "" || textBoxEndAngle.Text.Trim() == "")
            {
                AppendLog($"✖ 注意：舵机运动角度未填入,在参数设定页面中填入");
            }

            isDetectionEnabled = true;

            if (!isDetecting)
            {
                isDetecting = true;

                Task.Run(() =>
                {
                    while (isRunning && isDetecting)
                    {
                        Bitmap currentFrame = null;

                        // 获取最新帧
                        lock (frameLock)
                        {
                            if (latestFrameCache != null)
                                currentFrame = (Bitmap)latestFrameCache.Clone();
                        }

                        if (currentFrame != null)
                        {
                            if (isDetectionEnabled)
                                DetectAndTrackForeignObject(currentFrame);

                            currentFrame.Dispose();
                        }

                        Thread.Sleep(30);
                    }
                });
            }

            //btnOpenForeigndetection.Enabled = !isDetecting;
            //btnControlWaterpump.Enabled = !isDetecting;
            //btnServoSteering.Enabled = !isDetecting;
        }
        private void BtnCloseForeigndetection_Click(object? sender, EventArgs e)
        {
            if (!isDetecting)
            {
                AppendLog($"✖ 异物检测为关闭状态");
                return;
            }

            // 停止检测循环
            isDetecting = false;
            isDetectionEnabled = false;

            AppendLog($"✔ 异物检测已关闭");

            //btnOpenForeigndetection.Enabled = !isDetecting;
            //btnControlWaterpump.Enabled = !isDetecting;
            //btnServoSteering.Enabled = !isDetecting;
        }
        private void BtnCaluCRC_Click(object? sender, EventArgs e)
        {
            try
            {
                string input = textBox2.Text.Trim(); 
                if (string.IsNullOrWhiteSpace(input))
                {
                    AppendLog($" ✖ 命令不能为空");
                    return;
                }

                string hexInput = input.Replace(" ", "");
                if (hexInput.Length % 2 != 0)
                {
                    AppendLog($" ✖ 十六进制命令长度必须为偶数");
                    return;
                }

                // 转 byte[]
                byte[] command = new byte[hexInput.Length / 2];
                for (int i = 0; i < command.Length; i++)
                {
                    string hexByte = hexInput.Substring(i * 2, 2);
                    if (!byte.TryParse(hexByte, System.Globalization.NumberStyles.HexNumber, null, out command[i]))
                    {
                        AppendLog($" ✖ 无效的十六进制字符：{hexByte}");
                        return;
                    }
                }

                // 计算 CRC
                byte[] crc = CalculateModbusCRC(command);

                // 拼接完整帧
                byte[] fullCommand = command.Concat(crc).ToArray();

                // 显示日志
                AppendLog($" ✔ 输入命令（不含CRC）：{BitConverter.ToString(command).Replace("-", " ")}");
                AppendLog($" ✔ 计算 CRC：{BitConverter.ToString(crc).Replace("-", " ")}\r");
                AppendLog($" ✔ 完整命令（含CRC）：{BitConverter.ToString(fullCommand).Replace("-", " ")}");
            }
            catch (Exception ex)
            {
                AppendLog($" ✖ 命令处理失败：{ex.Message}");
            }
        }
        private void BtnSavePaintApplyLayer_Click(object? sender, EventArgs e)
        {
            // 创建一个新的 Bitmap，用来保存涂抹层
            int width = pictureBox1.Width;
            int height = pictureBox1.Height;

            using (Bitmap paintLayer = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(paintLayer))
                {
                    g.Clear(Color.Transparent);

                    // 锁
                    lock (brushPointsLock)
                    {
                        foreach (var line in lines)
                        {
                            g.DrawLine(new Pen(Color.FromArgb(50, 192, 255, 192), line.Thickness), //保存的涂抹层 淡绿色
                                       line.Start.X, line.Start.Y,
                                       line.End.X, line.End.Y);
                        }
                    }
                }

                try
                {
                    if (!Directory.Exists(savedLayerFile))
                        Directory.CreateDirectory(savedLayerFile);

                    string savePath = Path.Combine(savedLayerFile, $"Layer_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                    // 独立副本保存
                    using (Bitmap saveCopy = (Bitmap)paintLayer.Clone())
                    {
                        saveCopy.Save(savePath, ImageFormat.Png);
                    }

                    AppendLog($"涂抹层已保存：{savePath}");
                }
                catch (Exception ex)
                {
                    AppendLog($"保存涂抹层失败: {ex.Message}");
                }
            }
        }
        private void BtnReloadLatestPaintLayer_Click(object? sender, EventArgs e)
        {
            try
            {
                string[] files = Directory.GetFiles(savedLayerFile, "*.png");
                if (files.Length == 0)
                {
                    AppendLog($"✖ 无可用涂抹层文件");
                    return;
                }

                string latestFile = files.OrderByDescending(f => File.GetCreationTime(f)).First();
                Bitmap loadedLayer = new Bitmap(latestFile);

                lock (paintLayerLock)
                {
                    // 清空旧的涂层
                    drawingLayer?.Dispose();
                    drawingLayer = new Bitmap(loadedLayer); // 用最新涂层替换
                    lines.Clear(); 
                }

                loadedLayer.Dispose();

                pictureBox1.Invalidate(); // 刷新显示
                pictureBox3.Invalidate(); // 刷新显示
                AppendLog($"✔ 已加载最新涂抹层: {latestFile}");
            }
            catch (Exception ex)
            {
                AppendLog($"✖ 加载涂抹层失败: {ex.Message}");
            }
        }
        private void BtnClearCurrentPaintLayer_Click(object? sender, EventArgs e)
        {
            try
            {
                lock (paintLayerLock)
                {
                    // 清空当前绘制层
                    drawingLayer?.Dispose();
                    drawingLayer = null;

                    // 清空线段数据
                    lines.Clear();
                }

                // 重绘 PictureBox，使涂抹层立即消失
                pictureBox1.Invalidate();
                pictureBox3.Invalidate();
                AppendLog($"✔当前涂抹层已清除（未删除历史文件）");
            }
            catch (Exception ex)
            {
                AppendLog($"✖ 清除涂抹层失败: {ex.Message}");
            }
        }

        private void BtnControlWaterpump_Click(object? sender, EventArgs e)
        {

            try
            {
                // 防呆：串口未打开
                if (modbuserialPortControl.serialPort == null ||
                    !modbuserialPortControl.serialPort.IsOpen)
                {
                    AppendLog($"✖ 串口未打开，无法控制水泵");
                    return;
                }

                byte[] command;

                if (!isPumpOn)
                {
                    // 打开水泵
                    command = modbuserialPortControl.BuildWriteSingleCommand(0x05, 0x0000, 0xFF00);
                    isPumpOn = true;
                    button11.Text = "关闭水泵";
                    AppendLog($"💧 水泵已打开");
                }
                else
                {
                    // 关闭水泵
                    command = modbuserialPortControl.BuildWriteSingleCommand(0x05, 0x0000, 0x0000);
                    isPumpOn = false;
                    button11.Text = "打开水泵";
                    AppendLog($"💧 水泵已关闭");
                }

                // 发送命令
                modbuserialPortControl.serialPort.Write(command, 0, command.Length);
            }

            catch (Exception ex)
            {
                AppendLog($"✖ 控制水泵失败: {ex.Message}");
            }
        }


        private async void BtnServoSteering_Click(object? sender, EventArgs e)
        {

            // 防呆：串口未打开
            if (modbuserialPortControl.serialPort == null || !modbuserialPortControl.serialPort.IsOpen)
            {
                AppendLog($"✖ 串口未打开，无法控制舵机");
                return;
            }

            string single = textBoxSingleAngle.Text.Trim();
            string minText = textBoxSingleMinAngle.Text.Trim();
            string maxText = textBoxSingleMaxAngle.Text.Trim();

            // 情况①：三个 TextBox 都有值 → 不动作
            if (!string.IsNullOrEmpty(single) &&
                !string.IsNullOrEmpty(minText) &&
                !string.IsNullOrEmpty(maxText))
            {
                AppendLog("!同时输入单角度和范围角度，无法判断动作，已取消。");
                return;
            }

            // 情况②：只输入单角度
            if (!string.IsNullOrEmpty(single) &&
                string.IsNullOrEmpty(minText) &&
                string.IsNullOrEmpty(maxText))
            {
                if (!int.TryParse(single, out int angle))
                {
                    MessageBox.Show("请输入正确的数字角度！");
                    return;
                }

                // 防呆
                angle = Math.Max(0, Math.Min(270, angle));
                textBoxSingleAngle.Text = angle.ToString();
                
                ushort servoVal = (ushort)(angle * 0x010E / 270);

                byte[] cmd = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, servoVal);
                modbuserialPortControl.serialPort.Write(cmd, 0, cmd.Length);
                UpdateCurrentAngle(angle);

                AppendLog($" 舵机转到 {angle}°（值 {servoVal}）");
                return;
            }

            // 情况③：只输入 Min + Max（范围角度动作）
            if (string.IsNullOrEmpty(single) &&
                !string.IsNullOrEmpty(minText) &&
                !string.IsNullOrEmpty(maxText))
            {
                if (!int.TryParse(minText, out int minAngle) ||
                    !int.TryParse(maxText, out int maxAngle))
                {
                    MessageBox.Show("请输入正确的范围角度！");
                    return;
                }

                // 防呆
                minAngle = Math.Max(0, Math.Min(270, minAngle));
                maxAngle = Math.Max(0, Math.Min(270, maxAngle));

                // min > max 时自动交换
                if (minAngle > maxAngle)
                    (minAngle, maxAngle) = (maxAngle, minAngle);

                textBoxSingleMinAngle.Text = minAngle.ToString();
                textBoxSingleMaxAngle.Text = maxAngle.ToString();

                ushort servoMin = (ushort)(minAngle * 0x010E / 270);
                ushort servoMax = (ushort)(maxAngle * 0x010E / 270);

                //// 先到 min
                //byte[] cmdMin = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, servoMin);
                //modbuserialPortControl.serialPort.Write(cmdMin, 0, cmdMin.Length);
                //await Task.Delay(1000);

                //// 再到 max
                //byte[] cmdMax = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, servoMax);
                //modbuserialPortControl.serialPort.Write(cmdMax, 0, cmdMax.Length);
                //await Task.Delay(1000);


                // 平滑移动到 min
                await MoveServoWithRealtimeAngleAsync(servoMin);
                await Task.Delay(500); 

                // 平滑移动到 max
                await MoveServoWithRealtimeAngleAsync(servoMax);
                await Task.Delay(500);

                AppendLog($"↔舵机已执行范围动作：{minAngle}° → {maxAngle}°");
                return;
            }

            // 情况④：所有都空
            AppendLog("✖未检测到任何输入，不执行动作。");

        }

        private void BtnEstablishConnect_Click(object? sender, EventArgs e)
        {
            try
            {
                string selectedPort = comboBoxPorts.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedPort))
                {
                    AppendLog($"✖ 未选择串口");
                    return;
                }

                bool ok = modbuserialPortControl.OpenPort(selectedPort);
                if (ok)
                {
                    toolStripStatusLabel4.Text = "通讯已建立✔";
                    toolStripStatusLabel4.ForeColor = Color.DarkOrange;
                    modbuserialPortControl.Initial();
                    AppendLog($"🔌 串口通讯已建立");
                }
                else
                {
                    toolStripStatusLabel4.Text = "通讯建立失败✖";
                    toolStripStatusLabel4.ForeColor = Color.Red;
                    modbuserialPortControl.Initial();
                    AppendLog($"✖ 串口通讯建立失败");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"✖ 串口通讯异常: {ex.Message}");
            }
        }

        private void BtnDisconnect_Click(object? sender, EventArgs e)
        {
            // 关闭通讯
            modbuserialPortControl.ClosePort();
            toolStripStatusLabel4.Text = "通讯已断开✖";
            toolStripStatusLabel4.ForeColor = Color.Red;

            // 停止检测标志
            isDetecting = false;

            //// 禁用按钮
            //btnSnapImage.Enabled = isDetecting;
            //btnSendCommand.Enabled = isDetecting;
            //btnOpenForeigndetection.Enabled = isDetecting;

            // 清理缓存
            lock (frameLock)
            {
                latestFrameCache?.Dispose();
                latestFrameCache = null;
            }
        }

        private void BtnOpenCamera_Click(object? sender, EventArgs e)
        {
            if (isRunning) return; // 防止重复开启

            isRunning = true;

            // 如果之前有 capture 先释放
            if (capture != null)
            {
                try
                {
                    capture.Release();
                    capture.Dispose();
                }
                catch { }
                finally
                {
                    capture = null;
                }
            }

            capture = new VideoCapture(1); // 摄像头索引根据实际调整
            if (!capture.IsOpened())
            {
                AppendLog($"✖ 无法打开相机！");
                return;
            }

            frameWidth = capture.Get(VideoCaptureProperties.FrameWidth);
            frameHeight = capture.Get(VideoCaptureProperties.FrameHeight);
            double fps = capture.Get(VideoCaptureProperties.Fps);

            toolStripStatusLabel1.Text = $"FPS: {fps:F1}";
            toolStripStatusLabel2.Text = $"Width: {frameWidth}";
            toolStripStatusLabel3.Text = $"Height: {frameHeight}";
            toolStripStatusLabel1.ForeColor = Color.DarkOrange;
            toolStripStatusLabel2.ForeColor = Color.DarkOrange;
            toolStripStatusLabel3.ForeColor = Color.DarkOrange;

            // 开启采集线程
            Task.Run(() =>
            {
                while (isRunning && capture != null && capture.IsOpened())
                {
                    using var frame = new Mat();
                    if (!capture.Read(frame) || frame.Empty())
                        continue;

                    using var bmp = BitmapConverter.ToBitmap(frame);

                    // 同步更新两个 PictureBox
                    UpdatePictureBoxes(bmp);

                    Bitmap pbImage = (Bitmap)bmp.Clone();

                    pictureBox1.Invoke(() =>
                    {
                        var old = pictureBox1.Image;
                        pictureBox1.Image = pbImage;
                        old?.Dispose();
                    });

                    Bitmap cacheCopy = (Bitmap)bmp.Clone();
                    lock (frameLock)
                    {
                        var oldCache = latestFrameCache;
                        latestFrameCache = cacheCopy;
                        oldCache?.Dispose();
                    }

                    Thread.Sleep(30);
                }
            });

            AppendLog($"📷 相机已打开\r");
        }

        private void BtnCloseCamera_Click(object? sender, EventArgs e)
        {
            if (!isRunning) return;

            isRunning = false;



            // 释放摄像头
            if (capture != null)
            {
                try
                {
                    capture.Release();
                    capture.Dispose();
                }
                catch { }
                finally
                {
                    capture = null;
                }
            }

            // 清空 PictureBox1 显示
            pictureBox1.Invoke(() =>
            {
                var oldImage = pictureBox1.Image;
                pictureBox1.Image = null;
                oldImage?.Dispose();
            });

            // 清空底图 PictureBox
            if (pictureBox2 != null)
            {
                pictureBox2.Invoke(() =>
                {
                    var oldBgImage = pictureBox2.Image;
                    pictureBox2.Image = null;
                    oldBgImage?.Dispose();
                });
            }
            // 清空 PictureBox3 显示
            pictureBox3.Invoke(() =>
            {
                var oldImage = pictureBox3.Image;
                pictureBox3.Image = null;
                oldImage?.Dispose();
            });
            // 清零 StatusStrip
            toolStripStatusLabel1.Text = "";
            toolStripStatusLabel2.Text = "";
            toolStripStatusLabel3.Text = "";
            AppendLog($"📷 相机已关闭");

        }


        //计算 Modbus RTU CRC16 校验码
        private byte[] CalculateModbusCRC(byte[] data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0)
                        crc = (ushort)((crc >> 1) ^ 0xA001);
                    else
                        crc >>= 1;
                }
            }

            // 返回低字节在前，高字节在后
            return new byte[] { (byte)(crc & 0xFF), (byte)(crc >> 8) };
        }

        private void LoadSavedLayer()
        {
            try
            {
                if (!Directory.Exists(savedLayerFile)) return;

                var files = Directory.GetFiles(savedLayerFile, "*.png");
                if (files.Length == 0) return;

                string latestFile = files.OrderByDescending(f => File.GetCreationTime(f)).FirstOrDefault();
                if (latestFile != null)
                {
                    Bitmap loadedLayer = new Bitmap(latestFile);

                    lock (brushPointsLock)
                    {
                        drawingLayer?.Dispose();
                        drawingLayer = (Bitmap)loadedLayer.Clone();
                    }

                    loadedLayer.Dispose();

                    // 不显示
                    isDrawing = false;

                    AppendLog($"✔ 已加载最新涂抹层: {latestFile} ");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"✖ 加载涂抹层失败: {ex.Message} ");
            }
        }

        private void TabControl1_SelectedIndexChanged(object? sender, EventArgs e)
        {
            switch (tabControl1.SelectedIndex)
            {
                case 0:
                    tabPage1.Controls.Add(panel1);
                    tabPage1.Controls.Add(groupBox4);
                    tabPage1.Controls.Add(button7);
                    tabPage1.Controls.Add(button8);
                    tabPage1.Controls.Add(button9);
                    tabPage1.Controls.Add(label19);
                    tabPage1.Controls.Add(textBox3);
                    break;
                case 1:
                    tabPage2.Controls.Add(panel1);
                    tabPage2.Controls.Add(button7);
                    tabPage2.Controls.Add(button8);
                    tabPage2.Controls.Add(button9);
                    tabPage2.Controls.Add(label19);
                    tabPage2.Controls.Add(textBox3);
                    break;
                case 2:
                    tabPage3.Controls.Add(panel1);
                    tabPage3.Controls.Add(groupBox4);
                    tabPage3.Controls.Add(label19);
                    tabPage3.Controls.Add(textBox3);
                    break;
            }
        }

        private void UpdatePictureBoxes(Bitmap bmp)
        {
            if (pictureBox1.InvokeRequired || pictureBox3.InvokeRequired)
            {
                pictureBox1.Invoke(new Action(() => UpdatePictureBoxes(bmp)));
                return;
            }

            // 先释放原来的图片，避免内存泄漏
            pictureBox1.Image?.Dispose();
            pictureBox3.Image?.Dispose();

            // 克隆图片，保证每个 PictureBox 独立
            pictureBox1.Image = (Bitmap)bmp.Clone();
            pictureBox3.Image = (Bitmap)bmp.Clone();
        }


        #endregion



        #region 涂层
        private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDrawing = true;
                currentLinePoints.Clear();
                currentLinePoints.Add(new DrawingPoint { X = e.X, Y = e.Y });
            }
        }
        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing && e.Button == MouseButtons.Left)
            {
                currentLinePoints.Add(new DrawingPoint { X = e.X, Y = e.Y });
                // 在 drawingLayer 上画线
                if (drawingLayer == null)
                {
                    drawingLayer = new Bitmap(pictureBox1.Width, pictureBox1.Height);
                }
                using (Graphics g = Graphics.FromImage(drawingLayer))
                {
                    using (Pen pen = new Pen(Color.FromArgb(50, 255, 192, 192), brushSize))//绘制的涂抹层 透明红
                    {
                        int count = currentLinePoints.Count;
                        if (count > 1)
                        {
                            g.DrawLine(pen,
                                currentLinePoints[count - 2].X, currentLinePoints[count - 2].Y,
                                currentLinePoints[count - 1].X, currentLinePoints[count - 1].Y);
                        }
                    }
                }
                pictureBox1.Invalidate();
            }
        }
        private void PictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDrawing)
            {
                isDrawing = false;
                // 当前线段加入总线段列表
                if (currentLinePoints.Count > 1)
                {
                    lines.Add(new LineSegment
                    {
                        Start = currentLinePoints.First(),
                        End = currentLinePoints.Last(),
                        Thickness = brushSize
                    });
                }
                currentLinePoints.Clear();
                pictureBox1.Invalidate();
            }
        }
        private void PictureBox1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // 只在绘制时显示涂抹层
            if (isDrawing && drawingLayer != null)
            {
                g.DrawImage(drawingLayer, 0, 0);
            }

            // 绘制当前鼠标拖动的线条
            if (currentLinePoints.Count > 1)
            {
                using (Pen pen = new Pen(Color.FromArgb(50, 255, 192, 192), brushSize))//绘制的涂抹层 透明红
                {
                    for (int i = 1; i < currentLinePoints.Count; i++)
                    {
                        g.DrawLine(pen, currentLinePoints[i - 1].X, currentLinePoints[i - 1].Y,
                                          currentLinePoints[i].X, currentLinePoints[i].Y);
                    }
                }
            }

            DrawForeignObjectHighlight(e.Graphics);
        }



        private void PictureBox3_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isDrawing = true;
                currentLinePoints.Clear();
                currentLinePoints.Add(new DrawingPoint { X = e.X, Y = e.Y });
            }
        }
        private void PictureBox3_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing && e.Button == MouseButtons.Left)
            {
                currentLinePoints.Add(new DrawingPoint { X = e.X, Y = e.Y });
                // 在 drawingLayer 上画线
                if (drawingLayer == null)
                {
                    drawingLayer = new Bitmap(pictureBox3.Width, pictureBox3.Height);
                }
                using (Graphics g = Graphics.FromImage(drawingLayer))
                {
                    using (Pen pen = new Pen(Color.FromArgb(50, 255, 192, 192), brushSize))//绘制的涂抹层 透明红
                    {
                        int count = currentLinePoints.Count;
                        if (count > 1)
                        {
                            g.DrawLine(pen,
                                currentLinePoints[count - 2].X, currentLinePoints[count - 2].Y,
                                currentLinePoints[count - 1].X, currentLinePoints[count - 1].Y);
                        }
                    }
                }
                pictureBox3.Invalidate();
            }
        }
        private void PictureBox3_MouseUp(object sender, MouseEventArgs e)
        {
            if (isDrawing)
            {
                isDrawing = false;
                // 当前线段加入总线段列表
                if (currentLinePoints.Count > 1)
                {
                    lines.Add(new LineSegment
                    {
                        Start = currentLinePoints.First(),
                        End = currentLinePoints.Last(),
                        Thickness = brushSize
                    });
                }
                currentLinePoints.Clear();
                pictureBox3.Invalidate();
            }
        }
        private void PictureBox3_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            // 只在绘制时显示涂抹层
            if (isDrawing && drawingLayer != null)
            {
                g.DrawImage(drawingLayer, 0, 0);
            }

            // 绘制当前鼠标拖动的线条
            if (currentLinePoints.Count > 1)
            {
                using (Pen pen = new Pen(Color.FromArgb(50, 255, 192, 192), brushSize))//绘制的涂抹层 透明红
                {
                    for (int i = 1; i < currentLinePoints.Count; i++)
                    {
                        g.DrawLine(pen, currentLinePoints[i - 1].X, currentLinePoints[i - 1].Y,
                                          currentLinePoints[i].X, currentLinePoints[i].Y);
                    }
                }
            }

            DrawForeignObjectHighlight(e.Graphics);
        }
        #endregion


        #region 数值变化
        private void numericUpDownDiffThreshold_ValueChanged(object sender, EventArgs e)
        {
            diffThreshold = (int)numericUpDownDiffThreshold.Value;
        }
        private void numericUpDownRegionMinSize_ValueChanged(object sender, EventArgs e)
        {
            regionMinSize = (int)numericUpDownRegionMinSize.Value;
        }
        private void numericUpDownHighlightDuration_ValueChanged(object sender, EventArgs e)
        {
            highlightDuration = (int)numericUpDownHighlightDuration.Value;
        }
        private void numericUpDownBrushSize_ValueChanged(object sender, EventArgs e)
        {
            brushSize = (int)numericUpDownBrushSize.Value;
        }
        private void numericUpDownOverlapThreshold_ValueChanged(object sender, EventArgs e)
        {
            overlapThreshold = (float)numericUpDownOverlapThreshold.Value;
        }
        #endregion


        ushort AngleToServoValue(float angle)
        {
            angle = Math.Clamp(angle, 0f, 270f);
            return (ushort)(angle * 0x010E / 270f);
        }

        void StartServoJog(int direction)
        {
            if (isServoJogging) return;

        
            currentAngle = Math.Clamp(currentAngle, 0, 270);

            isServoJogging = true;

            servoJogTimer = new System.Timers.Timer(jogInterval);
            servoJogTimer.Elapsed += (s, e) =>
            {
                currentAngle += direction * jogStep;

                if (currentAngle < 0) currentAngle = 0;
                if (currentAngle > 270) currentAngle = 270;

                ushort value = AngleToServoValue((int)Math.Round(currentAngle));


                byte[] cmd = modbuserialPortControl
                    .BuildWriteSingleCommand(0x06, 0x0000, value);

                modbuserialPortControl.serialPort.Write(cmd, 0, cmd.Length);

                UpdateCurrentAngle(currentAngle);
            };
        servoJogTimer.Start();
        }

        void StopServoJog()
        {
            if (!isServoJogging) return;

            isServoJogging = false;

            servoJogTimer?.Stop();
            servoJogTimer?.Dispose();
            servoJogTimer = null;
        }

        void UpdateCurrentAngle(float angle)
        {
            currentAngle = Math.Clamp(angle, 0, 270);

            // 更新UI
            if (textBox3.InvokeRequired)
            {
                textBox3.BeginInvoke(new Action(() =>
                {
                    textBox3.Text = currentAngle.ToString("F1"); // 保留一位小数
                }));
            }
            else
            {
                textBox3.Text = currentAngle.ToString("F1");
            }
        }

        private async Task MoveServoWithRealtimeAngleAsync(ushort targetServoValue)
        {
            float targetAngle = targetServoValue * 270f / 0x010E; // 转换回角度
            targetAngle = Math.Clamp(targetAngle, 0f, 270f);

            float step = 0.5f;       // 每次移动 0.5°
            int interval = 20;       // 每步间隔 20ms

            while (Math.Abs(currentAngle - targetAngle) > 0.01f)
            {
                if (currentAngle < targetAngle)
                    currentAngle += step;
                else
                    currentAngle -= step;

                currentAngle = Math.Clamp(currentAngle, 0f, 270f);

                // 更新 UI
                if (textBox3.InvokeRequired)
                    textBox3.Invoke(() => textBox3.Text = currentAngle.ToString("F1"));
                else
                    textBox3.Text = currentAngle.ToString("F1");

                // 发送当前角度到舵机
                ushort value = (ushort)(currentAngle * 0x010E / 270f);
                byte[] cmd = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, value);
                modbuserialPortControl.serialPort.Write(cmd, 0, cmd.Length);

                await Task.Delay(interval);
            }
        }


        #region 异物标转换为舵机数值
        private async Task RotateServoAsync(Bitmap frameBeforeServo)
        {
            Bitmap localFrame = (Bitmap)frameBeforeServo.Clone();
            frameBeforeServo.Dispose();

            if (modbuserialPortControl.serialPort == null || !modbuserialPortControl.serialPort.IsOpen)
                return;

            try
            {
                isServoRunning = true;

                int minAngle = int.Parse(textBoxStartAngle.Text.Trim());
                int maxAngle = int.Parse(textBoxEndAngle.Text.Trim());
                int totalDuration = (int)numericUpDownExerciseDuration.Value;

                if (minAngle < 0) minAngle = 0;
                if (maxAngle > 270) maxAngle = 270;
                if (minAngle > maxAngle) (minAngle, maxAngle) = (maxAngle, minAngle);

                ushort servoMin = (ushort)(minAngle * 0x010E / 270);
                ushort servoMax = (ushort)(maxAngle * 0x010E / 270);

                DateTime startTime = DateTime.Now;

                while ((DateTime.Now - startTime).TotalSeconds < totalDuration)
                {
                    //// 左
                    //currentAngle = minAngle;          // 更新当前角度
                    //byte[] cmdMin = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, servoMin);
                    //modbuserialPortControl.serialPort.Write(cmdMin, 0, cmdMin.Length);
                    //await Task.Delay(1000);

                    //if ((DateTime.Now - startTime).TotalSeconds >= totalDuration) break;

                    //// 右
                    //currentAngle = maxAngle;          // 更新当前角度
                    //byte[] cmdMax = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, servoMax);
                    //modbuserialPortControl.serialPort.Write(cmdMax, 0, cmdMax.Length);
                    //await Task.Delay(1000);

                    // 左
                    await MoveServoWithRealtimeAngleAsync(servoMin);

                    if ((DateTime.Now - startTime).TotalSeconds >= totalDuration) break;

                    // 右
                    await MoveServoWithRealtimeAngleAsync(servoMax);

                }
                UpdateCurrentAngle(currentAngle);
                AppendLog($"✔ 舵机完成动作，范围：{minAngle}-{maxAngle}°，总时长上限：{totalDuration}s ");
            }
            catch (Exception ex)
            {
                AppendLog($"✖ 舵机动作失败: {ex.Message}\r");
            }
            finally
            {
                isServoRunning = false;

                // 统一复位
                if (isPumpOn)
                {
                    try
                    {
                        byte[] pumpOff = modbuserialPortControl.BuildWriteSingleCommand(0x05, 0x0000, 0x0000);
                        modbuserialPortControl.serialPort.Write(pumpOff, 0, pumpOff.Length);
                        isPumpOn = false;
                        AppendLog($"💧 舵机动作完成，水泵已关闭");

                        byte[] cmdHome = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, 0x0087);
                        modbuserialPortControl.serialPort.Write(cmdHome, 0, cmdHome.Length);
                        AppendLog($"🔁 舵机已回初始位置");
                        UpdateCurrentAngle(currentAngle);
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"✖ 关闭水泵失败: {ex.Message}");
                    }
                }
            }

            try
            {
                await Task.Delay(300); // 等待舵机稳定
                Bitmap frameCopy = null;

                // 安全获取最新帧
                lock (frameLock)
                {
                    if (latestFrameCache != null)
                        frameCopy = (Bitmap)latestFrameCache.Clone();
                }

                if (frameCopy != null)
                {
                    UpdateBackgroundImage(frameCopy); // 已在 UpdateBackgroundImage 内锁住
                    frameCopy.Dispose();
                    AppendLog($"📷 已使用舵机动作后的画面更新底图 ");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"✖ 舵机结束后更新底图失败: {ex.Message}");
            }
        }
        #endregion
    }

}


