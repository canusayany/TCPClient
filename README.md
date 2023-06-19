# TCPClient
.net 4.8 tcpClient , double queue ,keepalive
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
