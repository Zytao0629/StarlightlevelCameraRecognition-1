using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.IO.Ports;
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
        Button btnChangePanorama;

        #region 全局变量
        private VideoCapture capture;  //相机对象，
        private bool isRunning = false; //相机是否打开
        private CancellationTokenSource cts;
        private ModbusRTU modbuserialPortControl;

        private Bitmap backgroundImage;  // 存储背景图
        private bool isBackgroundCaptured = false;  // 是否已捕获背景

        private bool isDrawing = false;              // 是否正在绘制
        private List<DrawingPoint> brushPoints = new List<DrawingPoint>(); // 所有涂抹的像素点
        private int brushSize = 50;                  // 画笔大小，可根据需要调整
        private float overlapThreshold = 0.3f;       // 异物红框和涂抹区域重叠阈值


        private List<(Rectangle rect, DateTime detectTime)> foreignObjects = new();
        private int highlightDuration = 20;  // 异物高亮时间
        private int diffThreshold = 70;
        private int regionMinSize = 30;
        private int cleanFrameCount = 0;
        private bool isDetecting = false; // 是否正在异物检测
        private bool isDetectionEnabled = true;  // 控制异物检测是否生效
        private DateTime lastBackgroundSaveTime = DateTime.MinValue;

        private double frameWidth;
        private double frameHeight;


        private readonly object foreignObjectsLock = new object();
        private readonly object brushPointsLock = new object();


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
            btnChangePanorama = button7;

            btnOpenDevice.Click += BtnOpenDevice_Click;
            btnCloseDevice.Click += BtnCloseDevice_Click;
            btnSendCommand.Click += BtnSendCommand_Click;
            btnSnapImage.Click += BtnSnapImage_Click;
            btnOpenForeigndetection.Click += BtnOpenForeigndetection_Click;
            btnCaluCRC.Click += BtnCaluCRC_Click;
            btnChangePanorama.Click += BtnChangePanorama_Click;

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
        }

        

        //事件订阅
        private void ModbuserialPortControl_OnStatusMessage(string message)
        {
            AppendLog($"{DateTime.Now:HH:mm:ss} {message}");
        }

        private void AppendLog(string message)
        {
            if (richTextBox1.InvokeRequired)
            {
                richTextBox1.Invoke(new Action(() => AppendLog(message)));
            }
            else
            {
                richTextBox1.AppendText(message + "\r");
                richTextBox1.ScrollToCaret();
            }
        }








        #region blob的寻找监测高亮
        // 寻找blob
        private List<Rectangle> FindAllDifferenceRegions(Bitmap background, Bitmap current)
        {
            if (background == null || current == null || background.Size != current.Size)
                return new List<Rectangle>();

            bool[,] differenceMask = new bool[current.Width, current.Height];

            for (int y = 0; y < current.Height; y++)
            {
                for (int x = 0; x < current.Width; x++)
                {
                    Color bgColor = background.GetPixel(x, y);
                    Color currColor = current.GetPixel(x, y);

                    int bgBrightness = (bgColor.R + bgColor.G + bgColor.B) / 3;
                    int currBrightness = (currColor.R + currColor.G + currColor.B) / 3;
                    int diff = Math.Abs(bgBrightness - currBrightness);

                    differenceMask[x, y] = diff > diffThreshold;
                }
            }

            List<Rectangle> regions = new List<Rectangle>();
            bool[,] visited = new bool[current.Width, current.Height];

            for (int y = 0; y < current.Height; y++)
            {
                for (int x = 0; x < current.Width; x++)
                {
                    if (differenceMask[x, y] && !visited[x, y])
                    {
                        int minX = x, minY = y, maxX = x, maxY = y;
                        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();
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

                                if (nx >= 0 && nx < current.Width && ny >= 0 && ny < current.Height &&
                                    differenceMask[nx, ny] && !visited[nx, ny])
                                {
                                    visited[nx, ny] = true;
                                    queue.Enqueue((nx, ny));
                                }
                            }
                        }

                        int regionWidth = maxX - minX;
                        int regionHeight = maxY - minY;
                        if (regionWidth > regionMinSize && regionHeight > regionMinSize)
                        {
                            regions.Add(new Rectangle(minX, minY, regionWidth, regionHeight));
                        }
                    }
                }
            }

            return regions;
        }

        //检测并跟踪移动blob
        private void DetectAndTrackForeignObject(Bitmap currentFrame)
        {
            if (currentFrame == null) return;
            if (!isBackgroundCaptured || backgroundImage == null)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] ⚠️ 背景图未捕获");
                currentFrame.Dispose();
                return;
            }

            try
            {
                List<Rectangle> newRegions = FindAllDifferenceRegions(backgroundImage, currentFrame);

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
                        MoveToObject(obj); // 触发舵机旋转
                        this.Invoke(() => AppendLog($"[{DateTime.Now:HH:mm:ss}] ✅ 异物占比达标，执行移动动作\r"));
                    }



                    // 保存异物画面
                    string resultFolder = @"D:\DetectionResultScreen";
                    Directory.CreateDirectory(resultFolder);
                    string resultPath = Path.Combine(resultFolder, $"异物画面_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    currentFrame.Save(resultPath, System.Drawing.Imaging.ImageFormat.Png);

                    
                }
                else
                {
                    // 无异物 → 计数 + 更新底图（达到连续无异物帧数时）
                    cleanFrameCount++;
                    if (cleanFrameCount >= 120)
                    {
                        UpdateBackgroundImage(currentFrame);
                        cleanFrameCount = 0; // 更新底图后计数归零
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] 🔄 底图已更新");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] DetectAndTrackForeignObject 异常: {ex.Message}");
            }
            finally
            {
                currentFrame.Dispose();
            }
        }

        // 绘制异物高亮标记
        private void DrawForeignObjectHighlight(Graphics g)
        {
            // 筛选出未超时的blob
            var activeObjects = foreignObjects
                .Where(obj => (DateTime.Now - obj.detectTime).TotalSeconds < highlightDuration)
                .ToList();

            // 绘制每个blob
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
            foreignObjects = activeObjects;
        }
        //计算占比
        private float CalculateOverlap(Rectangle foreignObject)
        {
            if (brushPoints.Count == 0)
                return 0f;

            // 计算涂抹区域中有多少像素点落在异物区域内
            int overlapCount = brushPoints.Count(p => foreignObject.Contains(p));

            // 计算异物所占比例：异物像素 / 涂抹区域像素
            return (float)overlapCount / brushPoints.Count;
        }
        // 实时更新底图
        private void UpdateBackgroundImage(Bitmap currentFrame)
        {
            if (backgroundImage == null)
                return;

            float alpha = 0.95f; // 原底图权重
            float beta = 1f - alpha;// 当前帧权重

            for (int y = 0; y < backgroundImage.Height; y++)
            {
                for (int x = 0; x < backgroundImage.Width; x++)
                {
                    Color bgColor = backgroundImage.GetPixel(x, y);
                    Color currColor = currentFrame.GetPixel(x, y);
                    int blendedR = (int)(bgColor.R * alpha + currColor.R * beta);
                    int blendedG = (int)(bgColor.G * alpha + currColor.G * beta);
                    int blendedB = (int)(bgColor.B * alpha + currColor.B * beta);
                    backgroundImage.SetPixel(x, y, Color.FromArgb(blendedR, blendedG, blendedB));
                }
            }



            // 每4秒保存一次底图（测试用）
            if ((DateTime.Now - lastBackgroundSaveTime).TotalSeconds >= 4)
            {
                try
                {
                    string dir = @"D:\BackgroundTestGet";
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    string path = $@"{dir}\底图{DateTime.Now:yyyyMMddHHmmss}.png";
                    backgroundImage.Save(path);
                    lastBackgroundSaveTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    AppendLog($"保存背景图出错：{ex.Message}");
                }
            }
        }
        #endregion


        #region 按钮事件
        private void BtnOpenDevice_Click(object? sender, EventArgs e)
        {
            if (isRunning) return;

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
            toolStripStatusLabel1.ForeColor = Color.Green;
            toolStripStatusLabel2.ForeColor = Color.Green;
            toolStripStatusLabel3.ForeColor = Color.Green;

            isRunning = true;

            Task.Run(() =>
            {
                while (isRunning && capture.IsOpened())
                {
                    using var frame = new Mat();
                            capture.Read(frame);
                    if (frame.Empty()) continue;

                    var bmp = BitmapConverter.ToBitmap(frame);
                    Bitmap bmpClone = (Bitmap)bmp.Clone();
                    bmp.Dispose();

                    pictureBox1.Invoke(() =>
                    {
                        var old = pictureBox1.Image;
                        pictureBox1.Image = bmpClone;
                        old?.Dispose();
                    });

                    Thread.Sleep(30);
                }
            });



            //通讯
            string selectedPort = comboBoxPorts.SelectedItem.ToString();
            bool ok = modbuserialPortControl.OpenPort(selectedPort);
            if (ok)
            {
                toolStripStatusLabel4.Text = "通讯已建立✅";
                toolStripStatusLabel4.ForeColor = Color.Green;
                modbuserialPortControl.Initial();
            }
            else
            {
                toolStripStatusLabel4.Text = "通讯建立失败⚠️";
                toolStripStatusLabel4.ForeColor = Color.Red;
                modbuserialPortControl.Initial();
            }

            btnCloseDevice.Enabled = true;
            btnSnapImage.Enabled = true;
            btnSendCommand.Enabled = true;
        }
        private void BtnCloseDevice_Click(object? sender, EventArgs e)
        {
            if (!isRunning) return;

            isRunning = false;
            cts?.Cancel();
            capture?.Release();
            capture = null;

            // 清空 PictureBox
            pictureBox1.Image?.Dispose();
            pictureBox1.Image = null;

            // 清零 StatusStrip 后缀
            toolStripStatusLabel1.Text = "";
            toolStripStatusLabel2.Text = "";
            toolStripStatusLabel3.Text = "";

            modbuserialPortControl.ClosePort();
            toolStripStatusLabel4.Text = "通讯已断开⚠️";
            toolStripStatusLabel4.ForeColor = Color.Red;

            btnSnapImage.Enabled = false;
            btnSendCommand.Enabled = false;

        }
        private void BtnSendCommand_Click(object? sender, EventArgs e)
        {
            try
            {
                string input = textBox1.Text.Trim();
                if (string.IsNullOrWhiteSpace(input))
                {
                    AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 命令不能为空\r");
                    return;
                }

                if (!modbuserialPortControl.serialPort.IsOpen)
                {
                    AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 串口未打开\r");
                    return;
                }

                // 清空接收缓冲区，避免旧数据干扰
                modbuserialPortControl.serialPort.DiscardInBuffer();

                // 1. 处理格式化命令 C/R/I
                if (input.Contains(','))
                {
                    string[] parts = input.Split(',');
                    if (parts.Length != 3)
                    {
                        AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 指令格式错误\r");
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
                            AppendLog($"{DateTime.Now:HH:mm:ss} ➡️ 发送线圈命令：{input}\r");
                            Thread.Sleep(50);
                            modbuserialPortControl.ReadCoils(address, 1);
                        }
                        else if (ushort.TryParse(param, out ushort count))
                        {
                            modbuserialPortControl.ReadCoils(address, count);
                            AppendLog($"{DateTime.Now:HH:mm:ss} ➡️ 发送读线圈命令：{input}\r");
                        }
                        else
                        {
                            AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 参数错误\r");
                        }
                    }
                    // 保持寄存器
                    else if (type == "R")
                    {
                        if (ushort.TryParse(param, out ushort value))
                        {
                            bool success = modbuserialPortControl.WriteHoldingRegister(address, value);
                            AppendLog($"{DateTime.Now:HH:mm:ss} ➡️ 发送写寄存器命令：{input}\r");
                            Thread.Sleep(50);
                            modbuserialPortControl.ReadHoldingRegisters(address, 1);
                        }
                        else if (ushort.TryParse(param, out ushort count))
                        {
                            modbuserialPortControl.ReadHoldingRegisters(address, count);
                            AppendLog($"{DateTime.Now:HH:mm:ss} ➡️ 发送读寄存器命令：{input}\r");
                        }
                        else
                        {
                            AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 参数错误\r");
                        }
                    }
                    // 输入寄存器
                    else if (type == "I")
                    {
                        if (ushort.TryParse(param, out ushort count))
                        {
                            modbuserialPortControl.ReadInputRegisters(address, count);
                            AppendLog($"{DateTime.Now:HH:mm:ss} ➡️ 发送读输入寄存器命令：{input}\r");
                        }
                        else
                        {
                            AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 参数错误\r");
                        }
                    }
                    else
                    {
                        AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 未知指令类型\r");
                    }
                }
                else
                {
                    // 2. 原始十六进制命令
                    string hex = input.Replace(" ", "");
                    if (hex.Length % 2 != 0)
                    {
                        AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 十六进制长度必须为偶数\r");
                        return;
                    }

                    byte[] command = new byte[hex.Length / 2];
                    for (int i = 0; i < command.Length; i++)
                    {
                        command[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                    }

                    modbuserialPortControl.serialPort.Write(command, 0, command.Length);
                    AppendLog($"{DateTime.Now:HH:mm:ss} ➡️ 发送原始命令：{BitConverter.ToString(command).Replace("-", " ")}\r");

                    // 等待响应（可根据设备调整等待时间）
                    Thread.Sleep(100);
                    int bytesToRead = modbuserialPortControl.serialPort.BytesToRead;
                    if (bytesToRead > 0)
                    {
                        byte[] response = new byte[bytesToRead];
                        modbuserialPortControl.serialPort.Read(response, 0, bytesToRead);
                        AppendLog($"{DateTime.Now:HH:mm:ss} ⬅️ 收到响应：{BitConverter.ToString(response).Replace("-", " ")}\r");
                    }
                    else
                    {
                        AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 未收到响应\r");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 执行异常：{ex.Message}\r");
            }
        }
        private void BtnSnapImage_Click(object? sender, EventArgs e)
        {
            if (capture == null || !capture.IsOpened() || !isRunning)
            {
                this.richTextBox1.AppendText("摄像头未打开或没有图像可捕获！");
                return;
            }

            using (var frame = new Mat())
            {
                // 读取当前帧
                capture.Read(frame);
                if (frame.Empty())
                {
                    AppendLog("捕获失败，当前帧为空！");
                    return;
                }

                // 转 Bitmap
                var bmp = BitmapConverter.ToBitmap(frame);

                // 设置保存文件夹
                string SnapFilePath = @"D:\SnapImage";
                if (!Directory.Exists(SnapFilePath))
                    Directory.CreateDirectory(SnapFilePath);

                // 保存
                string fileName = Path.Combine(SnapFilePath, $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bmp.Save(fileName, System.Drawing.Imaging.ImageFormat.Png);

                AppendLog($"{DateTime.Now:HH:mm:ss}当前帧已保存为 {fileName}");
            }
        }
        private void BtnOpenForeigndetection_Click(object? sender, EventArgs e)
        {
            if (capture == null || !capture.IsOpened() || !isRunning)
            {
                AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 摄像头未打开，无法捕获背景");
                return;
            }

            // 捕获初始背景
            using (var frame = new Mat())
            {
                capture.Read(frame);
                if (frame.Empty())
                {
                    AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 捕获背景失败，当前帧为空");
                    return;
                }

                backgroundImage = BitmapConverter.ToBitmap(frame);
                isBackgroundCaptured = true;

                string folderPath = @"D:\BackgroundIntialImage";
                Directory.CreateDirectory(folderPath);
                string filePath = Path.Combine(folderPath, $"{DateTime.Now:yyyyMMdd_HHmmss} 初始背景图.png");
                backgroundImage.Save(filePath);

                AppendLog($"{DateTime.Now:HH:mm:ss} ✅ 已捕获初始背景图，开启异物检测");
            }

            //if (isDetecting) return;
            //isDetecting = true;

            //Task.Run(() =>
            //{
            //    while (isRunning && capture != null && capture.IsOpened() && isDetecting)
            //    {
            //        using var frame = new Mat();
            //        capture.Read(frame);
            //        if (frame.Empty()) continue;

            //        using var currentFrame = BitmapConverter.ToBitmap(frame);
            //        DetectAndTrackForeignObject(currentFrame);

            //        Thread.Sleep(30);
            //    }
            //});
            //
            isDetectionEnabled = true;   // 确保检测开启
            if (!isDetecting)
            {
                isDetecting = true;

                Task.Run(() =>
                {
                    while (isRunning && capture != null && capture.IsOpened() && isDetecting)
                    {
                        using var frame = new Mat();
                        capture.Read(frame);
                        if (frame.Empty()) continue;

                        using var currentFrame = BitmapConverter.ToBitmap(frame);

                        if (isDetectionEnabled)   // 只有启用检测才执行
                            DetectAndTrackForeignObject(currentFrame);

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
                string input = textBox2.Text.Trim(); // 注意改成 textbox2
                if (string.IsNullOrWhiteSpace(input))
                {
                    AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 命令不能为空\r");
                    return;
                }

                // 去掉空格，保证是连续的16进制字符
                string hexInput = input.Replace(" ", "");
                if (hexInput.Length % 2 != 0)
                {
                    AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 十六进制命令长度必须为偶数\r");
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

            //// 发送
            //if (modbuserialPortControl.serialPort.IsOpen)
            //{
            //    modbuserialPortControl.serialPort.Write(fullCommand, 0, fullCommand.Length);
            //    Thread.Sleep(100); // 等待响应
            //    int bytesToRead = modbuserialPortControl.serialPort.BytesToRead;
            //    if (bytesToRead > 0)
            //    {
            //        byte[] response = new byte[bytesToRead];
            //        modbuserialPortControl.serialPort.Read(response, 0, bytesToRead);
            //        richTextBox1.AppendText($"{DateTime.Now:HH:mm:ss} ⬅️ 收到响应：{BitConverter.ToString(response).Replace("-", " ")}\r");
            //    }
            //    else
            //    {
            //        richTextBox1.AppendText($"{DateTime.Now:HH:mm:ss} ⚠️ 未收到响应\r");
            //    }
            //}
            //    else
            //    {
            //        richTextBox1.AppendText($"{DateTime.Now:HH:mm:ss} ⚠️ 串口未打开，无法发送\r");
            //    }
            //}
            //catch (Exception ex)
            //{
            //    richTextBox1.AppendText($"{DateTime.Now:HH:mm:ss} ⚠️ 命令执行失败：{ex.Message}\r");
            //}

            catch (Exception ex)
            {
                AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 命令处理失败：{ex.Message}\r");
            }
        }
        private void BtnChangePanorama_Click(object? sender, EventArgs e)
        {
            isDetectionEnabled = false;  // 暂停检测
            btnOpenForeigndetection.Enabled = true; 
            AppendLog($"{DateTime.Now:HH:mm:ss} ⚠️ 异物检测已暂停，可移动摄像头或更换背景");
        }



        //计算 Modbus RTU CRC16 校验码（多项式 0xA001）
        /// </summary>
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

            // 返回低字节在前，高字节在后（Modbus RTU标准）
            return new byte[] { (byte)(crc & 0xFF), (byte)(crc >> 8) };
        }


        #endregion

        #region 涂层
        private void PictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (isDrawing && brushPoints.Count > 0) // 仅在绘制时显示
            {
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(100, Color.Green)))
                {
                    foreach (var p in brushPoints)
                        e.Graphics.FillRectangle(brush, p.X, p.Y, 1, 1);
                }
            }

            // 2. 绘制异物高亮
            DrawForeignObjectHighlight(e.Graphics);
        }
        private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                isDrawing = true;
        }
        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing)
            {
                for (int dx = -brushSize / 2; dx <= brushSize / 2; dx++)
                {
                    for (int dy = -brushSize / 2; dy <= brushSize / 2; dy++)
                    {
                        int x = e.X + dx;
                        int y = e.Y + dy;
                        if (x >= 0 && y >= 0 && x < pictureBox1.Width && y < pictureBox1.Height)
                            brushPoints.Add(new DrawingPoint(x, y));
                    }
                }

                pictureBox1.Invalidate();
            }
        }
        private void PictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                isDrawing = false;

            pictureBox1.Invalidate(); // 立即刷新，移除画面显示
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



        #region 异物中心坐标转换为舵机数值
        // 只控制旋转舵机的新版本
        void MoveToObject(Rectangle objRect)
        {

            if (modbuserialPortControl.serialPort == null || !modbuserialPortControl.serialPort.IsOpen) return;

            // 舵机旋转极限值（按实际机械调整）
            ushort servoMin = 0x0005;
            ushort servoMax = 0x1010;

            // 每次旋转一圈，可分两段，先到左极限再到右极限
            try
            {
                // 舵机左极限
                byte[] cmdLeft = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, servoMin);
                modbuserialPortControl.serialPort.DiscardInBuffer();
                modbuserialPortControl.serialPort.Write(cmdLeft, 0, cmdLeft.Length);
                Thread.Sleep(1800); // 给舵机动作时间

                // 舵机右极限
                byte[] cmdRight = modbuserialPortControl.BuildWriteSingleCommand(0x06, 0x0000, servoMax);
                modbuserialPortControl.serialPort.Write(cmdRight, 0, cmdRight.Length);
                Thread.Sleep(1800);

                AppendLog($"[{DateTime.Now:HH:mm:ss}] ✅ 舵机旋转一圈完成");
            }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] ⚠️ 舵机旋转失败: {ex.Message}");
            }

        }

        #endregion
    }


}
