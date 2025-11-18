using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StarlightlevelCameraRecognition
{
    internal class SerialPortControl
    {

        public SerialPort serialPort;

        // 事件
        public event Action<string> OnStatusMessage;

        public SerialPortControl()
        {
            serialPort = new SerialPort();
        }

        /// <summary>
        /// 打开串口
        /// </summary>
        public bool OpenPort(string portName, int baudrate = 9600, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
        {
            try
            {
                if (serialPort.IsOpen)
                    serialPort.Close();

                serialPort.PortName = portName;
                serialPort.BaudRate = baudrate;
                serialPort.Parity = parity;
                serialPort.DataBits = dataBits;
                serialPort.StopBits = stopBits;

                serialPort.Open();
                OnStatusMessage?.Invoke($"串口 {portName} 已打开,通讯已建立");
                return true;
            }
            catch (Exception ex)
            {
                OnStatusMessage?.Invoke($"打开串口失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        public void ClosePort()
        {
            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                    OnStatusMessage?.Invoke($"串口已关闭，通讯已断开");
                }
            }
            catch (Exception ex)
            {
                OnStatusMessage?.Invoke($"关闭串口失败: {ex.Message}");
            }
        }

        public void SendCommand(byte relayId, byte commandType)
        {
            byte startByte = 0xA0; //起始位
            byte addressByte = relayId;  //地址位
            byte dataByte = commandType; //数据位
            byte checkByte = (byte)((startByte + addressByte + dataByte) % 0x100); //校验位

            byte[] cmd = new byte[] { startByte, addressByte, dataByte, checkByte };//完整命令

            if (serialPort == null || !serialPort.IsOpen)
            {
                OnStatusMessage?.Invoke("⚠️ 串口未打开，无法发送命令");
                return;
            }

            serialPort.Write(cmd, 0, cmd.Length);//命令 0位开始  发送长度为整个命令长
            OnStatusMessage?.Invoke($"➡️ 发送命令：{BitConverter.ToString(cmd).Replace("-", " ")}");
        }

        /// <summary>
        /// 接收带反馈命令的返回
        /// </summary>
        public void ReceiveCommand()
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                OnStatusMessage?.Invoke("⚠️ 串口未打开，无法接收反馈");
                return;
            }

            try
            {
                int bytesToRead = serialPort.BytesToRead; //当前串口读到的命令长度
                if (bytesToRead < 4)
                    return; // 没有完整反馈

                byte[] buffer = new byte[bytesToRead];
                serialPort.Read(buffer, 0, bytesToRead); //从0开始 读取完整命令长度


                // 处理一次命令
                for (int i = 0; i + 3 < buffer.Length; i += 4)
                {
                    byte startByte = buffer[i]; //起始位
                    byte addressByte = buffer[i + 1]; //地址位
                    byte dataByte = buffer[i + 2];  //数据位
                    byte checkByte = buffer[i + 3]; //校验位

                    // 校验
                    byte expectedCheck = (byte)((startByte + addressByte + dataByte) % 0x100);
                    if (checkByte != expectedCheck)
                    {
                        OnStatusMessage?.Invoke($"⚠️反馈校验失败: {BitConverter.ToString(buffer, i, 4)}");
                        continue;
                    }

                    string status = dataByte == 0x01 ? "开" : dataByte == 0x00 ? "关" : "未知";
                    OnStatusMessage?.Invoke($"⬅收到反馈：{BitConverter.ToString(buffer, i, 4)} 状态：{status}");
                }
            }
            catch (Exception ex)
            {
                OnStatusMessage?.Invoke($"⚠️ 接收数据异常: {ex.Message}");
            }
        }

        // 初始化
        public void Initial()
        {
            if (!serialPort.IsOpen)
            {
                OnStatusMessage?.Invoke("⚠️串口未打开，无法初始化继电器状态");
                return;
            }

            OnStatusMessage?.Invoke("🔄开始初始化继电器状态...");

            for (byte relayId = 1; relayId <= 4; relayId++)
            {

                SendCommand(relayId, 0x00);
                Thread.Sleep(100);
                ReceiveCommand();
            }

            OnStatusMessage?.Invoke("✅继电器状态初始化完成");
        }

        /// <summary>
        /// 发送带反馈命令，并自动接收反馈
        /// </summary>
        public void SendCommandWithFeedback(byte relayId, byte commandType)
        {
            // 发送命令
            SendCommand(relayId, commandType);
            System.Threading.Thread.Sleep(50);
            // 接收反馈
            ReceiveCommand();
        }
    }
}



