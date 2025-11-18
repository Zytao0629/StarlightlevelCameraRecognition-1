using System;
using System.IO.Ports;
using System.Threading;

public class ModbusRTU
{
    public SerialPort serialPort;
    public event Action<string> OnStatusMessage;
    public byte SlaveAddress { get; set; } = 1;

    public ModbusRTU()
    {
        serialPort = new SerialPort
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };
    }

    #region 串口操作
    public bool OpenPort(string portName, int baudrate = 115200, Parity parity = Parity.Even,
                        int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        try
        {
            if (serialPort == null)
                serialPort = new SerialPort();

            if (serialPort.IsOpen)
                serialPort.Close();

            serialPort.PortName = portName;
            serialPort.BaudRate = baudrate;
            serialPort.Parity = parity;
            serialPort.DataBits = dataBits;
            serialPort.StopBits = stopBits;

            serialPort.Open();
            OnStatusMessage?.Invoke($"✅ 串口 {portName} 已打开，准备通讯");
            return true;
        }
        catch (Exception ex)
        {
            OnStatusMessage?.Invoke($"❌ 打开串口失败: {ex.Message}");
            return false;
        }
    }

    public void ClosePort()
    {
        if (serialPort.IsOpen)
        {
            serialPort.Close();
            OnStatusMessage?.Invoke("🔌 串口已关闭");
        }
    }

    private bool CheckPortOpen()
    {
        if (serialPort == null || !serialPort.IsOpen)
        {
            OnStatusMessage?.Invoke("⚠️ 串口未打开");
            return false;
        }
        return true;
    }
    #endregion

    #region 命令构造（Command Builder）
    /// <summary>
    /// 构造通用读命令（0x01/0x03/0x04）
    /// </summary>
    private byte[] BuildReadCommand(byte function, ushort startAddress, ushort count)
    {
        byte[] cmd = new byte[6];
        cmd[0] = SlaveAddress;
        cmd[1] = function;
        cmd[2] = (byte)(startAddress >> 8);
        cmd[3] = (byte)(startAddress & 0xFF);
        cmd[4] = (byte)(count >> 8);
        cmd[5] = (byte)(count & 0xFF);
        return AppendCRC(cmd);
    }

    /// <summary>
    /// 构造单个写命令（0x05或0x06）
    /// </summary>
    public byte[] BuildWriteSingleCommand(byte function, ushort address, ushort value)
    {
        byte[] cmd = new byte[6];
        cmd[0] = SlaveAddress;
        cmd[1] = function;
        cmd[2] = (byte)(address >> 8);
        cmd[3] = (byte)(address & 0xFF);
        cmd[4] = (byte)(value >> 8);
        cmd[5] = (byte)(value & 0xFF);
        return AppendCRC(cmd);
    }
    #endregion

    #region 发送与接收（核心）
    private byte[] SendAndReceive(byte[] command, int expectedBytes)
    {
        try
        {
            serialPort.DiscardInBuffer();
            serialPort.Write(command, 0, command.Length);
            //OnStatusMessage?.Invoke($"➡️ 发送命令：{BitConverter.ToString(command).Replace("-", " ")}");

            int wait = 0;
            while (serialPort.BytesToRead < expectedBytes && wait < 300)
            {
                Thread.Sleep(10);
                wait += 10;
            }

            if (serialPort.BytesToRead == 0)
            {
                OnStatusMessage?.Invoke("⚠️ 未收到设备响应");
                return null;
            }

            byte[] response = new byte[serialPort.BytesToRead];
            serialPort.Read(response, 0, response.Length);
           // OnStatusMessage?.Invoke($"⬅️ 收到响应：{BitConverter.ToString(response).Replace("-", " ")}");

            // CRC验证
            if (!ValidateCRC(response))
            {
                OnStatusMessage?.Invoke("⚠️ CRC校验失败");
                return null;
            }

            return response;
        }
        catch (TimeoutException)
        {
            OnStatusMessage?.Invoke("⚠️ 通讯超时");
            return null;
        }
        catch (Exception ex)
        {
            OnStatusMessage?.Invoke($"⚠️ 发送接收错误: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region 功能实现（API层）
    public void ReadCoils(ushort start, ushort count)
    {
        if (!CheckPortOpen()) return;
        byte[] cmd = BuildReadCommand(0x01, start, count);
        byte[] resp = SendAndReceive(cmd, 5 + (count + 7) / 8 + 2);
        if (resp == null) return;
        ParseCoilsResponse(resp, count);
    }

    public bool WriteCoil(ushort addr, bool on)
    {
        if (!CheckPortOpen()) return false;
        byte[] cmd = BuildWriteSingleCommand(0x05, addr, on ? (ushort)0xFF00 : (ushort)0x0000);
        byte[] resp = SendAndReceive(cmd, 8);
        return resp != null;
    }

    public void ReadHoldingRegisters(ushort start, ushort count)
    {
        if (!CheckPortOpen()) return;
        byte[] cmd = BuildReadCommand(0x03, start, count);
        byte[] resp = SendAndReceive(cmd, 5 + count * 2);
        if (resp == null) return;
        ParseRegistersResponse(resp, count);
    }

    public bool WriteHoldingRegister(ushort addr, ushort value)
    {
        if (!CheckPortOpen()) return false;
        byte[] cmd = BuildWriteSingleCommand(0x06, addr, value);
        byte[] resp = SendAndReceive(cmd, 8);
        return resp != null;
    }

    public void ReadInputRegisters(ushort start, ushort count)
    {
        if (!CheckPortOpen()) return;
        byte[] cmd = BuildReadCommand(0x04, start, count);
        byte[] resp = SendAndReceive(cmd, 5 + count * 2);
        if (resp == null) return;
        ParseRegistersResponse(resp, count, true);
    }
    #endregion

    #region 响应解析
    private void ParseCoilsResponse(byte[] resp, ushort count)
    {
        byte dataLen = resp[2];
        for (int i = 0; i < count; i++)
        {
            int byteIndex = 3 + (i / 8);
            int bitIndex = i % 8;
            bool on = (resp[byteIndex] & (1 << bitIndex)) != 0;
            OnStatusMessage?.Invoke($"线圈[{i}] = {(on ? "ON" : "OFF")}");
        }
    }

    private void ParseRegistersResponse(byte[] resp, ushort count, bool isInput = false)
    {
        for (int i = 0; i < count; i++)
        {
            int index = 3 + i * 2;
            ushort value = (ushort)((resp[index] << 8) | resp[index + 1]);
            OnStatusMessage?.Invoke($"{(isInput ? "输入" : "保持")}寄存器[{i}] = {value}");
        }
    }
    #endregion

    #region CRC工具
    private byte[] AppendCRC(byte[] data)
    {
        ushort crc = CalculateCrc(data);
        byte[] result = new byte[data.Length + 2];
        Array.Copy(data, result, data.Length);
        result[^2] = (byte)(crc & 0xFF);
        result[^1] = (byte)(crc >> 8);
        return result;
    }

    private bool ValidateCRC(byte[] data)
    {
        if (data.Length < 4) return false;
        ushort recvCrc = (ushort)((data[^1] << 8) | data[^2]);
        byte[] noCrc = new byte[data.Length - 2];
        Array.Copy(data, noCrc, noCrc.Length);
        return CalculateCrc(noCrc) == recvCrc;
    }

    private ushort CalculateCrc(byte[] data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (ushort)((crc & 1) != 0 ? (crc >> 1) ^ 0xA001 : crc >> 1);
        }
        return crc;
    }
    #endregion



    public void Initial()
    {
        if (!CheckPortOpen())
        {
            OnStatusMessage?.Invoke("✖ 串口未打开，无法初始化设备");
            return;
        }

        OnStatusMessage?.Invoke("🔁开始初始化：舵机和高低机回到初始位置...");

        try
        {
            //  通讯测试
          
            Thread.Sleep(50);
            OnStatusMessage?.Invoke("✔ 通讯测试通过，从机在线");

            //舵机回初始（寄存器0x0000, 初始0085）
            byte[] servoInitCmd = BuildWriteSingleCommand(0x06, 0x0000, 0x0085);
            serialPort.Write(servoInitCmd, 0, servoInitCmd.Length);
            Thread.Sleep(200); // 给舵机动作时间
            OnStatusMessage?.Invoke("✔ 舵机已回初始位置");

            // 高低机回初始（寄存器0x0001, 初始0055）
            byte[] liftInitCmd = BuildWriteSingleCommand(0x06, 0x0001, 0x0055);
            serialPort.Write(liftInitCmd, 0, liftInitCmd.Length);
            Thread.Sleep(200); // 给高低机动作时间
            OnStatusMessage?.Invoke("✔ 高低机已回初始位置");

            OnStatusMessage?.Invoke("✔ 初始化完成，设备处于安全初始状态");
        }
        catch
        {
            OnStatusMessage?.Invoke("✖ 初始化异常，请检查设备连接");
        }
    }


}
