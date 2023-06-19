using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TCPClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TCPClient tCPClient = new TCPClient(IPAddress.Parse("127.0.0.1"), 10027, new ReceiveFilter(new FilterInfo(FrameFilterType.FixedHeaderReceiveLengthFilter, 3, 3, 3)));
            tCPClient.Open();
            tCPClient.IsConnectedAction = (isConnected) =>
            {
                Console.WriteLine($"连接状态为{isConnected}");
                if (isConnected)
                {
                    //打印时分秒毫秒
                    Console.WriteLine($"连接时间为{DateTime.Now.ToString("HH:mm:ss:fff")}");
                    //打印本地地址与端口,打印远程地址与端口
                    Console.WriteLine($"本地地址与端口为{tCPClient.LocalEndPoint}");
                    Console.WriteLine($"远程地址与端口为{tCPClient.RemoteEndPoint}");
                }
                else
                {
                    Console.WriteLine($"断开连接时间为{DateTime.Now.ToString("HH:mm:ss:fff")}");
                }

            }; Console.WriteLine($"本地地址与端口为{tCPClient.LocalEndPoint}");
            Console.WriteLine($"远程地址与端口为{tCPClient.RemoteEndPoint}");
            tCPClient.OnReceive = (data) =>
            {

                Console.WriteLine($"收到数据：{BitConverter.ToString(data.ToArray())} \r\n 中文转换:{Encoding.Default.GetString(data.ToArray())}");
                // bRTCPClient.Send(data.ToArray());
            };
            while (true)
            {
                Console.ReadKey();
                Console.WriteLine("发送然后读取开始");
                var datas = tCPClient.SendAndRead(new byte[] { 01, 2, 3, 4, 5, 6 }, 50, 5000);
                if (datas != null)
                {
                    Console.WriteLine(BitConverter.ToString(datas));
                    Console.WriteLine(Encoding.Default.GetString(datas));
                }
                Console.WriteLine("发送然后读取结束");

            }

        }
    }
    public interface ICommunication
    {
        /// <summary>
        /// 连接
        /// </summary>
        /// <param name="connunicationTimeOut">通信连接超时时间(最大心跳间隔),超过次时间没有收到会认为一定断开连接单位s</param>
        /// <returns></returns>
        bool Open(int connunicationTimeOut = 0);
        bool Close();
        bool Send(byte[] data);
        List<byte> Receive(int span = 0);
        Action<List<byte>> OnReceive { get; set; }
        bool IsConnected { get; }
        Action<bool> IsConnectedAction { get; set; }
        ///// <summary>

        //bool IsOpen();
        /// <summary>
        /// 发送并且等待数据,如果超时没有收到数据返回null,此函数接收时会阻止解包函数工作
        /// </summary>
        /// <param name="sendData">发送的字节数据</param>
        /// <param name="sleepMillisecond">发送后等待时间单位ms</param>
        /// <param name="timeOut">超时时间,超过时间如果没有收到数据返回null,单位ms</param>
        /// <returns></returns>
        Byte[] SendAndRead(byte[] sendData, int sleepMillisecond, int timeOut);

    }
    #region 粘包处理
    public enum FrameFilterType
    {
        /// <summary>
        /// 固定尾
        /// </summary>
        FixTail,
        /// <summary>
        /// 固定头和尾
        /// </summary>
        FixHeaderAndTail,
        /// <summary>
        /// 固定长度
        /// </summary>
        FixLength,
        /// <summary>
        /// 固定长度和头
        /// </summary>
        FixHeaderAndLength,
        /// <summary>
        /// 固定尾和长度
        /// </summary>
        FixTailAndLength,
        /// <summary>
        /// 固定帧头,并且帧头包含数据帧长度
        /// </summary>
        FixedHeaderReceiveLengthFilter
    }
    public class FilterInfo
    {
        public FrameFilterType FilterType { get; set; }
        public List<byte> Header { get; set; }
        public List<byte> Tail { get; set; }
        public int Length { get; set; }
        /// <summary>
        /// 开始字节
        /// </summary>
        public int StartByte { get; set; }
        /// <summary>
        /// 结束字节
        /// </summary>
        public int EndByte { get; set; }
        /// <summary>
        /// 变长的数据帧,数据长度字节不包含帧尾的需要规定帧尾的长度
        /// </summary>
        public int NotFixLengthTailLength { get; set; }

        public FilterInfo(FrameFilterType filterType, List<byte> header, int length, int endByte)
        {
            FilterType = filterType;
            Header = header;
            Length = length;
            EndByte = endByte;
        }

        public FilterInfo(FrameFilterType filterType, List<byte> tail, int length)
        {
            FilterType = filterType;
            Tail = tail;
            Length = length;
        }
        /// <summary>
        /// 变长数据帧(type=5)需要的信息
        /// </summary>
        /// <param name="filterType"></param>
        /// <param name="startByte"></param>
        /// <param name="endByte"></param>
        /// <param name="notFixLengthTailLength"></param>
        public FilterInfo(FrameFilterType filterType, int startByte, int endByte, int notFixLengthTailLength)
        {
            FilterType = filterType;
            StartByte = startByte;
            EndByte = endByte;
            NotFixLengthTailLength = notFixLengthTailLength;
        }
        /// <summary>
        /// 固定尾需要的信息
        /// </summary>
        /// <param name="filterType"></param>
        /// <param name="tail"></param>
        public FilterInfo(FrameFilterType filterType, List<byte> tail)
        {
            FilterType = filterType;
            Tail = tail;
        }

        public FilterInfo()
        {
        }
    }
    /// <summary>
    /// 处理粘包
    /// </summary>
    public class ReceiveFilter
    {
        public object Locker = new object();
        public readonly FilterInfo FilterInfo = null;
        //  readonly ILogHelper Log = new BR.ECS.DeviceDriver.Share.LogHelper();

        public ReceiveFilter(FilterInfo filterInfo)
        {
            this.FilterInfo = filterInfo;
        }

        public ReceiveFilter()
        {
        }

        /// <summary>
        /// 根据帧格式不同,从数据缓存中找出符合要求的数据帧
        /// </summary>
        /// <param name="bytes">数据缓存</param>
        /// <returns>符合要求的数据包</returns>
        public List<byte[]> GetFrame(List<byte> bytes)
        {
            List<byte[]> res = new List<byte[]>();
            switch (FilterInfo.FilterType)
            {

                case FrameFilterType.FixTail:
                    return GetFrameByFixTail(bytes);
                case FrameFilterType.FixHeaderAndTail:
                    return GetFrameByFixHeaderAndTail(bytes);
                case FrameFilterType.FixLength:
                    return GetPacketsByLength(bytes, res);
                case FrameFilterType.FixHeaderAndLength:
                    return GetPacketsByFixHeaderAndLength(bytes, res);
                case FrameFilterType.FixTailAndLength:
                    return GetPacketsByFixTailAndLength(bytes, res);
                case FrameFilterType.FixedHeaderReceiveLengthFilter:
                    return GetPacketsByFixedHeaderReceiveLengthFilter(bytes, res);
                default:
                    //    Log.Warning("数据帧格式未知");
                    return null;
            }
        }

        private List<byte[]> GetFrameByFixTail(List<byte> bytes)
        {
            List<byte[]> res = null;
            while (true)
            {
                var indexEnd = GetEndIndex(bytes, 0);
                if (indexEnd == -1)
                {
                    return res;
                }
                else
                {
                    if (res == null)
                    {
                        res = new List<byte[]>();
                    }
                    res.Add(bytes.GetRange(0, indexEnd + 1).ToArray());
                    lock (Locker)
                    {
                        bytes.RemoveRange(0, indexEnd + 1);
                    }

                }
            }
        }

        private List<byte[]> GetPacketsByFixedHeaderReceiveLengthFilter(List<byte> bytes, List<byte[]> res)
        {
            if (FilterInfo.StartByte < 1)
            {
                //   Log.Error($"StartByte必须为大于0的整数,当前值为{FilterInfo.StartByte}");
                return null;
            }
            if (FilterInfo.StartByte > FilterInfo.EndByte)
            {
                //   Log.Error($"StartByte必须为不大于EndByte,当前StartByte值为{FilterInfo.StartByte},EndByte={FilterInfo.EndByte}");
                return null;
            }
            var byteCount = FilterInfo.EndByte - FilterInfo.StartByte + 1;
            UInt32 lengthLen = 0;//数据帧长度,不包含数据头,默认使用小端,如果使用大端,在此段代码做修改
            while (bytes.Count >= FilterInfo.EndByte)
            {

                switch (byteCount)
                {
                    case 1:
                        lengthLen = bytes[FilterInfo.StartByte - 1];
                        break;
                    case 2:
                        lengthLen = BitConverter.ToUInt16(bytes.ToArray(), FilterInfo.StartByte - 1);
                        break;
                    case 3:
                        lengthLen = BitConverter.ToUInt32(bytes.ToArray(), FilterInfo.StartByte - 1) & 0x00FFFFFF;
                        break;
                    case 4:
                        lengthLen = BitConverter.ToUInt32(bytes.ToArray(), FilterInfo.StartByte - 1);
                        break;
                    default:
                        //         Log.Warning("解析数据报错误!长度不在预定范围!");
                        break;
                }
                if (bytes.Count >= FilterInfo.EndByte + lengthLen + FilterInfo.NotFixLengthTailLength)
                {
                    var packet = bytes.GetRange(0, (int)(FilterInfo.EndByte + lengthLen + FilterInfo.NotFixLengthTailLength)).ToArray();//此处将long转换为int,导致了精度损失,如果单个数据包超过2g请考虑修改此处.
                    res.Add(packet);
                    lock (Locker)
                    {
                        bytes.RemoveRange(0, (int)(FilterInfo.EndByte + lengthLen + FilterInfo.NotFixLengthTailLength));
                    }
                }
                else
                {
                    break;
                }
            }

            return res;
        }

        private List<byte[]> GetPacketsByFixTailAndLength(List<byte> bytes, List<byte[]> res)
        {
            while (bytes.Count >= FilterInfo.Length)
            {
                var tempBytes = bytes.GetRange(0, FilterInfo.Length).ToArray();
                bool isTailFind = true;
                for (int i = 0; i < FilterInfo.Tail.Count; i++)
                {
                    if (FilterInfo.Tail[i] != tempBytes[FilterInfo.Length - FilterInfo.Tail.Count + i])
                    {
                        lock (Locker)
                        {
                            //         Log.Error($"数据头不对移除一字节{bytes[0]:x2}");
                            bytes.RemoveRange(0, 1);

                        }
                        isTailFind = false;
                        break;
                    }
                }
                if (isTailFind)
                {
                    res.Add(tempBytes);
                    lock (Locker)
                    {
                        bytes?.RemoveRange(0, FilterInfo.Length);
                    }
                }
            }
            return res;
        }

        private List<byte[]> GetPacketsByFixHeaderAndLength(List<byte> bytes, List<byte[]> res)
        {
            while (bytes.Count >= FilterInfo.Length)
            {
                bool isFindHeader = true;
                for (int i = 0; i < FilterInfo.Header.Count; i++)
                {
                    if (FilterInfo.Header[i] != bytes[i])
                    {
                        isFindHeader = false;

                        lock (Locker)
                        {
                            int index = bytes.FindIndex(0, bytes.Count, x => x == FilterInfo.Header[0]);

                            if (index == -1)
                            {
                                bytes.Clear();
                                //          Log.Error($"清空");
                            }
                            else
                            {
                                bytes.RemoveRange(0, index + 1);
                                //         Log.Error($"数据头不对移除{index + 1}字节");
                            }
                        }
                        break;
                    }
                }
                if (isFindHeader)
                {
                    res.Add(bytes.GetRange(0, FilterInfo.Length).ToArray());
                    lock (Locker)
                    {
                        bytes.RemoveRange(0, FilterInfo.Length);
                    }
                }

            }

            return res;
        }

        private List<byte[]> GetPacketsByLength(List<byte> bytes, List<byte[]> res)
        {
            while (bytes.Count >= FilterInfo.Length)
            {
                res.Add(bytes.GetRange(0, FilterInfo.Length).ToArray());
                lock (Locker)
                {
                    bytes.RemoveRange(0, FilterInfo.Length);
                }
            }
            return res;
        }
        #region 固定包头与包尾的解析
        private List<byte[]> GetFrameByFixHeaderAndTail(List<byte> Bytes)
        {
            List<byte[]> res = new List<byte[]>();
            while (true)
            {
                byte[] resFrame = GetSingleFrameByFixHeaderAndTail(Bytes);
                if (resFrame == null)
                {
                    break;
                }
                else
                {
                    res.Add(resFrame);
                }
            }
            return res;
        }

        private byte[] GetSingleFrameByFixHeaderAndTail(List<byte> Bytes)
        {
            for (int i = 0; i < Bytes.Count; i++)
            {
                for (int p = 0; p < FilterInfo.Header.Count; p++)
                {
                    if (Bytes[i + p] == FilterInfo.Header[p])
                    {
                        if (p + 1 == FilterInfo.Header.Count)
                        {
                            int startIndex = i;
                            int endIndex = GetEndIndex(Bytes, p);
                            if (endIndex == -1)
                            {
                                return null;
                            }
                            if (endIndex > startIndex)
                            {
                                var res = Bytes.GetRange(startIndex, endIndex - startIndex + 1).ToArray();
                                lock (Locker)
                                {
                                    Bytes.RemoveRange(0, endIndex + 1);
                                }
                                return res;
                            }
                        }
                        else
                        {

                            break;
                        }
                    }

                }
            }
            return null;
        }
        /// <summary>
        /// 获取Bytes中与帧尾相符的位置
        /// </summary>
        /// <param name="Bytes"></param>
        /// <param name="p">从当前位置开始查找</param>
        /// <returns></returns>
        private int GetEndIndex(List<byte> Bytes, int p)
        {
            for (int e = p; e < Bytes.Count; e++)
            {
                for (int p1 = 0; p1 < FilterInfo.Tail.Count; p1++)
                {
                    if (Bytes[e + p1] == FilterInfo.Tail[p1])
                    {
                        if (p1 + 1 == FilterInfo.Tail.Count)
                        {
                            return e + p1;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return -1;
        }
        #endregion

    }
    #endregion
    #region 生产消费队列
    public class ProConQueue<T> : SafeQueue<T>, IDisposable where T : class
    {

        //  private ILogHelper Log = AppEngine.Container.Resolve<ILogHelper>();

        private ManualResetEvent managePending = new ManualResetEvent(initialState: false);

        private bool isNeedRun = true;



        private Action<T> m_doWork;

        private bool disposedValue;

        public ProConQueue(Action<T> doWork, uint sleepMS)
        {
            ProConQueue<T> myQueue = this;
            if (doWork == null)
            {
                //   Log.Error("传参不能为空!");
                throw new ArgumentNullException();
            }

            m_doWork = doWork;
            Task.Factory.StartNew(delegate
            {
                myQueue.ManageContainer(sleepMS);
            }, TaskCreationOptions.LongRunning);
        }

        private void ManageContainer(uint sleepMS)
        {
            while (isNeedRun)
            {
                managePending.Reset();
                if (TryDequeue(out var result))
                {
                    try
                    {
                        m_doWork?.Invoke(result);
                    }
                    catch (Exception ex)
                    {
                        //        Log.Error("队列执行失败,执行的函数为:" + m_doWork.Method.Name + ",捕捉信息:" + ex.Message);
                    }

                    if (sleepMS != 0)
                    {
                        Thread.Sleep((int)sleepMS);
                    }
                }
                else
                {
                    managePending.WaitOne();
                }
            }
        }

        public void Add(T data)
        {
            Enqueue(data);
            managePending.Set();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    base.Dispose();
                    isNeedRun = false;
                    managePending.Dispose();
                    m_doWork = null;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
    public class SafeQueue<T> : IDisposable where T : class
    {
        private Queue<T> queue = new Queue<T>();

        private bool disposedValue;

        public int Count
        {
            get
            {
                lock (queue)
                {
                    return queue.Count;
                }
            }
        }

        public void Clear()
        {
            lock (queue)
            {
                queue.Clear();
            }
        }

        public void Enqueue(T item)
        {
            lock (queue)
            {
                queue.Enqueue(item);
            }
        }

        public bool TryDequeue(out T result)
        {
            lock (queue)
            {
                result = default(T);
                if (queue.Count > 0)
                {
                    result = queue.Dequeue();
                    return true;
                }

                return false;
            }
        }

        public bool TryDequeueAll(out T[] result)
        {
            lock (queue)
            {
                result = queue.ToArray();
                queue.Clear();
                return result.Length != 0;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Clear();
                    queue = null;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
    #endregion
    //对System.Net.Sockets.socket进行封装
    public class TCPClient : ICommunication
    {
        public TCPClient()
        {

        }
        public int MaxReceiveBuffSize = 1024 * 1024 * 10;
        public int MaxReceiveQueueCount = 100;
        public int MaxSendQueueCount = 100;
        public ReceiveFilter ReceiveFilter;
        private System.Net.Sockets.Socket _socket;
        public IPAddress IP;
        NetworkStream _stream;
        public int Port;
        private ProConQueue<List<byte>> receiveProConQueue;
        private ProConQueue<List<byte>> sendProConQueue;
        public EndPoint LocalEndPoint => _socket?.LocalEndPoint;
        public EndPoint RemoteEndPoint => _socket?.RemoteEndPoint;
        /// <summary>
        /// 用于表示在主动close时不需要重连
        /// </summary>
        private bool isNeedReConnect = false;
        /// <summary>
        /// 用于表示使用者需要不需要使用重连
        /// 只能处理发现的断联情况,不包含业务链接断开,
        /// 业务链接断需要使用者主动close,然后再主动connect,并且注意,在主动close时,需要将IsUserNeedReConnect设置为false
        /// </summary>
        public bool IsUserNeedReConnect = false;
        public TCPClient(IPAddress ip, int port)
        {
            IP = ip;
            Port = port;

        }

        public TCPClient(IPAddress iP, int port, ReceiveFilter receiveFilter)
        {
            ReceiveFilter = receiveFilter ?? throw new ArgumentNullException(nameof(receiveFilter));
            IP = iP ?? throw new ArgumentNullException(nameof(iP));
            Port = port;
        }

        private bool Connect()
        {
            try
            {


                IniSocket();
                _socket.Connect(IP, Port);
                _stream = new NetworkStream(_socket);
                sendProConQueue = new ProConQueue<List<byte>>(SendToServer, 0);
                receiveProConQueue = new ProConQueue<List<byte>>(CallOnReceive, 0);
                ReadAsync();
                isNeedReConnect = true;
                return true;
            }
            catch (Exception ex)
            {
                var mess = ex.Message;
                //todo log
                return false;
            }
        }
        private void CallOnReceive(List<byte> bytes)
        {
            OnReceive?.Invoke(bytes);
        }
        public Action<List<byte>> OnReceive { get; set; }
        public Action<bool> IsConnectedAction { get; set; }
        private List<byte> tempBufferForReceive = new List<byte>();
        private List<byte> tempBufferForWriteAndRead = new List<byte>();
        private bool isTempBufferWaitData = false;
        public bool IsConnected
        {
            get
            {
                if (_socket != null)
                {

                    return _socket.Connected;
                }
                else
                {
                    return false;
                }
            }
        }
        public void IniSocket()
        {
            _socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

            #region 设置TCP的探测包工作参数
            _socket.NoDelay = true;
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);//禁用延迟关闭，以确保立即释放连接资源
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);//启用Keep会增加网络流量和CPU负载。此外，keep-alive机制可能会导致连接的延迟
            byte[] keepAlive = new byte[12];
            Buffer.BlockCopy(BitConverter.GetBytes((UInt32)1), 0, keepAlive, 0, 4);//启用Keep-Alive，将Keep-Alive设置放到keepAlive缓冲区的最开始的4个字节
            Buffer.BlockCopy(BitConverter.GetBytes((UInt32)30000), 0, keepAlive, 4, 4);//开始首次探测前的TCP空闭时间
            Buffer.BlockCopy(BitConverter.GetBytes((UInt32)5000), 0, keepAlive, 8, 4);//两次探测间的时间间隔
            _socket.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);
            #endregion

        }
        public void EndReadAsync()
        {
            _stream.EndRead(null);
        }

        //异步读取TCPclient的缓存,当有数据时使用回调提示有数据
        public void ReadAsync()
        {
            try
            {
                if (_stream.CanRead)
                {
                    byte[] buffer = new byte[1024];
                    _stream.BeginRead(buffer, 0, buffer.Length, new AsyncCallback(OnRead), buffer);
                }

            }
            catch (Exception ex)
            {
                var mess = ex.Message;
                //todo log
            }
        }
        private void OnRead(IAsyncResult ar)
        {
            try
            {
                if (_stream.CanRead)
                {
                    byte[] buffer = (byte[])ar.AsyncState;
                    int bytesRead = _stream.EndRead(ar);
                    if (bytesRead > 0)
                    {
                        List<byte> list = new List<byte>();
                        for (int i = 0; i < bytesRead; i++)
                        {
                            list.Add(buffer[i]);
                        }

                        HandleReceiveData(list);
                        ReadAsync();
                    }
                    else
                    {

                        _stream.Close();
                        _socket.Close();
                        HandleSocketClosed();

                    }
                }
            }
            catch (Exception ex)
            {
                var mess = ex.Message;
                //todo log
            }
        }

        private void HandleSocketClosed()
        {
            IsConnectedAction?.Invoke(false);
            //todo log
            if (isNeedReConnect & IsUserNeedReConnect)
            {
                ReConnect();
            }
        }

        public void ReConnect()
        {
            Close();
            Task.Delay(3000).Wait();
            Open();
        }

        private void HandleReceiveData(List<byte> list)
        {
            //处理接收到的数据
            //分三种,1.如果传入粘包处理,使用粘包处理进行粘包处理,输出的时完整的数据帧
            //2.如果没有传入粘包处理,输出的时每次接收到的数据
            //3.如果使用WriteAndRead,那么数据直接存储在一个临时的缓存,WriteAndRead返回时,返回这个缓存
            if (isTempBufferWaitData)
            {
                tempBufferForWriteAndRead?.AddRange(list);
            }
            else if (ReceiveFilter != null)
            {
                tempBufferForReceive?.AddRange(list);
                var frames = ReceiveFilter.GetFrame(tempBufferForReceive);
                if (frames != null)
                {
                    foreach (var item in frames)
                    {
                        receiveProConQueue.Add(item.ToList());
                    }
                    if (tempBufferForReceive.Count > MaxReceiveBuffSize)
                    {
                        tempBufferForReceive.RemoveRange(0, tempBufferForReceive.Count - MaxReceiveBuffSize);
                        var message = $"接收数据缓存大于{MaxReceiveBuffSize},将删除头部数据";

                    }
                }
            }
            else
            {
                receiveProConQueue.Add(list);
            }
            if (receiveProConQueue.Count > MaxReceiveBuffSize)
            {
                var message = $"接收队列数据帧数大于{MaxReceiveBuffSize}";

            }

        }

        //异步发送数据
        public void SendAsync(byte[] buffer)
        {
            try
            {
                _stream.BeginWrite(buffer, 0, buffer.Length, new AsyncCallback(OnWrite), _stream);
            }
            catch (Exception ex)
            {
                var mess = ex.Message;
                //todo log
            }
        }
        private void OnWrite(IAsyncResult ar)
        {
            try
            {
                NetworkStream stream = (NetworkStream)ar.AsyncState;
                stream.EndWrite(ar);
            }
            catch (Exception ex)
            {
                var mess = ex.Message;
                //todo log
            }
        }

        int sendBytes = 0;
        /// <summary>
        /// 最终发送的执行方
        /// </summary>
        /// <param name="buffer"></param>
        public void SendToServer(List<byte> buffer)
        {
            try
            {
                this.sendBytes += buffer.Count;
                Console.WriteLine($"发送数据个数{sendBytes}");
                _stream.Write(buffer.ToArray(), 0, buffer.Count);
            }
            catch (Exception ex)
            {
                var mess = ex.Message;
                //todo log
            }
        }

        public bool Open(int connunicationTimeOut = 0)
        {
            var res = Connect();
            if (res)
            {
                IsConnectedAction?.Invoke(true);
            }
            //todo log
            return res;
        }
        public bool Close()
        {
            try
            {
                _stream.Close();
                _socket.Close();
                receiveProConQueue.Dispose();
                sendProConQueue.Dispose();
                isNeedReConnect = false;
                return true;
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                return false;
            }

        }


        public bool Send(byte[] data)
        {
            if (IsConnected && sendProConQueue != null)
            {
                sendProConQueue.Add(data.ToList());
                return true;
            }
            else
            {
                var mess = $"当前IsConnected:{IsConnected},sendProConQueue:{sendProConQueue},无法发送数据{BitConverter.ToString(data)}";
                //todo log
                return false;
            }

        }

        public List<byte> Receive(int span = 0)
        {
            tempBufferForWriteAndRead.Clear();
            Thread.Sleep(span);
            return tempBufferForWriteAndRead;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sendData"></param>
        /// <param name="sleepMillisecond">查询间隔</param>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public byte[] SendAndRead(byte[] sendData, int sleepMillisecond, int timeOut)
        {
            if (!IsConnected)
            {//todo log

                return null;
            }
            isTempBufferWaitData = true;
            var now = DateTime.Now;
            var isSuccess = Send(sendData);
            List<byte> bytes = new List<byte>();
            if (isSuccess)
            {

                while (true)
                {
                    var res = Receive(sleepMillisecond);
                    if (res.Count != 0)
                    {
                        bytes.AddRange(res);
                    }
                    if (bytes.Count != 0 & res.Count == 0) //当没有新数据时退出
                    {
                        isTempBufferWaitData = false;
                        return bytes.ToArray();
                    }
                    //判断超时
                    if ((DateTime.Now - now).TotalMilliseconds > timeOut)
                    {
                        if (bytes.Count > 0)
                        {
                            isTempBufferWaitData = false;
                            return bytes.ToArray();
                        }
                        else
                        {
                            isTempBufferWaitData = false;
                            return null;
                        }
                    }

                }

            }
            else
            {
                isTempBufferWaitData = false;
                return null;
            }
        }
    }
}
