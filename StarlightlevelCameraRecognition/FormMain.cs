using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing; 
using System.Drawing.Imaging;
using System.IO.Ports;
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


        #region 全局变量
        private VideoCapture capture;  //相机对象，
        private bool isRunning = false; //相机是否打开
        private CancellationTokenSource cts;
        private ModbusRTU modbuserialPortControl;

        private Bitmap backgroundImage;  // 存储背景图
        private Bitmap backgroundImageForUI; // 用于 PictureBox 显示，UI线程使用
        private bool isBackgroundCaptured = false;  // 是否已捕获背景
        private bool isUpdatingBackground = false;
    

        private bool isDrawing = false;              // 是否正在绘制
        private List<LineSegment> lines = new();       // 所有已画的线
        private List<DrawingPoint> currentLinePoints = new(); // 当前鼠标拖动形成的线
        private int brushSize = 100;                  // 画笔大小，可根据需要调整
        private float overlapThreshold = 0.3f;       // 异物红框和涂抹区域重叠阈值
        private string savedLayerFile = @"D:\LineLayers"; // 保存路径
        // 临时显示层
        private Bitmap drawingLayer;
        public class LineSegment
        {
            public DrawingPoint Start { get; set; }
            public DrawingPoint End { get; set; }
            public int Thickness { get; set; }
        }



        private List<(Rectangle rect, DateTime detectTime)> foreignObjects = new();
        private int highlightDuration = 20;  // 异物高亮时间
        private int diffThreshold = 70;
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

            btnOpenDevice.Click += BtnOpenDevice_Click;
            btnCloseDevice.Click += BtnCloseDevice_Click;
            btnSendCommand.Click += BtnSendCommand_Click;
            btnSnapImage.Click += BtnSnapImage_Click;
            btnOpenForeigndetection.Click += BtnOpenForeigndetection_Click;
            btnCaluCRC.Click += BtnCaluCRC_Click;
            btnSavePaintApplyLayer.Click += BtnSavePaintApplyLayer_Click;
            btnReloadLatestPaintLayer.Click += BtnReloadLatestPaintLayer_Click;

            pictureBox1.MouseDown += PictureBox1_MouseDown;
            pictureBox1.MouseMove += PictureBox1_MouseMove;
            pictureBox1.MouseUp += PictureBox1_MouseUp;
            pictureBox1.Paint += PictureBox1_Paint;


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

            button2.Enabled = false;
            button3.Enabled = false;
            button4.Enabled = false;
            modbuserialPortControl = new ModbusRTU();
            modbuserialPortControl.SlaveAddress = 1;
            modbuserialPortControl.OnStatusMessage += ModbuserialPortControl_OnStatusMessage;//事件订阅

            LoadSavedLayer();
        }

      



        //事件订阅
        private void ModbuserialPortControl_OnStatusMessage(string message)
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} {message}");
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

            // 使用 LockBits + Marshal.Copy（安全版本）提高性能
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



            //--------------------------------------------------------------
            // 获取当前涂抹层（线程安全克隆）
            //--------------------------------------------------------------
            Bitmap layerCopy = null;
            lock (paintLayerLock)
            {
                if (paintApplyLayer != null)
                    layerCopy = (Bitmap)paintApplyLayer.Clone();
            }




            if (!isBackgroundCaptured || backgroundImage == null)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] ✖ 背景图未捕获");
                frameCopyForDetection.Dispose();
                return;
            }

            Bitmap bgCopy;
            lock (foreignObjectsLock)
            {
                if (backgroundImage == null)
                    return;

                // 克隆独立副本，线程安全
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
                        $"[{DateTime.Now:HH:mm:ss}] 🕷 异物占比: {overlapRatio:P1}，阈值: {overlapThreshold:P1}\r"));

                    if (overlapRatio >= overlapThreshold)
                    {
                        if (!isPumpOn)
                        {
                            byte[] pumpOn = modbuserialPortControl.BuildWriteSingleCommand(0x05, 0x0000, 0xFF00);
                            modbuserialPortControl.serialPort.Write(pumpOn, 0, pumpOn.Length);
                            isPumpOn = true;
                            AppendLog($"[{DateTime.Now:HH:mm:ss}] 💧 水泵已打开");
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
                        // 异物消失，关闭水泵
                        if (isPumpOn)
                        {
                            byte[] pumpOff = modbuserialPortControl.BuildWriteSingleCommand(0x05, 0x0000, 0x0000);
                            modbuserialPortControl.serialPort.Write(pumpOff, 0, pumpOff.Length);
                            isPumpOn = false;
                            AppendLog($"[{DateTime.Now:HH:mm:ss}] 💧 水泵已关闭");
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
                    // 无异物时，定时更新底图
                    double intervalSeconds = (double)numericUpDownCleanTime.Value;
                    if ((DateTime.Now - lastBackgroundUpdateTime).TotalSeconds >= intervalSeconds)
                    {
                        UpdateBackgroundImage(frameCopyForDetection);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] DetectAndTrackForeignObject 异常: {ex.Message}");
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

            // 更新异物列表（移除超时）
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


            // --- 保存底图到 D:\BackgroundDebugGet ---
            try
            {
                if (backgroundImage == null) return;  // 避免 null

                string dir = @"D:\BackgroundDebugGet";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, $"底图_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                // 使用新的 Bitmap 实例保存，防止占用冲突
                using (Bitmap saveBmp = (Bitmap)backgroundImage.Clone())
                {
                    saveBmp.Save(path, ImageFormat.Png);
                }

                AppendLog($"[{DateTime.Now:HH:mm:ss}] 🔁 底图已保存");
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
                this.richTextBox1.AppendText("无法打开摄像头！");
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

                    // --- UI 用独立副本 ---
                    Bitmap pbImage = (Bitmap)bmp.Clone();
                    pictureBox1.Invoke(() =>
                    {
                        var old = pictureBox1.Image;
                        pictureBox1.Image = pbImage;
                        old?.Dispose();
                    });

                    // --- 后台缓存用独立副本 ---
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


            LoadSavedLayer();

            // 通讯初始化
            string selectedPort = comboBoxPorts.SelectedItem.ToString();
            bool ok = modbuserialPortControl.OpenPort(selectedPort);
            if (ok)
            {
                toolStripStatusLabel4.Text = "通讯已建立✔";
                toolStripStatusLabel4.ForeColor = Color.Green;
                modbuserialPortControl.Initial();
            }
            else
            {
                toolStripStatusLabel4.Text = "通讯建立失败✖";
                toolStripStatusLabel4.ForeColor = Color.Red;
                modbuserialPortControl.Initial();
            }

            btnCloseDevice.Enabled = true;
            btnSnapImage.Enabled = true;
            btnSendCommand.Enabled = true;
            btnOpenForeigndetection.Enabled = true;
        }

        private void BtnCloseDevice_Click(object? sender, EventArgs e)
        {
            if (!isRunning) return;

            isRunning = false;

            // 取消异物检测线程
            cts?.Cancel();

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

            // 清空 PictureBox 显示，确保线程安全
            pictureBox1.Invoke(() =>
            {
                var oldImage = pictureBox1.Image;
                pictureBox1.Image = null;
                oldImage?.Dispose();
            });

            // 清空底图 PictureBox（如果存在）
            if (pictureBox2 != null)
            {
                pictureBox2.Invoke(() =>
                {
                    var oldBgImage = pictureBox2.Image;
                    pictureBox2.Image = null;
                    oldBgImage?.Dispose();
                });
            }

            // 清零 StatusStrip
            toolStripStatusLabel1.Text = "";
            toolStripStatusLabel2.Text = "";
            toolStripStatusLabel3.Text = "";

            // 关闭通讯
            modbuserialPortControl.ClosePort();
            toolStripStatusLabel4.Text = "通讯已断开✖";
            toolStripStatusLabel4.ForeColor = Color.Red;

            // 禁用按钮
            btnSnapImage.Enabled = false;
            btnSendCommand.Enabled = false;
            btnOpenForeigndetection.Enabled = false;

            // 清理缓存
            lock (frameLock)
            {
                latestFrameCache?.Dispose();
                latestFrameCache = null;
            }

            // 停止检测标志
            isDetecting = false;
        }

        private void BtnSendCommand_Click(object? sender, EventArgs e)
        {
            try
            {
                string input = textBox1.Text.Trim();
                if (string.IsNullOrWhiteSpace(input))
                {
                    AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 命令不能为空\r");
                    return;
                }

                if (!modbuserialPortControl.serialPort.IsOpen)
                {
                    AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 串口未打开\r");
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
                        AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 指令格式错误\r");
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
                            AppendLog($"{DateTime.Now:HH:mm:ss} →发送线圈命令：{input}\r");
                            Thread.Sleep(50);
                            modbuserialPortControl.ReadCoils(address, 1);
                        }
                        else if (ushort.TryParse(param, out ushort count))
                        {
                            modbuserialPortControl.ReadCoils(address, count);
                            AppendLog($"{DateTime.Now:HH:mm:ss} → 发送读线圈命令：{input}\r");
                        }
                        else
                        {
                            AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 参数错误\r");
                        }
                    }
                    // 保持寄存器
                    else if (type == "R")
                    {
                        if (ushort.TryParse(param, out ushort value))
                        {
                            bool success = modbuserialPortControl.WriteHoldingRegister(address, value);
                            AppendLog($"{DateTime.Now:HH:mm:ss} → 发送写寄存器命令：{input}\r");
                            Thread.Sleep(50);
                            modbuserialPortControl.ReadHoldingRegisters(address, 1);
                        }
                        else if (ushort.TryParse(param, out ushort count))
                        {
                            modbuserialPortControl.ReadHoldingRegisters(address, count);
                            AppendLog($"{DateTime.Now:HH:mm:ss} → 发送读寄存器命令：{input}\r");
                        }
                        else
                        {
                            AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 参数错误\r");
                        }
                    }
                    // 输入寄存器
                    else if (type == "I")
                    {
                        if (ushort.TryParse(param, out ushort count))
                        {
                            modbuserialPortControl.ReadInputRegisters(address, count);
                            AppendLog($"{DateTime.Now:HH:mm:ss} → 发送读输入寄存器命令：{input}\r");
                        }
                        else
                        {
                            AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 参数错误\r");
                        }
                    }
                    else
                    {
                        AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 未知指令类型\r");
                    }
                }
                else
                {
                    // 原始十六进制命令
                    string hex = input.Replace(" ", "");
                    if (hex.Length % 2 != 0)
                    {
                        AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 十六进制长度必须为偶数\r");
                        return;
                    }

                    byte[] command = new byte[hex.Length / 2];
                    for (int i = 0; i < command.Length; i++)
                    {
                        command[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                    }

                    modbuserialPortControl.serialPort.Write(command, 0, command.Length);
                    AppendLog($"{DateTime.Now:HH:mm:ss} → 发送原始命令：{BitConverter.ToString(command).Replace("-", " ")}\r");

                    // 等待响应
                    Thread.Sleep(100);
                    int bytesToRead = modbuserialPortControl.serialPort.BytesToRead;
                    if (bytesToRead > 0)
                    {
                        byte[] response = new byte[bytesToRead];
                        modbuserialPortControl.serialPort.Read(response, 0, bytesToRead);
                        AppendLog($"{DateTime.Now:HH:mm:ss} ← 收到响应：{BitConverter.ToString(response).Replace("-", " ")}\r");
                    }
                    else
                    {
                        AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 未收到响应\r");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 执行异常：{ex.Message}\r");
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

            // 安全获取最新帧
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
                AppendLog($"{DateTime.Now:HH:mm:ss} 当前帧已保存为 {fileName}");
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
                AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 暂无画面缓存，无法捕获背景");
                return;
            }

            // 捕获初始背景（使用缓存副本）
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

            AppendLog($"{DateTime.Now:HH:mm:ss} ✔ 已捕获初始背景图，开启异物检测");

            isDetectionEnabled = true;

            if (!isDetecting)
            {
                isDetecting = true;

                Task.Run(() =>
                {
                    while (isRunning && isDetecting)
                    {
                        Bitmap currentFrame = null;

                        // 安全获取最新帧
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

            btnOpenForeigndetection.Enabled = !isDetecting;
        }

        private void BtnCaluCRC_Click(object? sender, EventArgs e)
        {
            try
            {
                string input = textBox2.Text.Trim(); 
                if (string.IsNullOrWhiteSpace(input))
                {
                    AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 命令不能为空\r");
                    return;
                }

                string hexInput = input.Replace(" ", "");
                if (hexInput.Length % 2 != 0)
                {
                    AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 十六进制命令长度必须为偶数\r");
                    return;
                }

                // 转 byte[]
                byte[] command = new byte[hexInput.Length / 2];
                for (int i = 0; i < command.Length; i++)
                {
                    string hexByte = hexInput.Substring(i * 2, 2);
                    if (!byte.TryParse(hexByte, System.Globalization.NumberStyles.HexNumber, null, out command[i]))
                    {
                        AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 无效的十六进制字符：{hexByte}\r");
                        return;
                    }
                }

                // 计算 CRC
                byte[] crc = CalculateModbusCRC(command);

                // 拼接完整帧
                byte[] fullCommand = command.Concat(crc).ToArray();

                // 显示日志
                AppendLog($"{DateTime.Now:HH:mm:ss} ✅ 输入命令（不含CRC）：{BitConverter.ToString(command).Replace("-", " ")}\r");
                AppendLog($"{DateTime.Now:HH:mm:ss} ✅ 计算 CRC：{BitConverter.ToString(crc).Replace("-", " ")}\r");
                AppendLog($"{DateTime.Now:HH:mm:ss} ✅ 完整命令（含CRC）：{BitConverter.ToString(fullCommand).Replace("-", " ")}\r");
            }
            catch (Exception ex)
            {
                AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 命令处理失败：{ex.Message}\r");
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

                    // 锁住 brushPointsLock，确保访问 lines 列表安全
                    lock (brushPointsLock)
                    {
                        foreach (var line in lines)
                        {
                            g.DrawLine(new Pen(Color.Red, line.Thickness),
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

                    // 使用独立副本保存
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
                    AppendLog($"[{DateTime.Now:HH:mm:ss}] ✖ 无可用涂抹层文件");
                    return;
                }

                string latestFile = files.OrderByDescending(f => File.GetCreationTime(f)).First();
                Bitmap loadedLayer = new Bitmap(latestFile);

                lock (paintLayerLock)
                {
                    // 清空旧的涂层
                    drawingLayer?.Dispose();
                    drawingLayer = new Bitmap(loadedLayer); // 用最新涂层替换
                    lines.Clear(); // 可选：清空原有线条列表
                }

                loadedLayer.Dispose();

                pictureBox1.Invalidate(); // 刷新显示
                AppendLog($"[{DateTime.Now:HH:mm:ss}] ✔ 已加载最新涂抹层: {latestFile}");
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] ✖ 加载涂抹层失败: {ex.Message}");
            }
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

                    AppendLog($"{DateTime.Now:HH:mm:ss} ✔ 已加载最新涂抹层: {latestFile}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"{DateTime.Now:HH:mm:ss} ✖ 加载涂抹层失败: {ex.Message}");
            }
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
                    using (Pen pen = new Pen(Color.Blue, brushSize))
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
                using (Pen pen = new Pen(Color.Blue, brushSize))
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
                    // 左
                    byte[] cmdMin = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, servoMin);
                    modbuserialPortControl.serialPort.Write(cmdMin, 0, cmdMin.Length);
                    await Task.Delay(1000);

                    if ((DateTime.Now - startTime).TotalSeconds >= totalDuration) break;

                    // 右
                    byte[] cmdMax = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, servoMax);
                    modbuserialPortControl.serialPort.Write(cmdMax, 0, cmdMax.Length);
                    await Task.Delay(1000);
                }

                AppendLog($"[{DateTime.Now:HH:mm:ss}] ✔ 舵机完成动作，范围：{minAngle}-{maxAngle}°，总时长上限：{totalDuration}s");
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] ✖ 舵机动作失败: {ex.Message}");
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
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] 💧 舵机动作完成，水泵已关闭");

                        byte[] cmdHome = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, 0x0087);
                        modbuserialPortControl.serialPort.Write(cmdHome, 0, cmdHome.Length);
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] 🔁 舵机已回初始位置");
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] ✖ 关闭水泵失败: {ex.Message}");
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
                    AppendLog($"[{DateTime.Now:HH:mm:ss}] 📷 已使用舵机动作后的画面更新底图");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] ✖ 舵机结束后更新底图失败: {ex.Message}");
            }
        }
        #endregion
    }

}


